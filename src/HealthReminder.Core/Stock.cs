using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>How much of a medication is on hand, and how much is taken a day on average.</summary>
public sealed record Supply(double OnHand, double DailyUse)
{
    /// <summary>Whole days it lasts at that rate; null when nothing is taken on a schedule.</summary>
    public int? DaysLeft => DailyUse > 0 ? (int)Math.Floor(Math.Max(0, OnHand) / DailyUse) : null;

    /// <summary>A week or less left: time to refill.</summary>
    public bool Low => DaysLeft is <= 7 || OnHand <= 0;
}

/// <summary>Something in the kitchen and how much: in the catalog's unit for what it knows ("g", "egg", "slice"), or the
/// one typed ("g", "mL", or "" for a count). <paramref name="Unsure"/> when the amount is a guess (read from a photo, or
/// more was used than was counted), so it's worth checking. <paramref name="Added"/> is when it was bought or put in
/// (or thawed), <paramref name="Frozen"/> when it went in the freezer (null when it isn't in it), and
/// <paramref name="UseBySet"/> whether <paramref name="UseBy"/> was set by hand or read from a photo rather than worked
/// out from those. <paramref name="Place"/> is the place it was put in (null for where it goes, by what it is), and
/// <paramref name="Icon"/>, <paramref name="Color"/> and <paramref name="Group"/> what was picked for it, null for what
/// its name says (<see cref="Foods.Guess"/>). <paramref name="Note"/> is a second name for which one it is (a brand:
/// "Schneiders"), and <paramref name="Packing"/> the packs it's in and what each piece weighs.</summary>
public sealed record PantryItem(string Name, double Amount, string Unit, DateTime? UseBy, DateTime Counted, bool Unsure,
    DateTime Added = default, DateTime? Frozen = null, bool UseBySet = false,
    long? Place = null, string? Icon = null, string? Color = null, FoodGroup? Group = null,
    string? Note = null, Packing? Packing = null)
{
    public bool InFreezer => Frozen is not null;

    /// <summary>When it was put in: <see cref="Added"/>, or for what was kept before that was known, when it was counted.</summary>
    public DateTime Since => Added == default ? Counted : Added;

    /// <summary>What its name says it is.</summary>
    public FoodLook Guess => Foods.Guess(Name);

    public string ShownIcon => Icon ?? Guess.Icon;

    /// <summary>Its picture's colour: the one picked, or the one its name says while its picture is the one its name
    /// says ("Pepsi" is blue); null for the picture's own.</summary>
    public string? ShownColor => Color ?? (Icon is null ? Guess.Color : null);

    public FoodGroup ShownGroup => Group ?? Guess.Group;
}

/// <summary>
/// How something comes: <paramref name="Packs"/> packs of it (each a <paramref name="Word"/>: "box", "pack", "bag"), and
/// how much one piece of it is, or a whole pack (<paramref name="OfPack"/>) of <paramref name="PerPack"/> pieces:
/// <paramref name="Size"/> g, or mL when <paramref name="Volume"/>. "2 pies in 1 box, 200 g each"; "8 hot dogs in 1
/// pack, a 500 g pack of 12", which makes a hot dog about 42 g.
/// </summary>
public sealed record Packing(double? Packs = null, string Word = "pack", double? Size = null, bool Volume = false, bool OfPack = false, double? PerPack = null)
{
    public Dimension Dimension => Volume ? Dimension.Volume : Dimension.Mass;

    /// <summary>What one piece is: its own size, or a pack's shared by what a full one holds; null when it can't be told.</summary>
    public double? Each => Size is not { } size ? null : !OfPack ? size : PerPack is > 0 and var count ? size / count : null;

    public bool IsEmpty => Packs is null && Size is null && PerPack is null;
}

/// <summary>The order things are in, within each group of a place.</summary>
public enum KitchenSort { Name, BestBefore, Newest, Oldest, Brand }

/// <summary>A place in the kitchen things are kept, named as its owner likes ("Deep freezer"), of a
/// <paramref name="Kind"/>: what's in one of kind Freezer is frozen. Shown in the order of <paramref name="Sort"/>.</summary>
public sealed record KitchenPlace(long Id, string Name, Kept Kind, int Sort = 0)
{
    /// <summary>The places a new kitchen has, in the order they're shown.</summary>
    public static readonly IReadOnlyList<KitchenPlace> Defaults =
    [
        new(0, "Freezer", Kept.Freezer, 0),
        new(0, "Fridge", Kept.Fridge, 1),
        new(0, "Pantry", Kept.Pantry, 2),
        new(0, "Cupboard", Kept.Cupboard, 3),
        new(0, "Counter", Kept.Counter, 4),
    ];
}

/// <summary>
/// How cold the fridge and freezer are, in °C: 4 °C (40 °F) and −18 °C (0 °F), the North American standard, unless set.
/// Food keeps less long in a warmer one: in a fridge, roughly half as long for every 5 °C (9 °F) over 4 °C, as bacteria
/// grow faster; in a freezer, roughly half as long for every 4 °C (7 °F) over −18 °C, as frozen food dries and goes off
/// in taste sooner. A colder one isn't counted as keeping food longer.
/// </summary>
public sealed record ColdStorage(double FridgeC = ColdStorage.StandardFridgeC, double FreezerC = ColdStorage.StandardFreezerC)
{
    public const double StandardFridgeC = (40 - 32) / 1.8;
    public const double StandardFreezerC = (0 - 32) / 1.8;

    public static readonly ColdStorage Standard = new();

    public static double ToF(double c) => c * 1.8 + 32;
    public static double FromF(double f) => (f - 32) / 1.8;

    /// <summary>How much of the usual time food keeps in the fridge: 1 at 4 °C or colder.</summary>
    public double FridgeFactor => FridgeC <= StandardFridgeC + 0.05 ? 1 : Math.Max(0.2, Math.Pow(0.5, (FridgeC - StandardFridgeC) / 5));

    /// <summary>How much of the usual time food keeps frozen: 1 at −18 °C or colder.</summary>
    public double FreezerFactor => FreezerC <= StandardFreezerC + 0.05 ? 1 : Math.Max(0.1, Math.Pow(0.5, (FreezerC - StandardFreezerC) / 4));

    /// <summary>What's wrong with the temperatures, if anything, with the standard ones in the unit given.</summary>
    public List<string> Warnings(bool fahrenheit)
    {
        string T(double c) => fahrenheit ? $"{Math.Round(ToF(c)):0} °F" : $"{Math.Round(c):0} °C";
        var warnings = new List<string>();
        if (FridgeFactor < 1)
            warnings.Add($"The fridge is warmer than {T(StandardFridgeC)}: bacteria grow faster above it, so food keeps less long, and its dates are shortened. Turn it colder.");
        else if (FridgeC <= 0)
            warnings.Add($"At {T(0)} or below, food in the fridge can freeze.");
        if (FreezerC > -12)
            warnings.Add($"The freezer is too warm to keep food frozen solid ({T(StandardFreezerC)} is the standard): turn it colder, or have it checked.");
        else if (FreezerFactor < 1)
            warnings.Add($"The freezer is warmer than {T(StandardFreezerC)}: frozen food loses its quality sooner, and its dates are shortened.");
        return warnings;
    }
}

/// <summary>
/// The kitchen's stock: what's bought or seen in a photo goes in, what the meals cooked use comes out, and what's in
/// doubt (a guess, used past zero, or not counted for two weeks) is set aside to be counted again.
/// </summary>
public static partial class Pantry
{
    /// <summary>After this long without a count, an amount is in doubt.</summary>
    public static readonly TimeSpan Recount = TimeSpan.FromDays(14);

    [GeneratedRegex(@"\b(loaf|loaves|pack|packs|package|packages|box|boxes|bag|bags|carton|cartons|tray|trays|tub|tubs|bunch|bunches|case|cases)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PackWords();

    /// <summary>What's tracked of something: what the catalog counts it in; staples (oil, spices) aren't counted.</summary>
    public static bool Tracked(string name) => GroceryCatalog.Get(name) is not { Staple: true };

    /// <summary>An amount as typed ("2 L", "1 loaf", "a dozen", "500 g", "2"), in the unit something is kept in: the
    /// catalog's for what it knows (a loaf of bread is 20 slices; a typed count of something sold in packs, packs), or the
    /// kind typed for the rest. Nothing typed, or nothing readable, is one pack.</summary>
    public static (double Amount, string Unit) Amount(string name, string quantity)
    {
        var (amount, unit, _) = Read(name, quantity);
        return (amount, unit);
    }

    [GeneratedRegex(@"^\s*(\d+([.,]\d+)?|\d+/\d+|[½¼¾]|an?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex BareNumber();

    /// <summary>The amount, its unit, and whether it was read rather than guessed: "a handful" of something weighed
    /// isn't a weight.</summary>
    private static (double Amount, string Unit, bool Read) Read(string name, string? quantity)
    {
        quantity ??= "";
        var parsed = Quantities.Parse(quantity);
        if (GroceryCatalog.Get(name) is { } info)
        {
            if (parsed is not { } q) return (info.Pack, info.Unit, false);
            var packs = PackWords().IsMatch(quantity);
            return info.Unit switch
            {
                "g" or "mL" when q.Dimension is Dimension.Mass or Dimension.Volume => (q.Amount, info.Unit, true),   // 1 mL taken as 1 g
                "g" or "mL" => (q.Amount * info.Pack, info.Unit, packs || BareNumber().IsMatch(quantity)),          // "2" or "2 bags": packs
                _ when q.Dimension == Dimension.Count => (packs ? q.Amount * info.Pack : q.Amount, info.Unit, true),
                _ => (info.Pack, info.Unit, false),                                                                   // "1 kg" of eggs: a pack
            };
        }
        return parsed switch
        {
            { Dimension: Dimension.Mass } q => (q.Amount, "g", true),
            { Dimension: Dimension.Volume } q => (q.Amount, "mL", true),
            { } q => (q.Amount, "", true),
            null => (1, "", false),
        };
    }

    /// <summary>"300 g", "8 eggs", "1.2 L", "2"; "none" at zero or less.</summary>
    public static string Show(PantryItem item, MeasureSystem system) =>
        item.Amount <= 0 ? "none"
        : item.Unit == "g" ? Quantities.Format(new Quantity(item.Amount, Dimension.Mass), system)
        : item.Unit == "mL" ? Quantities.Format(new Quantity(item.Amount, Dimension.Volume), system)
        : Amounts.Format(Math.Round(item.Amount * 4) / 4, item.Unit);

    /// <summary>"300 g", "2 in 1 box · 200 g each", "8 in 1 pack · 42 g each", "1 · a 500 g pack": how much there is, the
    /// packs it's in and what one piece is.</summary>
    public static string Describe(PantryItem item, MeasureSystem system)
    {
        var text = Show(item, system);
        if (item.Packing is not { } packing) return text;
        if (packing.Packs is { } packs && packs > 0 && item.Amount > 0) text += " in " + Amounts.Format(packs, packing.Word);
        if (packing.Each is { } each) text += " · " + Quantities.Format(new Quantity(each, packing.Dimension), system) + " each";
        else if (packing.Size is { } size) text += $" · a {Quantities.Format(new Quantity(size, packing.Dimension), system)} {packing.Word}";
        return text;
    }

    /// <summary>What it comes to, from what one piece is: "400 g in all" for 2 pies of 200 g, "about 6" for 250 g of
    /// 42 g hot dogs; "" when that can't be told, or is what's shown already.</summary>
    public static string InAll(PantryItem item, MeasureSystem system)
    {
        if (item.Packing is not { Each: { } each } packing || item.Amount <= 0 || each <= 0) return "";
        if (item.Unit is "g" or "mL")
            return (item.Unit == "mL") == packing.Volume ? $"about {Amounts.Format(Math.Round(item.Amount / each * 2) / 2, "")}" : "";
        return Quantities.Format(new Quantity(item.Amount * each, packing.Dimension), system) + " in all";
    }

    /// <summary>The packing as typed: how many packs ("1"), what one's called ("box"), what a piece or a pack weighs or
    /// holds ("200 g", "1 lb", "355 mL"), whether that's a pack's, and how many a full pack holds. Blank is none; the
    /// problem says what couldn't be read.</summary>
    public static (Packing? Packing, string Problem) ReadPacking(string packs, string word, string size, bool ofPack, string perPack)
    {
        double? Number(string text, out bool bad)
        {
            bad = false;
            if (text.Trim().Length == 0) return null;
            if (Quantities.Parse(text) is { Dimension: Dimension.Count, Amount: > 0 } q) return q.Amount;
            bad = true;
            return null;
        }
        var count = Number(packs, out var badPacks);
        var full = Number(perPack, out var badFull);
        Quantity? measure = size.Trim().Length == 0 ? null : Quantities.Parse(size);
        var problem = badPacks ? "How many packs is a number: 1, 2, ½."
            : measure is { Dimension: Dimension.Count } || size.Trim().Length > 0 && measure is null ? "A size is a weight or an amount: 200 g, 1 lb, 355 mL."
            : badFull && ofPack ? "What a full pack holds is a number: 12."
            : "";
        var packing = new Packing(count, word.Trim().Length > 0 ? word.Trim().ToLowerInvariant() : "pack", measure?.Amount,
            measure?.Dimension == Dimension.Volume, ofPack, ofPack ? full : null);
        return (packing.IsEmpty ? null : packing, problem);
    }

    /// <summary>In the order asked for: by name (and brand), soonest best-before first, newest or oldest in first, or by
    /// brand. What keeps comes last by date; what's frozen counts from when it went in.</summary>
    public static IEnumerable<PantryItem> Sorted(IEnumerable<PantryItem> items, KitchenSort sort)
    {
        var byName = StringComparer.OrdinalIgnoreCase;
        DateTime In(PantryItem p) => p.Frozen ?? p.Since;
        return sort switch
        {
            KitchenSort.BestBefore => items.OrderBy(p => p.UseBy ?? DateTime.MaxValue).ThenBy(p => p.Name, byName),
            KitchenSort.Newest => items.OrderByDescending(In).ThenBy(p => p.Name, byName),
            KitchenSort.Oldest => items.OrderBy(In).ThenBy(p => p.Name, byName),
            KitchenSort.Brand => items.OrderBy(p => p.Note is { Length: > 0 } ? 0 : 1).ThenBy(p => p.Note ?? "", byName).ThenBy(p => p.Name, byName),
            _ => items.OrderBy(p => p.Name, byName).ThenBy(p => p.Note ?? "", byName),
        };
    }

    /// <summary>Put in (bought, or thawed) on another day: its best-before is worked out from it, unless set by hand.</summary>
    public static PantryItem SetAdded(PantryItem item, DateTime day, ColdStorage? cold = null) =>
        Refresh(item with { Added = day.Date + item.Since.TimeOfDay }, cold);

    /// <summary>Into the freezer on another day: its best-before is worked out from it, unless set by hand.</summary>
    public static PantryItem SetFrozen(PantryItem item, DateTime day, ColdStorage? cold = null) =>
        item.Frozen is { } frozen ? Refresh(item with { Frozen = day.Date + frozen.TimeOfDay }, cold) : item;

    /// <summary>For something not in the catalog, how long it's taken to keep frozen.</summary>
    public const int FreezerMonthsUnknown = 3;

    /// <summary>
    /// The usual best-before: in the freezer, from when it went in, for as long as it's at its best frozen; otherwise
    /// from when it was bought, put in or thawed, for as long as it keeps where it's kept (a few days in the fridge, for
    /// what was bought frozen and has thawed). Shortened by a warm fridge or freezer. Null when it keeps for years, or
    /// isn't known; for what doesn't freeze well, as if it weren't frozen.
    /// </summary>
    public static DateTime? Usual(PantryItem item, ColdStorage? cold = null)
    {
        cold ??= ColdStorage.Standard;
        var info = GroceryCatalog.Get(item.Name);
        if (item.Frozen is { } frozen && (info is null || info.FreezerMonths > 0))
            return frozen.Date.AddDays(Math.Max(1, Math.Round((info?.FreezerMonths ?? FreezerMonthsUnknown) * 30.4 * cold.FreezerFactor)));
        var (days, fridge) = info switch
        {
            null => (0, false),
            { Kept: Kept.Freezer } => (3, true),
            _ => (info.KeepDays, info.Kept == Kept.Fridge),
        };
        return days > 0 ? item.Since.Date.AddDays(Math.Max(1, Math.Round(days * (fridge ? cold.FridgeFactor : 1)))) : null;
    }

    /// <summary>Its best-before worked out again (when the fridge or freezer changes), unless it was set by hand or read
    /// from a photo.</summary>
    public static PantryItem Refresh(PantryItem item, ColdStorage? cold = null) => item.UseBySet ? item : item with { UseBy = Usual(item, cold) };

    /// <summary>Whether something goes straight in the freezer when it's bought: what's sold frozen, or what a photo
    /// shows in one.</summary>
    private static DateTime? FrozenOn(string name, DateTime now, bool inFreezer) =>
        inFreezer || GroceryCatalog.Get(name) is { Kept: Kept.Freezer } ? now : null;

    /// <summary>Something bought (or seen to be new): added to what there is, or new if there was none. A count already
    /// below zero starts again from zero. Its best-before is the one said for it (<paramref name="useWithinDays"/>, from a
    /// photo), or the usual one from now; what was already frozen stays frozen.</summary>
    public static PantryItem Add(PantryItem? existing, string name, string quantity, DateTime now, int useWithinDays = 0, bool unsure = false,
        bool inFreezer = false, ColdStorage? cold = null)
    {
        var (amount, unit) = Amount(name, quantity);
        var item = existing is null || existing.Unit != unit
            ? new PantryItem(name, amount, unit, null, now, unsure, now, FrozenOn(name, now, inFreezer))
            : existing with { Amount = Math.Max(0, existing.Amount) + amount, Unsure = existing.Unsure || unsure, Added = now, Frozen = existing.Frozen ?? FrozenOn(name, now, inFreezer) };
        return useWithinDays > 0 ? item with { UseBy = now.Date.AddDays(useWithinDays), UseBySet = true } : Refresh(item with { UseBySet = false }, cold);
    }

    /// <summary>Counted: this much there is now. When it was put in, whether it's frozen and its best-before stay as they
    /// were, unless a photo says otherwise.</summary>
    public static PantryItem Set(string name, string quantity, DateTime now, int useWithinDays = 0, bool unsure = false, PantryItem? existing = null,
        bool inFreezer = false, ColdStorage? cold = null)
    {
        var (amount, unit) = Amount(name, quantity);
        var item = existing is null
            ? new PantryItem(name, amount, unit, null, now, unsure, now, FrozenOn(name, now, inFreezer))
            : existing with { Amount = amount, Unit = unit, Counted = now, Unsure = unsure, Frozen = existing.Frozen ?? (inFreezer ? now : null) };
        return useWithinDays > 0 ? item with { UseBy = now.Date.AddDays(useWithinDays), UseBySet = true } : Refresh(item, cold);
    }

    /// <summary>Put in the freezer now: its best-before is from today, for as long as it keeps frozen.</summary>
    public static PantryItem Freeze(PantryItem item, DateTime now, ColdStorage? cold = null) =>
        Refresh(item with { Frozen = now, UseBySet = false }, cold);

    /// <summary>Taken out of the freezer to thaw: it keeps from now as it does fresh where it's kept, which for meat and
    /// fish is a day or two in the fridge.</summary>
    public static PantryItem Thaw(PantryItem item, DateTime now, ColdStorage? cold = null) =>
        Refresh(item with { Frozen = null, Added = now, UseBySet = false }, cold);

    /// <summary>
    /// Where something is: the place it was put in, while that's there and is a freezer just when it's frozen; or else
    /// the first place of the kind its name says (a freezer for what's frozen), or the nearest kind there is.
    /// </summary>
    public static KitchenPlace PlaceOf(PantryItem item, IReadOnlyList<KitchenPlace> places)
    {
        if (item.Place is { } id && places.FirstOrDefault(p => p.Id == id) is { } chosen && chosen.Kind == Kept.Freezer == item.InFreezer)
            return chosen;
        var kind = item.InFreezer ? Kept.Freezer : item.Guess.Kept;
        Kept[] near = kind switch
        {
            Kept.Freezer => [Kept.Freezer, Kept.Fridge],
            Kept.Fridge => [Kept.Fridge, Kept.Pantry, Kept.Cupboard, Kept.Counter],
            Kept.Pantry => [Kept.Pantry, Kept.Cupboard, Kept.Counter],
            Kept.Cupboard => [Kept.Cupboard, Kept.Pantry, Kept.Counter],
            _ => [Kept.Counter, Kept.Pantry, Kept.Cupboard],
        };
        var ordered = places.OrderBy(p => p.Sort).ToList();
        foreach (var k in near)
            if (ordered.FirstOrDefault(p => p.Kind == k) is { } place) return place;
        return ordered[0];
    }

    /// <summary>Moved to another place: into a freezer it's frozen from now, out of one it's thawed from now, and
    /// between two freezers it keeps the day it went in.</summary>
    public static PantryItem Move(PantryItem item, KitchenPlace to, DateTime now, ColdStorage? cold = null)
    {
        var moved = item with { Place = to.Id };
        return (to.Kind == Kept.Freezer, item.InFreezer) switch
        {
            (true, false) => Freeze(moved, now, cold),
            (false, true) => Thaw(moved, now, cold),
            _ => moved,
        };
    }

    /// <summary>A best-before set by hand, kept until it's put back to the usual one, frozen or thawed.</summary>
    public static PantryItem SetUseBy(PantryItem item, DateTime? useBy) => item with { UseBy = useBy?.Date, UseBySet = true };

    /// <summary>Back to the usual best-before.</summary>
    public static PantryItem UsualUseBy(PantryItem item, ColdStorage? cold = null) => Refresh(item with { UseBySet = false }, cold);

    /// <summary>"9 months frozen", "doesn't freeze well", for what the stock shows beside something in the freezer.</summary>
    public static string FreezerNote(string name) => GroceryCatalog.Get(name) switch
    {
        { FreezerMonths: 0, Kept: not Kept.Freezer } => "doesn't freeze well",
        { FreezerMonths: var months } => $"best within {months} month{(months == 1 ? "" : "s")} frozen",
        null => $"about {FreezerMonthsUnknown} months frozen, as a rule",
    };

    /// <summary>What cooking a recipe leaves of what it used: each ingredient's amount taken from what there is of it.
    /// Ingredients the catalog knows are matched by name; others (from Claude's ideas) by the stock's name in theirs,
    /// with the amount read from it. More used than there was makes the count a guess.</summary>
    public static List<PantryItem> Cook(IReadOnlyList<PantryItem> pantry, Recipe recipe)
    {
        var changed = new Dictionary<string, PantryItem>(StringComparer.OrdinalIgnoreCase);
        PantryItem Current(PantryItem p) => changed.TryGetValue(p.Name, out var c) ? c : p;
        foreach (var ingredient in recipe.Ingredients)
        {
            PantryItem? item;
            double use;
            if (ingredient.Grocery.Length > 0)
            {
                item = pantry.FirstOrDefault(p => p.Name.Equals(ingredient.Grocery, StringComparison.OrdinalIgnoreCase));
                if (item is null || GroceryCatalog.Get(ingredient.Grocery) is not { Staple: false } info || info.Unit != item.Unit) continue;
                use = ingredient.Use;
            }
            else
            {
                item = pantry.FirstOrDefault(p => Mentions(ingredient.Name, p.Name));
                if (item is null) continue;
                var (amount, unit, read) = Read(item.Name, ingredient.Amount);
                if (unit != item.Unit || !read)
                {
                    changed[item.Name] = Current(item) with { Unsure = true };
                    continue;
                }
                use = ingredient.Amount.Length == 0 ? 0 : amount;
            }
            var now = Current(item);
            var left = now.Amount - use;
            changed[item.Name] = now with { Amount = Math.Max(0, left), Unsure = now.Unsure || left < 0 };
        }
        return changed.Values.ToList();
    }

    /// <summary>Whether a recipe's ingredient ("2 large eggs, beaten") names a food in the stock ("Eggs").</summary>
    private static bool Mentions(string ingredient, string food)
    {
        var stem = food.Trim().ToLowerInvariant();
        if (stem.EndsWith("es", StringComparison.Ordinal) && stem.Length > 4) stem = stem[..^2];
        else if (stem.EndsWith('s') && stem.Length > 3) stem = stem[..^1];
        return Regex.IsMatch(ingredient.ToLowerInvariant(), $@"\b{Regex.Escape(stem)}");
    }

    /// <summary>What's in doubt and worth counting: a guess, or not counted for two weeks (unless there's none).</summary>
    public static List<PantryItem> InDoubt(IEnumerable<PantryItem> pantry, DateTime now) =>
        pantry.Where(p => p.Unsure || now - p.Counted > Recount && p.Amount > 0).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>What's in the kitchen, for meal ideas: what there's some of, soonest to use first, and what's frozen
    /// (it needs thawing first).</summary>
    public static List<KitchenFood> Kitchen(IEnumerable<PantryItem> pantry, MeasureSystem system) =>
        pantry.Where(p => p.Amount > 0)
            .OrderBy(p => p.UseBy ?? DateTime.MaxValue)
            .Select(p => new KitchenFood(p.Name, Show(p, system) + (p.InFreezer ? ", in the freezer" : ""), p.UseBy))
            .ToList();

    /// <summary>What there's some of that's best used within <paramref name="withinDays"/> days.</summary>
    public static List<PantryItem> UseSoon(IEnumerable<PantryItem> pantry, DateTime now, int withinDays = 3) =>
        pantry.Where(p => p.Amount > 0 && p.UseBy is { } by && by >= now.Date && by <= now.Date.AddDays(withinDays)).OrderBy(p => p.UseBy).ToList();
}
