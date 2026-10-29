using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Strongthium.HealthReminder;

/// <summary>
/// The on-top button for logging a cigarette. A click logs one; when cigarettes are timed (Smoking page), the first
/// click is when it's lit and the next when it's out, and the button shows the minutes in between. It shows how many
/// today, red once the day's goal is reached. Drag it anywhere; it stays where it's put. A click is taken once: for a
/// moment after it, and for a double-click, more clicks are let go, so one cigarette isn't logged twice.
/// </summary>
public sealed partial class SmokeButton : Window
{
    private static readonly Brush Idle = new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F));
    private static readonly Brush Lit = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1.5);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer settling = new() { Interval = Settle };
    private readonly ToolTip tip = new();
    private Point down;
    private bool pressed;

    public SmokeButton()
    {
        InitializeComponent();
        timer.Tick += (_, _) => Update();
        settling.Tick += (_, _) =>
        {
            settling.Stop();
            Face.Cursor = Cursors.Hand;
            Look();
        };
        var menu = new ContextMenu();
        menu.Items.Add(Item("Undo the last one", (_, _) => SmokeLog.UndoLast()));
        menu.Items.Add(Item("Open the Smoking page", (_, _) => App.Current.ShowPage(MainWindow.SmokingPage)));
        menu.Items.Add(Item("Open Stats", (_, _) => App.Current.ShowPage(MainWindow.StatsPage)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Hide this button", (_, _) => App.Current.SmokeButtonShown = false));
        ContextMenu = menu;
        // Made as it opens, so that the time since the last one is right then.
        ToolTip = tip;
        ToolTipService.SetInitialShowDelay(this, 300);
        ToolTipOpening += (_, _) => tip.Content = TipText(DateTime.Now);
        Face.MouseEnter += (_, _) => Look();
        Face.MouseLeave += (_, _) => Look();
        Loaded += (_, _) => Place();
    }

    private static MenuItem Item(string header, RoutedEventHandler click)
    {
        var item = new MenuItem { Header = header };
        item.Click += click;
        return item;
    }

    /// <summary>Where it was left, or the right edge of the screen, a little below the middle; always on a screen.</summary>
    private void Place()
    {
        var area = SystemParameters.WorkArea;
        var saved = (App.Current.Database.Setting("smoke_button_at") ?? "").Split(',');
        // The button is the window's top-left 68 × 68; the window may be wider.
        const double size = 68;
        double x = area.Right - size - 12, y = area.Top + area.Height * 0.6;
        if (saved.Length == 2 && double.TryParse(saved[0], CultureInfo.InvariantCulture, out var sx) && double.TryParse(saved[1], CultureInfo.InvariantCulture, out var sy))
            (x, y) = (sx, sy);
        Left = Math.Clamp(x, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - size);
        Top = Math.Clamp(y, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - size);
        Update();
    }

    /// <summary>Under the mouse it lifts a little and lightens; while a click settles it's pressed in.</summary>
    private void Look()
    {
        var over = Face.IsMouseOver;
        var (scale, shade, color) = settling.IsEnabled ? (0.94, 0.22, Colors.Black) : over ? (1.08, 0.16, Colors.White) : (1.0, 0.0, Colors.White);
        Shade.Background = new SolidColorBrush(color);
        var duration = new Duration(TimeSpan.FromMilliseconds(120));
        Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration));
        Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration));
        Shade.BeginAnimation(OpacityProperty, new DoubleAnimation(shade, duration));
        Shadow.BlurRadius = over && !settling.IsEnabled ? 14 : 8;
    }

    /// <summary>Shows the count today, and the time since it was lit while one is going.</summary>
    public void Update()
    {
        var open = SmokeLog.Open();
        var today = SmokeLog.Today();
        var target = SmokeLog.TargetToday();
        CountText.Text = today.ToString(CultureInfo.CurrentCulture);
        Badge.Background = target is { } t && today >= t ? Brushes.DarkRed : new SolidColorBrush(Color.FromRgb(0x45, 0x5A, 0x64));
        Badge.Visibility = today > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (open is not null)
        {
            var going = DateTime.Now - open.Started;
            TimerText.Text = $"{(int)going.TotalMinutes}:{going.Seconds:00}";
            Face.Background = Lit;
            if (!timer.IsEnabled) timer.Start();
        }
        else
        {
            Face.Background = Idle;
            timer.Stop();
        }
        TimerText.Visibility = open is null ? Visibility.Collapsed : Visibility.Visible;
        Cigarette.Visibility = open is null ? Visibility.Visible : Visibility.Collapsed;
        if (tip.IsOpen) tip.Content = TipText(DateTime.Now);
    }

    /// <summary>What to do, how long since the last one, and today against yesterday by this time.</summary>
    private static string TipText(DateTime now)
    {
        var smokes = App.Current.Database.Smokes(now.Date.AddDays(-1));
        var open = SmokeLog.Open();
        var target = SmokeLog.TargetToday();
        var lines = new List<string>
        {
            open is not null ? $"Lit {DailyStats.Span(now - open.Started)} ago: click when it's out"
            : App.Current.SmokeTimed ? "Click when you light one, and again when it's out" : "Click to log a cigarette",
        };
        var last = smokes.Where(s => s.Id != open?.Id).Select(s => s.Started).DefaultIfEmpty().Max();
        lines.Add(last == default ? "None since yesterday"
            : $"Last one {DailyStats.Span(now - last)} ago, at {(last.Date == now.Date ? "" : "yesterday ")}{Format.Time(last)}");
        var today = DailyStats.ByThisTime(smokes, now, 0);
        var then = DailyStats.ByThisTime(smokes, now, 1);
        var diff = today - then;
        lines.Add($"{today} today" + (target is { } goal ? $" (goal {goal})" : "") + $"; {then} by this time yesterday: "
            + (diff == 0 ? "the same" : $"{Math.Abs(diff)} {(diff < 0 ? "fewer" : "more")}"));
        lines.Add("Drag to move it; right-click for more");
        return string.Join("\n", lines);
    }

    /// <summary>Whether a click now would be one too many: during the moment after the last.</summary>
    private bool Settling => settling.IsEnabled;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        // The second click of a double-click is the same cigarette.
        if (e.ClickCount > 1) return;
        pressed = true;
        down = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var moved = e.GetPosition(this) - down;
        if (Math.Abs(moved.X) < 4 && Math.Abs(moved.Y) < 4) return;
        // A drag, not a click.
        pressed = false;
        ReleaseMouseCapture();
        DragMove();
        App.Current.Database.SetSetting("smoke_button_at", string.Create(CultureInfo.InvariantCulture, $"{Left:0},{Top:0}"));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!pressed) return;
        pressed = false;
        ReleaseMouseCapture();
        if (Settling) return;
        settling.Start();
        Face.Cursor = Cursors.Arrow;
        Look();
        tip.IsOpen = false;
        SmokeLog.Click();
    }
}
