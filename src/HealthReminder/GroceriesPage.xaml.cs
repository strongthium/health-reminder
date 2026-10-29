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

/// <summary>
/// Something in the kitchen's stock, on a line: its picture (with a badge when it's in doubt), its name and brand, how
/// much, and when it's best used by, with a box to type what there is when it's counted. Opened, the line shows the
/// rest to change in place: its name and brand, how much and the packs it's in, where it's kept and its group, and
/// its dates. What's typed there is kept as it's left (<see cref="GroceriesPage"/>), and the line shows it at once.
/// </summary>
public sealed class PantryRow : INotifyPropertyChanged
{
    private static readonly System.Windows.Media.Brush Soon = KindLook.Brush(System.Windows.Media.Color.FromRgb(0xC2, 0x57, 0x00));
    private static readonly System.Windows.Media.Brush Past = KindLook.Brush(System.Windows.Media.Color.FromRgb(0xD6, 0x45, 0x41));

    public static readonly IReadOnlyList<string> GroupNames = Enum.GetValues<FoodGroup>().Select(Foods.Name).ToList();
    public static readonly IReadOnlyList<string> SizeKinds = ["each", "a pack"];

    private readonly MeasureSystem system;
    private readonly DateTime now;
    private bool open, selected, selecting;

    public PantryRow(PantryItem item, MeasureSystem system, DateTime now, IReadOnlyList<KitchenPlace> places)
    {
        this.system = system;
        this.now = now;
        Places = places;
        Item = item;
        Reset();
    }

    public PantryItem Item { get; private set; }
    public IReadOnlyList<KitchenPlace> Places { get; }
    public string Icon => Item.ShownIcon;
    public string? Tint => Item.ShownColor;
    public string Name => Item.Name;
    public string Note => Item.Note is { Length: > 0 } note ? note : "";
    public string NoteShown => Note.Length > 0 ? "  " + Note : "";
    public string Shown => Pantry.Describe(Item, system);
    public bool InDoubt => Item.Unsure || now - Item.Counted > Pantry.Recount && Item.Amount > 0;
    public string PictureTip => (InDoubt ? "In doubt: count it, or say the amount is right. " : "") + "Click to change its picture and colour";

    /// <summary>What was typed to count it; Set shows once there is some, and "Right" while there isn't.</summary>
    public string Count
    {
        get => count;
        set
        {
            count = value;
            Changed(nameof(Typed), nameof(CanSayRight));
        }
    }
    private string count = "";
    public bool Typed => count.Trim().Length > 0;
    public bool CanSayRight => InDoubt && !Typed;

    /// <summary>Opened, to change what it is in place.</summary>
    public bool Open
    {
        get => open;
        set { open = value; Changed(nameof(Open)); }
    }

    /// <summary>Picked, to move or change with others.</summary>
    public bool Selected
    {
        get => selected;
        set { selected = value; Changed(nameof(Selected)); }
    }

    /// <summary>Things are being picked: each line has a box to tick.</summary>
    public bool Selecting
    {
        get => selecting;
        set { selecting = value; Changed(nameof(Selecting)); }
    }

    /// <summary>"good until Nov 3", "use by Thursday", "past its best Oct 30", "keeps".</summary>
    public string When => Item.UseBy is { } by
        ? by.Date < now.Date ? $"past its best {by:MMM d}"
        : by.Date == now.Date ? "use today"
        : by.Date == now.Date.AddDays(1) ? "use by tomorrow"
        : by.Date <= now.Date.AddDays(2) ? $"use by {by:dddd}"
        : by.Year == now.Year || by < now.AddMonths(3) ? $"good until {by:MMM d}" : $"good until {by:MMM yyyy}"
        : "keeps";

    // Not null: a null brush draws the text in nothing.
    public System.Windows.Media.Brush WhenBrush => Item.UseBy is { } by && by.Date < now.Date ? Past
        : Item.UseBy is { } soon && soon.Date <= now.Date.AddDays(2) ? Soon
        : (System.Windows.Media.Brush)Application.Current.FindResource("TextFillColorSecondaryBrush");

    public string Detail => string.Join(" · ", new[]
    {
        Pantry.InAll(Item, system),
        Item.Frozen is { } frozen ? $"Frozen {frozen:MMM d}, {Pantry.FreezerNote(Item.Name)}" : $"Added {Item.Since:MMM d}",
        Item.UseBySet ? "best-before set by hand" : "",
        $"counted {Item.Counted:MMM d}",
        Item.Unsure ? "the amount is a guess" : "",
        "click to change it",
    }.Where(p => p.Length > 0));

    // ---- Opened: what's typed, kept when it's left (GroceriesPage.Keep), and the rest as it is.

    public string NameText { get; set; } = "";
    public string NoteText { get; set; } = "";
    public string AmountText { get; set; } = "";
    public string PacksText { get; set; } = "";
    public string WordText { get; set; } = "";
    public string SizeText { get; set; } = "";
    public string PerPackText { get; set; } = "";

    /// <summary>0 when the size is each piece's, 1 when it's a whole pack's.</summary>
    public int SizeKind
    {
        get => sizeKind;
        set { sizeKind = value; Changed(nameof(OfPack)); }
    }
    private int sizeKind;
    public bool OfPack => sizeKind == 1;

    public string InAllText => Pantry.InAll(Item, system);
    public KitchenPlace Place => Pantry.PlaceOf(Item, Places);
    public int GroupIndex => (int)Item.ShownGroup;
    public bool Frozen => Item.InFreezer;
    public DateTime AddedDate => Item.Since.Date;
    public DateTime? FrozenDate => Item.Frozen?.Date;
    public DateTime? UseByDate => Item.UseBy?.Date;
    public bool UseBySet => Item.UseBySet;
    public string DateNote => Item.UseBySet ? "Set by hand: it stays, frozen or not, until it's put back to the usual date."
        : Item.UseBy is null ? "It keeps: no date to watch."
        : Item.InFreezer ? "Worked out from when it went in the freezer, for as long as it's at its best frozen."
        : "Worked out from when it was added, for as long as it keeps where it's kept.";
    public string Problem { get; private set; } = "";

    /// <summary>It's changed: the line shows it, and the boxes what it is now.</summary>
    public void Update(PantryItem changed, string problem = "")
    {
        Item = changed;
        Reset();
        Problem = problem;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>The packing as it was shown: only what's typed over it is read again (1.1 lb shown isn't 500 g).</summary>
    private string shownPacking = "";
    private string PackingTyped => string.Join("|", PacksText, WordText, SizeText, sizeKind, PerPackText);
    public bool PackingEdited => PackingTyped != shownPacking;

    private void Reset()
    {
        NameText = Item.Name;
        NoteText = Item.Note ?? "";
        AmountText = Pantry.Show(Item, system);
        var packing = Item.Packing;
        PacksText = packing?.Packs is { } packs ? Amounts.Format(packs, "") : "";
        WordText = packing?.Word ?? "pack";
        SizeText = packing?.Size is { } size ? Quantities.Format(new Quantity(size, packing.Dimension), system) : "";
        PerPackText = packing?.PerPack is { } per ? Amounts.Format(per, "") : "";
        sizeKind = packing?.OfPack == true ? 1 : 0;
        shownPacking = PackingTyped;
    }

    private void Changed(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A group of what's in a place ("Beverages"), and what's in it; while things are being picked, with a box to pick them all.</summary>
public sealed record GroupSection(string Title, List<PantryRow> Rows, bool Selecting = false);

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
    public GroceriesPage()
    {
        InitializeComponent();
        // Left, the page starts afresh: nothing open, nothing picked.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) return;
            KeepOpen();
            (openName, selecting) = (null, false);
            picked.Clear();
            SearchBox.Text = "";
        };
        searchWait.Tick += (_, _) =>
        {
            searchWait.Stop();
            KeepOpen();
            Redraw();
        };
    }

    private static App App => App.Current;

    /// <summary>Shown again, as it is now; not while something's typed, opened or picked here, which that would lose.</summary>
    public void Refresh()
    {
        if (IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBox || openName is not null || selecting) return;
        Redraw();
    }

    private void Redraw()
    {
        var system = App.Measures;
        var items = App.Database.Groceries();
        var toBuyAll = items.Where(i => i.Bought is null).ToList();
        var boughtAll = items.Where(i => i.Bought is not null).ToList();
        var toBuy = toBuyAll.Where(i => Matches(i.Name)).Select(i => new GroceryRow(i, App.Database.LastBought(i.Name), system)).ToList();
        var bought = boughtAll.Where(i => Matches(i.Name)).Select(i => new GroceryRow(i, null, system)).ToList();
        ToBuyList.ItemsSource = toBuy;
        BoughtList.ItemsSource = bought;
        ToBuyPanel.Show(toBuy.Count > 0);
        BoughtPanel.Show(bought.Count > 0);
        Empty.Show(items.Count == 0 && !Searching);
        ToBuyCount.Text = Searching ? $"{toBuy.Count} of {toBuyAll.Count}" : toBuy.Count.ToString(CultureInfo.CurrentCulture);
        var total = boughtAll.Sum(b => b.Price ?? 0);
        TotalText.Text = (Searching ? $"{bought.Count} of {boughtAll.Count}" : boughtAll.Count.ToString(CultureInfo.CurrentCulture))
            + (total > 0 ? " · " + total.ToString("C", CultureInfo.CurrentCulture) : "");
        SummaryText.Text = items.Count == 0 ? "What to buy, and what it cost"
            : $"{toBuyAll.Count} to buy" + (boughtAll.Count > 0 ? $" · {boughtAll.Count} in the cart" : "") + (total > 0 ? $" · {total.ToString("C", CultureInfo.CurrentCulture)} so far" : "");
        var typed = NameBox.Text;
        NameBox.ItemsSource = App.Database.GroceryNames();
        NameBox.Text = typed;
        ShowQuantityHint();

        ShowKitchen(system);
        ShowCheckTime();
        ClearSearch.Show(Searching);
        NoMatch.Show(Searching && toBuy.Count + bought.Count + kitchenShown == 0);
        NoMatchText.Text = $"Nothing on the list or in the kitchen matches “{SearchBox.Text.Trim()}”. To add it, type it under What.";
        PhotosButton.IsEnabled = App.ClaudeOn && !reading;
        if (!reading && !FoundPanel.IsVisible)
            PhotosText.Text = App.ClaudeOn
                ? "Photos of your fridge, cupboard, groceries or a receipt: Claude lists what's in them."
                : "To add groceries from photos, turn on Help from Claude in Settings.";
    }

    // ---- Search -----------------------------------------------------------------------------------------------

    private readonly System.Windows.Threading.DispatcherTimer searchWait = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private int kitchenShown;

    private bool Searching => SearchBox.Text.Trim().Length > 0;

    /// <summary>The words for each picture: its kind and what it shows ("Pasta", "Elbow macaroni").</summary>
    private static readonly Dictionary<string, string> PictureWords =
        Foods.IconSections.SelectMany(s => s.Icons.Select(i => (i.Key, Words: s.Section + " " + i.Label))).ToDictionary(i => i.Key, i => i.Words);

    /// <summary>Whether every word searched for is in a name (or its brand), or starts a word of what kind of food it is:
    /// "pep" finds Pepsi and Red peppers, "diet pep" just Diet Pepsi, "pasta" the spaghetti and the rotini, and "meat"
    /// what's in Meat &amp; fish.</summary>
    private bool Matches(string name, string? note, FoodGroup group, string icon)
    {
        var text = name + " " + note;
        var kind = (Foods.Name(group) + " " + PictureWords.GetValueOrDefault(icon)).Split([' ', '&'], StringSplitOptions.RemoveEmptyEntries);
        return SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(word => text.Contains(word, StringComparison.CurrentCultureIgnoreCase)
                || kind.Any(k => k.StartsWith(word, StringComparison.CurrentCultureIgnoreCase)));
    }

    private bool Matches(string name)
    {
        var look = Foods.Guess(name);
        return Matches(name, null, look.Group, look.Icon);
    }

    /// <summary>Shown as it's typed, once typing pauses.</summary>
    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        searchWait.Stop();
        searchWait.Start();
    }

    private void Search_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !Searching) return;
        e.Handled = true;
        SearchBox.Text = "";
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
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
        Redraw();
        PhotosText.Text = message.Length > 0 ? message : PhotosText.Text;
        App.KitchenChanged();
    }

    // ---- The stock ----------------------------------------------------------------------------------------------

    /// <summary>The line opened, by name; the names picked, while things are being picked.</summary>
    private string? openName;
    private bool selecting;
    private readonly HashSet<string> picked = new(StringComparer.OrdinalIgnoreCase);
    private List<PantryRow> rows = [];
    private List<KitchenPlace> places = [];

    /// <summary>The stock: by place, in the places' order, and in each by group, in the order picked.</summary>
    private void ShowKitchen(MeasureSystem system)
    {
        var now = DateTime.Now;
        places = PantryLog.Places();
        var sort = PantryLog.Sort;
        rows = Pantry.Sorted(PantryLog.Items(), sort).Select(p => new PantryRow(p, system, now, places)).ToList();
        picked.IntersectWith(rows.Select(r => r.Name));
        foreach (var row in rows)
        {
            row.Open = string.Equals(row.Name, openName, StringComparison.OrdinalIgnoreCase);
            row.Selecting = selecting;
            row.Selected = picked.Contains(row.Name);
            row.PropertyChanged += Row_PropertyChanged;
        }
        var placeOf = rows.ToDictionary(r => r, r => Pantry.PlaceOf(r.Item, places).Id);
        // Searching, only what matches is shown, in every place it's in, closed or not.
        var shown = rows.Where(r => Matches(r.Name, r.Note, r.Item.ShownGroup, r.Item.ShownIcon)).ToList();
        var closed = Searching ? new HashSet<long>() : ClosedPlaces();
        KitchenList.ItemsSource = places
            .Select(place => new PlaceSection(place,
                shown.Where(r => placeOf[r] == place.Id)
                    .GroupBy(r => r.Item.ShownGroup).OrderBy(g => g.Key)
                    .Select(g => new GroupSection(Foods.Name(g.Key), g.ToList(), selecting))
                    .ToList(),
                !closed.Contains(place.Id)))
            .Where(s => s.Groups.Count > 0)
            .ToList();
        kitchenShown = shown.Count;
        PlaceList.ItemsSource = places.Select((place, i) => new PlaceRow(place, placeOf.Values.Count(id => id == place.Id), i == 0, i == places.Count - 1, places.Count == 1)).ToList();
        if (NewPlaceKind.ItemsSource is null)
        {
            NewPlaceKind.ItemsSource = PlaceKindChoice.All;
            NewPlaceKind.SelectedIndex = 0;
        }
        if (SortBox.ItemsSource is null) SortBox.ItemsSource = SortNames;
        sorting = true;
        SortBox.SelectedIndex = (int)sort;
        sorting = false;
        NoKitchen.Show(rows.Count == 0);
        var doubt = rows.Count(r => r.InDoubt);
        KitchenSummary.Text = rows.Count == 0 ? ""
            : Searching ? $"{shown.Count} of {rows.Count} things"
            : $"{rows.Count} things" + (doubt > 0 ? $" · {doubt} to check" : "");
        SelectButtonText.Text = selecting ? "Done picking" : "Pick";
        ShowPicked();
    }

    /// <summary>In a narrow window, the sort and the buttons go under the heading.</summary>
    private void KitchenHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 640;
        Grid.SetRow(KitchenTools, narrow ? 1 : 0);
        Grid.SetColumn(KitchenTools, narrow ? 0 : 1);
        Grid.SetColumnSpan(KitchenTools, narrow ? 2 : 1);
        KitchenTools.Margin = new Thickness(0, narrow ? 8 : 0, 0, 0);
    }

    private static readonly string[] SortNames = ["By name", "Best before first", "Newest first", "Oldest first", "By brand"];
    private bool sorting;

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sorting || SortBox.SelectedIndex < 0) return;
        PantryLog.Sort = (KitchenSort)SortBox.SelectedIndex;
        Redraw();
    }

    private static PantryRow? PantryRowOf(object sender) => (sender as FrameworkElement)?.DataContext as PantryRow;

    /// <summary>Counted: what's typed is what there is.</summary>
    private void PantrySet_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row || row.Count.Trim().Length == 0) return;
        PantryLog.Counted(row.Item.Name, row.Count);
        Redraw();
    }

    private void PantryRight_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        PantryLog.Confirmed(row.Item);
        Redraw();
    }

    private void PantryCount_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        PantrySet_Click(sender, e);
    }

    /// <summary>Its picture and the picture's colour.</summary>
    private void PantryLook_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        var dialog = new FoodLookWindow(row.Item) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is not { } changed) return;
        PantryLog.Changed(changed);
        Redraw();
        App.KitchenChanged();
    }

    // ---- A line opened, to change in place ------------------------------------------------------------------------

    private Point pressedAt;
    private PantryRow? pressed;

    private void PantryLine_MouseDown(object sender, MouseButtonEventArgs e)
    {
        pressed = PantryRowOf(sender);
        pressedAt = e.GetPosition(this);
    }

    /// <summary>A click opens the line, or closes it; picking, it picks it.</summary>
    private void PantryLine_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row || row != pressed) return;
        pressed = null;
        if (selecting)
        {
            row.Selected = !row.Selected;
            return;
        }
        KeepOpen();
        openName = row.Open ? null : row.Name;
        Redraw();
    }

    /// <summary>Dragged a little way: it, or what's picked with it, is carried to another place.</summary>
    private void PantryLine_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || pressed is not { } row || PantryRowOf(sender) != row) return;
        var moved = e.GetPosition(this) - pressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        pressed = null;
        var carried = row.Selected ? rows.Where(r => r.Selected).Select(r => r.Name).ToArray() : [row.Name];
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(string[]), carried), DragDropEffects.Move);
    }

    private void Place_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(string[])) ? DragDropEffects.Move : DragDropEffects.None;
        if (sender is Border border && e.Effects == DragDropEffects.Move) border.SetResourceReference(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
        e.Handled = true;
    }

    private void Place_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border) border.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
    }

    private void Place_Drop(object sender, DragEventArgs e)
    {
        Place_DragLeave(sender, e);
        if ((sender as FrameworkElement)?.DataContext is not PlaceSection section || e.Data.GetData(typeof(string[])) is not string[] names) return;
        var moving = rows.Where(r => names.Contains(r.Name, StringComparer.OrdinalIgnoreCase)).Select(r => r.Item).ToList();
        MoveTo(moving, section.Place);
    }

    private void MoveTo(List<PantryItem> moving, KitchenPlace to)
    {
        KeepOpen();
        PantryLog.Move(moving, to);
        Redraw();
        App.KitchenChanged();
        PickedText.Text = $"Moved {moving.Count} to {to.Name}.";
    }

    /// <summary>What's typed in the line that's open, kept: its name and brand, how much, and the packs it's in.</summary>
    private void KeepOpen()
    {
        if (rows.FirstOrDefault(r => r.Open) is { } row) Keep(row);
    }

    private void Keep(PantryRow row)
    {
        var item = row.Item;
        var problem = "";
        var amount = row.AmountText.Trim();
        if (amount.Length > 0 && amount != Pantry.Show(item, App.Measures) && Quantities.Parse(amount) is not null)
            item = PantryLog.Recounted(item, amount);
        item = item with { Note = row.NoteText.Trim() is { Length: > 0 } note ? note : null };
        var (packing, wrong) = row.PackingEdited ? Pantry.ReadPacking(row.PacksText, row.WordText, row.SizeText, row.OfPack, row.PerPackText) : (item.Packing, "");
        if (wrong.Length == 0) item = item with { Packing = packing };
        else problem = wrong;
        if (item != row.Item) PantryLog.Changed(item);
        var name = row.NameText.Trim();
        if (name.Length > 0 && name != item.Name)
        {
            if (PantryLog.Rename(item, name))
            {
                if (string.Equals(openName, item.Name, StringComparison.OrdinalIgnoreCase)) openName = name;
                if (picked.Remove(item.Name)) picked.Add(name);
                item = item with { Name = name };
            }
            else problem = $"Something else is called {name} already.";
        }
        if (item != row.Item || problem != row.Problem || name != item.Name) row.Update(item, problem);
    }

    private void PantryField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is { } row) Keep(row);
    }

    private void PantryField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || PantryRowOf(sender) is not { } row) return;
        e.Handled = true;
        Keep(row);
    }

    private void PantryDone_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        Keep(row);
        if (row.Problem.Length > 0) return;
        openName = null;
        Redraw();
        App.KitchenChanged();
    }

    private void Changed(PantryRow row, PantryItem item)
    {
        if (item == row.Item) return;
        PantryLog.Changed(item);
        row.Update(item);
    }

    private void PantrySizeKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PantryRowOf(sender) is { } row && sender is ComboBox { IsKeyboardFocusWithin: true } or ComboBox { IsDropDownOpen: true }) Keep(row);
    }

    /// <summary>Another place: frozen in a freezer from today, thawed out of one.</summary>
    private void PantryPlace_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row || sender is not ComboBox { SelectedItem: KitchenPlace place } || place.Id == row.Place.Id) return;
        Changed(row, PantryLog.MovedTo(row.Item, place));
    }

    private void PantryGroup_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row || sender is not ComboBox { SelectedIndex: >= 0 and var index } || index == row.GroupIndex) return;
        var group = (FoodGroup)index;
        Changed(row, row.Item with { Group = group == row.Item.Guess.Group ? null : group });
    }

    private void PantryAdded_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (PantryRowOf(sender!) is not { } row || sender is not DatePicker { SelectedDate: { } day } || day.Date == row.AddedDate) return;
        Changed(row, PantryLog.Added(row.Item, day));
    }

    private void PantryFrozen_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (PantryRowOf(sender!) is not { } row || sender is not DatePicker { SelectedDate: { } day } || day.Date == row.FrozenDate) return;
        Changed(row, PantryLog.FrozenOn(row.Item, day));
    }

    private void PantryUseBy_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (PantryRowOf(sender!) is not { } row || sender is not DatePicker picker || picker.SelectedDate?.Date == row.UseByDate) return;
        Changed(row, Pantry.SetUseBy(row.Item, picker.SelectedDate));
    }

    private void PantryUsualDate_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is { } row) Changed(row, PantryLog.UsualUseBy(row.Item));
    }

    // ---- Picking things, to move or change them together -------------------------------------------------------

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        selecting = !selecting;
        if (!selecting) picked.Clear();
        else openName = null;
        Redraw();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PantryRow row || e.PropertyName != nameof(PantryRow.Selected)) return;
        if (row.Selected) picked.Add(row.Name);
        else picked.Remove(row.Name);
        ShowPicked();
    }

    /// <summary>All of a group picked, or none.</summary>
    private void GroupPick_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not GroupSection group) return;
        var all = !group.Rows.All(r => r.Selected);
        foreach (var row in group.Rows) row.Selected = all;
    }

    private void ShowPicked()
    {
        var count = picked.Count;
        PickedBar.Show(selecting);
        // Room at the end, so the bar doesn't cover what's last.
        KitchenPanel.Margin = new Thickness(0, 0, 0, selecting ? 120 : 0);
        PickedCount.Text = count == 0 ? "Click things, or drag them to a place" : $"{count} picked";
        foreach (var box in new Control[] { MovePickedBox, GroupPickedBox, PickedGone }) box.IsEnabled = count > 0;
        if (MovePickedBox.ItemsSource != places)
        {
            MovePickedBox.ItemsSource = places;
            GroupPickedBox.ItemsSource = PantryRow.GroupNames;
        }
        if (count > 0) PickedText.Text = "";
    }

    private List<PantryItem> Picked() => rows.Where(r => r.Selected).Select(r => r.Item).ToList();

    private void MovePicked_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MovePickedBox.SelectedItem is not KitchenPlace to) return;
        MovePickedBox.SelectedIndex = -1;
        MoveTo(Picked(), to);
    }

    private void GroupPicked_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (GroupPickedBox.SelectedIndex < 0) return;
        var group = (FoodGroup)GroupPickedBox.SelectedIndex;
        GroupPickedBox.SelectedIndex = -1;
        var changing = Picked();
        foreach (var item in changing) PantryLog.Changed(item with { Group = group == item.Guess.Group ? null : group });
        Redraw();
        PickedText.Text = $"{changing.Count} put in {Foods.Name(group)}.";
    }

    private void PickedGone_Click(object sender, RoutedEventArgs e)
    {
        var gone = Picked();
        foreach (var item in gone) PantryLog.Gone(item);
        picked.Clear();
        Redraw();
        App.KitchenChanged();
        PickedText.Text = $"Took {gone.Count} off.";
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
        Redraw();
    }

    private void Places_Click(object sender, RoutedEventArgs e) => PlacesCard.Show(!PlacesCard.IsVisible);

    private static PlaceRow? PlaceRowOf(object sender) => (sender as FrameworkElement)?.DataContext as PlaceRow;

    private void PlaceName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (PlaceRowOf(sender) is not { } row || row.Name.Trim().Length == 0 || row.Name.Trim() == row.Place.Name) return;
        PantryLog.ChangePlace(row.Place with { Name = row.Name.Trim() });
        Redraw();
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
        Dispatcher.InvokeAsync(() => Redraw());
        App.KitchenChanged();
    }

    private void PlaceUp_Click(object sender, RoutedEventArgs e) => MovePlace(sender, -1);

    private void PlaceDown_Click(object sender, RoutedEventArgs e) => MovePlace(sender, 1);

    private void MovePlace(object sender, int by)
    {
        if (PlaceRowOf(sender) is not { } row) return;
        PantryLog.MovePlace(row.Place, by);
        Redraw();
    }

    private void PlaceDelete_Click(object sender, RoutedEventArgs e)
    {
        if (PlaceRowOf(sender) is not { } row) return;
        PantryLog.DeletePlace(row.Place);
        Redraw();
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
        Redraw();
    }

    private void PantryGone_Click(object sender, RoutedEventArgs e)
    {
        if (PantryRowOf(sender) is not { } row) return;
        PantryLog.Gone(row.Item);
        Redraw();
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
        if (PutAway(name, QuantityBox.Text.Trim()) is not { } place) return;
        NameBox.Text = "";
        QuantityBox.Text = "";
        Redraw();
        if (place.Length > 0) QuantityHint.Text = $"Added {name} to {place}.";
    }

    /// <summary>
    /// Into the kitchen, by hand. What's there already is added to where it is. What's new goes where it was put the last
    /// time it was new, or else where it's said to go, asked first; null when that's cancelled. The place's name, or ""
    /// when it isn't kept track of.
    /// </summary>
    private string? PutAway(string name, string quantity)
    {
        if (!Pantry.Tracked(name)) return "";
        if (PantryLog.Have(name))
        {
            PantryLog.Bought(name, quantity);
            return Pantry.PlaceOf(PantryLog.Items().First(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)), PantryLog.Places()).Name;
        }
        if (PantryLog.PutBefore(name) is { } before)
        {
            PantryLog.PutAway(name, quantity, before.Place, before.Group);
            return before.Place.Name;
        }
        var dialog = new WhereWindow(PantryLog.New(name, quantity), PantryLog.Places(), App.Measures) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Place is not { } place) return null;
        PantryLog.PutAway(name, quantity, place, dialog.Group);
        return place.Name;
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
        Redraw();
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
        // Into the kitchen's stock first: asked where, when it's new there, it isn't bought until that's said.
        if (PutAway(row.Item.Name, row.QuantityText.Trim()) is null) return;
        row.Item.Quantity = row.QuantityText.Trim();
        row.Item.Price = price;
        row.Item.Bought = DateTime.Now;
        App.Database.Save(row.Item);
        Redraw();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        row.Item.Bought = null;
        row.Item.Price = null;
        App.Database.Save(row.Item);
        Redraw();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        App.Database.Delete(row.Item);
        Redraw();
    }

    private void DoneShopping_Click(object sender, RoutedEventArgs e)
    {
        App.Database.ClearBought();
        Redraw();
    }
}
