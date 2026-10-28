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
/// more was used than was counted), so it's worth checking.</summary>
public sealed record PantryItem(string Name, double Amount, string Unit, DateTime? UseBy, DateTime Counted, bool Unsure);

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

    /// <summary>When it's best used by: what was said for it (from a photo), or what the catalog says it keeps.</summary>
    public static DateTime? UseBy(string name, DateTime now, int useWithinDays = 0) =>
        useWithinDays > 0 ? now.Date.AddDays(useWithinDays) : GroceryCatalog.Get(name) is { OpenDays: > 0 } info ? now.Date.AddDays(info.OpenDays) : null;

    /// <summary>Something bought (or seen to be new): added to what there is, or new if there was none. A count already
    /// below zero starts again from zero.</summary>
    public static PantryItem Add(PantryItem? existing, string name, string quantity, DateTime now, int useWithinDays = 0, bool unsure = false)
    {
        var (amount, unit) = Amount(name, quantity);
        var useBy = UseBy(name, now, useWithinDays);
        if (existing is null || existing.Unit != unit) return new(name, amount, unit, useBy, now, unsure);
        return existing with { Amount = Math.Max(0, existing.Amount) + amount, UseBy = useBy ?? existing.UseBy, Unsure = existing.Unsure || unsure };
    }

    /// <summary>Counted: this much there is now.</summary>
    public static PantryItem Set(string name, string quantity, DateTime now, int useWithinDays = 0, bool unsure = false, PantryItem? existing = null)
    {
        var (amount, unit) = Amount(name, quantity);
        return new(name, amount, unit, UseBy(name, now, useWithinDays) is { } by && useWithinDays > 0 ? by : existing?.UseBy ?? UseBy(name, now), now, unsure);
    }

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

    /// <summary>What's in the kitchen, for meal ideas: what there's some of, soonest to use first.</summary>
    public static List<KitchenFood> Kitchen(IEnumerable<PantryItem> pantry, MeasureSystem system) =>
        pantry.Where(p => p.Amount > 0)
            .OrderBy(p => p.UseBy ?? DateTime.MaxValue)
            .Select(p => new KitchenFood(p.Name, Show(p, system), p.UseBy))
            .ToList();

    /// <summary>What there's some of that's best used within <paramref name="withinDays"/> days.</summary>
    public static List<PantryItem> UseSoon(IEnumerable<PantryItem> pantry, DateTime now, int withinDays = 3) =>
        pantry.Where(p => p.Amount > 0 && p.UseBy is { } by && by >= now.Date && by <= now.Date.AddDays(withinDays)).OrderBy(p => p.UseBy).ToList();
}
