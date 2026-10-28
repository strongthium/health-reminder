using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>An entry of the "What is it?" list.</summary>
public sealed class KindChoice(ReminderKind kind)
{
    public ReminderKind Kind { get; } = kind;
    public string Label => Kind.Label();
    public PillAppearance Sample { get; } = new(["WHITE"], "ROUND", 0, "", 1);
}

/// <summary>One thing found: a U.S. label on DailyMed (of one or more strengths) or a Canadian product.</summary>
public sealed class SearchResult
{
    private static readonly Brush Canada = KindLook.Brush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush UnitedStates = KindLook.Brush(Color.FromRgb(0x1F, 0x4E, 0x99));

    public SearchResult(LabelSummary label) =>
        (Label, Country, CountryBrush, Title, Detail) = (label, "U.S.", UnitedStates, label.Title, "DailyMed label, " + label.Published);

    public SearchResult(CanadianDrug drug)
    {
        (Drug, Country, CountryBrush) = (drug, "Canada", Canada);
        Title = drug.Name + (drug.Form.Length > 0 ? $" ({ReminderWindow.FormOf(drug).ToLowerInvariant()})" : "");
        Detail = $"DIN {drug.Product.Din} · {CanadianDrug.Titled(drug.Product.Company)} · {drug.Status}";
    }

    public LabelSummary? Label { get; }
    public CanadianDrug? Drug { get; }
    public string Country { get; }
    public Brush CountryBrush { get; }
    public string Title { get; }
    public string Detail { get; }
    /// <summary>A product no longer sold is shown faded.</summary>
    public double Opacity => Drug is { Marketed: false } ? 0.6 : 1;
}

public sealed class ProductItem(PillProduct product, CanadianDrug? drug = null)
{
    public PillProduct Product { get; } = product;
    /// <summary>The Canadian product it is, when it came from Health Canada.</summary>
    public CanadianDrug? Drug { get; } = drug;
    public ReminderKind Kind => ReminderWindow.KindOfForm(Product.Form);
    public string Look => Product.Appearance.IsKnown ? Product.Appearance.Describe(Product.Form)
        : Drug is not null ? "What it looks like is read from its product monograph" : "";
    public string NdcText => Drug is not null ? $"DIN {Drug.Product.Din} · {Drug.Status}" : Product.Ndc.Length > 0 ? "NDC " + Product.Ndc : "";
}

/// <summary>A pill of the same drug and strength from another maker, to pick if it looks like yours.</summary>
public sealed class LookAlikeItem(LookAlike lookAlike)
{
    public LookAlike LookAlike { get; } = lookAlike;
    public PillProduct Product => LookAlike.Product;
    public string Look => Product.Appearance.Describe(Product.Form);
    public string Detail => (LookAlike.Count > 1 ? $"On {LookAlike.Count} U.S. labels" : "U.S. label: " + LookAlike.Label.Title)
        + (Product.Image.Length > 0 ? " · with a photo" : "");
}

/// <summary>A photo to choose from: downloaded (not saved yet) or the one already saved.</summary>
public sealed class PhotoItem(string name, byte[]? bytes, ImageSource bitmap, string source)
{
    public string Name { get; } = name;
    public byte[]? Bytes { get; } = bytes;
    public ImageSource Bitmap { get; } = bitmap;
    public string Source { get; } = source;
}

/// <summary>A time being edited, with its amount.</summary>
public sealed class TimeSlot(TimeSpan? time, double? amount, bool showAmount, double step)
{
    public TimeSpan? Time { get; set; } = time;
    public double? Amount { get; set; } = amount;
    public bool ShowAmount { get; } = showAmount;
    public double Step { get; } = step;
}

/// <summary>
/// Adds or edits a reminder. For a medication the left side looks it up: by name, by a Canadian DIN in Health
/// Canada's Drug Product Database, or by a U.S. NDC on DailyMed, then the strength. A U.S. label says what the pill
/// looks like, and a photo of the very pill is looked for; for a Canadian product the look is read from its product
/// monograph. Either way the same drug and strength from other makers is listed, to pick the one that looks like
/// yours. The right side is what is saved: name, DIN and prescription number, times and amounts, days, injection sites
/// or reading units, and what it looks like.
/// </summary>
public sealed partial class ReminderWindow : Window
{
    private readonly Reminder reminder;
    private readonly bool isNew;
    private readonly ObservableCollection<TimeSlot> times = [];
    private readonly ObservableCollection<PhotoItem> photos = [];
    private readonly Dictionary<DayOfWeek, ToggleButton> dayButtons = [];
    private readonly Dictionary<string, CheckBox> siteChecks = [];
    private CancellationTokenSource? lookup, photoSearch, lookAlikeSearch;
    private string form, setId, ndc, rxCui, labelTitle, autoName = "", autoUnit = "", monographUrl = "";
    private string searchedNdc = "";
    private PhotoItem? foundPhoto;
    private bool loading = true, confirmDelete;

    private static readonly string[] Scores = ["Not scored", "Scored in half", "Scored in thirds", "Scored in quarters"];

    public ReminderWindow(Reminder? existing)
    {
        InitializeComponent();
        Icon = App.Icon;
        isNew = existing is null;
        reminder = existing ?? new Reminder { Times = [new ReminderTime(new TimeOnly(8, 0), 1)] };
        Title = isNew ? "Add a reminder" : "Edit " + reminder.Name;
        DeleteButton.Show(!isNew);
        (form, setId, ndc, rxCui, labelTitle) = (reminder.Form, reminder.SetId, reminder.Ndc, reminder.RxCui, reminder.LabelTitle);

        KindBox.ItemsSource = Kinds.All.Select(k => new KindChoice(k)).ToList();
        KindBox.SelectedIndex = Array.IndexOf(Kinds.All, reminder.Kind);
        NameBox.Text = reminder.Name;
        DinBox.Text = reminder.Din;
        RxBox.Text = reminder.RxNumber;
        ShowSupply();
        UnitBox.Text = reminder.Unit;
        NotesBox.Text = reminder.Notes;
        ActiveBox.IsChecked = reminder.Active;
        VideoBox.IsChecked = reminder.RecordVideo;
        TimeList.ItemsSource = times;
        UnitBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => ShowTimesHeading()));

        var culture = CultureInfo.CurrentCulture;
        foreach (var day in Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)culture.DateTimeFormat.FirstDayOfWeek + i) % 7)))
        {
            var button = new ToggleButton
            {
                Content = culture.DateTimeFormat.GetAbbreviatedDayName(day),
                IsChecked = reminder.Days.Contains(day),
                Margin = new Thickness(0, 0, 6, 6),
                MinWidth = 52,
            };
            dayButtons[day] = button;
            DayPanel.Children.Add(button);
        }

        var sites = reminder.Sites.Count > 0 ? reminder.Sites : [.. InjectionSites.All.Take(4)];
        foreach (var site in InjectionSites.All)
        {
            var check = new CheckBox { Content = site, IsChecked = sites.Contains(site), Margin = new Thickness(0, 4, 14, 4), MinWidth = 150 };
            siteChecks[site] = check;
            SiteChecks.Children.Add(check);
        }

        Color1Box.ItemsSource = new[] { "(unknown)" }.Concat(PillView.Colors).ToList();
        Color2Box.ItemsSource = new[] { "(none)" }.Concat(PillView.Colors).ToList();
        ShapeBox.ItemsSource = new[] { "(unknown)" }.Concat(PillView.Shapes).ToList();
        ScoreBox.ItemsSource = Scores;
        SizeBox.ValueChanged += (_, _) => UpdatePreview();
        ShowAppearance(reminder.Appearance);

        PhotoList.ItemsSource = photos;
        if (Photos.Load(App.Current.Database.ImageDirectory, reminder.Photo) is { } saved)
            foundPhoto = new PhotoItem(reminder.Photo, null, saved, reminder.PhotoSource);

        loading = false;
        ShowKind(fillDefaults: false);
        Loaded += (_, _) => (isNew ? (Control)KindBox : NameBox).Focus();
        Closed += (_, _) =>
        {
            lookup?.Cancel();
            photoSearch?.Cancel();
            lookAlikeSearch?.Cancel();
        };
    }

    private ReminderKind Kind => KindBox.SelectedItem is KindChoice choice ? choice.Kind : ReminderKind.Pill;

    /// <summary>A Canadian product's form as a label writes it: "Tablet (Extended-Release)" → "TABLET, EXTENDED-RELEASE".</summary>
    public static string FormOf(CanadianDrug drug) => drug.Form.ToUpperInvariant().Replace(" (", ", ", StringComparison.Ordinal).Replace(")", "", StringComparison.Ordinal);

    /// <summary>The kind of reminder a label's dosage form is.</summary>
    public static ReminderKind KindOfForm(string form)
    {
        var f = form.ToUpperInvariant();
        return f.Contains("TABLET") || f.Contains("CAPSULE") || f.Contains("LOZENGE") || f.Contains("PILL") ? ReminderKind.Pill
            : f.Contains("INJECT") || f.Contains("PEN") || f.Contains("SYRINGE") ? ReminderKind.Injection
            : f.Contains("INHAL") || f.Contains("AEROSOL") ? ReminderKind.Inhaler
            : f.Contains("OPHTHALMIC") || f.Contains("DROP") || f.Contains("OTIC") ? ReminderKind.Drops
            : f.Contains("PATCH") || f.Contains("CREAM") || f.Contains("OINTMENT") || f.Contains("GEL") || f.Contains("LOTION") ? ReminderKind.Patch
            : f.Contains("SOLUTION") || f.Contains("SUSPENSION") || f.Contains("SYRUP") || f.Contains("ELIXIR") || f.Contains("LIQUID") ? ReminderKind.Liquid
            : ReminderKind.OtherMedication;
    }

    private void Kind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading) ShowKind(fillDefaults: true);
    }

    /// <summary>Shows the parts of the form the kind needs; for a new reminder, fills in its usual unit and name.</summary>
    private void ShowKind(bool fillDefaults)
    {
        var kind = Kind;
        var medication = kind.IsMedication();
        LookupPanel.Show(medication);
        NumbersPanel.Show(medication);
        SupplyPanel.Show(medication && kind.HasAmount());
        Columns.ColumnDefinitions[0].Width = medication ? new GridLength(5, GridUnitType.Star) : new GridLength(0);
        DetailsPanel.Margin = medication ? new Thickness(8, 0, 0, 0) : new Thickness(0);
        DetailsHeading.Text = medication ? "2. Your medication" : kind.Label();
        UnitPanel.Show(kind.HasAmount() && kind != ReminderKind.Meal && kind != ReminderKind.Drink);
        UnitBox.ItemsSource = kind.Units();
        ValueUnitPanel.Show(kind.ValueUnits().Count > 0);
        ValueUnitBox.ItemsSource = kind.ValueUnits();
        ValueUnitBox.SelectedItem = kind.ValueUnits().Contains(reminder.ValueUnit) ? reminder.ValueUnit : kind.ValueUnits().FirstOrDefault();
        SitesPanel.Show(kind == ReminderKind.Injection);
        AppearanceExpander.Show(kind == ReminderKind.Pill);
        LookPanel.Show(kind == ReminderKind.Pill);

        if (fillDefaults)
        {
            if (string.IsNullOrWhiteSpace(UnitBox.Text) || UnitBox.Text == autoUnit || !kind.Units().Contains(UnitBox.Text))
                UnitBox.Text = autoUnit = kind.DefaultUnit(form);
            if ((string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == autoName) && kind.DefaultName().Length > 0)
                NameBox.Text = autoName = kind.DefaultName();
        }
        else if (string.IsNullOrWhiteSpace(UnitBox.Text))
        {
            UnitBox.Text = autoUnit = kind.DefaultUnit(form);
        }
        ShowTimes();
        UpdatePreview();
    }

    /// <summary>Rebuilds the time rows, keeping what was typed, with an amount box when the kind counts something.</summary>
    private void ShowTimes()
    {
        var kind = Kind;
        // A drink is kept in millilitres and typed in the unit drinks are measured in (Settings).
        var drinkUnit = App.Current.DrinkUnit;
        var current = times.Count > 0
            ? times.Select(t => (t.Time, t.Amount)).ToList()
            : reminder.Times.Select(t => ((TimeSpan?)t.Time.ToTimeSpan(),
                (double?)(kind == ReminderKind.Drink && reminder.Unit == "mL" ? Quantities.FromMl(t.Amount, drinkUnit) : t.Amount))).ToList();
        var step = kind switch
        {
            ReminderKind.Pill => 0.5,
            ReminderKind.Meal => 50,
            ReminderKind.Drink => drinkUnit switch { DrinkUnit.Cup => 0.25, DrinkUnit.FluidOunce => 1, _ => 50 },
            ReminderKind.Liquid => 5,
            _ => 1,
        };
        times.Clear();
        foreach (var (time, amount) in current) times.Add(new TimeSlot(time, amount, kind.HasAmount(), step));
        ShowTimesHeading();
    }

    private void ShowTimesHeading()
    {
        if (TimesHeading is null) return;
        TimesHeading.Text = Kind.HasAmount() ? $"When, and how much ({Amounts.Plural(CurrentUnit)})" : "When";
        if (StockUnit is not null) StockUnit.Text = Amounts.Plural(CurrentUnit);
    }

    // ---- On hand -----------------------------------------------------------------------------------------------

    /// <summary>What the count shows when the editor opens (or after a refill): a change to it is a new count.</summary>
    private double? shownStock;

    private void ShowSupply()
    {
        var supply = reminder.Id == 0 ? null : App.Current.Database.Supply(reminder);
        shownStock = supply is null ? null : Math.Round(Math.Max(0, supply.OnHand), 2);
        StockBox.Value = shownStock;
        var refills = reminder.Id == 0 ? [] : App.Current.Database.Refills(reminder.Id);
        StockText.Text = supply is null
            ? "Type how many you have to keep count: refills you add go up, and only doses marked taken come off."
            : $"Counted {reminder.StockAt:MMM d}; doses marked taken come off it" + (supply.DaysLeft is { } days ? $", about {days} day{(days == 1 ? "" : "s")} left." : ".")
              + (refills.Count > 0 ? " Refills: " + string.Join(", ", refills.Take(4).Select(r => $"{Amounts.Format(r.Amount, reminder.Unit)} on {r.At:MMM d}")) + "." : "");
    }

    /// <summary>A refill received: added to the count, and kept (for a reminder already saved).</summary>
    private void Refill_Click(object sender, RoutedEventArgs e)
    {
        if (RefillBox.Value is not { } amount || amount <= 0) return;
        var now = DateTime.Now;
        if (reminder.Id != 0 && reminder.StockCount is not null)
        {
            App.Current.Database.AddRefill(reminder.Id, amount, now);
            ShowSupply();
        }
        else
        {
            // Not counted yet: the refill is what there is (or adds to what was typed).
            StockBox.Value = (StockBox.Value ?? 0) + amount;
            StockText.Text = $"With the refill: {Amounts.Format(StockBox.Value.Value, CurrentUnit)}. Saved with the reminder.";
        }
        RefillBox.Value = null;
    }

    /// <summary>The count to save: a new one when it was changed or first typed; none when it was emptied.</summary>
    private void ReadSupply()
    {
        if (StockBox.Value is not { } count)
        {
            (reminder.StockCount, reminder.StockAt) = (null, null);
            return;
        }
        if (shownStock is { } shown && Math.Abs(shown - count) < 0.001 && reminder.StockCount is not null) return;
        (reminder.StockCount, reminder.StockAt) = (count, DateTime.Now);
    }

    private string CurrentUnit => Kind switch
    {
        ReminderKind.Meal => "kcal",
        ReminderKind.Drink => App.Current.DrinkUnit.Label(),
        _ => (UnitBox.Text ?? "").Trim(),
    };

    private void ShowAppearance(PillAppearance appearance)
    {
        var wasLoading = loading;
        loading = true;
        Color1Box.SelectedIndex = appearance.Colors.Count > 0 ? Math.Max(0, Array.IndexOf(PillView.Colors, appearance.Colors[0]) + 1) : 0;
        Color2Box.SelectedIndex = appearance.Colors.Count > 1 ? Math.Max(0, Array.IndexOf(PillView.Colors, appearance.Colors[1]) + 1) : 0;
        ShapeBox.SelectedIndex = Array.FindIndex(PillView.Shapes, s => s.Equals(appearance.Shape, StringComparison.OrdinalIgnoreCase)) + 1;
        SizeBox.Value = appearance.SizeMm;
        ImprintBox.Text = appearance.Imprint;
        ScoreBox.SelectedIndex = Math.Clamp(appearance.Score - 1, 0, Scores.Length - 1);
        loading = wasLoading;
    }

    private PillAppearance ReadAppearance()
    {
        if (Kind != ReminderKind.Pill) return PillAppearance.Unknown;
        var colors = new List<string>();
        if (Color1Box.SelectedIndex > 0) colors.Add(PillView.Colors[Color1Box.SelectedIndex - 1]);
        if (Color2Box.SelectedIndex > 0 && !colors.Contains(PillView.Colors[Color2Box.SelectedIndex - 1])) colors.Add(PillView.Colors[Color2Box.SelectedIndex - 1]);
        var shape = ShapeBox.SelectedIndex > 0 ? PillView.Shapes[ShapeBox.SelectedIndex - 1] : "";
        return new PillAppearance(colors, shape, SizeBox.Value ?? 0, (ImprintBox.Text ?? "").Trim(), Math.Max(1, ScoreBox.SelectedIndex + 1));
    }

    /// <summary>The photo that will be saved: one chosen from the label by hand, else the one found of this pill.</summary>
    private PhotoItem? ChosenPhoto => Kind != ReminderKind.Pill ? null : PhotoList.SelectedItem as PhotoItem ?? foundPhoto;

    private void UpdatePreview()
    {
        if (loading) return;
        var appearance = ReadAppearance();
        Preview.Kind = Kind;
        Preview.Appearance = appearance;
        Preview.Form = form;
        LookText.Text = appearance.IsKnown
            ? appearance.Describe(form)
            : "Not known yet. Find it on the left, or describe it under \"Change how it looks\".";
        SourceText.Text = !Kind.IsMedication() || labelTitle.Length == 0 ? ""
            : setId.Length == 0 ? $"From Health Canada: {labelTitle}" + (rxCui.Length > 0 ? $", RxCUI {rxCui}" : "")
            : $"From DailyMed: {labelTitle}" + (ndc.Length > 0 ? $", NDC {ndc}" : "") + (rxCui.Length > 0 ? $", RxCUI {rxCui}" : "");
        var photo = ChosenPhoto;
        PreviewPhoto.Source = photo?.Bitmap;
        PreviewPhotoBorder.Show(photo is not null);
        if (photo is not null && photo.Source.Length > 0 && PhotoList.SelectedItem is null) PhotoStatus.Text = "Photo from " + photo.Source + ".";
    }

    private void Appearance_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();
    private void Imprint_Changed(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void Photo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PhotoList.SelectedItem is not null) PhotoStatus.Text = "Using a picture you chose from the label.";
        UpdatePreview();
    }

    private void ClearPhoto_Click(object sender, RoutedEventArgs e)
    {
        PhotoList.SelectedItem = null;
        PhotoStatus.Text = foundPhoto is null ? "" : "Photo from " + foundPhoto.Source + ".";
        UpdatePreview();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _ = Search();
    }

    private void Search_Click(object sender, RoutedEventArgs e) => _ = Search();

    private static CancellationToken Restart(ref CancellationTokenSource? source)
    {
        source?.Cancel();
        source = new CancellationTokenSource();
        return source.Token;
    }

    /// <summary>
    /// Looks the medication up: a DIN in Health Canada's database, an NDC on DailyMed, and a name in both. Canada's
    /// products come first on a Canadian PC, the U.S. labels first elsewhere.
    /// </summary>
    public async Task Search()
    {
        var query = (SearchBox.Text ?? "").Trim();
        if (query.Length < 2) return;
        var cancel = Restart(ref lookup);
        var din = HealthCanada.IsDin(query);
        searchedNdc = DailyMed.IsNdc(query) ? query : "";
        SearchStatus.Text = din ? "Looking the DIN up at Health Canada…" : searchedNdc.Length > 0 ? "Looking the NDC up on DailyMed…" : "Searching Health Canada and DailyMed…";
        LabelList.ItemsSource = null;
        ProductList.ItemsSource = null;
        LookAlikesExpander.Show(false);

        var problems = new List<string>();
        async Task<List<SearchResult>> Try(Func<Task<List<SearchResult>>> search, string source)
        {
            try
            {
                return await search();
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
            {
                if (!cancel.IsCancellationRequested) problems.Add($"Couldn't reach {source}: {exception.Message}");
                return [];
            }
        }
        var canada = din || searchedNdc.Length == 0
            ? Try(async () => (await App.Current.HealthCanada.SearchAsync(query, cancel)).Select(d => new SearchResult(d)).ToList(), "Health Canada")
            : Task.FromResult(new List<SearchResult>());
        var unitedStates = !din
            ? Try(async () => (await App.Current.DailyMed.SearchAsync(query, cancel)).Select(l => new SearchResult(l)).ToList(), "DailyMed")
            : Task.FromResult(new List<SearchResult>());
        var (ca, us) = (await canada, await unitedStates);
        if (cancel.IsCancellationRequested) return;

        var canadaFirst = din || RegionInfo.CurrentRegion.TwoLetterISORegionName == "CA";
        var results = canadaFirst ? [.. ca, .. us] : us.Concat(ca).ToList();
        LabelList.ItemsSource = results;
        SearchStatus.Text = results.Count switch
        {
            0 when problems.Count > 0 => string.Join(" ", problems),
            0 => din ? "No Canadian product has that DIN. Check the 8 digits, or search by name."
                : searchedNdc.Length > 0 ? "No label has that NDC. Check the number, or search by name."
                : "Nothing found. Check the spelling, or try the generic name (the ingredient).",
            1 => "Found one.",
            _ => $"Found {results.Count}: {ca.Count} in Canada, {us.Count} U.S. labels. Pick yours, or the closest." + (problems.Count > 0 ? " " + string.Join(" ", problems) : ""),
        };
        if (results.Count == 1) LabelList.SelectedIndex = 0;
    }

    private async void Label_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LabelList.SelectedItem is not SearchResult result) return;
        var cancel = Restart(ref lookup);
        ProductList.ItemsSource = null;
        photos.Clear();
        LabelPhotos.Show(result.Label is not null);

        if (result.Drug is { } drug)
        {
            // A Canadian product is one strength: it is chosen at once.
            var product = new PillProduct("", CanadianDrug.Titled(drug.Product.Brand), FormOf(drug), drug.Strength, PillAppearance.Unknown, [], "", "");
            ProductList.ItemsSource = new List<ProductItem> { new(product, drug) };
            ProductList.SelectedIndex = 0;
            SearchStatus.Text = drug.Marketed ? "" : $"Health Canada lists this product as \"{drug.Status}\": it may no longer be sold.";
            return;
        }

        var label = result.Label!;
        SearchStatus.Text = "Reading the label…";
        try
        {
            var products = await App.Current.DailyMed.ProductsAsync(label.SetId, cancel);
            if (cancel.IsCancellationRequested) return;
            ProductList.ItemsSource = products.Select(p => new ProductItem(p)).ToList();
            SearchStatus.Text = products.Count == 0 ? "This label lists no products." : "Pick your strength. A strength that isn't here may be on another label, or under its other makers once one is picked.";
            var match = searchedNdc.Length > 0 ? products.FindIndex(p => DailyMed.NdcMatches(p, searchedNdc)) : -1;
            if (match < 0 && products.Count == 1) match = 0;
            if (match >= 0) ProductList.SelectedIndex = match;
            await LoadLabelPictures(label, cancel);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (!cancel.IsCancellationRequested) SearchStatus.Text = "Couldn't read the label: " + exception.Message;
        }
    }

    /// <summary>The label's pictures, to choose from by hand if no photo of the pill itself was found.</summary>
    private async Task LoadLabelPictures(LabelSummary label, CancellationToken cancel)
    {
        var images = await App.Current.DailyMed.ImagesAsync(label.SetId, cancel);
        using var limit = new SemaphoreSlim(4);
        await Task.WhenAll(images.Select(async image =>
        {
            await limit.WaitAsync(cancel);
            try
            {
                var bytes = await App.Current.DailyMed.DownloadAsync(image.Url, cancel);
                if (cancel.IsCancellationRequested) return;
                if (Photos.FromBytes(bytes, 360) is { } picture) photos.Add(new PhotoItem(label.SetId + "-" + image.Name, bytes, picture, label.Title));
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                // A picture that doesn't load is left out.
            }
            finally
            {
                limit.Release();
            }
        }));
    }

    private void Product_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProductList.SelectedItem is not ProductItem item || LabelList.SelectedItem is not SearchResult result) return;
        var product = item.Product;
        if (item.Drug is { } drug)
        {
            (form, setId, ndc, labelTitle) = (product.Form, "", "", $"{CanadianDrug.Titled(drug.Product.Brand)}, DIN {drug.Product.Din}");
            DinBox.Text = drug.Product.Din;
        }
        else
        {
            (form, setId, ndc, labelTitle) = (product.Form, result.Label!.SetId, product.Ndc, result.Label.Title);
        }
        rxCui = "";
        monographUrl = "";
        MonographPanel.Show(false);
        var kind = item.Kind;
        if (kind != Kind) KindBox.SelectedIndex = Array.IndexOf(Kinds.All, kind);
        var name = $"{product.Name} {product.Strength}".Trim();
        if (string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == autoName) NameBox.Text = autoName = name;
        UnitBox.ItemsSource = kind.Units();
        if (string.IsNullOrWhiteSpace(UnitBox.Text) || UnitBox.Text == autoUnit || kind == ReminderKind.Pill) UnitBox.Text = autoUnit = kind.DefaultUnit(form);
        ShowAppearance(product.Appearance);
        foundPhoto = null;
        PhotoList.SelectedItem = null;
        UpdatePreview();
        if (item.Drug is { } canadian) _ = FindCanadianLook(canadian);
        else _ = FindPhoto(product, result.Label!);
    }

    /// <summary>Looks for a photo of the very pill chosen (<see cref="DailyMed.FindPhotoAsync"/>) and uses it; then
    /// lists the same drug and strength from other makers.</summary>
    private async Task FindPhoto(PillProduct product, LabelSummary label)
    {
        var cancel = Restart(ref photoSearch);
        PhotoStatus.Text = "";
        LookAlikesExpander.Show(false);
        try
        {
            if (KindOfForm(product.Form) != ReminderKind.Pill) return;
            PhotoStatus.Text = "Looking for a photo of this pill…";
            var progress = new Progress<string>(text =>
            {
                if (!cancel.IsCancellationRequested) PhotoStatus.Text = text;
            });
            var (photo, cui) = await App.Current.DailyMed.FindPhotoAsync(product, label, "", progress, cancel);
            if (cancel.IsCancellationRequested) return;
            rxCui = cui;
            if (rxCui.Length == 0 && product.PackageNdcs.FirstOrDefault() is { } package) rxCui = await App.Current.DailyMed.RxCuiAsync(package, cancel);
            if (rxCui.Length > 0) _ = FindLookAlikes([rxCui], product.Strength, open: false);
            if (photo is null)
            {
                PhotoStatus.Text = "DailyMed has no photo of this exact pill. The drawing shows what its label says; you can also pick one of the label's pictures, or another maker's pill, on the left.";
                UpdatePreview();
                return;
            }
            var bytes = await App.Current.DailyMed.DownloadAsync(photo.Url, cancel);
            if (cancel.IsCancellationRequested || Photos.FromBytes(bytes) is not { } picture) return;
            foundPhoto = new PhotoItem(photo.Name, bytes, picture, photo.Source);
            PhotoStatus.Text = "Found a photo of this pill, from " + photo.Source + ".";
            UpdatePreview();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            if (!cancel.IsCancellationRequested) PhotoStatus.Text = "Couldn't look for a photo: " + exception.Message;
        }
    }

    /// <summary>
    /// What a Canadian product looks like: read from its product monograph, which describes each strength's pill;
    /// and, to pick from, the same drug and strength as U.S. makers' labels describe it, found through RxNorm.
    /// </summary>
    private async Task FindCanadianLook(CanadianDrug drug)
    {
        var cancel = Restart(ref photoSearch);
        var pill = KindOfForm(drug.Form) == ReminderKind.Pill;
        var lookAlikes = Task.CompletedTask;
        try
        {
            var dailyMed = App.Current.DailyMed;
            var cuis = await dailyMed.ClinicalDrugsAsync(drug.Ingredients, drug.Form, cancel);
            if (cancel.IsCancellationRequested) return;
            rxCui = cuis.FirstOrDefault() ?? "";
            if (pill && cuis.Count > 0) lookAlikes = FindLookAlikes(cuis, drug.Ingredients.Count == 1 ? drug.Ingredients[0].StrengthText : "", open: false);
            if (!pill)
            {
                UpdatePreview();
                return;
            }

            PhotoStatus.Text = "Reading what the product monograph says it looks like…";
            var look = await App.Current.HealthCanada.LookFromMonographAsync(drug, cancel);
            if (cancel.IsCancellationRequested) return;
            monographUrl = look?.Url ?? "";
            MonographButton.Show(monographUrl.Length > 0);
            if (look is { Sentence.Length: > 0 })
            {
                MonographText.Text = "“" + look.Sentence + "”";
                MonographPanel.Show(true);
                if (look.Appearance.IsKnown) ShowAppearance(look.Appearance);
                PhotoStatus.Text = "The drawing is made from the monograph's description. To have a photo too, pick the U.S. pill on the left that looks the same, if there is one.";
                UpdatePreview();
                return;
            }
            MonographText.Text = monographUrl.Length > 0
                ? "No description of this strength's pill was found in it. It's usually under \"Dosage forms, strengths, composition and packaging\"."
                : "Health Canada has no product monograph for this product online.";
            MonographPanel.Show(true);
            PhotoStatus.Text = "Pick the pill on the left that looks like yours, or describe it under \"Change how it looks\".";
            await lookAlikes;
            if (LookAlikeList.Items.Count > 0) LookAlikesExpander.IsExpanded = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or IOException)
        {
            if (!cancel.IsCancellationRequested) PhotoStatus.Text = "Couldn't find what it looks like: " + exception.Message;
        }
    }

    /// <summary>Lists the pills of these clinical drugs at this strength, from every maker, to pick from.</summary>
    private async Task FindLookAlikes(IReadOnlyList<string> cuis, string strength, bool open)
    {
        var cancel = Restart(ref lookAlikeSearch);
        LookAlikeList.ItemsSource = null;
        LookAlikesExpander.Show(true);
        LookAlikesExpander.IsExpanded = open;
        LookAlikesHeader.Text = "The same drug and strength from other makers";
        LookAlikesStatus.Text = "Reading the labels of the same drug…";
        try
        {
            var progress = new Progress<string>(text =>
            {
                if (!cancel.IsCancellationRequested) LookAlikesStatus.Text = text;
            });
            var found = await App.Current.DailyMed.LookAlikesAsync(cuis, strength, progress, cancel: cancel);
            if (cancel.IsCancellationRequested) return;
            LookAlikeList.ItemsSource = found.Select(f => new LookAlikeItem(f)).ToList();
            LookAlikesHeader.Text = $"The same drug and strength from other makers ({found.Count})";
            LookAlikesStatus.Text = found.Count == 0
                ? "None of their labels describes the pill."
                : "As U.S. labels describe them. Each maker's pill looks different: pick the one that looks like yours, to use its picture.";
            LookAlikesExpander.Show(found.Count > 0 || open);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (!cancel.IsCancellationRequested) LookAlikesStatus.Text = "Couldn't read the other labels: " + exception.Message;
        }
    }

    /// <summary>Uses another maker's pill: its description, and its photo if its label has one.</summary>
    private async void LookAlike_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LookAlikeList.SelectedItem is not LookAlikeItem item) return;
        var cancel = Restart(ref photoSearch);
        ShowAppearance(item.Product.Appearance);
        foundPhoto = null;
        PhotoList.SelectedItem = null;
        var source = "a U.S. maker's pill of the same drug and strength, " + item.LookAlike.Label.Title;
        PhotoStatus.Text = "Using the look of " + source + ". Check that it matches your pill.";
        UpdatePreview();
        if (item.Product.Image.Length == 0) return;
        try
        {
            var photo = DailyMed.PhotoOf(item.Product, item.LookAlike.Label, source);
            var bytes = await App.Current.DailyMed.DownloadAsync(photo.Url, cancel);
            if (cancel.IsCancellationRequested || Photos.FromBytes(bytes) is not { } picture) return;
            foundPhoto = new PhotoItem(photo.Name, bytes, picture, photo.Source);
            UpdatePreview();
            PhotoStatus.Text = "Using the photo of " + source + ". Check that it matches your pill.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            if (!cancel.IsCancellationRequested) PhotoStatus.Text = "Couldn't download its photo: " + exception.Message;
        }
    }

    private void Monograph_Click(object sender, RoutedEventArgs e)
    {
        if (monographUrl.StartsWith("https://", StringComparison.Ordinal)) Process.Start(new ProcessStartInfo(monographUrl) { UseShellExecute = true });
    }

    private void AddTime_Click(object sender, RoutedEventArgs e)
    {
        var last = times.LastOrDefault();
        var time = last?.Time ?? new TimeSpan(8, 0, 0);
        var kind = Kind;
        times.Add(new TimeSlot(TimeSpan.FromHours((time.TotalHours + (kind == ReminderKind.Drink ? 2 : 12)) % 24), last?.Amount ?? 1, kind.HasAmount(), last?.Step ?? 1));
    }

    private void RemoveTime_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TimeSlot slot) times.Remove(slot);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var kind = Kind;
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0)
        {
            SaveError.Text = "Give it a name.";
            NameBox.Focus();
            return;
        }
        var days = dayButtons.Aggregate(DaySet.None, (set, b) => set.With(b.Key, b.Value.IsChecked == true));
        var active = ActiveBox.IsChecked == true;
        var chosenTimes = times.Where(t => t.Time is not null)
            .Select(t => new ReminderTime(TimeOnly.FromTimeSpan(t.Time!.Value),
                !kind.HasAmount() ? 1 : kind == ReminderKind.Drink ? Math.Round(Quantities.ToMl(t.Amount ?? 1, App.Current.DrinkUnit)) : t.Amount ?? 1))
            .ToList();
        var sites = InjectionSites.All.Where(s => siteChecks[s].IsChecked == true).ToList();
        var din = (DinBox.Text ?? "").Trim();
        var problem =
            active && chosenTimes.Count == 0 ? "Add a time to be reminded at, or untick \"Remind me\"."
            : active && days == DaySet.None ? "Pick at least one day."
            : kind.HasAmount() && chosenTimes.Any(t => t.Amount <= 0) ? "Each time needs an amount above zero."
            : chosenTimes.GroupBy(t => t.Time).Any(g => g.Count() > 1) ? "The same time is there twice."
            : kind == ReminderKind.Injection && sites.Count == 0 ? "Tick at least one injection site."
            : kind.IsMedication() && din.Length > 0 && !HealthCanada.IsDin(din) ? "A DIN is 8 digits."
            : null;
        if (problem is not null)
        {
            SaveError.Text = problem;
            return;
        }

        var database = App.Current.Database;
        var oldPhoto = reminder.Photo;
        try
        {
            var photo = ChosenPhoto;
            if (photo?.Bytes is not null)
            {
                var file = string.Concat(photo.Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '_' ? c : '_'));
                File.WriteAllBytes(Path.Combine(database.ImageDirectory, file), photo.Bytes);
                reminder.Photo = file;
            }
            else
            {
                reminder.Photo = photo?.Name ?? "";
            }
            reminder.PhotoSource = photo?.Source ?? "";

            reminder.Kind = kind;
            reminder.Name = name;
            reminder.Unit = kind == ReminderKind.Drink ? "mL" : kind.HasAmount() ? CurrentUnit : "";
            reminder.Notes = NotesBox.Text ?? "";
            reminder.Times = chosenTimes;
            reminder.Days = days;
            reminder.Active = active;
            reminder.RecordVideo = VideoBox.IsChecked == true;
            reminder.Appearance = ReadAppearance();
            reminder.Sites = kind == ReminderKind.Injection ? sites : [];
            reminder.ValueUnit = kind.IsCheck() ? ValueUnitBox.SelectedItem as string ?? "" : "";
            if (kind.IsMedication())
            {
                (reminder.Form, reminder.SetId, reminder.Ndc, reminder.RxCui, reminder.LabelTitle) = (form, setId, ndc, rxCui, labelTitle);
                (reminder.Din, reminder.RxNumber) = (din.Length > 0 ? HealthCanada.Din(din) : "", (RxBox.Text ?? "").Trim());
                if (kind.HasAmount()) ReadSupply();
            }
            else
            {
                (reminder.Form, reminder.SetId, reminder.Ndc, reminder.RxCui, reminder.LabelTitle) = ("", "", "", "", "");
                (reminder.Din, reminder.RxNumber) = ("", "");
                (reminder.StockCount, reminder.StockAt) = (null, null);
            }
            if (isNew) reminder.Created = DateTime.Now;
            database.Save(reminder);

            if (oldPhoto.Length > 0 && oldPhoto != reminder.Photo && !database.Reminders().Any(m => m.Photo == oldPhoto))
                File.Delete(Path.Combine(database.ImageDirectory, oldPhoto));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            SaveError.Text = "Couldn't save: " + exception.Message;
            return;
        }
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!confirmDelete)
        {
            confirmDelete = true;
            DeleteButton.Content = "Click again to delete";
            return;
        }
        App.Current.Database.Delete(reminder);
        DialogResult = true;
    }
}
