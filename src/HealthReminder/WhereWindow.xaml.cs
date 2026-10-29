using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>
/// Something new to the kitchen, added by hand: a button for each place, the one its name says it goes in first in
/// line and picked by Enter, and the group it's shown with there. A click on a place puts it there; <see cref="Place"/>
/// and <see cref="Group"/> are then what was picked.
/// </summary>
public sealed partial class WhereWindow : Window
{
    public KitchenPlace? Place { get; private set; }
    public FoodGroup Group => (FoodGroup)Math.Max(0, GroupBox.SelectedIndex);

    public WhereWindow(PantryItem item, IReadOnlyList<KitchenPlace> places, MeasureSystem system)
    {
        InitializeComponent();
        Icon = App.Icon;
        Preview.Icon = item.ShownIcon;
        Preview.Tint = item.ShownColor;
        NameText.Text = item.Name;
        DetailText.Text = Pantry.Describe(item, system);
        GroupBox.ItemsSource = PantryRow.GroupNames;
        GroupBox.SelectedIndex = (int)item.ShownGroup;

        var usual = Pantry.PlaceOf(item, places);
        foreach (var place in places.OrderBy(p => p.Sort))
        {
            var suggested = place.Id == usual.Id;
            var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            label.Children.Add(new FoodView { Icon = FoodIcons.IconOf(place.Kind), Width = 40, Height = 40, Margin = new Thickness(0, 2, 0, 6) });
            label.Children.Add(new TextBlock { Text = place.Name, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var note = suggested ? "Where it usually goes" : place.Name != Foods.Name(place.Kind) ? Foods.Name(place.Kind) : "";
            if (note.Length > 0)
                label.Children.Add(new TextBlock { Text = note, Style = (Style)FindResource("Caption"), HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var button = new Button { Content = label, Tag = place, Width = 128, MinHeight = 104, Padding = new Thickness(6, 8, 6, 8), Margin = new Thickness(0, 0, 8, 8), IsDefault = suggested };
            if (suggested) button.SetResourceReference(StyleProperty, "AccentButtonStyle");
            button.Click += Place_Click;
            PlacePanel.Children.Add(button);
            if (suggested) Loaded += (_, _) => button.Focus();
        }
    }

    private void Place_Click(object sender, RoutedEventArgs e)
    {
        Place = (KitchenPlace)((Button)sender).Tag;
        DialogResult = true;
    }
}
