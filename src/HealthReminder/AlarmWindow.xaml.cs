using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Strongthium.HealthReminder;

/// <summary>An idea from Claude on a meal's alarm, to have instead of the meal it shows.</summary>
public sealed class AlarmIdea(AlarmItem item, KitchenIdea idea)
{
    public AlarmItem Item => item;
    public KitchenIdea Idea => idea;
    public string Detail => new IdeaRow(idea).Detail;
}

/// <summary>One dose on the alarm: what to do, how much, and what it looks like; the reading or site to record.</summary>
public sealed class AlarmItem : INotifyPropertyChanged
{
    public AlarmItem(Dose dose, string imageDirectory, int snoozeMinutes, Schedule schedule, IReadOnlyList<Reminder> reminders)
    {
        Dose = dose;
        SnoozeText = $"Snooze {snoozeMinutes} min";
        Photo = Photos.Load(imageDirectory, Reminder.Photo);
        Site = schedule.NextSite(Reminder);
        if (Kind == ReminderKind.Drink)
        {
            var target = reminders.Where(r => r.Kind == ReminderKind.Drink && r.Active).Sum(r => r.Times.Sum(t => t.Amount));
            var drank = schedule.DrankToday(reminders, DateTime.Now);
            if (target > 0)
            {
                Progress = $"{Quantities.Drink(drank, App.Current.DrinkUnit)} of {Quantities.Drink(target, App.Current.DrinkUnit)} so far today";
                ProgressValue = drank;
                ProgressMaximum = target;
            }
        }
        if (Kind.IsMedication())
        {
            Missed = schedule.MissedBefore(dose);
            if (App.Current.Database.Supply(Reminder) is { Low: true } supply)
                SupplyText = (supply.OnHand <= 0 ? "None left by the count" : $"{Amounts.Format(supply.OnHand, Reminder.Unit)} left")
                    + (supply.DaysLeft is { } days && supply.OnHand > 0 ? $", about {days} day{(days == 1 ? "" : "s")}" : "")
                    + ": time to refill" + (Reminder.RxNumber.Length > 0 ? $" (Rx {Reminder.RxNumber})" : "") + ".";
        }
    }

    /// <summary>The dose before this one, when it was missed; the alarm then insists more.</summary>
    public Dose? Missed { get; }
    public string MissedText => Missed is { } missed
        ? $"The {Format.When(missed.Due, DateTime.Now)} dose was missed. Take this one as usual; don't take two to make up for it unless your doctor or pharmacist has said to."
        : "";
    /// <summary>When it's running low by the count: how much is left, and to refill.</summary>
    public string SupplyText { get; } = "";

    public Dose Dose { get; }
    public Reminder Reminder => Dose.Reminder;
    public ReminderKind Kind => Reminder.Kind;
    public string KindText => Kind.ShortLabel();
    public string Name => Reminder.Name;
    public string Instruction => Format.Instruction(Reminder, Dose.Amount);
    public string Notes => Reminder.Notes;
    public string Form => Reminder.Form;
    public PillAppearance Appearance => Reminder.Appearance;
    /// <summary>Pills are drawn as many times as are taken; anything else once.</summary>
    public double Count => Kind == ReminderKind.Pill && Reminder.Unit is "tablet" or "capsule" or "softgel" or "lozenge" ? Dose.Amount : 1;
    public double PictureHeight => Kind == ReminderKind.Pill ? 120 : 100;
    public string Look => Kind == ReminderKind.Pill && Appearance.IsKnown ? Appearance.Describe(Form) : "";
    /// <summary>A meal with its calories gets the day's idea of what to eat: "2 eggs, 2 slices of buttered toast and a
    /// glass of orange juice".</summary>
    public string Idea =>
        Kind != ReminderKind.Meal ? ""
        : Recipe is { } picked ? picked.Name
        : Reminder.Unit == "kcal" && Dose.Amount > 1 ? Meals.Suggest(Meals.SlotOf(Name), Dose.Amount, DateOnly.FromDateTime(Dose.Due)).ToString()
        : "";
    public string IdeaLabel => Recipe is null ? "FOR EXAMPLE" : "ON THE MENU";

    /// <summary>The meal as the menu has it (snacks aren't on it): what's picked for it, if anything.</summary>
    public PlannedMeal? Meal => Kind != ReminderKind.Meal ? null
        : KitchenLog.Meals(DateOnly.FromDateTime(Dose.Due)).FirstOrDefault(m => m.Meal == Name && m.At == Dose.Due);
    public Recipe? Recipe => Meal?.Recipe;
    public bool HasRecipe => Recipe is not null;

    // ---- Other ideas, from Claude ----

    private IReadOnlyList<AlarmIdea> ideas = [];
    private string ideasText = "";
    private bool asking;

    /// <summary>A meal on the menu, with Claude on, can be swapped for an idea from what's in the kitchen.</summary>
    public bool CanAskClaude => Meal is not null && App.Current.ClaudeOn;
    public bool CanAsk => CanAskClaude && !asking;

    public bool Asking
    {
        get => asking;
        set
        {
            asking = value;
            Notify(nameof(CanAsk));
        }
    }

    public IReadOnlyList<AlarmIdea> Ideas
    {
        get => ideas;
        set
        {
            ideas = value;
            Notify();
        }
    }

    public string IdeasText
    {
        get => ideasText;
        set
        {
            ideasText = value;
            Notify();
        }
    }

    /// <summary>Another meal was picked for it: what it shows follows.</summary>
    public void MealChanged()
    {
        Notify(nameof(Idea));
        Notify(nameof(IdeaLabel));
        Notify(nameof(HasRecipe));
    }

    /// <summary>The ideas of the same dose on the alarm before it was rebuilt.</summary>
    public void Keep(AlarmItem before)
    {
        ideas = before.ideas.Select(i => new AlarmIdea(this, i.Idea)).ToList();
        ideasText = before.ideasText;
        asking = before.asking;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([System.Runtime.CompilerServices.CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new(name));
    public string DueText => "Due at " + Format.Time(Dose.Due) + (Dose.Due.Date != DateTime.Today ? " yesterday" : "");
    public string SnoozeText { get; }
    public string DoneText => Reminder.RecordVideo ? Evidence.ButtonText(Kind) : Kind.DoneText();
    public ImageSource? Photo { get; }
    public bool HasPhoto => Photo is not null;

    /// <summary>A meal or a snack: something else eaten can be logged in its place.</summary>
    public bool IsMeal => Kind == ReminderKind.Meal;

    public bool IsInjection => Kind == ReminderKind.Injection && Reminder.Sites.Count > 0;
    public IReadOnlyList<string> Sites => Reminder.Sites;
    public string Site { get; set; }

    public bool IsCheck => Kind.IsCheck();
    public string Value { get; set; } = "";
    public string ValueUnit => Reminder.ValueUnit;
    public string ValueHint => Kind switch
    {
        ReminderKind.BloodSugar => ValueUnit == "mg/dL" ? "e.g. 110" : "e.g. 6.1",
        ReminderKind.BloodPressure => "e.g. 120/80",
        ReminderKind.Weight => ValueUnit == "lb" ? "e.g. 165" : "e.g. 75",
        _ => "",
    };
    public string Progress { get; } = "";
    public bool HasProgress => Progress.Length > 0;
    public double ProgressValue { get; }
    public double ProgressMaximum { get; } = 1;
}

/// <summary>
/// The alarm: everything that is due, each with a picture, until each is done, skipped or snoozed. When several are
/// due together, one button answers them all. It comes up in the middle of the screen and, by the nag setting, keeps
/// coming back there until it is answered: in front, flashing in the taskbar, shaking, with the rest of the screen
/// darkened, and louder each time. A dose that asks for a video is answered by recording one.
/// </summary>
public sealed partial class AlarmWindow : Window
{
    private readonly ObservableCollection<AlarmItem> items = [];
    private readonly DispatcherTimer nagTimer = new();
    private readonly DispatcherTimer pulseTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly List<(Window Window, System.Drawing.Rectangle Bounds)> dims = [];
    private Brush bandBrush = KindLook.Brush(ReminderKind.Pill);
    private double volume;
    private bool silenced, recording, pulse;

    public AlarmWindow()
    {
        InitializeComponent();
        Icon = App.Icon;
        DoseList.ItemsSource = items;
        nagTimer.Tick += (_, _) => Nag();
        pulseTimer.Tick += (_, _) =>
        {
            pulse = !pulse;
            Band.Background = pulse ? (Brush)FindResource("AlarmBandRed") : bandBrush;
        };
        Closed += (_, _) =>
        {
            nagTimer.Stop();
            pulseTimer.Stop();
            foreach (var dim in dims) dim.Window.Close();
            dims.Clear();
        };
        // Insistent: it can't be put away by minimizing; Snooze or Skip does that.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && Level == NagLevel.Insistent && !recording)
                Dispatcher.BeginInvoke(() => WindowState = WindowState.Normal);
        };
        // Sized to its content once shown: the middle of the screen is known then.
        SizeChanged += (_, _) =>
        {
            if (IsVisible) Native.Center(this);
        };
    }

    /// <summary>How hard it insists: as set, and a step more (gentle to persistent, persistent to insistent) when a dose
    /// on it follows one that was missed.</summary>
    private NagLevel Level =>
        items.Any(i => i.Missed is not null) && App.Current.Nag < NagLevel.Insistent ? App.Current.Nag + 1 : App.Current.Nag;

    private AlarmItem Item(Dose dose)
    {
        var app = App.Current;
        return new AlarmItem(dose, app.Database.ImageDirectory, app.SnoozeMinutes, app.Schedule, app.Reminders);
    }

    /// <summary>Adds the doses not already on it; true when there was one.</summary>
    public bool Add(IEnumerable<Dose> doses)
    {
        var added = false;
        foreach (var dose in doses)
        {
            if (items.Any(i => i.Dose.Key == dose.Key)) continue;
            // An earlier dose of the same reminder still on the alarm is replaced, not taken as well.
            foreach (var older in items.Where(i => i.Reminder.Id == dose.Reminder.Id).ToList()) items.Remove(older);
            items.Add(Item(dose));
            added = true;
        }
        Update();
        return added;
    }

    /// <summary>After the reminders change: drops doses of reminders deleted or paused, shows edits.</summary>
    public void Keep(List<Reminder> reminders)
    {
        var kept = items
            .Select(item => (item, reminder: reminders.FirstOrDefault(m => m.Id == item.Reminder.Id && m.Active)))
            .Where(x => x.reminder is not null && x.reminder.Times.Any(t => t.Time == TimeOnly.FromDateTime(x.item.Dose.Due)))
            .Select(x =>
            {
                var item = Item(x.item.Dose with { Reminder = x.reminder! });
                item.Keep(x.item);
                item.Value = x.item.Value;
                if (x.item.Site.Length > 0) item.Site = x.item.Site;
                return item;
            })
            .ToList();
        items.Clear();
        foreach (var item in kept) items.Add(item);
        if (items.Count == 0) Close();
        else Update();
    }

    /// <summary>Brings it up in the middle of the screen; with <paramref name="ring"/>, starts the sound again at the
    /// chosen volume.</summary>
    public void ShowAlarm(bool ring)
    {
        var app = App.Current;
        var level = Level;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (level == NagLevel.Insistent) ShowDims();
        Native.Center(this);
        Activate();
        Native.Flash(this);
        if (ring && !recording)
        {
            silenced = false;
            // After a missed dose it starts louder.
            volume = Math.Min(1, app.Volume / 100.0 + (items.Any(i => i.Missed is not null) ? 0.25 : 0));
            app.Ring(items.Select(i => i.Dose), volume);
        }
        SilenceButton.Show(AlarmSound.Playing);

        nagTimer.Interval = TimeSpan.FromSeconds(level == NagLevel.Insistent ? 10 : 30);
        if (level == NagLevel.Gentle) nagTimer.Stop();
        else nagTimer.Start();
        if (level == NagLevel.Insistent) pulseTimer.Start();
    }

    /// <summary>Every 30 or 10 seconds until answered: back to the middle, in front, flashing; shaking and louder.</summary>
    private void Nag()
    {
        if (recording || items.Count == 0) return;
        var app = App.Current;
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (Level == NagLevel.Insistent) ShowDims();
        foreach (var (dim, bounds) in dims) Native.Cover(dim, bounds, under: this);
        Native.Center(this);
        Activate();
        Native.Flash(this);
        // Not while Claude's ideas are being read.
        if (Level == NagLevel.Insistent && !items.Any(i => i.Asking || i.Ideas.Count > 0)) Shake();
        if (app.Escalate && !silenced && AlarmSound.Playing && volume < 1)
        {
            volume = Math.Min(1, volume + 0.15);
            app.Ring(items.Select(i => i.Dose), volume);
        }
    }

    private void Shake()
    {
        var origin = Left;
        double[] offsets = [-18, 18, -15, 15, -10, 10, -6, 6, 0];
        var step = 0;
        var shake = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        shake.Tick += (_, _) =>
        {
            Left = origin + offsets[step];
            if (++step == offsets.Length) shake.Stop();
        };
        shake.Start();
    }

    /// <summary>Darkens every screen behind the alarm. Windows places each over its screen, in physical pixels,
    /// whatever the screen's scaling, and just under the alarm.</summary>
    private void ShowDims()
    {
        if (dims.Count > 0) return;
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var dim = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0, Top = 0, Width = 1, Height = 1,
            };
            dim.MouseDown += (_, _) => Activate();
            // It is only a dark fill: it ignores the scaling of the screen it's on, so that WPF doesn't rescale and move
            // it when it lands on a screen scaled otherwise than the main one, and Windows places it in physical pixels.
            System.Windows.Interop.HwndSource.FromHwnd(Native.Handle(dim)).AddHook(Native.IgnoreDpiChanges);
            Native.Cover(dim, screen.Bounds, under: this);
            dim.Show();
            Native.Cover(dim, screen.Bounds, under: this);
            dims.Add((dim, screen.Bounds));
        }
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            foreach (var (dim, bounds) in dims) Native.Cover(dim, bounds, under: this);
            Native.BringToTop(this);
        });
    }

    /// <summary>What the heading says for these kinds: the one thing they all are, as closely as it can be said.</summary>
    public static string HeadingFor(IReadOnlyCollection<ReminderKind> kinds)
    {
        if (kinds.Distinct().Count() == 1)
        {
            switch (kinds.First())
            {
                case ReminderKind.Injection: return "Time for your injection";
                case ReminderKind.Inhaler: return "Time for your inhaler";
                case ReminderKind.BloodSugar: return "Time to test your blood sugar";
                case ReminderKind.BloodPressure: return "Time to take your blood pressure";
                case ReminderKind.Weight: return "Time to weigh yourself";
                case ReminderKind.Meal: return "Time to eat";
                case ReminderKind.Drink: return "Time for a drink";
            }
        }
        return kinds.All(k => k.IsMedication()) ? "Time to take your medication"
            : kinds.All(k => k.IsCheck()) ? "Time for your checks"
            : kinds.All(k => k.IsMedication() || k.IsCheck()) ? "Time for your medication and checks"
            : kinds.All(k => k.Group() == AlertGroup.EatDrink) ? "Time to eat and drink"
            : "It's time";
    }

    private void Update()
    {
        if (items.Count == 0) return;
        var kinds = items.Select(i => i.Kind).ToList();
        Heading.Text = Title = HeadingFor(kinds);
        Subtitle.Text = (items.Count > 1 ? $"{items.Count} things at once · " : "") + "Due at " + Format.Time(items.Min(i => i.Dose.Due));
        // The band, its picture and the button for all of them in the colour of what's due: of its kind if there's
        // one, or of the most important group.
        bandBrush = KindLook.Brush(KindLook.ColorOf(kinds));
        if (!pulse) Band.Background = bandBrush;
        AllButton.Background = bandBrush;
        var first = items.OrderBy(i => i.Kind.Group()).First();
        BandPicture.Kind = first.Kind;
        BandPicture.Appearance = first.Appearance;
        BandPicture.Form = first.Form;
        AllBar.Show(items.Count > 1);
        AllButton.Content = items.Any(i => i.Reminder.RecordVideo) ? $"Record and finish all {items.Count}" : $"Done with all {items.Count}";
    }

    /// <summary>Records the video the doses ask for, if any, with the alarm quiet and still meanwhile.</summary>
    private RecordOutcome? Record(IReadOnlyList<AlarmItem> answered)
    {
        if (!answered.Any(i => i.Reminder.RecordVideo)) return RecordOutcome.WithoutVideo;
        recording = true;
        AlarmSound.Stop();
        SilenceButton.Show(false);
        try
        {
            return Evidence.Record(this, answered.Select(i => i.Dose).ToList());
        }
        finally
        {
            recording = false;
        }
    }

    private static void Done(AlarmItem item, RecordOutcome outcome)
    {
        App.Current.Schedule.Done(item.Dose, DateTime.Now, item.IsCheck ? item.Value : "", item.IsInjection ? item.Site : "",
            item.Reminder.RecordVideo ? outcome.Video : "", item.Reminder.RecordVideo ? outcome.Sha256 : "");
        PantryLog.Eaten(item.Dose);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AlarmItem item) return;
        if (Record([item]) is not { } outcome) return;
        Answer(item, i => Done(i, outcome));
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AlarmItem item) Answer(item, i => App.Current.Schedule.Skipped(i.Dose, DateTime.Now));
    }

    /// <summary>Something else eaten in place of the meal: logged, and the meal done, unless it was logged on its own.
    /// Someone's there, so the sound stops.</summary>
    private void SomethingElse_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AlarmItem item) return;
        Silence_Click(sender, e);
        var food = new FoodWindow(item.Dose) { Owner = this };
        if (food.ShowDialog() != true) return;
        if (food.Meal?.Key == item.Dose.Key) Answer(item, _ => { });
        else App.Current.Changed();
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AlarmItem item)
            Answer(item, i => App.Current.Schedule.Snooze(i.Dose, DateTime.Now.AddMinutes(App.Current.SnoozeMinutes)));
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        var all = items.ToList();
        if (Record(all) is not { } outcome) return;
        foreach (var item in all) Done(item, outcome);
        items.Clear();
        Close();
        App.Current.Changed();
    }

    private void Answer(AlarmItem item, Action<AlarmItem> action)
    {
        action(item);
        items.Remove(item);
        if (items.Count == 0) Close();
        else Update();
        App.Current.Changed();
    }

    private void Silence_Click(object sender, RoutedEventArgs e)
    {
        AlarmSound.Stop();
        silenced = true;
        SilenceButton.Show(false);
    }

    // ---- Other ideas for a meal, from Claude --------------------------------------------------------------------

    /// <summary>Asks Claude for three meals from what's in the kitchen, other than the one shown. Someone's there, so
    /// the sound stops.</summary>
    private async void AskIdeas_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AlarmItem item || App.Current.Assistant is not { } assistant || item.Meal is not { } meal) return;
        Silence_Click(sender, e);
        var kitchen = Pantry.Kitchen(PantryLog.Items(), App.Current.Measures);
        if (kitchen.Count == 0)
        {
            item.IdeasText = "There's nothing in your kitchen yet: add groceries on the Groceries page (from photos, or as you buy them).";
            return;
        }
        item.Asking = true;
        item.Ideas = [];
        item.IdeasText = "Asking Claude for ideas from your kitchen…";
        var key = item.Dose.Key;
        List<KitchenIdea> found = [];
        string text;
        try
        {
            found = await assistant.IdeasAsync(kitchen, meal.Slot, meal.Kcal > 0 ? meal.Kcal : 600, null, DateTime.Now, instead: item.Idea);
            text = found.Count == 0 ? "No ideas came back; try again." : "Pick one to have instead:";
        }
        catch (KitchenAssistantException ex)
        {
            text = ex.Message;
        }
        // The alarm may have been rebuilt meanwhile, or the dose answered.
        if (items.FirstOrDefault(i => i.Dose.Key == key) is not { } now) return;
        now.Asking = false;
        now.Ideas = found.Select(i => new AlarmIdea(now, i)).ToList();
        now.IdeasText = text;
    }

    /// <summary>An idea picked: it's the meal now, here and on the menu, and eating it takes what it uses off the
    /// kitchen's stock.</summary>
    private void HaveIdea_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AlarmIdea choice) return;
        if (items.FirstOrDefault(i => i.Dose.Key == choice.Item.Dose.Key) is not { Meal: { } meal } item) return;
        KitchenLog.PickIdea(meal, choice.Idea, DateTime.Now);
        item.Ideas = [];
        item.IdeasText = "";
        item.MealChanged();
        App.Current.KitchenChanged();
    }

    private void RecipeSteps_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AlarmItem { Recipe: { } recipe } item) App.Current.ShowSteps(recipe, item.Dose.Due);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closed with the window's X: whatever is left is snoozed, so it comes back rather than being lost.
        foreach (var item in items) App.Current.Schedule.Snooze(item.Dose, DateTime.Now.AddMinutes(App.Current.SnoozeMinutes));
        items.Clear();
        base.OnClosing(e);
    }
}
