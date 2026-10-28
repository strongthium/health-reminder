using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Something in the kitchen, to change how it's shown and where it is: its picture and the picture's colour (the one its
/// name says, until one is picked), the place it's kept (a freezer freezes it), its group and its best-before. Nothing
/// changes until Done; <see cref="Result"/> is then what it is.
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
    private readonly KitchenPlace was;
    private string? icon;
    private string? color;
    private FoodGroup? group;
    // Null: the best-before as it is (or as moving it makes it); true: the one picked; false: back to the usual one.
    private bool? dateSet;
    private DateTime? picked;
    private bool showing;

    public PantryItem? Result { get; private set; }

    public FoodLookWindow(PantryItem item, IReadOnlyList<KitchenPlace> places)
    {
        InitializeComponent();
        Icon = App.Icon;
        this.item = item;
        (icon, color, group) = (item.Icon, item.Color, item.Group);
        was = Pantry.PlaceOf(item, places);
        NameText.Text = item.Name;

        foreach (var (key, label) in Foods.Icons)
            IconPanel.Children.Add(new Button
            {
                Style = (Style)FindResource("Bare"), Tag = key, ToolTip = label, Width = 46, Height = 46, Margin = new Thickness(0, 0, 4, 4),
                Content = new FoodView { Icon = key },
            });
        foreach (Button button in IconPanel.Children) button.Click += Icon_Click;
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
        PlaceBox.ItemsSource = places;
        PlaceBox.SelectedItem = was;
        GroupBox.ItemsSource = Enum.GetValues<FoodGroup>().Select(Foods.Name).ToList();
        GroupBox.SelectedIndex = (int)item.ShownGroup;
        ShowItem(Build());
    }

    /// <summary>What it is with what's picked so far.</summary>
    private PantryItem Build()
    {
        var edited = item with { Icon = icon, Color = color, Group = group };
        if (PlaceBox.SelectedItem is KitchenPlace place && place.Id != was.Id) edited = Pantry.Move(edited, place, DateTime.Now, App.Current.Cold);
        return dateSet switch
        {
            true when picked is not null => Pantry.SetUseBy(edited, picked),
            true or false => Pantry.UsualUseBy(edited, App.Current.Cold),
            null => edited,
        };
    }

    private void ShowItem(PantryItem edited)
    {
        showing = true;
        Preview.Icon = edited.ShownIcon;
        Preview.Tint = edited.ShownColor;
        foreach (Button button in IconPanel.Children) Mark(button, (string)button.Tag == edited.ShownIcon);
        var tint = FoodIcons.Hex(FoodIcons.ColorOf(edited.ShownIcon, edited.ShownColor));
        foreach (Button button in SwatchPanel.Children) Mark(button, string.Equals((string)button.Tag, tint, StringComparison.OrdinalIgnoreCase));
        UsualColorButton.IsEnabled = color is not null;
        DetailText.Text = string.Join(" · ", new[]
        {
            Pantry.Show(edited, App.Current.Measures),
            edited.Frozen is { } frozen ? $"frozen {frozen:MMM d}, {Pantry.FreezerNote(edited.Name)}" : $"added {edited.Since:MMM d}",
            $"counted {edited.Counted:MMM d}",
        });
        DateBox.SelectedDate = edited.UseBy;
        UsualDateButton.IsEnabled = edited.UseBySet;
        DateNote.Text = edited.UseBySet ? "Set by hand: it stays, frozen or not, until it's put back to the usual date."
            : edited.UseBy is null ? "It keeps: no date to watch."
            : edited.InFreezer ? "Worked out from when it went in the freezer, for as long as it's at its best frozen."
            : "Worked out from when it was added, for as long as it keeps where it's kept.";
        showing = false;
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
        ShowItem(Build());
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        color = (string)((Button)sender).Tag;
        ShowItem(Build());
    }

    private void MoreColors_Click(object sender, RoutedEventArgs e)
    {
        var edited = Build();
        var now = FoodIcons.ColorOf(edited.ShownIcon, edited.ShownColor);
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true, Color = System.Drawing.Color.FromArgb(now.R, now.G, now.B) };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        color = FoodIcons.Hex(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        ShowItem(Build());
    }

    private void UsualColor_Click(object sender, RoutedEventArgs e)
    {
        color = null;
        ShowItem(Build());
    }

    private void Place_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (showing || !IsInitialized) return;
        // Moved, its best-before is the one its new place gives it.
        if (dateSet == false) dateSet = null;
        ShowItem(Build());
    }

    private void Group_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (showing || GroupBox.SelectedIndex < 0) return;
        var chosen = (FoodGroup)GroupBox.SelectedIndex;
        group = chosen == item.Guess.Group ? null : chosen;
    }

    private void Date_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (showing) return;
        (dateSet, picked) = (true, DateBox.SelectedDate);
        ShowItem(Build());
    }

    private void UsualDate_Click(object sender, RoutedEventArgs e)
    {
        (dateSet, picked) = (false, null);
        ShowItem(Build());
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Result = Build();
        DialogResult = true;
    }
}
