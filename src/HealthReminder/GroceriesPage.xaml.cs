using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Strongthium.HealthReminder;

/// <summary>A line of the grocery list, with what's typed in as it's bought.</summary>
public sealed class GroceryRow(GroceryItem item, GroceryItem? last, MeasureSystem system) : INotifyPropertyChanged
{
    public GroceryItem Item => item;
    public string Name => item.Name;
    /// <summary>The amount in the units everything is shown in: "3.79 L" for "1 gallon". As typed, it's in the box.</summary>
    public string Shown => item.Show(system);
    public string LastText => last is { Price: { } price }
        ? $"Last time {price.ToString("C", CultureInfo.CurrentCulture)}" + (last.Show(system) is { Length: > 0 } amount ? $" for {amount}" : "")
          + (last.UnitPrice(system) is { Length: > 0 } each ? $" ({each})" : "") + $", {last.Bought:MMM d}"
        : "";

    /// <summary>How much was bought, the listed amount to start with.</summary>
    public string QuantityText { get; set; } = item.Quantity;
    public string PriceText { get; set; } = "";
    public string Problem { get; private set; } = "";

    public string BoughtText => (item.Show(system) is { Length: > 0 } shown ? shown + " · " : "") + $"{item.Bought:t}";
    public string PriceShown => item.Price?.ToString("C", CultureInfo.CurrentCulture) ?? "";
    public string UnitPriceText => item.UnitPrice(system);

    /// <summary>The price typed in, in the user's way of writing money ("3.49", "$3.49", "3,49 $"); null when empty.
    /// False when it isn't a price.</summary>
    public bool TryReadPrice(out decimal? price)
    {
        price = null;
        var text = PriceText.Trim();
        if (text.Length == 0) return true;
        if (decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out var value) && value >= 0
            || decimal.TryParse(text.TrimStart('$', '€', '£'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0)
        {
            price = value;
            return true;
        }
        Problem = "That isn't a price: type what it cost, like 3.49.";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Problem)));
        return false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A food Claude saw in a photo, to keep or not, its name and amount fixable.</summary>
public sealed class SeenRow(SeenFood food)
{
    public bool Keep { get; set; } = true;
    public string Name { get; set; } = food.Name;
    public string Quantity { get; set; } = food.Quantity;
    public int UseWithinDays => food.UseWithinDays;
    public bool Unsure => food.Confidence == "likely";
    public bool InFreezer => food.Where.Contains("freezer", StringComparison.OrdinalIgnoreCase);
    public string Detail => $"{food.Where}" + (food.UseWithinDays > 0 ? $", good for about {food.UseWithinDays} days" : ", keeps")
        + (Unsure ? " · not sure what it is" : "");
}

/// <summary>Something in the kitchen's stock, on a line: its picture, how much, when it's best used by and whether it's
/// in doubt, with a box to type what there is when it's counted. The rest is in <see cref="Detail"/>, its tooltip.</summary>
public sealed class PantryRow(PantryItem item, MeasureSystem system, DateTime now) : INotifyPropertyChanged
{
    private static readonly System.Windows.Media.Brush Soon = KindLook.Brush(System.Windows.Media.Color.FromRgb(0xC2, 0x57, 0x00));
    private static readonly System.Windows.Media.Brush Past = KindLook.Brush(System.Windows.Media.Color.FromRgb(0xD6, 0x45, 0x41));

    public PantryItem Item => item;
    public string Icon => item.ShownIcon;
    public string? Tint => item.ShownColor;
    public string Shown => Pantry.Show(item, system);
    public bool InDoubt => item.Unsure || now - item.Counted > Pantry.Recount && item.Amount > 0;
    /// <summary>What was typed to count it; Set shows once there is some, and "Right" while there isn't.</summary>
    public string Count
    {
        get => count;
        set
        {
            count = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Typed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSayRight)));
        }
    }
    private string count = "";
    public bool Typed => count.Trim().Length > 0;
    public bool CanSayRight => InDoubt && !Typed;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>"good until Nov 3", "use by Thursday", "past its best Oct 30", "keeps".</summary>
    public string When => item.UseBy is { } by
        ? by.Date < now.Date ? $"past its best {by:MMM d}"
        : by.Date == now.Date ? "use today"
        : by.Date == now.Date.AddDays(1) ? "use by tomorrow"
        : by.Date <= now.Date.AddDays(2) ? $"use by {by:dddd}"
        : by.Year == now.Year || by < now.AddMonths(3) ? $"good until {by:MMM d}" : $"good until {by:MMM yyyy}"
        : "keeps";

    // Not null: a null brush draws the text in nothing.
    public System.Windows.Media.Brush WhenBrush => item.UseBy is { } by && by.Date < now.Date ? Past
        : item.UseBy is { } soon && soon.Date <= now.Date.AddDays(2) ? Soon
        : (System.Windows.Media.Brush)Application.Current.FindResource("TextFillColorSecondaryBrush");

    public string Detail => string.Join(" · ", new[]
    {
        item.Frozen is { } frozen ? $"Frozen {frozen:MMM d}, {Pantry.FreezerNote(item.Name)}" : $"Added {item.Since:MMM d}",
        item.UseBySet ? "best-before set by hand" : "",
        $"counted {item.Counted:MMM d}",
        item.Unsure ? "the amount is a guess" : "",
    }.Where(p => p.Length > 0));
}

/// <summary>A group of what's in a place ("Beverages"), and what's in it.</summary>
public sealed record GroupSection(string Title, List<PantryRow> Rows);

/// <summary>A place in the kitchen and what's in it, by group; shown closed to just its heading when it's put away.</summary>
public sealed class PlaceSection(KitchenPlace place, List<GroupSection> groups, bool open)
{
    public KitchenPlace Place => place;
    public string Icon => FoodIcons.IconOf(place.Kind);
    public string Name => place.Name;
    public List<GroupSection> Groups => groups;
    public bool Open => open;
    public string Chevron => open ? "" : "";
    public string CountText
    {
        get
        {
            var rows = groups.SelectMany(g => g.Rows).ToList();
            var doubt = rows.Count(r => r.InDoubt);
            return $"{rows.Count} {(rows.Count == 1 ? "thing" : "things")}" + (doubt > 0 ? $" · {doubt} to check" : "");
        }
    }
}

/// <summary>A kind of place, as the list to pick one from shows it.</summary>
public sealed record PlaceKindChoice(Kept Kind, string Name)
{
    public static readonly IReadOnlyList<PlaceKindChoice> All =
        new[] { Kept.Freezer, Kept.Fridge, Kept.Pantry, Kept.Cupboard, Kept.Counter }.Select(k => new PlaceKindChoice(k, Foods.Name(k))).ToList();
}

/// <summary>A place, to rename, make another kind, move up or down, or take away.</summary>
public sealed class PlaceRow(KitchenPlace place, int count, bool first, bool last, bool only)
{
    public KitchenPlace Place => place;
    public string Name { get; set; } = place.Name;
    public Kept Kind => place.Kind;
    public string Icon => FoodIcons.IconOf(place.Kind);
    public IReadOnlyList<PlaceKindChoice> Kinds => PlaceKindChoice.All;
    public string CountText => count == 0 ? "empty" : $"{count} {(count == 1 ? "thing" : "things")}";
    public bool CanUp => !first;
    public bool CanDown => !last;
    public bool CanDelete => !only;
}

/// <summary>
/// The grocery list: what to buy, with its amount in any unit, and as each thing goes in the cart, how much it was and
/// what it cost. Liquids are all shown in one unit and weights in another (Settings, Measurements), and a price is
/// compared per litre or kilogram, or per gallon or pound, with the last one paid.
/// </summary>
public sealed partial class GroceriesPage : UserControl, IPage
{
    public GroceriesPage() => InitializeComponent();

    private static App App => App.Current;

    public void Refresh()
    {
        var system = App.Measures;
        var items = App.Database.Groceries();
        var toBuy = items.Where(i => i.Bought is null).Select(i => new GroceryRow(i, App.Database.LastBought(i.Name), system)).ToList();
        var bought = items.Where(i => i.Bought is not null).Select(i => new GroceryRow(i, null, system)).ToList();
        ToBuyList.ItemsSource = toBuy;
        BoughtList.ItemsSource = bought;
        ToBuyPanel.Show(toBuy.Count > 0);
        BoughtPanel.Show(bought.Count > 0);
        Empty.Show(items.Count == 0);
        ToBuyCount.Text = toBuy.Count.ToString(CultureInfo.CurrentCulture);
        var total = bought.Sum(b => b.Item.Price ?? 0);
        TotalText.Text = bought.Count + (total > 0 ? " · " + total.ToString("C", CultureInfo.CurrentCulture) : "");
        SummaryText.Text = items.Count == 0 ? "What to buy, and what it cost"
            : $"{toBuy.Count} to buy" + (bought.Count > 0 ? $" · {bought.Count} in the cart" : "") + (total > 0 ? $" · {total.ToString("C", CultureInfo.CurrentCulture)} so far" : "");
        var typed = NameBox.Text;
        NameBox.ItemsSource = App.Database.GroceryNames();
        NameBox.Text = typed;
        ShowQuantityHint();

        // The stock: by place, in the places' order, and in each by group, by name.
        var now = DateTime.Now;
        var places = PantryLog.Places();
        var kitchen = PantryLog.Items().Select(p => new PantryRow(p, system, now)).ToList();
        var placeOf = kitchen.ToDictionary(r => r, r => Pantry.PlaceOf(r.Item, places).Id);
        var closed = ClosedPlaces();
        KitchenList.ItemsSource = places
            .Select(place => new PlaceSection(place,
                kitchen.Where(r => placeOf[r] == place.Id)
                    .GroupBy(r => r.Item.ShownGroup).OrderBy(g => g.Key)
                    .Select(g => new GroupSection(Foods.Name(g.Key), g.OrderBy(r => r.Item.Name, StringComparer.OrdinalIgnoreCase).ToList()))
                    .ToList(),
                !closed.Contains(place.Id)))
            .Where(s => s.Groups.Count > 0)
            .ToList();
        PlaceList.ItemsSource = places.Select((place, i) => new PlaceRow(place, placeOf.Values.Count(id => id == place.Id), i == 0, i == places.Count - 1, places.Count == 1)).ToList();
        if (NewPlaceKind.ItemsSource is null)
        {
            NewPlaceKind.ItemsSource = PlaceKindChoice.All;
            NewPlaceKind.SelectedIndex = 0;
        }
        NoKitchen.Show(kitchen.Count == 0);
        var doubt = kitchen.Count(r => r.InDoubt);
        KitchenSummary.Text = kitchen.Count == 0 ? "" : $"{kitchen.Count} things" + (doubt > 0 ? $" · {doubt} to check" : "");
        ShowCheckTime();
        PhotosButton.IsEnabled = App.ClaudeOn && !reading;
        if (!reading && !FoundPanel.IsVisible)
            PhotosText.Text = App.ClaudeOn
                ? "Photos of your fridge, cupboard, groceries or a receipt: Claude lists what's in them."
                : "To add groceries from photos, turn on Help from Claude in Settings.";
    }

    // ---- From photos ------------------------------------------------------------------------------------------

    private bool reading;
    private List<SeenRow> found = [];

    private async void Photos_Click(object sender, RoutedEventArgs e)
    {
        if (App.Assistant is not { } assistant) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Photos of your groceries", Filter = KitchenPhotos.Filter, Multiselect = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true || dialog.FileNames.Length == 0) return;
        reading = true;
        PhotosButton.IsEnabled = false;
        FoundPanel.Show(false);
        PhotosText.Text = $"Reading {dialog.FileNames.Length} photo{(dialog.FileNames.Length == 1 ? "" : "s")}…";
        try
        {
            var photos = await Task.Run(() => dialog.FileNames.Take(10).Select(KitchenPhotos.Prepare).ToList());
            var seen = await assistant.ReadPhotosAsync(photos, GroceryCatalog.All.Where(g => !g.Staple).Select(g => g.Name));
            found = seen.Select(s => new SeenRow(s)).ToList();
            FoundList.ItemsSource = found;
            FoundPanel.Show(found.Count > 0);
            PhotosText.Text = found.Count == 0 ? "No food found in them." : $"{found.Count} found.";
        }
        catch (KitchenAssistantException ex)
        {
            PhotosText.Text = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            PhotosText.Text = "A photo couldn't be read: " + ex.Message;
        }
        finally
        {
            reading = false;
            PhotosButton.IsEnabled = App.ClaudeOn;
        }
    }

    /// <summary>A photo of where they're kept: the amounts found are what there is now. Amounts read from a photo are a
    /// guess until counted.</summary>
    private void FoundToKitchen_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in found.Where(f => f.Keep && f.Name.Trim().Length > 0))
            PantryLog.Counted(row.Name, row.Quantity, row.UseWithinDays, unsure: true, row.InFreezer);
        CloseFound($"Your kitchen has {found.Count(f => f.Keep)} counted from the photos.");
    }

    /// <summary>A receipt or a haul: added to what there is.</summary>
    private void FoundBought_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in found.Where(f => f.Keep && f.Name.Trim().Length > 0))
            PantryLog.Bought(row.Name, row.Quantity, row.UseWithinDays, row.Unsure, row.InFreezer);
        CloseFound($"Added {found.Count(f => f.Keep)} to your kitchen.");
    }

    private void FoundToList_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in found.Where(f => f.Keep && f.Name.Trim().Length > 0))
            App.Database.Save(new GroceryItem { Name = row.Name.Trim(), Quantity = row.Quantity.Trim(), Added = DateTime.Now });
        CloseFound($"Added {found.Count(f => f.Keep)} to the list.");
    }

    private void FoundCancel_Click(object sender, RoutedEventArgs e) => CloseFound("");

    private void CloseFound(string message)
    {
        found = [];
        FoundPanel.Show(false);
        Refresh();
        PhotosText.Text = message.Length > 0 ? message : PhotosText.Text;
        App.KitchenChanged();
    }

    // ---- The stock ----------------------------------------------------------------------------------------------

    private static PantryRow? PantryRowOf(object sender) => (sender as FrameworkElement)?.DataContext as PantryRow;

    /// <summary>Counted: what's typed is what there is.</summary>
    private void PantrySet_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row || row.Count.Trim().Length == 0) return;
        PantryLog.Counted(row.Item.Name, row.Count);
        Refresh();
    }

    private void PantryRight_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        PantryLog.Confirmed(row.Item);
        Refresh();
    }

    private void PantryCount_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        PantrySet_Click(sender, e);
    }

    /// <summary>Its picture and colour, its place (which freezes or thaws it), its group and its best-before.</summary>
    private void PantryLook_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        var dialog = new FoodLookWindow(row.Item, PantryLog.Places()) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is not { } changed) return;
        PantryLog.Changed(changed);
        Refresh();
        App.KitchenChanged();
    }

    // ---- The places -------------------------------------------------------------------------------------------

    /// <summary>The places shown closed, to just their heading.</summary>
    private static HashSet<long> ClosedPlaces() =>
        (App.Database.Setting("kitchen_closed") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0).ToHashSet();

    private void PlaceToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaceSection section) return;
        var closed = ClosedPlaces();
        if (!closed.Remove(section.Place.Id)) closed.Add(section.Place.Id);
        App.Database.SetSetting("kitchen_closed", string.Join(',', closed.Select(id => id.ToString(CultureInfo.InvariantCulture))));
        Refresh();
    }

    private void Places_Click(object sender, RoutedEventArgs e) => PlacesCard.Show(!PlacesCard.IsVisible);

    private static PlaceRow? PlaceRowOf(object sender) => (sender as FrameworkElement)?.DataContext as PlaceRow;

    private void PlaceName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (PlaceRowOf(sender) is not { } row || row.Name.Trim().Length == 0 || row.Name.Trim() == row.Place.Name) return;
        PantryLog.ChangePlace(row.Place with { Name = row.Name.Trim() });
        Refresh();
    }

    private void PlaceName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        PlaceName_LostFocus(sender, e);
    }

    /// <summary>Another kind: what was put in it is frozen or thawed to match.</summary>
    private void PlaceKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PlaceRowOf(sender) is not { } row || sender is not ComboBox { SelectedValue: Kept kind } || kind == row.Place.Kind) return;
        PantryLog.ChangePlace(row.Place with { Kind = kind });
        Dispatcher.InvokeAsync(Refresh);
        App.KitchenChanged();
    }

    private void PlaceUp_Click(object sender, RoutedEventArgs e) => MovePlace(sender, -1);

    private void PlaceDown_Click(object sender, RoutedEventArgs e) => MovePlace(sender, 1);

    private void MovePlace(object sender, int by)
    {
        if (PlaceRowOf(sender) is not { } row) return;
        PantryLog.MovePlace(row.Place, by);
        Refresh();
    }

    private void PlaceDelete_Click(object sender, RoutedEventArgs e)
    {
        if (PlaceRowOf(sender) is not { } row) return;
        PantryLog.DeletePlace(row.Place);
        Refresh();
        App.KitchenChanged();
    }

    private void NewPlace_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddPlace_Click(sender, e);
    }

    private void AddPlace_Click(object sender, RoutedEventArgs e)
    {
        if (NewPlaceKind.SelectedValue is not Kept kind) return;
        PantryLog.AddPlace(NewPlaceName.Text, kind);
        NewPlaceName.Text = "";
        Refresh();
    }

    private void PantryGone_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        PantryLog.Gone(row.Item);
        Refresh();
    }

    /// <summary>Straight into the kitchen's stock, not on the list.</summary>
    private void HaveIt_Click(object sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0)
        {
            NameBox.Focus();
            return;
        }
        PantryLog.Bought(name, QuantityBox.Text.Trim());
        NameBox.Text = "";
        QuantityBox.Text = "";
        Refresh();
    }

    private bool showingCheck;

    private void ShowCheckTime()
    {
        showingCheck = true;
        if (CheckDayBox.ItemsSource is null)
            CheckDayBox.ItemsSource = Enum.GetValues<DayOfWeek>().Select(d => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(d)).ToList();
        var (day, at) = PantryLog.CheckTime;
        CheckOnBox.IsChecked = PantryLog.CheckOn;
        CheckDayBox.SelectedIndex = (int)day;
        CheckAtBox.Time = at.ToTimeSpan();
        showingCheck = false;
    }

    private void CheckTime_Changed(object sender, RoutedEventArgs e)
    {
        if (showingCheck || CheckDayBox.SelectedIndex < 0) return;
        PantryLog.CheckOn = CheckOnBox.IsChecked == true;
        PantryLog.CheckTime = ((DayOfWeek)CheckDayBox.SelectedIndex, TimeOnly.FromTimeSpan(CheckAtBox.Time ?? new TimeSpan(10, 0, 0)));
    }

    private void Add_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Add();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add();

    private void Add()
    {
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0)
        {
            NameBox.Focus();
            return;
        }
        App.Database.Save(new GroceryItem { Name = name, Quantity = QuantityBox.Text.Trim(), Added = DateTime.Now });
        NameBox.Text = "";
        QuantityBox.Text = "";
        Refresh();
        NameBox.Focus();
    }

    private void Quantity_Changed(object sender, TextChangedEventArgs e) => ShowQuantityHint();

    private void ShowQuantityHint()
    {
        var text = QuantityBox.Text.Trim();
        QuantityHint.Text = text.Length == 0 ? "Any amount: 2 L, 1 gallon, 500 g, 2 lb, 12, a dozen."
            : Quantities.Parse(text) is { } amount ? "That's " + Quantities.Format(amount, App.Measures) + (amount.Dimension == Dimension.Count ? "" : $" ({(amount.Dimension == Dimension.Volume ? "liquid" : "weight")})") + "."
            : "Kept as typed: it isn't an amount the list can compare.";
    }

    private static GroceryRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as GroceryRow;

    private void Bought_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row || !row.TryReadPrice(out var price)) return;
        row.Item.Quantity = row.QuantityText.Trim();
        row.Item.Price = price;
        row.Item.Bought = DateTime.Now;
        App.Database.Save(row.Item);
        // Into the kitchen's stock.
        PantryLog.Bought(row.Item.Name, row.Item.Quantity);
        Refresh();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        row.Item.Bought = null;
        row.Item.Price = null;
        App.Database.Save(row.Item);
        Refresh();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        App.Database.Delete(row.Item);
        Refresh();
    }

    private void DoneShopping_Click(object sender, RoutedEventArgs e)
    {
        App.Database.ClearBought();
        Refresh();
    }
}
