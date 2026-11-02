using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// What can be printed, in black on white: a kitchen sheet to take round the fridge and cupboards and write on (what's
/// to check, or everything, from the places picked, by place and group, with room for how much there is, its
/// best-before and its name as the package has it), the grocery list to take to the store, and the stats and smoking,
/// with their charts and the days in a table. Each opens in a <see cref="PrintWindow"/>, to pick what goes on it and see the pages first.
/// </summary>
internal static class Printing
{
    /// <summary>Half an inch round the page; a little more at the foot, for its number.</summary>
    public const double Margin = 48;

    public static readonly Brush Gray = KindLook.Brush(Color.FromRgb(0x50, 0x56, 0x5C)), Rule = KindLook.Brush(Color.FromRgb(0xB8, 0xBD, 0xC2)),
        Shade = KindLook.Brush(Color.FromRgb(0xF0, 0xF1, 0xF3));

    private static readonly FontFamily Symbols = new("Segoe UI Symbol");

    // ---- The kitchen --------------------------------------------------------------------------------------------

    /// <summary>A kitchen sheet: what's to check, with <paramref name="toCheck"/>, or everything.</summary>
    public static void Kitchen(Window? owner, bool toCheck)
    {
        var now = DateTime.Now;
        var items = PantryLog.Items();
        var places = PantryLog.Places();
        var counts = KitchenSheet.Counts(items, places, now);
        var off = PlacesOff();
        PrintWindow? window = null;

        var options = new StackPanel();
        options.Children.Add(Label("What's on it", first: true));
        var checkBox = new RadioButton { Content = $"What needs checking ({counts.Values.Sum(c => c.ToCheck)})", IsChecked = toCheck };
        var allBox = new RadioButton { Content = $"Everything ({items.Count})", IsChecked = !toCheck, Margin = new Thickness(0, 4, 0, 0) };
        options.Children.Add(checkBox);
        options.Children.Add(allBox);
        options.Children.Add(Caption("Everything has empty lines under each place, to write in what's there that isn't on it."));
        options.Children.Add(Label("From"));
        var placeBoxes = places.Select(place => new CheckBox { IsChecked = !off.Contains(place.Id), Tag = place.Id, Margin = new Thickness(0, 0, 0, 4) }).ToList();
        void ShowCounts()
        {
            for (var i = 0; i < places.Count; i++)
            {
                var (all, check) = counts[places[i].Id];
                placeBoxes[i].Content = $"{places[i].Name}  ·  " + (toCheck ? $"{check} to check" : $"{all} {Things(all)}");
            }
        }
        ShowCounts();
        foreach (var box in placeBoxes) options.Children.Add(box);

        void Changed()
        {
            toCheck = checkBox.IsChecked == true;
            off = placeBoxes.Where(b => b.IsChecked != true).Select(b => (long)b.Tag).ToHashSet();
            SetPlacesOff(off);
            ShowCounts();
            window?.Rebuild();
        }
        checkBox.Checked += (_, _) => Changed();
        allBox.Checked += (_, _) => Changed();
        foreach (var box in placeBoxes) box.Click += (_, _) => Changed();

        window = new PrintWindow("Kitchen list", options, page =>
        {
            var picked = places.Select(p => p.Id).Where(id => !off.Contains(id)).ToHashSet();
            return KitchenDocument(page, KitchenSheet.Build(items, places, picked, toCheck, PantryLog.Sort, now), toCheck, picked.Count, now);
        });
        Open(window, owner);
    }

    /// <summary>The places left off the kitchen sheet the last time: none until some are.</summary>
    private static HashSet<long> PlacesOff() =>
        (App.Current.Database.Setting("print_places_off") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : -1).ToHashSet();

    private static void SetPlacesOff(IEnumerable<long> ids) =>
        App.Current.Database.SetSetting("print_places_off", string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))));

    private static FlowDocument KitchenDocument(Size page, IReadOnlyList<SheetPlace> sheet, bool toCheck, int placesPicked, DateTime now)
    {
        var document = Document(page);
        var count = sheet.Sum(p => p.Count);
        document.Blocks.Add(Title(toCheck ? "Kitchen check" : "What's in the kitchen"));
        document.Blocks.Add(Text($"{now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)} · {count} {Things(count)}" + (toCheck ? " to check" : "")
            + (sheet.Count > 0 ? " in " + And(sheet.Select(p => p.Place.Name)) : ""), 12, Gray));
        document.Blocks.Add(Text("Tick each one as you find it, and write how much there is, its best-before, and its name as the package has it when "
            + "that's different. " + (toCheck ? "Cross out what isn't there any more. " : "What's there that isn't on the list goes on the empty lines under its place. ")
            + "Then put it in on the Groceries page: how much in its box, or open it for the rest.", 12, Gray, new Thickness(0, 6, 0, 4)));
        if (sheet.Count == 0)
            document.Blocks.Add(Text(placesPicked == 0 ? "No places are picked." : "Nothing to check in the places picked.", 14, null, new Thickness(0, 16, 0, 0)));
        var system = App.Current.Measures;
        // What it is, how much, best before and its name on the package.
        (string, double)[] columns = [("What the program has", 2.4), ("How much", 1), ("Best before", 1), ("Name on the package", 1.5)];
        foreach (var place in sheet)
        {
            var heading = new Paragraph { Margin = new Thickness(0, 18, 0, 0), KeepWithNext = true };
            heading.Inlines.Add(new Run(place.Place.Name) { FontSize = 17, FontWeight = FontWeights.SemiBold });
            heading.Inlines.Add(new Run($"   {place.Count} {Things(place.Count)}" + (toCheck ? " to check" : "")) { FontSize = 12, Foreground = Gray });
            document.Blocks.Add(heading);
            var groups = place.Groups.Select(g => ((string?)Foods.Name(g.Group), (IReadOnlyList<TableRow>)g.Lines.Select(line => ItemRow(line, system)).ToList())).ToList();
            // With everything, empty rows after, for what isn't on it.
            if (!toCheck) groups.Add((place.Count > 0 ? "Also here" : "Here", Enumerable.Range(0, 4).Select(_ => WriteRow(null, [null, null, null])).ToList()));
            foreach (var block in Grouped(groups, columns, page.Width - 2 * Margin)) document.Blocks.Add(block);
        }
        return document;
    }

    /// <summary>Rows in groups, each a box to tick and then the <paramref name="columns"/>, which share what's left of the
    /// width as their shares say. Each group's name is kept on the page with its rows; the columns' names are at the top
    /// of the first.</summary>
    private static IEnumerable<Block> Grouped(IEnumerable<(string? Name, IReadOnlyList<TableRow> Rows)> groups, IReadOnlyList<(string Title, double Share)> columns,
        double width)
    {
        var first = true;
        Table Rows(IEnumerable<TableRow> rows)
        {
            var table = new Table { CellSpacing = 0, Margin = new Thickness(0, first ? 4 : 0, 0, 0) };
            // Widths worked out from the page, rather than shares of it, which a table gives a fixed column's share too.
            var each = (width - 26) / columns.Sum(c => c.Share);
            table.Columns.Add(new TableColumn { Width = new GridLength(26) });
            foreach (var column in columns) table.Columns.Add(new TableColumn { Width = new GridLength(each * column.Share) });
            var group = new TableRowGroup();
            if (first)
            {
                var header = new TableRow();
                foreach (var text in columns.Select(c => c.Title).Prepend(""))
                    header.Cells.Add(new TableCell(Text(text, 11, Gray, weight: FontWeights.SemiBold))
                    {
                        Padding = new Thickness(6, 4, 6, 4), BorderBrush = Gray, BorderThickness = new Thickness(0, 0, 0, 1),
                    });
                group.Rows.Add(header);
            }
            first = false;
            foreach (var row in rows) group.Rows.Add(row);
            table.RowGroups.Add(group);
            return table;
        }

        foreach (var (name, rows) in groups)
        {
            // The first group's name goes under the columns' names, in its table; the rest before theirs, kept with it.
            if (name is null) yield return Rows(rows);
            else if (first)
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(Text(name, 11, Gray, weight: FontWeights.SemiBold)) { ColumnSpan = columns.Count + 1, Padding = new Thickness(6, 10, 6, 3) });
                yield return Rows(rows.Prepend(row));
            }
            else
            {
                yield return new Paragraph(new Run(name)) { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Gray, Margin = new Thickness(6, 10, 0, 3), KeepWithNext = true };
                yield return Rows(rows);
            }
        }
    }

    private static TableRow ItemRow(SheetLine line, MeasureSystem system)
    {
        var item = line.Item;
        var what = new Paragraph { Margin = new Thickness(0) };
        what.Inlines.Add(new Run(item.Name) { FontWeight = FontWeights.SemiBold });
        if (item.Note is { Length: > 0 } note) what.Inlines.Add(new Run("  " + note) { Foreground = Gray });
        what.Inlines.Add(new LineBreak());
        what.Inlines.Add(new Run(string.Join(" · ", new[] { Pantry.Describe(item, system), Dates(item) }.Where(s => s.Length > 0))) { FontSize = 11, Foreground = Gray });
        if (line.Why.Length > 0)
        {
            what.Inlines.Add(new LineBreak());
            what.Inlines.Add(new Run("To check: " + line.Why) { FontSize = 11, FontStyle = FontStyles.Italic });
        }
        return WriteRow(what, [null, null, null]);
    }

    /// <summary>A row to write on: a box, what it is (or nothing, to write it in), and the rest, with lines between, each
    /// what's in it or nothing (null), to write in.</summary>
    private static TableRow WriteRow(Paragraph? what, IEnumerable<Paragraph?> rest)
    {
        var row = new TableRow();
        TableCell Cell(Block block, bool ruled) => new(block)
        {
            Padding = new Thickness(6, 7, 6, 7), BorderBrush = Rule, BorderThickness = new Thickness(ruled ? 1 : 0, 0, 0, 1),
        };
        row.Cells.Add(Cell(new Paragraph(new Run("☐") { FontFamily = Symbols, FontSize = 16 }) { Margin = new Thickness(0), LineHeight = 18 }, false));
        row.Cells.Add(Cell(what ?? Blank(), false));
        foreach (var cell in rest) row.Cells.Add(Cell(cell ?? Blank(), true));
        return row;
    }

    /// <summary>Room to write a line by hand.</summary>
    private static Paragraph Blank() => new(new Run(" ")) { Margin = new Thickness(0), LineHeight = 22, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };

    /// <summary>"frozen Oct 3 · good until about Jan 3, 2027", "best before Nov 9, 2026" (set by hand or read off it), or
    /// "no date".</summary>
    private static string Dates(PantryItem item) => string.Join(" · ", new[]
    {
        item.Frozen is { } frozen ? $"frozen {frozen.ToString("MMM d", CultureInfo.CurrentCulture)}" : "",
        item.UseBy is not { } by ? "no date"
            : item.UseBySet ? $"best before {by.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}"
            : $"good until about {by.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}",
    }.Where(s => s.Length > 0));

    // ---- The grocery list ----------------------------------------------------------------------------------------

    private static bool shopBySection = true, shopLastPrices = true;

    /// <summary>The grocery list to take to the store: by section, or as one list.</summary>
    public static void Groceries(Window? owner)
    {
        PrintWindow? window = null;
        var options = new StackPanel();
        options.Children.Add(Label("On it", first: true));
        foreach (var (name, get, set) in new (string, Func<bool>, Action<bool>)[]
        {
            ("By section, as a store has them", () => shopBySection, v => shopBySection = v),
            ("What each cost the last time", () => shopLastPrices, v => shopLastPrices = v),
        })
        {
            var box = new CheckBox { Content = name, IsChecked = get(), Margin = new Thickness(0, 0, 0, 4) };
            box.Click += (_, _) =>
            {
                set(box.IsChecked == true);
                window?.Rebuild();
            };
            options.Children.Add(box);
        }
        options.Children.Add(Caption("Empty lines at the end are for what you think of at the store.", new Thickness(0, 8, 0, 0)));
        window = new PrintWindow("Grocery list", options, page => GroceryDocument(page, DateTime.Now));
        Open(window, owner);
    }

    private static FlowDocument GroceryDocument(Size page, DateTime now)
    {
        var database = App.Current.Database;
        var system = App.Current.Measures;
        var sections = ShoppingSheet.Build(database.Groceries(), PantryLog.History(), shopBySection);
        var count = sections.Sum(s => s.Items.Count);
        var document = Document(page);
        document.Blocks.Add(Title("Grocery list"));
        document.Blocks.Add(Text($"{now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)} · {count} {Things(count)} to buy", 12, Gray));
        document.Blocks.Add(Text("Tick each one as it goes in the cart, and write what it cost. Back home, put the prices in on the Groceries page "
            + "as you mark each one bought: they're kept, to compare next time.", 12, Gray, new Thickness(0, 6, 0, 4)));
        if (count == 0) document.Blocks.Add(Text("Nothing is on the list to buy.", 14, null, new Thickness(0, 16, 0, 0)));

        TableRow Row(GroceryItem item)
        {
            var what = new Paragraph { Margin = new Thickness(0) };
            what.Inlines.Add(new Run(item.Name) { FontWeight = FontWeights.SemiBold });
            if (item.Note is { Length: > 0 } note) what.Inlines.Add(new Run("  " + note) { Foreground = Gray });
            if (shopLastPrices && database.LastBought(item.Name) is { Price: { } price } last)
            {
                what.Inlines.Add(new LineBreak());
                what.Inlines.Add(new Run($"last time {price.ToString("C", CultureInfo.CurrentCulture)}"
                    + (last.Show(system) is { Length: > 0 } amount ? $" for {amount}" : "")
                    + (last.UnitPrice(system) is { Length: > 0 } each ? $" ({each})" : "")
                    + (last.Bought is { } on ? $", {on.ToString("MMM d", CultureInfo.CurrentCulture)}" : "")) { FontSize = 11, Foreground = Gray });
            }
            return WriteRow(what, [item.Show(system) is { Length: > 0 } shown ? Text(shown, 13) : null, null]);
        }
        var groups = sections.Select(s => (s.Group is { } g ? Foods.Name(g) : null, (IReadOnlyList<TableRow>)s.Items.Select(Row).ToList())).ToList();
        groups.Add(("Also", Enumerable.Range(0, 5).Select(_ => WriteRow(null, [null, null])).ToList()));
        foreach (var block in Grouped(groups, [("What", 2.6), ("How much", 1), ("Paid", 0.9)], page.Width - 2 * Margin)) document.Blocks.Add(block);
        return document;
    }

    // ---- Stats and smoking ---------------------------------------------------------------------------------------

    /// <summary>The parts of the stats that can be left off.</summary>
    private enum StatsPart { Reminders, Smoking, Drinks, Calories, Days }

    private static readonly HashSet<StatsPart> statsOff = [];

    /// <summary>The stats of the last <paramref name="days"/> days, with the parts picked.</summary>
    public static void Stats(Window? owner, int days)
    {
        PrintWindow? window = null;
        var options = new StackPanel();
        options.Children.Add(Label("Days", first: true));
        options.Children.Add(DaysBox(days, d =>
        {
            days = d;
            window?.Rebuild();
        }));
        options.Children.Add(Label("On it"));
        foreach (var (part, name) in new[] { (StatsPart.Reminders, "Reminders"), (StatsPart.Smoking, "Cigarettes"), (StatsPart.Drinks, "Drinks"),
            (StatsPart.Calories, "Calories"), (StatsPart.Days, "Each day in a table") })
        {
            var box = new CheckBox { Content = name, IsChecked = !statsOff.Contains(part), Margin = new Thickness(0, 0, 0, 4) };
            box.Click += (_, _) =>
            {
                if (box.IsChecked == true) statsOff.Remove(part);
                else statsOff.Add(part);
                window?.Rebuild();
            };
            options.Children.Add(box);
        }
        window = new PrintWindow("Stats", options, page => StatsDocument(page, StatsReport.For(days, DateTime.Now)));
        Open(window, owner);
    }

    private static FlowDocument StatsDocument(Size page, StatsReport report)
    {
        var document = Document(page);
        bool On(StatsPart part) => !statsOff.Contains(part);
        document.Blocks.Add(Title("Stats"));
        document.Blocks.Add(Text(Range(report.Days), 12, Gray));
        if (report.Counting.Length > 0) document.Blocks.Add(Text(report.Counting, 12, Gray));

        document.Blocks.Add(Heading("What stands out"));
        foreach (var note in report.Notes)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            paragraph.Inlines.Add(new Run(new NoteRow(note).Label + ": ") { FontWeight = FontWeights.SemiBold });
            paragraph.Inlines.Add(new Run(note.Text));
            document.Blocks.Add(paragraph);
        }
        document.Blocks.Add(Tiles([report.Done, report.Missed, report.Smokes, report.Gap, report.Drinks, report.Kcal]));

        if (On(StatsPart.Reminders)) Chart(document, report.DoneChart);
        if (On(StatsPart.Smoking))
        {
            Chart(document, report.SmokeChart);
            Chart(document, report.GapChart);
        }
        if (On(StatsPart.Drinks)) Chart(document, report.DrinkChart);
        if (On(StatsPart.Calories)) Chart(document, report.KcalChart);

        if (On(StatsPart.Days))
        {
            var columns = new List<(string Title, Func<DayTally, string> Value)> { ("Day", DayName) };
            if (On(StatsPart.Reminders))
                columns.AddRange([("Done", d => Number(d.Done)), ("Skipped", d => Number(d.Skipped)), ("Missed", d => Number(d.Missed.Count))]);
            if (On(StatsPart.Smoking))
                columns.AddRange([("Cigarettes", d => Number(d.Smokes)), ("One every", d => d.Between is { } b ? DailyStats.Span(b) : "–")]);
            if (On(StatsPart.Drinks)) columns.Add(("Drinks", d => d.DrinksPlanned > 0 ? $"{d.Drinks} of {d.DrinksPlanned}" : Number(d.Drinks)));
            if (On(StatsPart.Calories)) columns.Add(("kcal", d => d.Kcal > 0 ? d.Kcal.ToString("N0", CultureInfo.CurrentCulture) : "–"));
            DayTable(document, report.Days, columns);
        }
        // What was missed, by name, for whoever reads it.
        if (On(StatsPart.Reminders) && report.Days.Where(d => d.Missed.Count > 0).ToList() is { Count: > 0 } missedDays)
        {
            document.Blocks.Add(Heading("What was missed"));
            foreach (var day in missedDays)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                paragraph.Inlines.Add(new Run(DayName(day) + ": ") { FontWeight = FontWeights.SemiBold });
                paragraph.Inlines.Add(new Run(string.Join(", ", day.Missed.Select(m => $"{m.Reminder.Name} at {Format.Time(m.Due)}"))));
                document.Blocks.Add(paragraph);
            }
        }
        return document;
    }

    /// <summary>Smoking over the last <paramref name="days"/> days: the numbers, the charts and each day.</summary>
    public static void Smoking(Window? owner, int days)
    {
        PrintWindow? window = null;
        var options = new StackPanel();
        options.Children.Add(Label("Days", first: true));
        options.Children.Add(DaysBox(days, d =>
        {
            days = d;
            window?.Rebuild();
        }));
        window = new PrintWindow("Smoking", options, page => SmokingDocument(page, days));
        Open(window, owner);
    }

    private static FlowDocument SmokingDocument(Size page, int days)
    {
        var now = DateTime.Now;
        var report = StatsReport.For(days, now);
        var tracking = App.Current.Schedule.Tracking;
        var smokes = App.Current.Database.Smokes(tracking.Counted(now.Date.AddDays(-Math.Max(28, days))));
        var stats = HealthReminder.Smoking.Stats(smokes, now);
        var target = SmokeLog.TargetToday();
        var brand = SmokeLog.Brand;

        var document = Document(page);
        document.Blocks.Add(Title("Smoking"));
        document.Blocks.Add(Text(Range(report.Days), 12, Gray));
        if (brand.Name.Length > 0 || brand.PackPrice is not null)
            document.Blocks.Add(Text(string.Join(", ", new[]
            {
                brand.Name,
                $"{brand.PerPack} a pack",
                brand.PackPrice is { } p ? $"{p.ToString("C", CultureInfo.CurrentCulture)} a pack" : "",
            }.Where(s => s.Length > 0)), 12, Gray));

        var tiles = new List<StatsTile>
        {
            new(stats.Today.ToString(CultureInfo.CurrentCulture), target is { } t ? $"today, of a goal of {t}" : "today"),
            new(stats.PerDay.ToString("0.#", CultureInfo.CurrentCulture), "a day, last 7 days"),
            new(stats.SinceLast is { } since ? DailyStats.Span(since) : "–", "since the last one"),
        };
        if (brand.PackPrice is { } price && brand.PerPack > 0)
            tiles.Add(new(((decimal)(stats.PerDay * 7) * price / brand.PerPack).ToString("C0", CultureInfo.CurrentCulture), "a week, at this rate"));
        document.Blocks.Add(Tiles(tiles));

        if (SmokingPage.GoalSummary() is { } goal) Said(document, "Cutting down", goal);
        Said(document, "Time per cigarette", SmokingPage.DurationSummary(stats));
        Said(document, "The first of the day", SmokingPage.FirstSummary(stats));

        Chart(document, report.SmokeChart);
        Chart(document, report.GapChart);

        string Of(DayTally d, bool last)
        {
            var lit = smokes.Where(s => DateOnly.FromDateTime(s.Started) == d.Day).Select(s => s.Started).ToList();
            return lit.Count == 0 ? "–" : Format.Time(last ? lit.Max() : lit.Min());
        }
        DayTable(document, report.Days,
        [
            ("Day", DayName),
            ("Cigarettes", d => Number(d.Smokes)),
            ("One every", d => d.Between is { } b ? DailyStats.Span(b) : "–"),
            ("First", d => Of(d, false)),
            ("Last", d => Of(d, true)),
        ]);
        return document;
    }

    /// <summary>A heading and what's said under it.</summary>
    private static void Said(FlowDocument document, string heading, string text)
    {
        document.Blocks.Add(Heading(heading, 14));
        document.Blocks.Add(Text(text, 13));
    }

    /// <summary>"Last 14 days: Oct 20 to Nov 2, 2026".</summary>
    private static string Range(IReadOnlyList<DayTally> days) =>
        $"{(days.Count == 1 ? "Today" : $"The last {days.Count} days")}: "
        + (days.Count == 1 ? "" : days[0].Day.ToString("MMM d", CultureInfo.CurrentCulture) + " to ")
        + days[^1].Day.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

    private static string DayName(DayTally d) =>
        d.Day == DateOnly.FromDateTime(DateTime.Today) ? "Today" : d.Day.ToString("ddd MMM d", CultureInfo.CurrentCulture);

    private static string Number(int n) => n.ToString(CultureInfo.CurrentCulture);

    /// <summary>The numbers on their own, three to a row.</summary>
    private static Table Tiles(IReadOnlyList<StatsTile> tiles)
    {
        var table = new Table { CellSpacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        for (var i = 0; i < 3; i++) table.Columns.Add(new TableColumn());
        var rows = new TableRowGroup();
        table.RowGroups.Add(rows);
        for (var i = 0; i < tiles.Count; i += 3)
        {
            var row = new TableRow();
            foreach (var tile in tiles.Skip(i).Take(3))
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };
                paragraph.Inlines.Add(new Run(tile.Value) { FontSize = 20, FontWeight = FontWeights.SemiBold });
                paragraph.Inlines.Add(new LineBreak());
                paragraph.Inlines.Add(new Run(tile.Label) { FontSize = 11, Foreground = Gray });
                row.Cells.Add(new TableCell(paragraph) { Background = Shade, Padding = new Thickness(12, 8, 12, 8) });
            }
            rows.Rows.Add(row);
        }
        return table;
    }

    /// <summary>A chart, under its title, what it says and what its colours are. Its bars are drawn once they're on the
    /// page, so they take the paper's colours.</summary>
    private static void Chart(FlowDocument document, StatsChart chart)
    {
        var title = new Paragraph { Margin = new Thickness(0, 18, 0, 0), KeepWithNext = true };
        title.Inlines.Add(new Run(chart.Title) { FontSize = 15, FontWeight = FontWeights.SemiBold });
        foreach (var (name, brush) in chart.Legend ?? [])
        {
            title.Inlines.Add(new Run("     ■ ") { FontFamily = Symbols, FontSize = 12, Foreground = brush });
            title.Inlines.Add(new Run(name) { FontSize = 11, Foreground = Gray });
        }
        document.Blocks.Add(title);
        var summary = Text(chart.Summary, 11, Gray, new Thickness(0, 2, 0, 8));
        summary.KeepWithNext = true;
        document.Blocks.Add(summary);
        var bars = new DayBars();
        document.Blocks.Add(new BlockUIContainer(bars));
        bars.Show(chart.Bars, chart.Line, chart.Format);
    }

    /// <summary>The days in a table, today last.</summary>
    private static void DayTable(FlowDocument document, IReadOnlyList<DayTally> days, IReadOnlyList<(string Title, Func<DayTally, string> Value)> columns)
    {
        document.Blocks.Add(Heading("Each day"));
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 0) };
        for (var i = 0; i < columns.Count; i++)
            table.Columns.Add(new TableColumn { Width = new GridLength(i == 0 ? 1.4 : 1, GridUnitType.Star) });
        var rows = new TableRowGroup();
        table.RowGroups.Add(rows);
        var header = new TableRow();
        foreach (var column in columns)
            header.Cells.Add(new TableCell(Text(column.Title, 11, Gray, weight: FontWeights.SemiBold))
            {
                Padding = new Thickness(6, 4, 6, 4), BorderBrush = Gray, BorderThickness = new Thickness(0, 0, 0, 1),
            });
        rows.Rows.Add(header);
        foreach (var day in days)
        {
            var row = new TableRow();
            foreach (var column in columns)
                row.Cells.Add(new TableCell(Text(column.Value(day), 12))
                {
                    Padding = new Thickness(6, 3, 6, 3), BorderBrush = Rule, BorderThickness = new Thickness(0, 0, 0, 0.5),
                });
            rows.Rows.Add(row);
        }
        document.Blocks.Add(table);
    }

    // ---- The paper -----------------------------------------------------------------------------------------------

    /// <summary>A page of the size given, in one column, black on white; the theme's brushes the charts use, as on paper.</summary>
    private static FlowDocument Document(Size page)
    {
        var document = new FlowDocument
        {
            PageWidth = page.Width,
            PageHeight = page.Height,
            PagePadding = new Thickness(Margin, Margin, Margin, Margin + 12),
            ColumnWidth = page.Width,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            TextAlignment = TextAlignment.Left,
        };
        document.Resources["TextFillColorPrimaryBrush"] = Brushes.Black;
        document.Resources["TextFillColorSecondaryBrush"] = Gray;
        document.Resources["DividerStrokeColorDefaultBrush"] = Rule;
        document.Resources["SubtleFillColorSecondaryBrush"] = Shade;
        return document;
    }

    private static Paragraph Title(string text) => new(new Run(text)) { FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };

    private static Paragraph Heading(string text, double size = 15) =>
        new(new Run(text)) { FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 4), KeepWithNext = true };

    private static Paragraph Text(string text, double size, Brush? brush = null, Thickness? margin = null, FontWeight? weight = null)
    {
        var paragraph = new Paragraph(new Run(text)) { FontSize = size, Margin = margin ?? new Thickness(0) };
        if (brush is not null) paragraph.Foreground = brush;
        if (weight is { } w) paragraph.FontWeight = w;
        return paragraph;
    }

    private static string Things(int count) => count == 1 ? "thing" : "things";

    /// <summary>"Fridge", "Fridge and Freezer", "Fridge, Freezer and Pantry".</summary>
    private static string And(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count <= 1 ? string.Concat(list) : string.Join(", ", list.SkipLast(1)) + " and " + list[^1];
    }

    // ---- The options ---------------------------------------------------------------------------------------------

    private static TextBlock Label(string text, bool first = false)
    {
        var label = new TextBlock { Text = text, Style = (Style)Application.Current.FindResource("Label") };
        if (first) label.Margin = new Thickness(0, 4, 0, 4);
        return label;
    }

    /// <summary>A note, under the option before it unless it's put elsewhere.</summary>
    private static TextBlock Caption(string text, Thickness? margin = null) =>
        new() { Text = text, Style = (Style)Application.Current.FindResource("Caption"), Margin = margin ?? new Thickness(28, 2, 0, 0) };

    private static ComboBox DaysBox(int days, Action<int> changed)
    {
        var box = new ComboBox
        {
            ItemsSource = StatsReport.Ranges.Select(d => $"Last {d} days").ToList(),
            SelectedIndex = Math.Max(0, Array.IndexOf(StatsReport.Ranges, days)),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 160,
        };
        box.SelectionChanged += (_, _) => changed(StatsReport.Ranges[Math.Max(0, box.SelectedIndex)]);
        return box;
    }

    private static void Open(PrintWindow window, Window? owner)
    {
        if (owner is { IsVisible: true }) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.ShowDialog();
    }
}
