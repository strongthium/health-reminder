using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>
/// Something put in the kitchen by hand, the first time it's asked: with some of it there already, a button for each to
/// add it to; and a button for each place, to keep it apart there (a second bag), or to put it there when it's new. The
/// group it's shown with is picked too. What's measured (900 g, 2 L) is kept apart by Enter, and what's counted (eggs,
/// cans) added to what's there; when it's new, Enter puts it where its name says. A click picks: then
/// <see cref="Into"/> is what it's added to, or <see cref="Place"/> where it's kept apart, and <see cref="Group"/> its
/// group.
/// </summary>
public sealed partial class WhereWindow : Window
{
    public KitchenPlace? Place { get; private set; }
    public PantryItem? Into { get; private set; }
    public FoodGroup Group => (FoodGroup)Math.Max(0, GroupBox.SelectedIndex);

    public WhereWindow(PantryItem item, IReadOnlyList<PantryItem> have, IReadOnlyList<KitchenPlace> places, MeasureSystem system)
    {
        InitializeComponent();
        Icon = App.Icon;
        Preview.Icon = item.ShownIcon;
        Preview.Tint = item.ShownColor;
        NameText.Text = item.Name;
        DetailText.Text = Pantry.Describe(item, system);
        GroupBox.ItemsSource = PantryRow.GroupNames;
        GroupBox.SelectedIndex = (int)(have.FirstOrDefault()?.ShownGroup ?? item.ShownGroup);

        var measured = item.Unit is "g" or "mL";
        Button? first = null;
        Button Tile(string icon, string? tint, string title, string note, object tag, bool suggested)
        {
            var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            label.Children.Add(new FoodView { Icon = icon, Tint = tint, Width = 40, Height = 40, Margin = new Thickness(0, 2, 0, 6) });
            label.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            if (note.Length > 0)
                label.Children.Add(new TextBlock { Text = note, Style = (Style)FindResource("Caption"), HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var button = new Button { Content = label, Tag = tag, Width = 128, MinHeight = 104, Padding = new Thickness(6, 8, 6, 8), Margin = new Thickness(0, 0, 8, 8), IsDefault = suggested };
            if (suggested)
            {
                button.SetResourceReference(StyleProperty, "AccentButtonStyle");
                first = button;
            }
            button.Click += Tile_Click;
            return button;
        }

        // Some there already: to add it to.
        HavePanel.Show(have.Count > 0);
        foreach (var (some, i) in have.Select((p, i) => (p, i)))
            HaveTiles.Children.Add(Tile(some.ShownIcon, some.ShownColor, Pantry.Show(some, system), "in the " + Pantry.PlaceOf(some, places).Name, some,
                !measured && i == 0));
        PlaceLabel.Text = have.Count > 0 ? "Or keep it apart, a bag of its own, in" : "Where does it go?";

        var usual = have.Count > 0 ? Pantry.PlaceOf(have[0], places) : Pantry.PlaceOf(item, places);
        foreach (var place in places.OrderBy(p => p.Sort))
        {
            var suggested = place.Id == usual.Id && (have.Count == 0 || measured);
            var note = place.Id == usual.Id ? (have.Count > 0 ? "Where the other is" : "Where it usually goes") : place.Name != Foods.Name(place.Kind) ? Foods.Name(place.Kind) : "";
            PlaceTiles.Children.Add(Tile(FoodIcons.IconOf(place.Kind), null, place.Name, note, place, suggested));
        }
        Loaded += (_, _) => first?.Focus();
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        switch (((Button)sender).Tag)
        {
            case PantryItem some:
                Into = some;
                break;
            case KitchenPlace place:
                Place = place;
                break;
        }
        DialogResult = true;
    }
}
