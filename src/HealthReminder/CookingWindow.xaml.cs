using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Strongthium.HealthReminder;

/// <summary>A step of the recipe being cooked: ticked off when done, with a countdown when it takes time.</summary>
public sealed class StepItem(CookStep step) : INotifyPropertyChanged
{
    private static readonly Brush Ringing = Frozen(0x4D, 0xE6, 0x51, 0x00), Running = Frozen(0x26, 0x15, 0x65, 0xC0);
    private bool done;

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public string Text => step.Text;
    public bool HasTimer => step.Minutes > 0;
    /// <summary>When the timer ends; null when it isn't running.</summary>
    public DateTime? Ends { get; private set; }
    public bool Finished { get; private set; }

    public bool Done
    {
        get => done;
        set
        {
            done = value;
            if (done) Stop();
            Changed(nameof(Done));
            Changed(nameof(Background));
        }
    }

    public string TimerText => Finished ? "Time's up" : Ends is { } ends ? Remaining(ends - DateTime.Now) : $"{step.Minutes} min";
    public Brush Background => Finished ? Ringing : Ends is not null ? Running : (Brush)Application.Current.FindResource("SubtleFillColorSecondaryBrush");

    private static string Remaining(TimeSpan left) => left <= TimeSpan.Zero ? "0:00" : $"{(int)left.TotalMinutes}:{left.Seconds:00}";

    /// <summary>Starts the timer, or stops it if it's running or done.</summary>
    public void Toggle()
    {
        if (Ends is not null || Finished) Stop();
        else Ends = DateTime.Now.AddMinutes(step.Minutes);
        Tick();
    }

    private void Stop()
    {
        Ends = null;
        Finished = false;
    }

    /// <summary>Updates the countdown; true when it has just run out.</summary>
    public bool Tick()
    {
        var ended = Ends is { } ends && ends <= DateTime.Now && !Finished;
        if (ended)
        {
            Finished = true;
            Ends = null;
        }
        Changed(nameof(TimerText));
        Changed(nameof(Background));
        return ended;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// The recipe being cooked, at the right side of the screen: what it needs, ticked off, and its steps, each that takes
/// time with a timer that chimes and brings the window forward when it's up. It can stay in front of other windows
/// (Settings for the meal alerts, or the box at its foot).
/// </summary>
public sealed partial class CookingWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private List<StepItem> steps = [];

    public CookingWindow()
    {
        InitializeComponent();
        Icon = App.Icon;
        OnTopBox.IsChecked = Topmost = KitchenLog.StepsOnTop;
        timer.Tick += (_, _) => Tick();
        var area = SystemParameters.WorkArea;
        Height = Math.Min(area.Height - 40, 900);
        Left = area.Right - Width - 20;
        Top = area.Top + 20;
        Closed += (_, _) => timer.Stop();
    }

    private static readonly Brush Kitchen = new SolidColorBrush(Color.FromRgb(0xB8, 0x4A, 0x0B));
    private Action? finished;

    /// <summary>A recipe; "Cooked it" takes what it used off the kitchen's stock.</summary>
    public void Show(Recipe recipe, DateTime? eatAt, Action whenCooked)
    {
        ShowSteps("Cooking: " + recipe.Name, recipe.Name,
            (eatAt is { } at ? $"To eat at {Format.Time(at)} · " : "") + $"about {recipe.PrepMinutes + recipe.CookMinutes} minutes · {recipe.Kcal:N0} kcal",
            recipe.Ingredients.Select(i => i.ToString()), recipe.Steps,
            "A food thermometer in the thickest part tells when meat and fish are cooked safely; colour doesn't.", Kitchen, whenCooked);
        FinishedButton.Content = "Cooked it";
    }

    /// <summary>Any list of steps: a recipe's, or a workout's (with <paramref name="whenDone"/> for its Done button).</summary>
    public void ShowSteps(string title, string name, string subtitle, IEnumerable<string> needs, IEnumerable<CookStep> list, string note, Brush header, Action? whenDone)
    {
        Title = title;
        Header.Background = header;
        NameText.Text = name;
        WhenText.Text = subtitle;
        IngredientList.ItemsSource = needs.ToList();
        steps = list.Select(s => new StepItem(s)).ToList();
        StepList.ItemsSource = steps;
        NoteText.Text = note;
        NoteText.Show(note.Length > 0);
        finished = whenDone;
        FinishedButton.Content = "Done";
        FinishedButton.Show(whenDone is not null);
        timer.Start();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void Finished_Click(object sender, RoutedEventArgs e)
    {
        finished?.Invoke();
        Close();
    }

    private void Tick()
    {
        var rang = false;
        foreach (var step in steps.Where(s => s.Ends is not null || s.Finished))
            rang |= step.Tick();
        if (!rang) return;
        // A timer is up: chime once and come forward.
        if (App.Current.ChimeWav(AlertGroup.EatDrink, App.Current.Volume / 100.0) is { } wav) AlarmSound.Play(wav, loop: false);
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void Timer_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is StepItem step) step.Toggle();
    }

    private void OnTop_Click(object sender, RoutedEventArgs e)
    {
        Topmost = OnTopBox.IsChecked == true;
        KitchenLog.StepsOnTop = Topmost;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
