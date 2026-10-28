using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Something in the kitchen's picture, picked from those of its kind of thing (containers, drinks, bread…), and the
/// picture's colour: the one its name says, until one is picked. Nothing changes until Done; <see cref="Result"/> is
/// then what it is.
/// </summary>
public sealed partial class FoodLookWindow : Window
{
    /// <summary>The colours offered, with More colours… for any other.</summary>
    private static readonly string[] Swatches =
    [
        "#D32F2F", "#C2185B", "#8E244D", "#7B1FA2", "#3949AB", "#1E4FA3", "#4FA3E0", "#00897B", "#2E7D32",
        "#7CC242", "#F2C230", "#F7A21B", "#EF6C00", "#A0522D", "#6D4C41", "#9EA4AA", "#424242", "#F4F2EC",
    ];

    private readonly PantryItem item;
    private readonly List<Button> pictures = [];
    private string? icon;
    private string? color;

    public PantryItem? Result { get; private set; }

    public FoodLookWindow(PantryItem item)
    {
        InitializeComponent();
        Icon = App.Icon;
        this.item = item;
        (icon, color) = (item.Icon, item.Color);
        NameText.Text = item.Name + (item.Note is { Length: > 0 } note ? " · " + note : "");
        DetailText.Text = "Click a picture, then a colour for it";

        foreach (var (section, icons) in Foods.IconSections)
        {
            IconPanel.Children.Add(new TextBlock { Text = section, Style = (Style)FindResource("Caption"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
            var row = new WrapPanel();
            foreach (var (key, label) in icons)
            {
                var button = new Button
                {
                    Style = (Style)FindResource("Bare"), Tag = key, ToolTip = label, Width = 50, Height = 50, Margin = new Thickness(0, 0, 4, 4),
                    Content = new FoodView { Icon = key },
                };
                button.Click += Icon_Click;
                pictures.Add(button);
                row.Children.Add(button);
            }
            IconPanel.Children.Add(row);
        }
        foreach (var hex in Swatches)
        {
            var swatch = new Button
            {
                Style = (Style)FindResource("Bare"), Tag = hex, Width = 34, Height = 34, Margin = new Thickness(0, 0, 4, 4), ToolTip = hex,
                Content = new Border
                {
                    Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = KindLook.Brush(FoodIcons.TryParse(hex)!.Value),
                    BorderBrush = KindLook.Brush(Colors.Gray, 0x80), BorderThickness = new Thickness(1),
                },
            };
            swatch.Click += Swatch_Click;
            SwatchPanel.Children.Add(swatch);
        }
        ShowItem();
    }

    private PantryItem Build() => item with { Icon = icon, Color = color };

    private void ShowItem()
    {
        var edited = Build();
        Preview.Icon = edited.ShownIcon;
        Preview.Tint = edited.ShownColor;
        foreach (var button in pictures) Mark(button, (string)button.Tag == edited.ShownIcon);
        var tint = FoodIcons.Hex(FoodIcons.ColorOf(edited.ShownIcon, edited.ShownColor));
        foreach (Button button in SwatchPanel.Children) Mark(button, string.Equals((string)button.Tag, tint, StringComparison.OrdinalIgnoreCase));
        UsualColorButton.IsEnabled = color is not null;
    }

    private static void Mark(Button button, bool on)
    {
        if (on) button.SetResourceReference(BorderBrushProperty, "AccentFillColorDefaultBrush");
        else button.BorderBrush = Brushes.Transparent;
    }

    private void Icon_Click(object sender, RoutedEventArgs e)
    {
        var key = (string)((Button)sender).Tag;
        // Its name's picture keeps the colour its name says.
        icon = key == item.Guess.Icon ? null : key;
        ShowItem();
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        color = (string)((Button)sender).Tag;
        ShowItem();
    }

    private void MoreColors_Click(object sender, RoutedEventArgs e)
    {
        var edited = Build();
        var now = FoodIcons.ColorOf(edited.ShownIcon, edited.ShownColor);
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true, Color = System.Drawing.Color.FromArgb(now.R, now.G, now.B) };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        color = FoodIcons.Hex(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        ShowItem();
    }

    private void UsualColor_Click(object sender, RoutedEventArgs e)
    {
        color = null;
        ShowItem();
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Result = Build();
        DialogResult = true;
    }
}
