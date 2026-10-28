namespace Strongthium.HealthReminder;

/// <summary>
/// What a grocery is counted in (<paramref name="Unit"/>: "egg", "g", "mL", "can"), how much a usual pack holds
/// (<paramref name="Pack"/>, written as the grocery list reads it in <paramref name="PackText"/>), how long it keeps
/// (<paramref name="Keeps"/>), and how many days it lasts once opened or, if fresh, once bought
/// (<paramref name="OpenDays"/>; 0 when it's months). A staple (oil, spices, sauces) lasts and isn't counted.
/// Where it's <paramref name="Kept"/> once bought, and for how many days it's best (<paramref name="KeepDays"/>; 0 for
/// years, or for what's bought frozen), give its best-before; <paramref name="FreezerMonths"/> is how long it's at its
/// best in a freezer at −18 °C (0 °F), 0 when it doesn't freeze well.
/// </summary>
public sealed record GroceryInfo(string Name, string Unit, double Pack, string PackText, string Keeps, int OpenDays, bool Freezes = false, bool Staple = false,
    Kept Kept = Kept.Fridge, int KeepDays = 0, int FreezerMonths = 0);

/// <summary>Where something is kept once it's bought; also the kind of a place in the kitchen (<see cref="KitchenPlace"/>). The
/// catalog says Cupboard for whatever keeps at room temperature, and the food's kind says whether that's the pantry.</summary>
public enum Kept { Fridge, Freezer, Cupboard, Counter, Pantry }

/// <summary>Something bought that's best used, or frozen, by a day.</summary>
public sealed record UseSoonItem(GroceryInfo Info, DateTime Bought, DateTime UseBy);

/// <summary>A grocery for the week's menu: how much the recipes use, the packs to buy, and a word on what's left.</summary>
public sealed record ShoppingLine(GroceryInfo Info, double Needed, int Packs, string Note);

/// <summary>
/// The groceries the recipes use, in the packs they're usually sold in (Canadian and U.S. sizes) and with how long they
/// keep, from the USDA's FoodKeeper and Canada's food storage guidance, rounded to what's easy to remember. Best-before
/// days are the short end of what each keeps, and freezer months its best quality at −18 °C (0 °F): frozen food stays
/// safe longer, but drier and blander.
/// </summary>
public static class GroceryCatalog
{
    public static readonly IReadOnlyList<GroceryInfo> All =
    [
        new("Eggs", "egg", 12, "12", "3 to 5 weeks in the fridge", 0, Kept: Kept.Fridge, KeepDays: 28, FreezerMonths: 12),
        new("Bread", "slice", 20, "1 loaf", "5 to 7 days on the counter; months frozen (toast it from frozen)", 6, Freezes: true, Kept: Kept.Counter, KeepDays: 6, FreezerMonths: 3),
        new("Milk", "mL", 2000, "2 L", "5 to 7 days once opened", 6, Kept: Kept.Fridge, KeepDays: 6, FreezerMonths: 3),
        new("Butter", "g", 454, "454 g", "1 to 3 months in the fridge", 0, Kept: Kept.Fridge, KeepDays: 60, FreezerMonths: 9),
        new("Oats", "g", 1000, "1 kg", "a year in the cupboard", 0, Kept: Kept.Cupboard, KeepDays: 365, FreezerMonths: 0),
        new("Bananas", "banana", 6, "6", "2 to 7 days on the counter", 5, Kept: Kept.Counter, KeepDays: 5, FreezerMonths: 3),
        new("Yogurt", "g", 650, "650 g", "7 to 10 days once opened", 8, Kept: Kept.Fridge, KeepDays: 10, FreezerMonths: 2),
        new("Granola", "g", 400, "400 g", "a month once opened", 0, Kept: Kept.Cupboard, KeepDays: 90, FreezerMonths: 0),
        new("Frozen berries", "g", 600, "600 g", "8 to 12 months in the freezer", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 8),
        new("Red peppers", "pepper", 1, "1", "1 to 2 weeks in the fridge; 3 to 5 days once cut", 4, Kept: Kept.Fridge, KeepDays: 10, FreezerMonths: 6),
        new("Spinach", "g", 142, "142 g", "3 to 5 days once opened", 4, Kept: Kept.Fridge, KeepDays: 4, FreezerMonths: 6),
        new("Cheese", "g", 400, "400 g", "3 to 4 weeks once opened; grated, it freezes", 0, Freezes: true, Kept: Kept.Fridge, KeepDays: 28, FreezerMonths: 6),
        new("Cheese slices", "slice", 16, "16", "2 to 4 weeks once opened", 0, Kept: Kept.Fridge, KeepDays: 21, FreezerMonths: 3),
        new("Sliced turkey", "g", 175, "175 g", "3 to 5 days once opened", 4, Freezes: true, Kept: Kept.Fridge, KeepDays: 4, FreezerMonths: 2),
        new("Lettuce", "head", 1, "1", "1 to 2 weeks in the fridge", 10, Kept: Kept.Fridge, KeepDays: 10, FreezerMonths: 0),
        new("Apples", "apple", 6, "6", "4 to 6 weeks in the fridge", 0, Kept: Kept.Fridge, KeepDays: 35, FreezerMonths: 8),
        new("Canned tuna", "can", 1, "1", "years in the cupboard; 3 to 4 days once opened", 0, Kept: Kept.Cupboard, KeepDays: 0, FreezerMonths: 0),
        new("Baby carrots", "g", 340, "340 g", "2 to 3 weeks in the fridge", 14, Kept: Kept.Fridge, KeepDays: 14, FreezerMonths: 8),
        new("Tortillas", "tortilla", 10, "10", "a week once opened, 3 to 4 in the fridge; they freeze", 7, Freezes: true, Kept: Kept.Cupboard, KeepDays: 7, FreezerMonths: 6),
        new("Canned beans", "can", 1, "1", "years in the cupboard; 3 to 4 days once opened", 0, Kept: Kept.Cupboard, KeepDays: 0, FreezerMonths: 0),
        new("Tomato soup", "can", 1, "1", "years in the cupboard; 3 to 4 days once opened", 0, Kept: Kept.Cupboard, KeepDays: 0, FreezerMonths: 0),
        new("Rice", "g", 2000, "2 kg", "a year or more in the cupboard; cooked, 3 to 4 days in the fridge", 0, Kept: Kept.Cupboard, KeepDays: 0, FreezerMonths: 0),
        new("Frozen mixed vegetables", "g", 1000, "1 kg", "8 to 12 months in the freezer", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 8),
        new("Chicken breasts", "breast", 4, "4", "fresh, 1 to 2 days in the fridge: freeze them one to a bag the day you buy them; 9 months frozen", 2, Freezes: true, Kept: Kept.Fridge, KeepDays: 2, FreezerMonths: 9),
        new("Salad greens", "g", 142, "142 g", "5 to 7 days; 3 to 5 once opened", 4, Kept: Kept.Fridge, KeepDays: 5, FreezerMonths: 0),
        new("Cucumbers", "cucumber", 1, "1", "about a week in the fridge", 6, Kept: Kept.Fridge, KeepDays: 7, FreezerMonths: 0),
        new("Tomatoes", "tomato", 4, "4", "3 to 5 days on the counter", 5, Kept: Kept.Counter, KeepDays: 5, FreezerMonths: 2),
        new("Dinner rolls", "roll", 12, "12", "3 to 4 days on the counter; they freeze", 4, Freezes: true, Kept: Kept.Counter, KeepDays: 4, FreezerMonths: 3),
        new("Frozen broccoli", "g", 500, "500 g", "8 to 12 months in the freezer", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 8),
        new("Steak", "steak", 2, "2", "fresh, 3 to 5 days in the fridge: freeze them one to a bag; 6 to 12 months frozen", 4, Freezes: true, Kept: Kept.Fridge, KeepDays: 4, FreezerMonths: 6),
        new("Potatoes", "potato", 10, "2.27 kg", "2 to 3 weeks in a cool, dark place", 0, Kept: Kept.Cupboard, KeepDays: 21, FreezerMonths: 0),
        new("Frozen green beans", "g", 500, "500 g", "8 to 12 months in the freezer", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 8),
        new("Salmon fillets", "fillet", 4, "4", "frozen, 2 to 3 months at their best", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 2),
        new("Lemons", "lemon", 1, "1", "3 to 4 weeks in the fridge", 0, Kept: Kept.Fridge, KeepDays: 28, FreezerMonths: 3),
        new("Ground beef", "g", 454, "454 g", "fresh, 1 to 2 days in the fridge: freeze it in 100 g bags the day you buy it; 3 to 4 months frozen", 2, Freezes: true, Kept: Kept.Fridge, KeepDays: 2, FreezerMonths: 3),
        new("Pasta", "g", 900, "900 g", "1 to 2 years in the cupboard", 0, Kept: Kept.Cupboard, KeepDays: 0, FreezerMonths: 0),
        new("Pasta sauce", "mL", 650, "650 mL", "5 to 7 days once opened; it freezes in portions", 6, Freezes: true, Kept: Kept.Cupboard, KeepDays: 365, FreezerMonths: 6),
        new("Pork chops", "chop", 4, "4", "fresh, 3 to 5 days in the fridge: freeze them one to a bag; 4 to 6 months frozen", 4, Freezes: true, Kept: Kept.Fridge, KeepDays: 4, FreezerMonths: 4),
        new("Onions", "onion", 3, "3", "1 to 2 months in a cool, dark place; 7 to 10 days once cut", 0, Kept: Kept.Cupboard, KeepDays: 45, FreezerMonths: 8),
        new("Canned tomatoes", "can", 1, "1", "years in the cupboard; 5 to 7 days once opened", 0, Kept: Kept.Cupboard, KeepDays: 0, FreezerMonths: 0),
        new("Hamburger patties", "patty", 8, "8", "3 to 4 months in the freezer", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 3),
        new("Hamburger buns", "bun", 8, "8", "5 to 7 days on the counter; they freeze", 6, Freezes: true, Kept: Kept.Counter, KeepDays: 6, FreezerMonths: 3),
        new("Sausages", "sausage", 6, "6", "fresh, 1 to 2 days in the fridge: freeze them two to a bag; 1 to 2 months frozen", 2, Freezes: true, Kept: Kept.Fridge, KeepDays: 2, FreezerMonths: 2),
        new("Frozen corn", "g", 750, "750 g", "8 to 12 months in the freezer", 0, Kept: Kept.Freezer, KeepDays: 0, FreezerMonths: 8),
        // Staples: they last, and a recipe uses a little.
        new("Cinnamon", "", 1, "", "years", 0, Staple: true, Kept: Kept.Cupboard),
        new("Paprika", "", 1, "", "years", 0, Staple: true, Kept: Kept.Cupboard),
        new("Chili powder", "", 1, "", "years", 0, Staple: true, Kept: Kept.Cupboard),
        new("Chili flakes", "", 1, "", "years", 0, Staple: true, Kept: Kept.Cupboard),
        new("Cooking oil", "", 1, "", "months", 0, Staple: true, Kept: Kept.Cupboard),
        new("Soy sauce", "", 1, "", "months", 0, Staple: true, Kept: Kept.Cupboard),
        new("Mayonnaise", "", 1, "", "2 months once opened", 0, Staple: true, Kept: Kept.Cupboard),
        new("Salad dressing", "", 1, "", "1 to 3 months once opened", 0, Staple: true, Kept: Kept.Cupboard),
        new("Salsa", "", 1, "", "2 weeks once opened", 0, Staple: true, Kept: Kept.Cupboard),
        new("Peanut butter", "", 1, "", "2 to 3 months once opened", 0, Staple: true, Kept: Kept.Cupboard),
    ];

    public static GroceryInfo? Get(string name) => All.FirstOrDefault(g => g.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What the recipes need, in whole packs, in order of name, with a note for what's left when it won't keep: fresh
    /// meat to freeze in portions, bread and the like to freeze, and anything else that would spoil before it's used.
    /// </summary>
    public static List<ShoppingLine> Shopping(IEnumerable<Recipe> recipes)
    {
        var lines = new List<ShoppingLine>();
        var uses = recipes.SelectMany(r => r.Ingredients).GroupBy(i => i.Grocery, StringComparer.OrdinalIgnoreCase);
        foreach (var group in uses.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (Get(group.Key) is not { } info) continue;
            var needed = group.Sum(i => i.Use);
            if (info.Staple || needed <= 0)
            {
                lines.Add(new(info, 0, 0, info.Staple ? "keep some on hand" : ""));
                continue;
            }
            var packs = (int)Math.Ceiling(needed / info.Pack - 1e-9);
            var left = packs * info.Pack - needed;
            var note = left <= 0.0001 ? "" :
                info.OpenDays is > 0 and <= 10 && left > info.Pack * 0.25
                    ? info.Freezes ? $"freeze the {Amount(left, info)} you won't use within {info.OpenDays} days"
                    : $"{Amount(left, info)} left over that keeps about {info.OpenDays} days: use it in another meal, or buy a smaller one"
                : $"{Amount(left, info)} left over; keeps {info.Keeps}";
            lines.Add(new(info, needed, packs, note));
        }
        return lines;
    }

    /// <summary>What was bought that won't keep long, latest first by name, to use (or freeze) by the day shown: those
    /// whose day is from today to <paramref name="withinDays"/> ahead.</summary>
    public static List<UseSoonItem> UseSoon(IEnumerable<GroceryItem> bought, DateTime now, int withinDays = 3) =>
        bought.Where(b => b.Bought is not null && b.Used is null && UseBy(b) is not null)
            .Select(b => new UseSoonItem(Get(b.Name) ?? new GroceryInfo(b.Name, "", 1, "", "", 0), b.Bought!.Value, UseBy(b)!.Value))
            .GroupBy(u => u.Info.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(u => u.Bought)!)
            .Where(u => u.UseBy >= now.Date && u.UseBy <= now.Date.AddDays(withinDays))
            .OrderBy(u => u.UseBy)
            .ToList();

    /// <summary>When something bought is best used by: what was said for it (from a photo), or what the catalog says
    /// it keeps once bought; null when it keeps for months, or isn't known.</summary>
    public static DateTime? UseBy(GroceryItem item) =>
        item.UseBy ?? (item.Bought is { } bought && Get(item.Name) is { OpenDays: > 0 } info ? bought.Date.AddDays(info.OpenDays) : null);

    /// <summary>What's in the kitchen, for meal ideas.</summary>
    public static List<KitchenFood> Kitchen(IEnumerable<GroceryItem> inKitchen) =>
        inKitchen.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(i => i.Bought)!)
            .Select(i => new KitchenFood(i.Name, i.Quantity, UseBy(i)))
            .OrderBy(f => f.UseBy ?? DateTime.MaxValue)
            .ToList();

    /// <summary>"3 eggs", "250 g", "½ can"; "a little" for less than an eighth (a leaf or two of a lettuce).</summary>
    public static string Amount(double amount, GroceryInfo info) =>
        info.Unit is "g" or "mL" ? $"{Math.Round(amount):0} {info.Unit}"
        : amount > 0 && amount < 0.125 ? "a little"
        : Amounts.Format(Math.Round(amount * 4) / 4, info.Unit);
}
