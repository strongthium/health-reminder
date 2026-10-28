using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Strongthium.HealthReminder;

/// <summary>A time of day: an hour (written as the user's culture writes it, 8 AM or 08) and minutes in fives, or
/// typed exactly.</summary>
public sealed class TimeBox : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(nameof(Time), typeof(TimeSpan?), typeof(TimeBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TimeBox)d).Show()));

    private readonly ComboBox hours = new() { Width = 96, Margin = new Thickness(0, 0, 6, 0) };
    private readonly ComboBox minutes = new() { Width = 80, IsEditable = true };
    private bool showing;

    public TimeSpan? Time
    {
        get => (TimeSpan?)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public TimeBox()
    {
        var twelveHour = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('h');
        hours.ItemsSource = Enumerable.Range(0, 24)
            .Select(h => twelveHour ? DateTime.Today.AddHours(h).ToString("h tt", CultureInfo.CurrentCulture) : h.ToString("00", CultureInfo.CurrentCulture))
            .ToList();
        minutes.ItemsSource = Enumerable.Range(0, 12).Select(m => (m * 5).ToString("00", CultureInfo.InvariantCulture)).ToList();
        hours.SelectionChanged += (_, _) => Read();
        minutes.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(Read);
        minutes.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Read()));
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { hours, new TextBlock { Text = ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, minutes } };
        Show();
    }

    private void Show()
    {
        if (showing) return;
        showing = true;
        var time = Time ?? new TimeSpan(8, 0, 0);
        hours.SelectedIndex = time.Hours;
        minutes.Text = time.Minutes.ToString("00", CultureInfo.InvariantCulture);
        showing = false;
    }

    private void Read()
    {
        if (showing || hours.SelectedIndex < 0) return;
        if (!int.TryParse(minutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) || m is < 0 or > 59) return;
        showing = true;
        Time = new TimeSpan(hours.SelectedIndex, m, 0);
        showing = false;
    }
}

/// <summary>A number typed, or stepped with the arrows beside it or the up and down keys, between a minimum and a
/// maximum; empty is null. The arrows are stacked in a narrow column to the right of the box, so that whatever width
/// it's given, the number has the room.</summary>
public sealed class NumberBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double?), typeof(NumberBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NumberBox)d).Show()));
    public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(nameof(Increment), typeof(double), typeof(NumberBox), new PropertyMetadata(1.0));

    private readonly TextBox text = new() { MinWidth = 0, VerticalContentAlignment = VerticalAlignment.Center };
    private bool showing;

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Increment
    {
        get => (double)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public double Minimum { get; set; }
    public double Maximum { get; set; } = double.MaxValue;

    /// <summary>Raised when the value changes, by typing or stepping.</summary>
    public event EventHandler? ValueChanged;

    public NumberBox()
    {
        RepeatButton Step(string glyph, int direction)
        {
            // The theme's buttons are at least 32 high; these are half that, one above the other.
            var button = new RepeatButton
            {
                Content = new TextBlock
                {
                    Text = glyph, FontSize = 8,
                    FontFamily = TryFindResource("SymbolThemeFontFamily") as System.Windows.Media.FontFamily ?? new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                },
                Width = 28, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), Focusable = false,
                VerticalAlignment = VerticalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = direction > 0 ? "More (or the up arrow key)" : "Less (or the down arrow key)",
            };
            button.Click += (_, _) => StepBy(direction);
            return button;
        }
        text.TextChanged += (_, _) =>
        {
            if (showing) return;
            if (text.Text.Trim().Length == 0) Set(null, typed: true);
            else if (double.TryParse(text.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var v)) Set(v, typed: true);
        };
        text.LostFocus += (_, _) => Show();
        text.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)) return;
            StepBy(e.Key == System.Windows.Input.Key.Up ? 1 : -1);
            text.CaretIndex = text.Text.Length;
            e.Handled = true;
        };
        var arrows = new Grid { Margin = new Thickness(4, 0, 0, 0), RowDefinitions = { new RowDefinition(), new RowDefinition() } };
        var up = Step("", 1);
        var down = Step("", -1);
        up.Margin = new Thickness(0, 0, 0, 1);
        Grid.SetRow(down, 1);
        arrows.Children.Add(up);
        arrows.Children.Add(down);
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition { MinWidth = 48 }, new ColumnDefinition { Width = GridLength.Auto } } };
        Grid.SetColumn(arrows, 1);
        grid.Children.Add(text);
        grid.Children.Add(arrows);
        Content = grid;
    }

    // To the nearest step, and rounded again so that 0.1 steps don't add up to 0.30000000000000004.
    private void StepBy(int direction) => Set(Math.Round(Math.Round(((Value ?? 0) + direction * Increment) / Increment) * Increment, 6));

    private void Set(double? value, bool typed = false)
    {
        if (value is { } v) value = Math.Clamp(v, Minimum, Maximum);
        showing = typed;
        Value = value;
        showing = false;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Show()
    {
        if (showing) return;
        showing = true;
        text.Text = Value?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
        showing = false;
    }
}
