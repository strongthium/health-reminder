using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Draws a pill from its <see cref="PillAppearance"/>: its shape and colours, the score line and the imprint, as many
/// times as are taken at once. The drawing fills the element; the pill's real size is said in words beside it. For the
/// other kinds of reminder it draws an icon (<see cref="KindIcons"/>).
/// </summary>
public sealed class PillView : FrameworkElement
{
    public static readonly DependencyProperty AppearanceProperty = Register(nameof(Appearance), typeof(PillAppearance), null);
    public static readonly DependencyProperty FormProperty = Register(nameof(Form), typeof(string), "");
    public static readonly DependencyProperty KindProperty = Register(nameof(Kind), typeof(ReminderKind), ReminderKind.Pill);
    public static readonly DependencyProperty CountProperty = Register(nameof(Count), typeof(double), 1.0);

    private static DependencyProperty Register(string name, Type type, object? fallback) =>
        DependencyProperty.Register(name, type, typeof(PillView), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>A pill is drawn from its appearance; anything else as an icon of its kind.</summary>
    public ReminderKind Kind
    {
        get => (ReminderKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>How many pills to draw: 2 for two tablets, 0.5 for half of one.</summary>
    public double Count
    {
        get => (double)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public PillAppearance? Appearance
    {
        get => (PillAppearance?)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>The dosage form: a capsule whose shape the label doesn't give is drawn as a capsule.</summary>
    public string Form
    {
        get => (string)GetValue(FormProperty);
        set => SetValue(FormProperty, value);
    }

    /// <summary>SPL's colour names, as colours that look like the pills.</summary>
    public static Color ColorOf(string name) => name.ToUpperInvariant() switch
    {
        "WHITE" => Color.FromRgb(0xF7, 0xF6, 0xF2),
        "BLACK" => Color.FromRgb(0x2A, 0x2A, 0x2E),
        "BLUE" => Color.FromRgb(0x3E, 0x7B, 0xD6),
        "BROWN" => Color.FromRgb(0x8B, 0x5A, 0x2B),
        "GRAY" or "GREY" => Color.FromRgb(0x9A, 0xA0, 0xA6),
        "GREEN" => Color.FromRgb(0x3F, 0xA4, 0x5B),
        "ORANGE" => Color.FromRgb(0xF2, 0x8C, 0x28),
        "PINK" => Color.FromRgb(0xF4, 0xA6, 0xC1),
        "PURPLE" => Color.FromRgb(0x8E, 0x5C, 0xC2),
        "RED" => Color.FromRgb(0xD6, 0x45, 0x41),
        "TURQUOISE" => Color.FromRgb(0x2E, 0xC4, 0xB6),
        "YELLOW" => Color.FromRgb(0xF2, 0xD2, 0x3C),
        _ => Color.FromRgb(0xE4, 0xE6, 0xEA),
    };

    /// <summary>The shapes SPL names, for the editor's list.</summary>
    public static readonly string[] Shapes =
    [
        "ROUND", "OVAL", "CAPSULE", "RECTANGLE", "SQUARE", "TRIANGLE", "DIAMOND", "PENTAGON (5 SIDED)", "HEXAGON (6 SIDED)",
        "HEPTAGON (7 SIDED)", "OCTAGON (8 SIDED)", "BULLET", "TEAR", "SEMI-CIRCLE", "TRAPEZOID", "DOUBLE CIRCLE", "CLOVER", "GEAR", "FREEFORM",
    ];

    public static readonly string[] Colors =
        ["WHITE", "YELLOW", "ORANGE", "PINK", "RED", "PURPLE", "BLUE", "TURQUOISE", "GREEN", "BROWN", "GRAY", "BLACK"];

    private double PixelsPerDip => VisualTreeHelper.GetDpi(this).PixelsPerDip;

    protected override void OnRender(DrawingContext context)
    {
        var bounds = new Rect(RenderSize);
        if (bounds.Width < 4 || bounds.Height < 4) return;
        if (Kind != ReminderKind.Pill)
        {
            KindIcons.Draw(context, Kind, bounds);
            return;
        }

        // As many pills as are taken at once, side by side; half a tablet is drawn as half. More than four are one
        // pill and the count.
        var count = Count <= 0 ? 1 : Count;
        var whole = (int)Math.Floor(count);
        var half = count - whole >= 0.25;
        var drawn = whole + (half ? 1 : 0);
        if (drawn > 4 || count - whole is > 0.01 and < 0.25 or > 0.75)
        {
            DrawPill(context, new Rect(bounds.X, bounds.Y, bounds.Width * 0.72, bounds.Height), false);
            var text = new FormattedText("×" + count.ToString("0.##", CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), Math.Max(10, bounds.Height * 0.28),
                new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x76)), PixelsPerDip);
            context.DrawText(text, new Point(bounds.Right - text.Width, bounds.Top + (bounds.Height - text.Height) / 2));
            return;
        }
        var width = bounds.Width / drawn;
        for (var i = 0; i < drawn; i++)
            DrawPill(context, new Rect(bounds.X + i * width, bounds.Y, width, bounds.Height), half && i == drawn - 1);
    }

    private void DrawPill(DrawingContext context, Rect area, bool half)
    {
        var appearance = Appearance ?? PillAppearance.Unknown;
        var known = appearance.IsKnown;
        var shape = appearance.Shape.ToUpperInvariant();
        if (shape.Length == 0) shape = Form.Contains("CAPSULE", StringComparison.OrdinalIgnoreCase) ? "CAPSULE" : "ROUND";

        // Width : height of each shape, and the box it's drawn in, centred, with room for the shadow.
        var aspect = shape switch
        {
            "CAPSULE" => 2.4,
            "OVAL" or "TEAR" or "BULLET" => 1.7,
            "RECTANGLE" or "TRAPEZOID" => 1.8,
            "SEMI-CIRCLE" => 2.0,
            "DOUBLE CIRCLE" => 1.6,
            _ => 1.0,
        };
        var w = Math.Min(area.Width * 0.92, area.Height * 0.86 * aspect);
        var h = w / aspect;
        if (w <= 2 || h <= 2) return;
        var box = new Rect(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2 - h * 0.03, w, h);
        var centerX = box.X + box.Width / 2;
        var centerY = box.Y + box.Height / 2;

        var geometry = Outline(shape, box);
        var colors = appearance.Colors.Count > 0 ? appearance.Colors.Select(ColorOf).ToList() : [ColorOf("")];
        var edge = Darken(colors[0], 0.45);

        // Half a tablet: the other half is a faint outline.
        if (half)
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(90, edge.R, edge.G, edge.B)), Math.Max(1, h * 0.025)) { DashStyle = new DashStyle([3, 3], 0) }, geometry);
        if (half) context.PushClip(new RectangleGeometry(new Rect(box.X, box.Y - h, box.Width / 2, box.Height * 3)));

        context.DrawGeometry(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), null, Outline(shape, new Rect(box.X, box.Y + h * 0.06, box.Width, box.Height)));

        if (colors.Count > 1)
        {
            // Two-tone: one colour a half, split across the long side.
            Rect[] halves = [new(box.X, box.Y, box.Width / 2, box.Height), new(centerX, box.Y, box.Width / 2, box.Height)];
            for (var i = 0; i < 2; i++)
            {
                context.PushClip(new RectangleGeometry(halves[i]));
                context.DrawGeometry(Shade(colors[i], box), null, geometry);
                context.Pop();
            }
            context.DrawLine(new Pen(new SolidColorBrush(Darken(colors[0], 0.3)), Math.Max(1, h * 0.025)), new Point(centerX, box.Top), new Point(centerX, box.Bottom));
        }
        else
        {
            context.DrawGeometry(Shade(colors[0], box), null, geometry);
        }
        var outline = new Pen(new SolidColorBrush(edge), Math.Max(1, h * 0.03));
        if (!known) outline.DashStyle = new DashStyle([3, 2], 0);
        context.DrawGeometry(null, outline, geometry);

        // A shine across the top.
        context.PushClip(geometry);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), null, new Point(centerX - w * 0.08, box.Top + h * 0.2), w * 0.32, h * 0.1);

        // Score lines across the short side: one for halves, two for thirds, a cross for quarters.
        if (appearance.Score > 1 && shape != "CAPSULE")
        {
            var pen = new Pen(new SolidColorBrush(Darken(colors[0], 0.35)), Math.Max(1, h * 0.03));
            if (appearance.Score == 4)
            {
                context.DrawLine(pen, new Point(centerX, box.Top), new Point(centerX, box.Bottom));
                context.DrawLine(pen, new Point(box.Left, centerY), new Point(box.Right, centerY));
            }
            else
            {
                for (var i = 1; i < appearance.Score; i++)
                {
                    var x = box.Left + box.Width * i / appearance.Score;
                    context.DrawLine(pen, new Point(x, box.Top), new Point(x, box.Bottom));
                }
            }
        }
        context.Pop();

        var sides = appearance.ImprintSides;
        var label = !known ? "?" : sides.Count > 0 ? sides[0] : "";
        if (label.Length > 0)
        {
            var ink = Luminance(colors[0]) > 0.55 ? Color.FromArgb(200, 40, 40, 48) : Color.FromArgb(220, 255, 255, 255);
            var size = Math.Clamp(h * (label.Length <= 3 ? 0.36 : 0.26), 7, 64);
            FormattedText Text(double emSize) => new(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), emSize, new SolidColorBrush(ink), PixelsPerDip);
            var text = Text(size);
            if (text.Width > w * 0.78) text = Text(Math.Max(6, size * w * 0.78 / text.Width));
            var center = colors.Count > 1 && text.Width <= w * 0.42 ? new Point(box.Left + box.Width * 0.25, centerY) : new Point(centerX, centerY);
            context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
        }
        if (half) context.Pop();
    }

    private static Geometry Outline(string shape, Rect r)
    {
        var (x, y, w, h) = (r.X, r.Y, r.Width, r.Height);
        switch (shape)
        {
            case "CAPSULE":
                return new RectangleGeometry(r, h / 2, h / 2);
            case "RECTANGLE":
                return new RectangleGeometry(r, h * 0.28, h * 0.28);
            case "SQUARE":
                return new RectangleGeometry(r, h * 0.18, h * 0.18);
            case "OVAL":
            case "ROUND":
            case "CLOVER":
            case "GEAR":
            case "FREEFORM":
                return new EllipseGeometry(r);
            case "DOUBLE CIRCLE":
                return new CombinedGeometry(GeometryCombineMode.Union,
                    new EllipseGeometry(new Rect(x, y, h, h)), new EllipseGeometry(new Rect(x + w - h, y, h, h)));
            case "TRIANGLE":
                return Polygon(r, 3, -90, rounded: true);
            case "DIAMOND":
                return Path(new Point(x + w / 2, y), new Point(x + w, y + h / 2), new Point(x + w / 2, y + h), new Point(x, y + h / 2));
            case "TRAPEZOID":
                return Path(new Point(x + w * 0.18, y), new Point(x + w * 0.82, y), new Point(x + w, y + h), new Point(x, y + h));
            case "SEMI-CIRCLE":
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(x, y + h), true, true);
                    c.ArcTo(new Point(x + w, y + h), new Size(w / 2, h), 0, false, SweepDirection.Clockwise, true, false);
                }
                g.Freeze();
                return g;
            }
            case "BULLET":
            case "TEAR":
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(x + w - h / 2, y), true, true);
                    c.ArcTo(new Point(x + w - h / 2, y + h), new Size(h / 2, h / 2), 0, false, SweepDirection.Clockwise, true, false);
                    if (shape == "BULLET")
                    {
                        c.LineTo(new Point(x + h * 0.15, y + h), true, false);
                        c.LineTo(new Point(x + h * 0.15, y), true, false);
                    }
                    else
                    {
                        c.QuadraticBezierTo(new Point(x + w * 0.2, y + h), new Point(x, y + h / 2), true, false);
                        c.QuadraticBezierTo(new Point(x + w * 0.2, y), new Point(x + w - h / 2, y), true, false);
                    }
                }
                g.Freeze();
                return g;
            }
            default:
                var sides = shape.StartsWith("PENTAGON", StringComparison.Ordinal) ? 5
                    : shape.StartsWith("HEXAGON", StringComparison.Ordinal) ? 6
                    : shape.StartsWith("HEPTAGON", StringComparison.Ordinal) ? 7
                    : shape.StartsWith("OCTAGON", StringComparison.Ordinal) ? 8 : 0;
                return sides > 0 ? Polygon(r, sides, -90, rounded: false) : new EllipseGeometry(r);
        }
    }

    private static Geometry Polygon(Rect r, int sides, double startDegrees, bool rounded)
    {
        var (cx, cy) = (r.X + r.Width / 2, r.Y + r.Height / 2);
        var points = Enumerable.Range(0, sides)
            .Select(i => (startDegrees + 360.0 * i / sides) * Math.PI / 180)
            .Select(a => new Point(cx + r.Width / 2 * Math.Cos(a), cy + r.Height / 2 * Math.Sin(a) + (sides == 3 ? r.Height * 0.12 : 0)))
            .ToArray();
        if (!rounded) return Path(points);
        // Rounded corners: cut each corner and curve through it.
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            static Point Between(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            c.BeginFigure(Between(points[0], points[1], 0.15), true, true);
            for (var i = 0; i < sides; i++)
            {
                var a = points[(i + 1) % sides];
                c.LineTo(Between(points[i], a, 0.85), true, false);
                c.QuadraticBezierTo(a, Between(a, points[(i + 2) % sides], 0.15), true, false);
            }
        }
        g.Freeze();
        return g;
    }

    private static Geometry Path(params Point[] points)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(points[0], true, true);
            foreach (var p in points.Skip(1)) c.LineTo(p, true, false);
        }
        g.Freeze();
        return g;
    }

    private static Brush Shade(Color color, Rect box) => new LinearGradientBrush(
        [new GradientStop(Lighten(color, 0.25), 0), new GradientStop(color, 0.55), new GradientStop(Darken(color, 0.12), 1)],
        new Point(box.X + box.Width / 2, box.Top), new Point(box.X + box.Width / 2, box.Bottom))
    {
        MappingMode = BrushMappingMode.Absolute,
    };

    private static Color Darken(Color c, double by) =>
        Color.FromRgb((byte)(c.R * (1 - by)), (byte)(c.G * (1 - by)), (byte)(c.B * (1 - by)));

    private static Color Lighten(Color c, double by) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * by), (byte)(c.G + (255 - c.G) * by), (byte)(c.B + (255 - c.B) * by));

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
}
