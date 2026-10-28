using System.Windows;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Icons for the reminders that aren't pills: a syringe, a bottle, an inhaler, a drop of blood, a plate, a glass...
/// Each is drawn in a 100 × 100 square, scaled to fit the space it's given.
/// </summary>
internal static class KindIcons
{
    private static readonly Brush Shadow = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
    private static readonly Pen Outline = new(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x4E)), 3);
    private static readonly Brush White = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xF8));
    private static readonly Brush Grey = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));
    private static readonly Brush DarkGrey = new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x76));
    private static readonly Brush Teal = new SolidColorBrush(Color.FromRgb(0x2E, 0xB8, 0xAA));
    private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(0x3E, 0x7B, 0xD6));
    private static readonly Brush Water = new SolidColorBrush(Color.FromArgb(200, 0x4A, 0xA3, 0xE0));
    private static readonly Brush Glass = new SolidColorBrush(Color.FromArgb(60, 0xA8, 0xD4, 0xF0));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x41));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xC7, 0x7C, 0x0E));
    private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(0xF2, 0x8C, 0x28));
    private static readonly Brush Skin = new SolidColorBrush(Color.FromRgb(0xE9, 0xD3, 0xB4));
    private static readonly Brush SkinDark = new SolidColorBrush(Color.FromRgb(0xD3, 0xB4, 0x8A));
    private static readonly Brush Yellow = new SolidColorBrush(Color.FromRgb(0xF2, 0xC8, 0x3C));
    private static readonly Brush Shine = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255));

    private const string DropPath = "M 50,8 C 50,8 20,48 20,64 A 30,30 0 0 0 80,64 C 80,48 50,8 50,8 Z";

    public static void Draw(DrawingContext context, ReminderKind kind, Rect bounds)
    {
        var side = Math.Min(bounds.Width, bounds.Height) * 0.9;
        if (side < 4) return;
        var origin = new Point(bounds.X + (bounds.Width - side) / 2, bounds.Y + (bounds.Height - side) / 2);
        context.PushTransform(new MatrixTransform(side / 100, 0, 0, side / 100, origin.X, origin.Y));

        void Path(Brush? fill, string data, Pen? pen = null) => context.DrawGeometry(fill, pen ?? Outline, Geometry.Parse(data));
        void Box(Brush? fill, double x, double y, double w, double h, double r, Pen? pen = null) =>
            context.DrawGeometry(fill, pen ?? Outline, new RectangleGeometry(new Rect(x, y, w, h), r, r));
        void Line(double x1, double y1, double x2, double y2, Pen? pen = null) => context.DrawLine(pen ?? Outline, new Point(x1, y1), new Point(x2, y2));
        void Round(Brush? fill, double x, double y, double r, Pen? pen = null) => context.DrawEllipse(fill, pen ?? Outline, new Point(x, y), r, r);

        switch (kind)
        {
            case ReminderKind.Injection:
                context.PushTransform(new RotateTransform(-36, 50, 50));
                {
                    Line(78, 50, 97, 50, new Pen(DarkGrey, 2));
                    Box(Grey, 71, 45, 8, 10, 2);
                    Box(Grey, 25, 33, 5, 34, 2);
                    Box(Grey, 8, 46, 22, 8, 1);
                    Box(DarkGrey, 4, 37, 6, 26, 2);
                    Box(White, 29, 39, 43, 22, 3);
                    context.DrawRectangle(Teal, null, new Rect(44, 41, 26, 18));
                    Box(DarkGrey, 30, 40, 6, 20, 1, new Pen(DarkGrey, 1));
                    for (var x = 40; x <= 66; x += 6) Line(x, 39, x, 45, new Pen(DarkGrey, 1.5));
                    Box(null, 29, 39, 43, 22, 3);
                }
                context.Pop();
                break;

            case ReminderKind.Liquid:
                Box(White, 40, 10, 20, 14, 3);
                Box(Amber, 43, 24, 14, 10, 2);
                Box(Amber, 24, 32, 52, 60, 10);
                Box(White, 30, 52, 40, 24, 3, new Pen(DarkGrey, 1.5));
                Line(36, 60, 64, 60, new Pen(DarkGrey, 2));
                Line(36, 68, 56, 68, new Pen(DarkGrey, 2));
                Box(Shine, 30, 36, 8, 12, 4, new Pen(Brushes.Transparent, 0));
                break;

            case ReminderKind.Inhaler:
                Box(Grey, 40, 6, 22, 44, 6);
                Path(Blue, "M 34,32 H 68 V 70 H 88 V 92 H 44 Q 34,92 34,82 Z");
                Box(DarkGrey, 76, 70, 12, 22, 2);
                Box(Shine, 40, 38, 8, 26, 4, new Pen(Brushes.Transparent, 0));
                break;

            case ReminderKind.Drops:
                Path(Blue, DropPath);
                Path(Shine, "M 36,58 Q 36,48 44,38", new Pen(Shine, 6));
                break;

            case ReminderKind.Patch:
                Box(Skin, 12, 22, 76, 56, 16, new Pen(SkinDark, 3));
                Box(SkinDark, 32, 36, 36, 28, 8, new Pen(Brushes.Transparent, 0));
                for (var x = 20; x <= 80; x += 10) { Round(SkinDark, x, 28, 1.5, new Pen(Brushes.Transparent, 0)); Round(SkinDark, x, 72, 1.5, new Pen(Brushes.Transparent, 0)); }
                break;

            case ReminderKind.OtherMedication:
                Box(White, 22, 8, 56, 18, 4);
                Box(Orange, 26, 26, 48, 66, 8);
                Box(White, 31, 44, 38, 28, 3, new Pen(DarkGrey, 1.5));
                Line(37, 54, 63, 54, new Pen(DarkGrey, 2));
                Line(37, 62, 55, 62, new Pen(DarkGrey, 2));
                break;

            case ReminderKind.BloodSugar:
                Path(Red, DropPath);
                Path(Shine, "M 36,58 Q 36,48 44,38", new Pen(Shine, 6));
                break;

            case ReminderKind.BloodPressure:
                Round(White, 50, 54, 38);
                for (var i = 0; i <= 8; i++)
                {
                    var angle = Math.PI * (0.75 + 1.5 * i / 8);
                    Line(50 + 30 * Math.Cos(angle), 54 + 30 * Math.Sin(angle), 50 + 36 * Math.Cos(angle), 54 + 36 * Math.Sin(angle), new Pen(DarkGrey, 2));
                }
                Line(50, 54, 72, 34, RoundPen(Red, 4));
                Round(DarkGrey, 50, 54, 5);
                break;

            case ReminderKind.Weight:
                Box(new SolidColorBrush(Color.FromRgb(0xE4, 0xE6, 0xEA)), 10, 18, 80, 72, 14);
                Box(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)), 32, 28, 36, 16, 3);
                Line(40, 36, 60, 36, new Pen(Teal, 3));
                Box(null, 20, 52, 26, 30, 6, new Pen(Grey, 2));
                Box(null, 54, 52, 26, 30, 6, new Pen(Grey, 2));
                break;

            case ReminderKind.OtherCheck:
                Box(White, 42, 8, 16, 66, 8);
                Round(Red, 50, 80, 13);
                Box(Red, 46.5, 36, 7, 40, 3, new Pen(Brushes.Transparent, 0));
                for (var y = 20; y <= 60; y += 10) Line(58, y, 64, y, new Pen(DarkGrey, 2));
                break;

            case ReminderKind.Meal:
                Round(Shadow, 50, 55, 34, new Pen(Brushes.Transparent, 0));
                Round(White, 50, 52, 33);
                Round(null, 50, 52, 22, new Pen(Grey, 2));
                Line(10, 18, 10, 86, RoundPen(DarkGrey, 4));
                foreach (var x in new[] { 5.0, 10, 15 }) Line(x, 16, x, 34, RoundPen(DarkGrey, 2.5));
                Path(DarkGrey, "M 90,16 Q 98,40 90,52 L 90,86", RoundPen(DarkGrey, 4));
                break;

            case ReminderKind.Drink:
                Path(Glass, "M 24,10 L 76,10 L 67,92 L 33,92 Z");
                Path(Water, "M 27.5,40 L 72.5,40 L 67,92 L 33,92 Z", new Pen(Brushes.Transparent, 0));
                Path(null, "M 24,10 L 76,10 L 67,92 L 33,92 Z");
                Path(null, "M 32,18 L 38,82", RoundPen(Shine, 5));
                break;

            default:
                Path(Yellow, "M 50,10 C 30,10 26,32 26,50 L 16,72 H 84 L 74,50 C 74,32 70,10 50,10 Z");
                Round(Yellow, 50, 82, 8);
                break;
        }
        context.Pop();
    }

    private static Pen RoundPen(Brush brush, double width) => new(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
}
