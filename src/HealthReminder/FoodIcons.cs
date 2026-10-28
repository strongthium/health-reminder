using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Pictures of what's in the kitchen (<see cref="Foods.Icons"/>) and of the places it's kept, drawn like
/// <see cref="KindIcons"/> in a 100 × 100 square. The main part of each is in a colour of its own: the item's, if one
/// was picked or its name says one (a red can for Coke, a blue one for Pepsi), or else the picture's usual one.
/// </summary>
internal static class FoodIcons
{
    private static readonly Pen Outline = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x4E)), 3));
    private static readonly Pen None = Frozen(new Pen(Brushes.Transparent, 0));
    private static readonly Brush White = Solid(0xFA, 0xFA, 0xF8);
    private static readonly Brush Cream = Solid(0xF3, 0xEB, 0xDA);
    private static readonly Brush Silver = Solid(0xD5, 0xD9, 0xDE);
    private static readonly Brush Grey = Solid(0x9A, 0xA0, 0xA6);
    private static readonly Brush DarkGrey = Solid(0x5F, 0x6B, 0x76);
    private static readonly Brush Leaf = Solid(0x5F, 0xA8, 0x3A);
    private static readonly Brush Stem = Solid(0x6B, 0x4A, 0x2B);
    private static readonly Brush Bun = Solid(0xE0, 0xA4, 0x5A);
    private static readonly Brush Crust = Solid(0xD9, 0xA0, 0x66);
    private static readonly Brush Noodle = Solid(0xF2, 0xD1, 0x8B);
    private static readonly Brush Chip = Solid(0xF6, 0xC3, 0x43);
    private static readonly Brush Red = Solid(0xC6, 0x28, 0x28);
    private static readonly Brush Chocolate = Solid(0x4E, 0x34, 0x2E);
    private static readonly Brush Wood = Solid(0x8D, 0x6E, 0x63);
    private static readonly Brush Glass = Frozen(new SolidColorBrush(Color.FromArgb(70, 0xA8, 0xD4, 0xF0)));
    private static readonly Brush Shine = Frozen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)));

    /// <summary>Each picture's usual colour, and those of the places.</summary>
    private static readonly Dictionary<string, Color> Usual = new()
    {
        ["can"] = Rgb(0xD3, 0x2F, 0x2F), ["bottle"] = Rgb(0x4F, 0xA3, 0xE0), ["juice"] = Rgb(0xF7, 0xA2, 0x1B), ["glass"] = Rgb(0xF7, 0xA2, 0x1B),
        ["milk"] = Rgb(0x2F, 0x80, 0xED), ["coffee"] = Rgb(0x6D, 0x4C, 0x41), ["eggs"] = Rgb(0xE5, 0xC4, 0x9A), ["cheese"] = Rgb(0xF6, 0xC3, 0x43),
        ["butter"] = Rgb(0xF4, 0xD3, 0x5E), ["yogurt"] = Rgb(0xE8, 0x5D, 0x8E), ["chicken"] = Rgb(0xC9, 0x7B, 0x3D), ["beef"] = Rgb(0xC0, 0x39, 0x2B),
        ["fish"] = Rgb(0x5B, 0x8D, 0xB8), ["sausage"] = Rgb(0xB5, 0x53, 0x3C), ["burger"] = Rgb(0x6B, 0x3E, 0x26), ["apple"] = Rgb(0xD9, 0x3A, 0x3A),
        ["banana"] = Rgb(0xF2, 0xC2, 0x30), ["citrus"] = Rgb(0xF5, 0xD1, 0x32), ["berries"] = Rgb(0x4A, 0x55, 0xA2), ["tomato"] = Rgb(0xE5, 0x39, 0x35),
        ["pepper"] = Rgb(0xD3, 0x2F, 0x2F), ["carrot"] = Rgb(0xEE, 0x7A, 0x1F), ["cucumber"] = Rgb(0x4E, 0x9A, 0x2E), ["leafy"] = Rgb(0x6D, 0xB3, 0x3F),
        ["potato"] = Rgb(0xC4, 0x9A, 0x6C), ["onion"] = Rgb(0xB5, 0x65, 0x1D), ["bread"] = Rgb(0xD9, 0xA0, 0x66), ["pizza"] = Rgb(0xF6, 0xC3, 0x43),
        ["noodles"] = Rgb(0xD8, 0x43, 0x15), ["box"] = Rgb(0x1E, 0x63, 0xB5), ["bag"] = Rgb(0xE5, 0x39, 0x35), ["tin"] = Rgb(0x2E, 0x7D, 0x32),
        ["jar"] = Rgb(0xA0, 0x52, 0x2D), ["sauce"] = Rgb(0xC6, 0x28, 0x28), ["spice"] = Rgb(0xB5, 0x45, 0x2B), ["cookie"] = Rgb(0xC6, 0x8B, 0x4E),
        ["icecream"] = Rgb(0xF4, 0x9A, 0xC1), ["ice"] = Rgb(0x4F, 0xA3, 0xE0), ["food"] = Rgb(0x90, 0xA4, 0xAE),
        ["Fridge"] = Rgb(0x5E, 0x9E, 0xD6), ["Freezer"] = Rgb(0x4F, 0xA3, 0xE0), ["Pantry"] = Rgb(0xC6, 0x8B, 0x4E),
        ["Cupboard"] = Rgb(0xA1, 0x88, 0x7F), ["Counter"] = Rgb(0x90, 0xA4, 0xAE),
    };

    /// <summary>The colour a picture is drawn in: "#RRGGBB" if given and readable, or else the picture's usual one.</summary>
    public static Color ColorOf(string icon, string? color) =>
        TryParse(color) ?? (Usual.TryGetValue(icon, out var usual) ? usual : Usual["food"]);

    public static Color? TryParse(string? color)
    {
        if (color is not { Length: 7 } || color[0] != '#' || !uint.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return null;
        return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>The picture of a place of a kind.</summary>
    public static string IconOf(Kept kind) => kind.ToString();

    public static void Draw(DrawingContext context, string icon, Color color, Rect bounds)
    {
        var side = Math.Min(bounds.Width, bounds.Height);
        if (side < 4) return;
        var origin = new Point(bounds.X + (bounds.Width - side) / 2, bounds.Y + (bounds.Height - side) / 2);
        context.PushTransform(new MatrixTransform(side / 100, 0, 0, side / 100, origin.X, origin.Y));

        var t = Brush(color);
        var d = Brush(Mix(color, Colors.Black, 0.28));
        var l = Brush(Mix(color, Colors.White, 0.45));

        void Path(Brush? fill, string data, Pen? pen = null) => context.DrawGeometry(fill, pen ?? Outline, Geometry.Parse(data));
        void Box(Brush? fill, double x, double y, double w, double h, double r, Pen? pen = null) =>
            context.DrawGeometry(fill, pen ?? Outline, new RectangleGeometry(new Rect(x, y, w, h), r, r));
        void Line(double x1, double y1, double x2, double y2, Pen pen) => context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        void Round(Brush? fill, double x, double y, double r, Pen? pen = null) => context.DrawEllipse(fill, pen ?? Outline, new Point(x, y), r, r);
        void Gleam(string data) => Path(null, data, RoundPen(Shine, 5));

        switch (icon)
        {
            case "can":
                Box(t, 28, 14, 44, 76, 8);
                Path(White, "M 28,44 C 42,34 58,60 72,46 L 72,57 C 58,71 42,45 28,55 Z", None);
                Box(Silver, 30, 7, 40, 10, 3);
                Box(Silver, 30, 84, 40, 9, 3);
                Box(Shine, 34, 22, 6, 54, 3, None);
                Box(null, 28, 14, 44, 76, 8);
                break;

            case "bottle":
                Path(t, "M 42,18 L 58,18 L 58,28 C 58,34 70,38 70,50 L 70,88 Q 70,94 64,94 L 36,94 Q 30,94 30,88 L 30,50 C 30,38 42,34 42,28 Z");
                Box(White, 30, 56, 40, 22, 0);
                Box(d, 40, 6, 20, 13, 3);
                Box(Shine, 35, 44, 6, 10, 3, None);
                break;

            case "juice":
                Path(White, "M 52,24 L 55,8 L 66,3 L 68,7 L 60,11 L 58,25 Z");
                Path(t, "M 22,30 L 62,30 L 62,92 L 22,92 Z");
                Path(d, "M 62,30 L 78,22 L 78,84 L 62,92 Z");
                Path(l, "M 22,30 L 38,22 L 78,22 L 62,30 Z");
                Round(White, 42, 62, 13);
                Path(Leaf, "M 42,50 Q 50,43 55,48 Q 48,55 42,50 Z", None);
                Round(t, 42, 64, 7, None);
                break;

            case "glass":
                Path(Glass, "M 22,8 L 78,8 L 69,92 L 31,92 Z");
                Path(t, "M 25,34 L 75,34 L 69,92 L 31,92 Z", None);
                Path(null, "M 22,8 L 78,8 L 69,92 L 31,92 Z");
                Gleam("M 32,18 L 38,80");
                break;

            case "milk":
                Path(White, "M 20,40 L 58,40 L 58,94 L 20,94 Z");
                Path(Silver, "M 58,40 L 78,30 L 78,84 L 58,94 Z");
                Path(White, "M 20,40 L 39,18 L 58,40 Z");
                Path(Silver, "M 39,18 L 59,8 L 78,30 L 58,40 Z");
                Path(White, "M 37,18 L 57,8 L 57,2 L 37,12 Z");
                Box(t, 20, 54, 38, 26, 0);
                Path(d, "M 58,54 L 78,44 L 78,70 L 58,80 Z");
                Path(White, "M 39,58 C 39,58 32,67 32,71 A 7,7 0 0 0 46,71 C 46,67 39,58 39,58 Z", None);
                break;

            case "eggs":
                Path(d, "M 62,12 C 48,12 40,34 40,50 C 40,64 50,74 62,74 C 74,74 84,64 84,50 C 84,34 76,12 62,12 Z");
                Path(t, "M 38,28 C 24,28 16,50 16,66 C 16,80 26,92 38,92 C 50,92 60,80 60,66 C 60,50 52,28 38,28 Z");
                Gleam("M 26,58 Q 28,46 34,40");
                break;

            case "cheese":
                Path(l, "M 10,46 L 66,22 L 90,34 L 30,58 Z");
                Path(d, "M 10,46 L 30,58 L 30,84 L 10,72 Z");
                Path(t, "M 30,58 L 90,34 L 90,62 L 30,84 Z");
                Round(d, 52, 64, 6, None);
                Round(d, 74, 52, 4, None);
                Round(d, 42, 74, 3.5, None);
                Round(d, 58, 36, 3.5, None);
                break;

            case "butter":
                Path(l, "M 8,48 L 54,32 L 92,44 L 46,60 Z");
                Path(d, "M 8,48 L 46,60 L 46,80 L 8,68 Z");
                Path(t, "M 46,60 L 92,44 L 92,64 L 46,80 Z");
                Path(White, "M 64,54 L 92,44 L 92,64 L 64,74 Z");
                Path(White, "M 64,54 L 92,44 L 76,39 L 50,48 Z");
                break;

            case "yogurt":
                Path(White, "M 18,32 L 82,32 L 74,90 Q 73,94 69,94 L 31,94 Q 27,94 26,90 Z");
                Path(t, "M 20,46 L 80,46 L 76,76 L 24,76 Z");
                Box(Silver, 12, 22, 76, 12, 4);
                break;

            case "coffee":
                Path(t, "M 70,40 C 92,40 92,78 70,78 L 70,69 C 81,69 81,49 70,49 Z");
                Path(t, "M 14,34 L 72,34 L 72,80 Q 72,92 60,92 L 26,92 Q 14,92 14,80 Z");
                Box(d, 14, 34, 58, 8, 0);
                Path(null, "M 32,26 C 27,20 37,16 32,7", RoundPen(Grey, 4));
                Path(null, "M 52,26 C 47,20 57,16 52,7", RoundPen(Grey, 4));
                break;

            case "chicken":
                Path(Cream, "M 50,62 L 32,80 C 28,76 20,76 17,81 C 14,86 18,91 23,90 C 22,95 28,99 32,95 C 36,91 34,87 37,84 L 56,68 Z");
                Path(t, "M 40,64 C 30,48 36,16 60,12 C 80,9 93,26 89,44 C 85,62 63,76 49,72 C 45,71 42,68 40,64 Z");
                Gleam("M 56,22 Q 70,16 80,26");
                break;

            case "beef":
                Path(Cream, "M 8,40 C 8,22 30,12 52,14 C 78,16 94,30 92,52 C 90,76 70,90 46,88 C 22,86 14,72 20,62 C 24,54 8,54 8,40 Z");
                Path(t, "M 17,40 C 17,27 34,21 52,23 C 72,25 83,36 82,52 C 81,68 67,79 47,78 C 32,77 26,69 30,62 C 33,55 17,53 17,40 Z", None);
                Round(Cream, 42, 42, 8);
                Path(null, "M 54,54 C 60,48 66,58 72,52", new Pen(l, 3));
                break;

            case "fish":
                Path(d, "M 70,50 L 95,28 L 90,50 L 95,72 Z");
                Path(t, "M 6,50 C 22,24 52,22 77,50 C 52,78 22,76 6,50 Z");
                Path(l, "M 13,57 C 30,69 52,69 70,57 C 52,63 30,63 13,57 Z", None);
                Path(null, "M 34,37 Q 41,50 34,63", new Pen(d, 3));
                Round(White, 21, 46, 4.5);
                Round(DarkGrey, 21, 46, 2, None);
                break;

            case "sausage":
                Round(d, 9, 70, 4);
                Round(d, 92, 38, 4);
                Path(t, "M 14,72 C 6,64 12,50 26,52 C 40,54 50,52 62,40 C 72,30 86,31 90,41 C 94,54 82,62 70,68 C 52,78 40,82 26,82 C 20,82 16,76 14,72 Z");
                Gleam("M 26,60 C 40,62 52,58 62,48");
                break;

            case "burger":
                Path(Bun, "M 12,46 C 12,26 30,14 50,14 C 70,14 88,26 88,46 Z");
                foreach (var (x, y) in new[] { (36.0, 26.0), (50, 22), (64, 27), (44, 34), (58, 35) }) Round(Cream, x, y, 2.2, None);
                Box(t, 10, 48, 80, 13, 6);
                Path(Leaf, "M 12,62 L 88,62 L 84,71 L 76,65 L 68,71 L 60,65 L 52,71 L 44,65 L 36,71 L 28,65 L 20,71 Z");
                Path(Bun, "M 14,71 L 86,71 Q 86,86 72,86 L 28,86 Q 14,86 14,71 Z");
                break;

            case "apple":
                Path(null, "M 50,30 C 50,20 52,12 58,7", RoundPen(Stem, 5));
                Path(Leaf, "M 55,20 C 59,10 71,6 81,10 C 77,20 65,24 55,20 Z");
                Path(t, "M 50,30 C 40,22 12,24 12,52 C 12,77 30,93 42,93 C 46,93 48,90 50,90 C 52,90 54,93 58,93 C 70,93 88,77 88,52 C 88,24 60,22 50,30 Z");
                Gleam("M 27,52 Q 28,42 37,36");
                break;

            case "banana":
                Box(Stem, 15, 13, 10, 13, 2);
                Path(t, "M 17,24 C 12,52 30,84 62,86 C 76,87 86,80 90,70 C 78,74 66,72 56,66 C 40,56 30,42 27,24 Z");
                Path(null, "M 25,32 C 30,52 44,68 72,76", new Pen(d, 3));
                break;

            case "citrus":
                Path(t, "M 4,50 L 13,44 L 13,56 Z M 96,50 L 87,44 L 87,56 Z");
                Path(t, "M 10,50 C 10,33 29,20 50,20 C 71,20 90,33 90,50 C 90,67 71,80 50,80 C 29,80 10,67 10,50 Z");
                Gleam("M 26,42 Q 34,31 46,29");
                break;

            case "berries":
                Round(t, 50, 34, 19);
                Round(t, 31, 64, 19);
                Round(t, 69, 64, 19);
                foreach (var (x, y) in new[] { (50.0, 22.0), (31, 52), (69, 52) }) Round(d, x, y, 4, None);
                Round(Shine, 25, 60, 4, None);
                Round(Shine, 63, 60, 4, None);
                break;

            case "tomato":
                Path(t, "M 50,28 C 26,28 10,42 10,61 C 10,80 28,92 50,92 C 72,92 90,80 90,61 C 90,42 74,28 50,28 Z");
                Path(Leaf, "M 50,18 L 54,29 L 67,27 L 57,36 L 64,45 L 52,40 L 50,49 L 48,40 L 36,45 L 43,36 L 33,27 L 46,29 Z");
                Gleam("M 23,60 Q 25,49 33,44");
                break;

            case "pepper":
                Path(Leaf, "M 45,36 C 45,26 50,16 59,12 L 63,17 C 56,21 54,28 54,36 Z");
                Path(t, "M 30,32 C 15,32 13,50 15,65 C 17,81 28,92 40,90 C 45,89 46,92 50,92 C 54,92 55,89 60,90 C 72,92 83,81 85,65 C 87,50 85,32 70,32 C 62,32 56,36 50,36 C 44,36 38,32 30,32 Z");
                Path(null, "M 50,40 C 46,56 46,74 50,90", new Pen(d, 3));
                Gleam("M 24,58 Q 24,46 30,40");
                break;

            case "carrot":
                Path(Leaf, "M 72,26 C 68,16 68,8 72,3 C 77,9 79,18 77,26 Z");
                Path(Leaf, "M 76,29 C 82,21 90,19 97,23 C 92,30 84,33 78,31 Z");
                Path(t, "M 70,25 C 81,27 84,40 75,49 L 27,91 C 20,97 9,88 16,81 L 56,34 C 60,28 64,24 70,25 Z");
                Line(40, 64, 50, 68, RoundPen(d, 3));
                Line(54, 47, 63, 51, RoundPen(d, 3));
                Line(29, 77, 37, 80, RoundPen(d, 3));
                break;

            case "cucumber":
                Path(t, "M 18,82 C 10,74 12,62 21,53 L 57,17 C 66,8 78,8 84,15 C 91,22 90,34 81,43 L 45,79 C 36,88 25,89 18,82 Z");
                foreach (var (x, y) in new[] { (30.0, 66.0), (44, 56), (56, 42), (68, 30), (40, 72), (64, 48), (76, 22) }) Round(l, x, y, 2.2, None);
                Gleam("M 26,62 L 60,28");
                break;

            case "leafy":
                Path(t, "M 50,92 C 25,92 8,76 10,53 C 11,38 21,28 33,26 C 35,15 43,10 50,10 C 57,10 65,15 67,26 C 79,28 89,38 90,53 C 92,76 75,92 50,92 Z");
                Path(l, "M 50,92 C 37,80 35,58 41,34 C 45,52 48,72 50,92 Z", None);
                Path(d, "M 50,92 C 63,80 65,58 59,34 C 55,52 52,72 50,92 Z", None);
                Path(null, "M 50,92 C 25,92 8,76 10,53 C 11,38 21,28 33,26 C 35,15 43,10 50,10 C 57,10 65,15 67,26 C 79,28 89,38 90,53 C 92,76 75,92 50,92 Z");
                break;

            case "potato":
                Path(t, "M 14,46 C 14,28 34,20 52,22 C 72,24 88,34 88,54 C 88,75 70,84 48,84 C 27,84 14,72 14,56 Z");
                foreach (var (x, y) in new[] { (34.0, 40.0), (62, 36), (72, 60), (44, 66) }) Round(d, x, y, 3, None);
                Gleam("M 26,48 Q 28,38 36,32");
                break;

            case "onion":
                Line(44, 90, 41, 97, RoundPen(Stem, 3));
                Line(50, 90, 50, 98, RoundPen(Stem, 3));
                Line(56, 90, 59, 97, RoundPen(Stem, 3));
                Path(t, "M 50,8 C 54,24 82,34 82,60 C 82,79 67,91 50,91 C 33,91 18,79 18,60 C 18,34 46,24 50,8 Z");
                Path(null, "M 50,20 C 39,35 34,57 40,89", new Pen(d, 2.5));
                Path(null, "M 50,20 C 61,35 66,57 60,89", new Pen(d, 2.5));
                break;

            case "bread":
                Path(t, "M 10,44 C 8,27 23,21 34,25 C 40,16 60,16 66,25 C 77,21 92,27 90,44 C 89,50 84,53 82,53 L 82,84 Q 82,88 78,88 L 22,88 Q 18,88 18,84 L 18,53 C 16,53 11,50 10,44 Z");
                Line(29, 39, 39, 32, RoundPen(d, 4));
                Line(45, 39, 55, 32, RoundPen(d, 4));
                Line(61, 39, 71, 32, RoundPen(d, 4));
                break;

            case "pizza":
                Path(t, "M 12,20 C 34,9 66,9 88,20 L 50,95 Z");
                Path(Crust, "M 12,20 C 34,9 66,9 88,20 L 84,29 C 63,19 37,19 16,29 Z");
                Round(Red, 40, 40, 7);
                Round(Red, 62, 42, 6);
                Round(Red, 50, 64, 6);
                break;

            case "noodles":
                Line(56, 42, 92, 8, RoundPen(Wood, 5));
                Line(62, 46, 97, 18, RoundPen(Wood, 5));
                Path(Noodle, "M 16,52 C 18,38 26,38 28,52 C 30,38 38,38 40,52 C 42,38 50,38 52,52 C 54,38 62,38 64,52 C 66,38 74,38 76,52 C 78,38 84,40 84,52 Z");
                Path(t, "M 8,52 L 92,52 C 92,75 73,92 50,92 C 27,92 8,75 8,52 Z");
                Line(14, 63, 86, 63, new Pen(l, 3));
                break;

            case "box":
                Path(t, "M 16,26 L 60,26 L 60,94 L 16,94 Z");
                Path(d, "M 60,26 L 82,13 L 82,81 L 60,94 Z");
                Path(l, "M 16,26 L 38,13 L 82,13 L 60,26 Z");
                Box(Noodle, 24, 44, 28, 32, 4);
                break;

            case "bag":
                Path(t, "M 22,15 L 78,15 L 74,26 C 83,42 83,70 77,84 L 80,90 L 20,90 L 23,84 C 17,70 17,42 26,26 Z");
                Box(d, 19, 7, 62, 11, 2);
                Box(d, 17, 85, 66, 9, 2);
                Path(Chip, "M 31,54 C 31,44 39,37 50,37 C 61,37 69,44 69,54 C 69,64 61,71 50,71 C 39,71 31,64 31,54 Z");
                break;

            case "tin":
                Path(Silver, "M 20,26 L 80,26 L 80,82 C 80,90 67,95 50,95 C 33,95 20,90 20,82 Z");
                Path(t, "M 20,40 C 20,46 33,50 50,50 C 67,50 80,46 80,40 L 80,76 C 80,82 67,86 50,86 C 33,86 20,82 20,76 Z");
                Path(White, "M 20,26 C 20,18 33,13 50,13 C 67,13 80,18 80,26 C 80,34 67,39 50,39 C 33,39 20,34 20,26 Z");
                Path(null, "M 27,26 C 27,21 37,18 50,18 C 63,18 73,21 73,26 C 73,31 63,34 50,34 C 37,34 27,31 27,26 Z", new Pen(Grey, 2));
                Box(Shine, 26, 50, 6, 30, 3, None);
                break;

            case "jar":
                Box(t, 16, 30, 68, 64, 10);
                Box(Shine, 22, 34, 6, 54, 3, None);
                Box(Cream, 16, 48, 68, 28, 0);
                Box(d, 20, 10, 60, 20, 4);
                break;

            case "sauce":
                Path(White, "M 46,3 L 54,3 L 56,18 L 44,18 Z");
                Box(White, 35, 17, 30, 14, 3);
                Path(t, "M 32,31 L 68,31 Q 75,31 75,40 L 77,86 Q 77,95 68,95 L 32,95 Q 23,95 23,86 L 25,40 Q 25,31 32,31 Z");
                Box(White, 27, 52, 46, 22, 3);
                Box(Shine, 30, 36, 6, 12, 3, None);
                break;

            case "spice":
                Box(Grey, 28, 6, 44, 22, 5);
                foreach (var x in new[] { 40.0, 50, 60 }) Round(DarkGrey, x, 17, 2.4, None);
                Box(t, 28, 28, 44, 66, 6);
                Box(Cream, 28, 46, 44, 28, 0);
                break;

            case "cookie":
                Round(t, 50, 50, 40);
                foreach (var (x, y, r) in new[] { (34.0, 34.0, 5.0), (60, 30, 4), (66, 58, 5.5), (38, 64, 4.5), (52, 48, 3.5), (68, 42, 3) })
                    Round(Chocolate, x, y, r, None);
                break;

            case "icecream":
                Path(Crust, "M 26,50 L 74,50 L 50,97 Z");
                Line(36, 58, 58, 80, new Pen(Stem, 2));
                Line(64, 58, 44, 80, new Pen(Stem, 2));
                Path(t, "M 20,52 C 15,30 31,12 50,12 C 69,12 85,30 80,52 C 72,47 66,56 58,51 C 52,47 46,56 38,51 C 31,47 26,54 20,52 Z");
                Round(Red, 52, 10, 6);
                break;

            case "ice":
            case "Freezer":
                for (var arm = 0; arm < 6; arm++)
                {
                    var a = Math.PI / 3 * arm - Math.PI / 2;
                    var (dx, dy) = (Math.Cos(a), Math.Sin(a));
                    foreach (var pen in new[] { RoundPen(Outline.Brush, 10), RoundPen(t, 5) })
                    {
                        Line(50, 50, 50 + 42 * dx, 50 + 42 * dy, pen);
                        foreach (var turn in new[] { -0.75, 0.75 })
                        {
                            var b = a + turn;
                            Line(50 + 26 * dx, 50 + 26 * dy, 50 + 26 * dx + 13 * Math.Cos(b), 50 + 26 * dy + 13 * Math.Sin(b), pen);
                        }
                    }
                }
                Round(t, 50, 50, 5, None);
                break;

            case "Fridge":
                Box(l, 20, 4, 60, 92, 8);
                Line(20, 36, 80, 36, Outline);
                Box(DarkGrey, 27, 14, 5, 14, 2.5, None);
                Box(DarkGrey, 27, 44, 5, 26, 2.5, None);
                Box(Shine, 70, 10, 5, 80, 2.5, None);
                break;

            case "Pantry":
                Box(d, 10, 6, 80, 88, 4);
                Box(l, 16, 12, 68, 76, 2, None);
                Box(d, 16, 37, 68, 5, 0, None);
                Box(d, 16, 63, 68, 5, 0, None);
                Box(Red, 22, 20, 10, 17, 2);
                Box(Leaf, 36, 25, 10, 12, 2);
                Box(Noodle, 54, 17, 22, 20, 2);
                Box(Silver, 22, 48, 12, 15, 2);
                Box(t, 40, 45, 14, 18, 2);
                Box(Red, 60, 50, 14, 13, 2);
                Box(Chip, 22, 71, 22, 17, 3);
                Box(Wood, 50, 74, 26, 14, 2);
                break;

            case "Cupboard":
                Box(t, 10, 12, 80, 78, 6);
                Box(null, 16, 18, 31, 66, 3, new Pen(d, 2.5));
                Box(null, 53, 18, 31, 66, 3, new Pen(d, 2.5));
                Line(50, 12, 50, 90, Outline);
                Round(White, 43, 51, 3.5);
                Round(White, 57, 51, 3.5);
                break;

            case "Counter":
                Round(Solid(0xF5, 0x7C, 0x00), 40, 32, 10);
                Round(Solid(0xD9, 0x3A, 0x3A), 58, 32, 10);
                Path(White, "M 26,42 C 26,34 36,34 50,34 C 64,34 74,34 74,42 Z");
                Box(Silver, 4, 42, 92, 11, 3);
                Box(t, 10, 53, 80, 41, 3);
                Line(10, 70, 90, 70, Outline);
                Box(DarkGrey, 30, 60, 16, 4, 2, None);
                Box(DarkGrey, 54, 60, 16, 4, 2, None);
                break;

            default:
                Path(t, "M 12,70 C 12,45 29,29 50,29 C 71,29 88,45 88,70 Z");
                Round(d, 50, 24, 6);
                Box(d, 6, 70, 88, 9, 4);
                Gleam("M 26,60 Q 28,48 38,41");
                break;
        }
        context.Pop();
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Color Mix(Color a, Color b, double by) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * by), (byte)(a.G + (b.G - a.G) * by), (byte)(a.B + (b.B - a.B) * by));

    private static Brush Brush(Color color) => KindLook.Brush(color);

    private static SolidColorBrush Solid(byte r, byte g, byte b) => Frozen(new SolidColorBrush(Color.FromRgb(r, g, b)));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Pen RoundPen(Brush brush, double width) => new(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
}

/// <summary>A food's picture (or a place's), on a square washed with its colour.</summary>
public sealed class FoodView : FrameworkElement
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(FoodView),
        new FrameworkPropertyMetadata("food", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(nameof(Tint), typeof(string), typeof(FoodView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WashProperty = DependencyProperty.Register(nameof(Wash), typeof(bool), typeof(FoodView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>One of <see cref="Foods.Icons"/>' keys, or a place's kind.</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>"#RRGGBB", or null for the picture's usual colour.</summary>
    public string? Tint
    {
        get => (string?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Whether it sits on a square washed with its colour.</summary>
    public bool Wash
    {
        get => (bool)GetValue(WashProperty);
        set => SetValue(WashProperty, value);
    }

    /// <summary>As big as it's let be, square: in a button, the button's size.</summary>
    protected override Size MeasureOverride(Size available)
    {
        var side = Math.Min(double.IsInfinity(available.Width) ? 32 : available.Width, double.IsInfinity(available.Height) ? 32 : available.Height);
        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext context)
    {
        var bounds = new Rect(RenderSize);
        if (bounds.Width < 4 || bounds.Height < 4) return;
        var icon = Icon ?? "food";
        var color = FoodIcons.ColorOf(icon, Tint);
        if (Wash)
        {
            var side = Math.Min(bounds.Width, bounds.Height);
            var square = new Rect(bounds.X + (bounds.Width - side) / 2, bounds.Y + (bounds.Height - side) / 2, side, side);
            context.DrawRoundedRectangle(KindLook.Brush(color, 0x2E), null, square, side / 5, side / 5);
            bounds = Rect.Inflate(square, -side * 0.14, -side * 0.14);
        }
        FoodIcons.Draw(context, icon, color, bounds);
    }
}
