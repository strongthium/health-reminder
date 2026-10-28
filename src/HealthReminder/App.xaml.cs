using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Strongthium.HealthReminder;

/// <summary>
/// Runs in the background, in the tray: every few seconds it checks which reminders are due and rings for them. The
/// main window lists the reminders and the day; closing it leaves the reminders running.
/// </summary>
public sealed partial class App : Application
{
    public Database Database { get; private set; } = null!;
    public Schedule Schedule { get; private set; } = null!;
    /// <summary>Made when a medication is first looked up, not at start.</summary>
    public DailyMed DailyMed => dailyMed ??= new(Http);
    public HealthCanada HealthCanada => healthCanada ??= new(Http);
    private HttpClient Http => http ??= DailyMed.CreateHttpClient();
    private DailyMed? dailyMed;
    private HealthCanada? healthCanada;
    private HttpClient? http;
    public List<Reminder> Reminders { get; private set; } = [];

    public static new App Current => (App)Application.Current;
    public static ImageSource Icon { get; } = Resource("Assets/health-reminder.png");

    private MainWindow? mainWindow;
    private AlarmWindow? alarmWindow;
    private Forms.NotifyIcon? trayIcon;
    private DateTime lastMinute;

    private static BitmapFrame Resource(string path) => BitmapFrame.Create(new Uri("pack://application:,,,/health-reminder;component/" + path));

    // ---- Settings ----------------------------------------------------------------------------------------------

    private T Choice<T>(string key, T fallback) where T : struct, Enum => Enum.TryParse<T>(Database.Setting(key), out var value) ? value : fallback;

    private int Number(string key, int fallback) => int.TryParse(Database.Setting(key), CultureInfo.InvariantCulture, out var n) ? n : fallback;

    public int SnoozeMinutes
    {
        get => Number("snooze_minutes", 10) is > 0 and var m ? m : 10;
        set => Database.SetSetting("snooze_minutes", value.ToString(CultureInfo.InvariantCulture));
    }

    private static string Key(AlertGroup group) => group.ToString().ToLowerInvariant();

    /// <summary>Each group's own sound, so that it can be told from the others without looking: a chime for
    /// medication, beeps for a check, a bell for a meal or a drink, unless chosen otherwise.</summary>
    public ChimeSound SoundFor(AlertGroup group) => Choice("sound_" + Key(group), group switch
    {
        AlertGroup.Check => ChimeSound.Beeps,
        AlertGroup.EatDrink => ChimeSound.Bell,
        _ => ChimeSound.Chime,
    });

    public void SetSound(AlertGroup group, ChimeSound sound) => Database.SetSetting("sound_" + Key(group), sound.ToString());

    /// <summary>The user's own WAV file for the group, for <see cref="ChimeSound.Custom"/>.</summary>
    public string SoundFileFor(AlertGroup group) => Database.Setting("sound_file_" + Key(group)) ?? "";

    public void SetSoundFile(AlertGroup group, string file) => Database.SetSetting("sound_file_" + Key(group), file);

    /// <summary>Whether the group's sound repeats until answered, or sounds once: medication and checks repeat,
    /// meals and drinks sound once, unless chosen otherwise.</summary>
    public bool RepeatsFor(AlertGroup group) => Database.Setting("sound_repeat_" + Key(group)) is { } value ? value == "on" : group != AlertGroup.EatDrink;

    public void SetRepeats(AlertGroup group, bool repeats) => Database.SetSetting("sound_repeat_" + Key(group), repeats ? "on" : "off");

    /// <summary>The sound's volume, 5 to 100.</summary>
    public int Volume
    {
        get => Math.Clamp(Number("volume", 80), 5, 100);
        set => Database.SetSetting("volume", value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The sound gets louder, up to full volume, each time the alarm comes back.</summary>
    public bool Escalate
    {
        get => Database.Setting("sound_escalate") != "off";
        set => Database.SetSetting("sound_escalate", value ? "on" : "off");
    }

    public NagLevel Nag
    {
        get => Choice("nag", NagLevel.Persistent);
        set => Database.SetSetting("nag", value.ToString());
    }

    /// <summary>How groceries and food are measured: metric unless the region is the U.S. (or Liberia or Myanmar).</summary>
    public MeasureSystem Measures
    {
        get => Choice("measures", RegionInfo.CurrentRegion.IsMetric ? MeasureSystem.Metric : MeasureSystem.Us);
        set => Database.SetSetting("measures", value.ToString());
    }

    /// <summary>What drinks are measured in when they're drunk.</summary>
    public DrinkUnit DrinkUnit
    {
        get => Choice("drink_unit", RegionInfo.CurrentRegion.IsMetric ? DrinkUnit.Millilitre : DrinkUnit.FluidOunce);
        set => Database.SetSetting("drink_unit", value.ToString());
    }

    /// <summary>How cold the fridge and freezer are: the North American standard, 4 °C (40 °F) and −18 °C (0 °F), until
    /// set. Setting it works out again the best-before dates that weren't set by hand.</summary>
    public ColdStorage Cold
    {
        get
        {
            static double Read(string? text, double standard) =>
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : standard;
            return new(Read(Database.Setting("fridge_c"), ColdStorage.StandardFridgeC), Read(Database.Setting("freezer_c"), ColdStorage.StandardFreezerC));
        }
        set
        {
            Database.SetSetting("fridge_c", value.FridgeC.ToString("R", CultureInfo.InvariantCulture));
            Database.SetSetting("freezer_c", value.FreezerC.ToString("R", CultureInfo.InvariantCulture));
            PantryLog.Redate();
        }
    }

    /// <summary>Whether temperatures are shown in °F: in the U.S. (and the other countries not on metric) until set.</summary>
    public bool Fahrenheit
    {
        get => Database.Setting("temperature") is { } saved ? saved == "F" : !RegionInfo.CurrentRegion.IsMetric;
        set => Database.SetSetting("temperature", value ? "F" : "C");
    }

    /// <summary>Whether Claude reads grocery photos and suggests meals; off until it's turned on with a key.</summary>
    public bool ClaudeOn
    {
        get => Database.Setting("claude") == "on" && AnthropicKey.Length > 0;
        set => Database.SetSetting("claude", value ? "on" : "off");
    }

    /// <summary>The user's Anthropic API key, kept encrypted for their Windows account (DPAPI); "" when there's none.</summary>
    public string AnthropicKey
    {
        get
        {
            try
            {
                return Database.Setting("anthropic_key") is { Length: > 0 } stored
                    ? System.Text.Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(stored), KeyEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser))
                    : "";
            }
            catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException)
            {
                return "";
            }
        }
        set => Database.SetSetting("anthropic_key", value.Length == 0 ? ""
            : Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(value.Trim()), KeyEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser)));
    }

    private static readonly byte[] KeyEntropy = "Strongthium health-reminder"u8.ToArray();

    /// <summary>Claude for the kitchen, when it's on; null otherwise.</summary>
    public KitchenAssistant? Assistant => ClaudeOn ? new KitchenAssistant(AnthropicKey) : null;

    /// <summary>What the main window's X does; it asks until told.</summary>
    public CloseAction CloseAction
    {
        get => Choice("close", CloseAction.Ask);
        set => Database.SetSetting("close", value.ToString());
    }

    /// <summary>The webcam to record with, by name; empty for the first one.</summary>
    public string Camera
    {
        get => Database.Setting("camera") ?? "";
        set => Database.SetSetting("camera", value);
    }

    /// <summary>How long a video can be, in seconds; it stops and is saved then.</summary>
    public int VideoSeconds
    {
        get => Math.Clamp(Number("video_seconds", 60), 10, 300);
        set => Database.SetSetting("video_seconds", value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The group's sound at a volume from 0 to 1, or null for none. A sound file that can't be read falls
    /// back to the chime.</summary>
    public byte[]? ChimeWav(AlertGroup group, double volume)
    {
        var chosen = SoundFor(group);
        if (chosen == ChimeSound.None) return null;
        if (chosen == ChimeSound.Custom)
        {
            try
            {
                var file = SoundFileFor(group);
                if (file.Length > 0 && new FileInfo(file) is { Exists: true, Length: < 20_000_000 } && Sounds.Scale(File.ReadAllBytes(file), volume) is { } wav) return wav;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine("health-reminder: sound file: " + exception.Message);
            }
            chosen = ChimeSound.Chime;
        }
        return Sounds.Make(chosen, volume);
    }

    /// <summary>Starts the sound for these doses at <paramref name="volume"/> (0 to 1): the sound of the most
    /// important group among them.</summary>
    public void Ring(IEnumerable<Dose> doses, double volume)
    {
        var group = doses.Select(d => d.Reminder.Kind).Group();
        if (ChimeWav(group, volume) is not { } wav) return;
        AlarmSound.Play(wav, RepeatsFor(group));
    }

    // ---- Running -----------------------------------------------------------------------------------------------

    /// <summary>Opens the reminders. Done before any window is made; separate so that a test can use its own.</summary>
    public void Open(Database database)
    {
        Database = database;
        Schedule = new Schedule(Database);
        Reminders = Database.Reminders();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!Program.Running) return;
        Open(new Database(Program.DataDirectory));
        Exit += (_, _) =>
        {
            AlarmSound.Stop();
            if (trayIcon is not null) trayIcon.Visible = false;
            trayIcon?.Dispose();
            Database.Dispose();
        };

        // The window first, so that it's up as soon as it can be; the rest once it has been drawn.
        if (!Program.StartMinimized) ShowMainWindow();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            ShowTrayIcon();
            if (SmokeButtonShown) ShowSmokeButton(true);
            PantryLog.SeedOnce();
            Autostart.Refresh();
            Program.ListenForOtherCopies(() => Dispatcher.BeginInvoke(ShowMainWindow));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
            Tick();
        });
    }

    private void ShowTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Health Reminder", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Log a cigarette", null, (_, _) => SmokeLog.Click());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit (no more reminders)", null, (_, _) => Quit());
        using (var icon = GetResourceStream(new Uri("pack://application:,,,/health-reminder;component/Assets/health-reminder.ico")).Stream)
            trayIcon = new Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(icon, Forms.SystemInformation.SmallIconSize),
                Text = "Health Reminder",
                ContextMenuStrip = menu,
                Visible = true,
            };
        trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) ShowMainWindow();
        };
        UpdateTrayText(DateTime.Now);
    }

    private void Tick()
    {
        var now = DateTime.Now;
        CheckDue(now);
        CheckKitchen(now);
        CheckExercise(now);
        CheckPantry(now);
        var minute = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute));
        if (minute != lastMinute)
        {
            lastMinute = minute;
            mainWindow?.Refresh();
            UpdateTrayText(now);
        }
    }

    /// <summary>Rings for the doses that are due, or adds them to the alarm that is already up.</summary>
    public void CheckDue(DateTime now)
    {
        var due = Schedule.Due(Reminders, now);
        if (due.Count == 0) return;
        if (alarmWindow is null)
        {
            alarmWindow = new AlarmWindow();
            alarmWindow.Closed += (_, _) =>
            {
                alarmWindow = null;
                AlarmSound.Stop();
            };
        }
        if (alarmWindow.Add(due) || !alarmWindow.IsVisible) alarmWindow.ShowAlarm(ring: true);
    }

    // ---- The kitchen ----------------------------------------------------------------------------------------------

    private KitchenAlert? kitchenAlert;
    private CookingWindow? cookingWindow;

    /// <summary>Shows the next kitchen alert that's due, one at a time. It chimes once, unless a medication alarm is
    /// ringing.</summary>
    private void CheckKitchen(DateTime now)
    {
        if (kitchenAlert is not null || Database is null) return;
        if (KitchenLog.Due(now).FirstOrDefault() is not { } due) return;
        kitchenAlert = new KitchenAlert(due);
        kitchenAlert.Closed += (_, _) => kitchenAlert = null;
        kitchenAlert.Show();
        if (alarmWindow is null && ChimeWav(AlertGroup.EatDrink, Volume / 100.0) is { } wav) AlarmSound.Play(wav, loop: false);
    }

    /// <summary>Opens the steps of a recipe at the side of the screen.</summary>
    public void ShowSteps(Recipe recipe, DateTime? eatAt)
    {
        if (cookingWindow is null)
        {
            cookingWindow = new CookingWindow();
            cookingWindow.Closed += (_, _) => cookingWindow = null;
        }
        // Cooked: what it used comes off the stock, once for the meal (eating it then doesn't take it off again).
        var key = eatAt is { } at ? PantryLog.MealKey(at, recipe) : $"{DateTime.Now:yyyy-MM-ddTHH:mm}|{recipe.Id}|{recipe.Name}";
        cookingWindow.Show(recipe, eatAt, () =>
        {
            PantryLog.Cooked(recipe, key);
            KitchenChanged();
        });
    }

    // ---- The kitchen check ------------------------------------------------------------------------------------

    private NoticeWindow? checkNotice;
    private readonly Dictionary<string, DateTime> checkSnoozed = [];

    /// <summary>The week's kitchen check, when it's time and something's in doubt.</summary>
    private void CheckPantry(DateTime now)
    {
        if (checkNotice is not null || Database is null || PantryLog.Due(now) is not { } check) return;
        if (checkSnoozed.TryGetValue(check.Key, out var until) && until > now) return;
        var minutes = Math.Max(5, (int)Math.Ceiling(check.Items.Count * 0.75));
        checkNotice = new NoticeWindow("Kitchen check", "Time to check the kitchen",
            $"{check.Items.Count} thing{(check.Items.Count == 1 ? "" : "s")} to count, about {minutes} minutes: "
            + string.Join(", ", check.Items.Take(8).Select(i => i.Name)) + (check.Items.Count > 8 ? ", and more" : "")
            + ". Counting them keeps the stock, the meal ideas and the shopping right.",
            KindLook.Brush(ReminderKind.Meal), "Count them now", "In 2 hours", "Not this week");
        checkNotice.Closed += (_, _) =>
        {
            switch (checkNotice.Answer)
            {
                case 0:
                    PantryLog.CheckDone(check);
                    ShowPage(Strongthium.HealthReminder.MainWindow.GroceriesPage);
                    break;
                case 1:
                    checkSnoozed[check.Key] = DateTime.Now.AddHours(2);
                    break;
                case 2:
                    PantryLog.CheckDone(check);
                    break;
            }
            checkNotice = null;
        };
        checkNotice.Show();
        if (alarmWindow is null && ChimeWav(AlertGroup.EatDrink, Volume / 100.0) is { } wav) AlarmSound.Play(wav, loop: false);
    }

    /// <summary>A meal was picked, or a kitchen alert answered.</summary>
    public void KitchenChanged() => mainWindow?.Refresh();

    // ---- Exercise -------------------------------------------------------------------------------------------------

    private ExerciseAlert? exerciseAlert;

    /// <summary>Shows the exercise alert that's due, if there is one; it chimes once, unless a medication alarm rings.</summary>
    private void CheckExercise(DateTime now)
    {
        if (exerciseAlert is not null || Database is null || ExerciseLog.Plan is not { } plan) return;
        if (ExerciseLog.Due(now).FirstOrDefault() is not { } due) return;
        exerciseAlert = new ExerciseAlert(due, plan);
        exerciseAlert.Closed += (_, _) => exerciseAlert = null;
        exerciseAlert.Show();
        if (alarmWindow is null && ChimeWav(AlertGroup.EatDrink, Volume / 100.0) is { } wav) AlarmSound.Play(wav, loop: false);
    }

    /// <summary>A workout's steps at the side of the screen; Done logs it.</summary>
    public void ShowWorkout(Workout workout)
    {
        if (cookingWindow is null)
        {
            cookingWindow = new CookingWindow();
            cookingWindow.Closed += (_, _) => cookingWindow = null;
        }
        cookingWindow.ShowSteps("Exercise: " + workout.Title, workout.Title,
            $"{workout.Summary} · about {workout.Kcal(ExerciseLog.WeightKg()):N0} kcal", workout.Needs, workout.Steps,
            ExerciseLog.Diabetes() ? ExerciseLog.Insulin : ExerciseLog.Safety, ExerciseLog.Green, () => ExerciseLog.Done(workout));
    }

    /// <summary>Exercise was set up, done or skipped.</summary>
    public void ExerciseChanged() => mainWindow?.Refresh();

    /// <summary>Called when a dose is answered or snoozed, and when the reminders change.</summary>
    public void Changed()
    {
        Reminders = Database.Reminders();
        alarmWindow?.Keep(Reminders);
        mainWindow?.Refresh();
        UpdateTrayText(DateTime.Now);
    }

    private void UpdateTrayText(DateTime now)
    {
        if (trayIcon is null) return;
        var next = Schedule.Next(Reminders, now);
        var text = next is null ? "Health Reminder" : $"Health Reminder: next, {next.Reminder.Name} at {Format.When(next.Due, now)}";
        trayIcon.Text = text.Length > 127 ? text[..127] : text;
    }

    /// <summary>Opens the main window on a page.</summary>
    public void ShowPage(int page)
    {
        ShowMainWindow();
        mainWindow?.Navigate(page);
    }

    // ---- Smoking --------------------------------------------------------------------------------------------------

    private SmokeButton? smokeButton;

    /// <summary>Whether the on-top button for logging a cigarette is on the screen.</summary>
    public bool SmokeButtonShown
    {
        get => Database.Setting("smoke_button") == "on";
        set
        {
            Database.SetSetting("smoke_button", value ? "on" : "off");
            ShowSmokeButton(value);
            mainWindow?.Refresh();
        }
    }

    /// <summary>Whether a cigarette is logged when it's lit and again when it's out, to time it.</summary>
    public bool SmokeTimed
    {
        get => Database.Setting("smoke_timed") == "on";
        set => Database.SetSetting("smoke_timed", value ? "on" : "off");
    }

    private void ShowSmokeButton(bool shown)
    {
        if (shown)
        {
            smokeButton ??= new SmokeButton();
            smokeButton.Show();
        }
        else
        {
            smokeButton?.Close();
            smokeButton = null;
        }
    }

    /// <summary>A cigarette was logged, finished or taken back.</summary>
    public void SmokeChanged()
    {
        smokeButton?.Update();
        mainWindow?.Refresh();
    }

    public void ShowMainWindow()
    {
        if (mainWindow is null)
        {
            mainWindow = new MainWindow();
            mainWindow.Closed += (_, _) => mainWindow = null;
        }
        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    public void Quit()
    {
        AlarmSound.Stop();
        mainWindow?.AllowClose();
        Shutdown();
    }

    // Signing out of Windows closes the window without asking.
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        mainWindow?.AllowClose();
        base.OnSessionEnding(e);
    }
}

/// <summary>Times, dates and doses as the user's culture writes them.</summary>
public static class Format
{
    public static string Time(TimeOnly time) => time.ToString("t", CultureInfo.CurrentCulture);

    public static string Time(DateTime time) => time.ToString("t", CultureInfo.CurrentCulture);

    /// <summary>"8:00 PM", "tomorrow 8:00 AM", "Thu 8:00 AM".</summary>
    public static string When(DateTime time, DateTime now) =>
        time.Date == now.Date ? Time(time)
        : time.Date == now.Date.AddDays(1) ? "tomorrow " + Time(time)
        : time.ToString("ddd ", CultureInfo.CurrentCulture) + Time(time);

    /// <summary>The meal a kitchen alert is for, from the alert's day: "Dinner at 6:00 PM", "Breakfast tomorrow at
    /// 7:30 AM".</summary>
    public static string Meal(KitchenEvent e)
    {
        var day = (e.Meal.At.Date - e.At.Date).Days switch
        {
            0 => "",
            1 => " tomorrow",
            _ => " on " + e.Meal.At.ToString("dddd", CultureInfo.CurrentCulture),
        };
        return $"{e.Meal.Meal}{day} at {Time(e.Meal.At)}";
    }

    /// <summary>What to do, in a few words: "Take 2 tablets", "Inject 12 units", "Drink 250 mL", "Check".</summary>
    public static string Instruction(Reminder reminder, double amount)
    {
        var kind = reminder.Kind;
        if (kind == ReminderKind.Meal) return amount > 1 ? $"Eat about {Amounts.Format(amount, reminder.Unit)}" : "Eat";
        if (!kind.HasAmount() || reminder.Unit.Length == 0) return kind.Verb();
        var what = Amount(amount, reminder.Unit, kind);
        return kind == ReminderKind.Drink ? $"Drink {what} of {reminder.Name.ToLowerInvariant()}" : $"{kind.Verb()} {what}".Trim();
    }

    /// <summary>An amount with its unit; a drink's millilitres in the unit drinks are measured in (Settings). A liquid
    /// medication stays in what it's dosed in.</summary>
    public static string Amount(double amount, string unit, ReminderKind kind) =>
        kind == ReminderKind.Drink && unit == "mL" ? Quantities.Drink(amount, App.Current.DrinkUnit) : Amounts.Format(amount, unit);

    /// <summary>A recorded reading with its unit: "6.4 mmol/L", "128/82 mmHg".</summary>
    public static string Reading(string value, string unit) => value.Length == 0 ? "" : unit.Length == 0 ? value : $"{value} {unit}";
}

/// <summary>Visible when the value is false.</summary>
public sealed class VisibleUnlessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible when the value is a string that isn't empty.</summary>
public sealed class VisibleIfTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Shown when a width is at least the one given (the parameter): what fits only in a wide window.</summary>
public sealed class VisibleIfWiderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double width && double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var least) && width >= least
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Photos from the data directory, small enough for the windows.</summary>
internal static class Photos
{
    public static ImageSource? Load(string directory, string file)
    {
        var path = Path.Combine(directory, file);
        if (file.Length == 0 || !File.Exists(path)) return null;
        try
        {
            return FromBytes(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static ImageSource? FromBytes(byte[] bytes, int width = 400)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = width;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or IOException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
