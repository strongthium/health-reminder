using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Strongthium.HealthReminder;

/// <summary>A day's bar: its parts from the bottom up, each a value in a colour, what the mouse over it says, and the
/// day's label under it.</summary>
public sealed record Bar(string Label, IReadOnlyList<(double Value, Brush Brush)> Parts, string Tip)
{
    public double Total => Parts.Sum(p => p.Value);
}

/// <summary>
/// A bar a day, from the left: thin bars rounded at the top and sat on the baseline, with a sliver between stacked
/// parts, faint gridlines at round numbers, and a dashed line for a goal or a plan. Each day's whole column shows what
/// its bar says under the mouse; today's, the last, has its number on top.
/// </summary>
public sealed class DayBars : UserControl
{
    private const double PlotHeight = 120;

    public DayBars() => Focusable = false;

    /// <param name="line">A goal or plan to draw across, with what it's called.</param>
    /// <param name="format">How a value is written, on the axis and the bars.</param>
    public void Show(IReadOnlyList<Bar> bars, (double Value, string Text)? line = null, Func<double, string>? format = null)
    {
        format ??= v => v.ToString("#,0.#", CultureInfo.CurrentCulture);
        var top = Nice(Math.Max(bars.Count == 0 ? 0 : bars.Max(b => b.Total), line?.Value ?? 0));
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PlotHeight) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foreach (var _ in bars) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var span = Math.Max(1, bars.Count);
        // The goal's or plan's name, at the end of its line.
        if (line is not null) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The scale: 0, half and the top, with faint lines across.
        foreach (var at in new[] { 0, 0.5, 1 })
        {
            var y = PlotHeight * (1 - at);
            var label = Text(format(top * at), 11);
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(0, Math.Max(0, y - 8), 8, 0);
            // Over the days' row too, so that the one at 0 isn't cut off.
            Grid.SetRow(label, 1);
            Grid.SetRowSpan(label, 2);
            grid.Children.Add(label);
            var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, Math.Min(PlotHeight - 1, y), 0, 0), Opacity = at == 0 ? 1 : 0.5 };
            rule.SetResourceReference(Border.BackgroundProperty, "DividerStrokeColorDefaultBrush");
            Grid.SetRow(rule, 1);
            Grid.SetColumn(rule, 1);
            Grid.SetColumnSpan(rule, span);
            grid.Children.Add(rule);
        }

        var step = (int)Math.Ceiling(bars.Count / 10.0);
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            // The column, the whole height, for the mouse.
            var column = new Border { Background = Brushes.Transparent, ToolTip = bar.Tip, CornerRadius = new CornerRadius(4), Margin = new Thickness(1, 0, 1, 0) };
            column.MouseEnter += (_, _) => column.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
            column.MouseLeave += (_, _) => column.Background = Brushes.Transparent;
            Grid.SetColumn(column, i + 1);
            Grid.SetRowSpan(column, 3);
            grid.Children.Add(column);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, MaxWidth = 26, Margin = new Thickness(3, 0, 3, 0), IsHitTestVisible = false };
            var parts = bar.Parts.Where(p => p.Value > 0).ToList();
            for (var p = parts.Count - 1; p >= 0; p--)
            {
                var height = top > 0 ? PlotHeight * parts[p].Value / top : 0;
                stack.Children.Add(new Border
                {
                    Height = Math.Max(2, height - (p > 0 ? 2 : 0)),
                    Background = parts[p].Brush,
                    CornerRadius = p == parts.Count - 1 ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(0),
                    // A sliver of the surface between stacked parts.
                    Margin = new Thickness(0, 0, 0, p > 0 ? 2 : 0),
                });
            }
            Grid.SetRow(stack, 1);
            Grid.SetColumn(stack, i + 1);
            grid.Children.Add(stack);

            if (i == bars.Count - 1 && bar.Total > 0)
            {
                var value = Text(format(bar.Total), 11);
                value.HorizontalAlignment = HorizontalAlignment.Center;
                value.VerticalAlignment = VerticalAlignment.Bottom;
                value.IsHitTestVisible = false;
                value.Margin = new Thickness(0, 0, 0, top > 0 ? PlotHeight * bar.Total / top + 2 : 0);
                value.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                Grid.SetRow(value, 0);
                Grid.SetRowSpan(value, 2);
                Grid.SetColumn(value, i + 1);
                grid.Children.Add(value);
            }

            if (i % step == 0 || i == bars.Count - 1)
            {
                var day = Text(bar.Label, 11);
                day.HorizontalAlignment = HorizontalAlignment.Center;
                day.Margin = new Thickness(0, 4, 0, 0);
                day.IsHitTestVisible = false;
                Grid.SetRow(day, 2);
                Grid.SetColumn(day, i + 1);
                grid.Children.Add(day);
            }
        }

        if (line is { } goal && top > 0)
        {
            var y = PlotHeight * (1 - goal.Value / top);
            var dash = new Line
            {
                X1 = 0, X2 = 1, Stretch = Stretch.Fill, StrokeThickness = 1.5, StrokeDashArray = [4, 3], VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, y, 0, 0), IsHitTestVisible = false,
            };
            dash.SetResourceReference(Shape.StrokeProperty, "TextFillColorSecondaryBrush");
            Grid.SetRow(dash, 1);
            Grid.SetColumn(dash, 1);
            Grid.SetColumnSpan(dash, span);
            grid.Children.Add(dash);
            var label = Text(goal.Text, 11);
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(6, Math.Max(0, y - 8), 0, 0);
            label.IsHitTestVisible = false;
            Grid.SetRow(label, 1);
            Grid.SetRowSpan(label, 2);
            Grid.SetColumn(label, span + 1);
            grid.Children.Add(label);
        }
        Content = grid;
    }

    private static TextBlock Text(string text, double size)
    {
        var block = new TextBlock { Text = text, FontSize = size };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return block;
    }

    /// <summary>A round number at or above <paramref name="value"/> for the top of the scale: 1, 1.5, 2, 2.5, 3, 4, 5, 6 or 8
    /// times a power of ten; 1 for nothing.</summary>
    public static double Nice(double value)
    {
        if (value <= 0) return 1;
        var power = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var m in new[] { 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 })
            if (m * power >= value) return m * power;
        return 10 * power;
    }
}
