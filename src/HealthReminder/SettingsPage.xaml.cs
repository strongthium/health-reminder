using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Strongthium.HealthReminder;

/// <summary>A group's sound, as the Settings page shows and changes it.</summary>
public sealed class GroupSound(AlertGroup group) : INotifyPropertyChanged
{
    private static App App => App.Current;

    public AlertGroup Group => group;
    public string Title => group.Label();
    public string Description => group switch
    {
        AlertGroup.Check => "Blood sugar, blood pressure, weight and other checks",
        AlertGroup.EatDrink => "Meals, snacks and water",
        _ => "Pills, injections and all other medication, and anything else",
    };
    /// <summary>The kind its picture is of.</summary>
    public ReminderKind Kind => group switch
    {
        AlertGroup.Check => ReminderKind.BloodSugar,
        AlertGroup.EatDrink => ReminderKind.Meal,
        _ => ReminderKind.Pill,
    };

    public IReadOnlyList<string> Sounds { get; } = Enum.GetValues<ChimeSound>().Select(s => s.Label()).ToList();
    public IReadOnlyList<string> Repeats { get; } = ["Until answered", "Once"];

    public int SoundIndex
    {
        get => (int)App.SoundFor(group);
        set
        {
            App.SetSound(group, (ChimeSound)value);
            Changed(nameof(IsCustom));
        }
    }

    public int RepeatIndex
    {
        get => App.RepeatsFor(group) ? 0 : 1;
        set => App.SetRepeats(group, value == 0);
    }

    public bool IsCustom => App.SoundFor(group) == ChimeSound.Custom;

    public string FileText => App.SoundFileFor(group) is { Length: > 0 } file ? Path.GetFileName(file) : "No file chosen: the chime plays";

    public void SetFile(string file)
    {
        App.SetSoundFile(group, file);
        Changed(nameof(FileText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>The sounds, how the reminder insists, the camera, starting with Windows, and where the data is.</summary>
public sealed partial class SettingsPage : UserControl, IPage
{
    private static readonly int[] SnoozeChoices = [5, 10, 15, 30, 60];
    private static readonly int[] VideoLengths = [30, 60, 120, 300];
    private bool loading;

    public SettingsPage()
    {
        InitializeComponent();
        var app = App.Current;
        loading = true;
        GroupSounds.ItemsSource = Enum.GetValues<AlertGroup>().Select(g => new GroupSound(g)).ToList();
        VolumeSlider.Value = app.Volume;
        VolumeText.Text = $"{app.Volume}%";
        EscalateBox.IsChecked = app.Escalate;
        (app.Nag switch { NagLevel.Gentle => NagGentle, NagLevel.Insistent => NagInsistent, _ => NagPersistent }).IsChecked = true;
        SnoozeBox.ItemsSource = SnoozeChoices.Select(m => m < 60 ? $"{m} minutes" : "1 hour").ToList();
        SnoozeBox.SelectedIndex = Math.Max(0, Array.IndexOf(SnoozeChoices, app.SnoozeMinutes));
        VideoLengthBox.ItemsSource = VideoLengths.Select(s => s < 60 ? $"{s} seconds" : s == 60 ? "1 minute" : $"{s / 60} minutes").ToList();
        VideoLengthBox.SelectedIndex = Math.Max(0, Array.IndexOf(VideoLengths, app.VideoSeconds));
        StartAtSignIn.IsChecked = Autostart.Enabled;
        CloseBox.SelectedIndex = (int)app.CloseAction;
        DrinkUnitBox.SelectedIndex = (int)app.DrinkUnit;
        MeasuresBox.SelectedIndex = (int)app.Measures;
        TemperatureUnitBox.SelectedIndex = app.Fahrenheit ? 1 : 0;
        ShowCold();
        ShowClaude();
        loading = false;

        DataFolder.Text = "Your reminders, readings, photos and videos are kept on this computer only, in " + app.Database.Directory;
        VersionText.Text = "Version " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "");
        Logo.Source = App.Icon;
        CameraBox.ItemsSource = new[] { "Looking for cameras…" };
        CameraBox.SelectedIndex = 0;
        CameraBox.IsEnabled = false;
        _ = FindCameras();
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText is null) return;
        VolumeText.Text = $"{(int)VolumeSlider.Value}%";
        if (!loading) App.Current.Volume = (int)VolumeSlider.Value;
    }

    /// <summary>Plays a group's sound at the chosen volume for a few seconds.</summary>
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not GroupSound sound) return;
        AlarmSound.Stop();
        if (App.Current.ChimeWav(sound.Group, VolumeSlider.Value / 100) is not { } wav) return;
        AlarmSound.Play(wav, loop: true);
        var stop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        stop.Tick += (_, _) =>
        {
            stop.Stop();
            AlarmSound.Stop();
        };
        stop.Start();
    }

    private void ChooseSound_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not GroupSound sound) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Choose the sound for " + sound.Title.ToLowerInvariant(), Filter = "WAV sound (*.wav)|*.wav" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        if (Sounds.Scale(File.ReadAllBytes(dialog.FileName), 1) is null)
        {
            MessageBox.Show(Window.GetWindow(this), "That isn't a WAV file Health Reminder can play.", "Health Reminder");
            return;
        }
        sound.SetFile(dialog.FileName);
    }

    private void Escalate_Changed(object sender, RoutedEventArgs e)
    {
        if (!loading) App.Current.Escalate = EscalateBox.IsChecked == true;
    }

    private void Nag_Changed(object sender, RoutedEventArgs e)
    {
        if (loading || sender is not RadioButton { IsChecked: true } button) return;
        App.Current.Nag = button == NagGentle ? NagLevel.Gentle : button == NagInsistent ? NagLevel.Insistent : NagLevel.Persistent;
    }

    private void Snooze_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && SnoozeBox.SelectedIndex >= 0) App.Current.SnoozeMinutes = SnoozeChoices[SnoozeBox.SelectedIndex];
    }

    private void StartAtSignIn_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        try
        {
            Autostart.Enabled = StartAtSignIn.IsChecked == true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            StartAtSignIn.IsChecked = Autostart.Enabled;
        }
    }

    private void ShowClaude()
    {
        var app = App.Current;
        var key = app.AnthropicKey;
        ClaudeBox.IsChecked = app.ClaudeOn;
        ClaudeBox.IsEnabled = key.Length > 0;
        ForgetKeyButton.Show(key.Length > 0);
        KeyText.Text = key.Length == 0 ? "Save a key first to turn it on."
            : $"A key ending in …{key[^Math.Min(4, key.Length)..]} is saved." + (app.ClaudeOn ? $" It uses {KitchenAssistant.Model}." : " It's off.");
    }

    private void Claude_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        App.Current.ClaudeOn = ClaudeBox.IsChecked == true;
        ShowClaude();
        App.Current.KitchenChanged();
    }

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Password.Trim();
        if (!key.StartsWith("sk-ant-", StringComparison.Ordinal))
        {
            KeyText.Text = "That doesn't look like an Anthropic API key: they start with sk-ant-.";
            return;
        }
        App.Current.AnthropicKey = key;
        App.Current.ClaudeOn = true;
        KeyBox.Clear();
        ShowClaude();
        App.Current.KitchenChanged();
    }

    private void ForgetKey_Click(object sender, RoutedEventArgs e)
    {
        App.Current.AnthropicKey = "";
        App.Current.ClaudeOn = false;
        ShowClaude();
        App.Current.KitchenChanged();
    }

    private void DrinkUnit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || DrinkUnitBox.SelectedIndex < 0) return;
        App.Current.DrinkUnit = (DrinkUnit)DrinkUnitBox.SelectedIndex;
        App.Current.Changed();
    }

    /// <summary>The fridge and freezer in the unit chosen, to the degree (the standard 4.4 °C shows as 4), and what's wrong
    /// with them.</summary>
    private void ShowCold()
    {
        var app = App.Current;
        var cold = app.Cold;
        double Shown(double c) => Math.Round(app.Fahrenheit ? ColdStorage.ToF(c) : c);
        FridgeBox.Minimum = app.Fahrenheit ? 14 : -10;
        FridgeBox.Maximum = app.Fahrenheit ? 68 : 20;
        FreezerBox.Minimum = -40;    // the same in both
        FreezerBox.Maximum = app.Fahrenheit ? 68 : 20;
        FridgeBox.Value = Shown(cold.FridgeC);
        FreezerBox.Value = Shown(cold.FreezerC);
        ShowColdWarning(cold);
    }

    private void ShowColdWarning(ColdStorage cold)
    {
        var warnings = cold.Warnings(App.Current.Fahrenheit);
        ColdWarning.Text = string.Join(" ", warnings);
        ColdWarning.Show(warnings.Count > 0);
    }

    private void Cold_Changed(object? sender, EventArgs e)
    {
        if (loading || FridgeBox.Value is not { } fridge || FreezerBox.Value is not { } freezer) return;
        var app = App.Current;
        var cold = app.Fahrenheit ? new ColdStorage(ColdStorage.FromF(fridge), ColdStorage.FromF(freezer)) : new ColdStorage(fridge, freezer);
        app.Cold = cold;
        ShowColdWarning(cold);
        app.KitchenChanged();
    }

    private void TemperatureUnit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || TemperatureUnitBox.SelectedIndex < 0) return;
        App.Current.Fahrenheit = TemperatureUnitBox.SelectedIndex == 1;
        loading = true;
        ShowCold();
        loading = false;
    }

    private void Measures_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && MeasuresBox.SelectedIndex >= 0) App.Current.Measures = (MeasureSystem)MeasuresBox.SelectedIndex;
    }

    private void Close_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && CloseBox.SelectedIndex >= 0) App.Current.CloseAction = (CloseAction)CloseBox.SelectedIndex;
    }

    /// <summary>Shows what closing does now, after the close dialog was told not to ask again.</summary>
    public void Refresh()
    {
        loading = true;
        CloseBox.SelectedIndex = (int)App.Current.CloseAction;
        loading = false;
    }

    private async Task FindCameras()
    {
        try
        {
            var names = await Camera.NamesAsync();
            loading = true;
            CameraBox.ItemsSource = names.Count > 0 ? names : ["No camera found"];
            var index = names.IndexOf(App.Current.Camera);
            CameraBox.SelectedIndex = index >= 0 ? index : 0;
            CameraBox.IsEnabled = names.Count > 0;
            loading = false;
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or DllNotFoundException)
        {
            CameraBox.ItemsSource = new[] { "Cameras can't be listed: " + exception.Message };
            CameraBox.SelectedIndex = 0;
            CameraBox.IsEnabled = false;
        }
    }

    private void FindCameras_Click(object sender, RoutedEventArgs e) => _ = FindCameras();

    private void Camera_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && CameraBox.IsEnabled && CameraBox.SelectedItem is string name) App.Current.Camera = name;
    }

    private void VideoLength_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && VideoLengthBox.SelectedIndex >= 0) App.Current.VideoSeconds = VideoLengths[VideoLengthBox.SelectedIndex];
    }

    private static void Open(string folder)
    {
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void OpenVideos_Click(object sender, RoutedEventArgs e) => Open(App.Current.Database.VideoDirectory);

    private void OpenData_Click(object sender, RoutedEventArgs e) => Open(App.Current.Database.Directory);
}
