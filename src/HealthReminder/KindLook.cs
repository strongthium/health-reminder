using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// The colour of each kind of reminder, so that a pill, an injection, a blood sugar test, a meal and a drink can be
/// told apart at a glance: on its badge, beside it in the lists, and across the top of the alarm. Each is dark enough
/// for white writing on it, and light enough to be seen on a dark background.
/// </summary>
public static class KindLook
{
    private static readonly Dictionary<Color, SolidColorBrush> Brushes = [];

    public static Color ColorOf(ReminderKind kind) => kind switch
    {
        ReminderKind.Injection => Color.FromRgb(0x7B, 0x3F, 0xB5),
        _ when kind.IsMedication() => Color.FromRgb(0x00, 0x79, 0x6B),
        ReminderKind.BloodSugar => Color.FromRgb(0xB0, 0x2A, 0x5B),
        _ when kind.IsCheck() => Color.FromRgb(0x3B, 0x55, 0xB5),
        ReminderKind.Meal => Color.FromRgb(0xB8, 0x4A, 0x0B),
        ReminderKind.Drink => Color.FromRgb(0x15, 0x65, 0xC0),
        _ => Color.FromRgb(0x54, 0x6E, 0x7A),
    };

    public static Color ColorOf(AlertGroup group) => group switch
    {
        AlertGroup.Check => ColorOf(ReminderKind.BloodPressure),
        AlertGroup.EatDrink => ColorOf(ReminderKind.Meal),
        _ => ColorOf(ReminderKind.Pill),
    };

    /// <summary>The colour of several reminders together: theirs if they're all of a kind, or else their group's.</summary>
    public static Color ColorOf(IReadOnlyCollection<ReminderKind> kinds) =>
        kinds.Distinct().Count() == 1 ? ColorOf(kinds.First()) : ColorOf(kinds.Group());

    public static SolidColorBrush Brush(Color color, byte alpha = 255)
    {
        var key = Color.FromArgb(alpha, color.R, color.G, color.B);
        if (!Brushes.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(key);
            brush.Freeze();
            Brushes[key] = brush;
        }
        return brush;
    }

    public static SolidColorBrush Brush(ReminderKind kind) => Brush(ColorOf(kind));

    /// <summary>A faint wash of the kind's colour, behind its picture.</summary>
    public static SolidColorBrush Tint(ReminderKind kind) => Brush(ColorOf(kind), 0x2E);
}

/// <summary>A <see cref="ReminderKind"/> or an <see cref="AlertGroup"/> to its colour; with the parameter "tint", to
/// a faint wash of it.</summary>
public sealed class KindBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value switch
        {
            ReminderKind kind => KindLook.ColorOf(kind),
            AlertGroup group => KindLook.ColorOf(group),
            _ => (Color?)null,
        };
        if (color is null) return null;
        return parameter as string == "tint" ? KindLook.Brush(color.Value, 0x2E) : KindLook.Brush(color.Value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
