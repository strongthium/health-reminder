namespace Strongthium.HealthReminder;

/// <summary>
/// What a grocery is counted in (<paramref name="Unit"/>: "egg", "g", "mL", "can"), how much a usual pack holds
/// (<paramref name="Pack"/>, written as the grocery list reads it in <paramref name="PackText"/>), how long it keeps
/// (<paramref name="Keeps"/>), and how many days it lasts once opened or, if fresh, once bought
/// (<paramref name="OpenDays"/>; 0 when it's months). A staple (oil, spices, sauces) lasts and isn't counted.
/// </summary>
public sealed record GroceryInfo(string Name, string Unit, double Pack, string PackText, string Keeps, int OpenDays, bool Freezes = false, bool Staple = false);

/// <summary>Something bought that's best used, or frozen, by a day.</summary>
public sealed record UseSoonItem(GroceryInfo Info, DateTime Bought, DateTime UseBy);

/// <summary>A grocery for the week's menu: how much the recipes use, the packs to buy, and a word on what's left.</summary>
public sealed record ShoppingLine(GroceryInfo Info, double Needed, int Packs, string Note);

/// <summary>
/// The groceries the recipes use, in the packs they're usually sold in (Canadian and U.S. sizes) and with how long they
/// keep, from the USDA's FoodKeeper and Canada's food storage guidance, rounded to what's easy to remember.
/// </summary>
public static class GroceryCatalog
{
    public static readonly IReadOnlyList<GroceryInfo> All =
    [
        new("Eggs", "egg", 12, "12", "3 to 5 weeks in the fridge", 0),
        new("Bread", "slice", 20, "1 loaf", "5 to 7 days on the counter; months frozen (toast it from frozen)", 6, Freezes: true),
        new("Milk", "mL", 2000, "2 L", "5 to 7 days once opened", 6),
        new("Butter", "g", 454, "454 g", "1 to 3 months in the fridge", 0),
        new("Oats", "g", 1000, "1 kg", "a year in the cupboard", 0),
        new("Bananas", "banana", 6, "6", "2 to 7 days on the counter", 5),
        new("Yogurt", "g", 650, "650 g", "7 to 10 days once opened", 8),
        new("Granola", "g", 400, "400 g", "a month once opened", 0),
        new("Frozen berries", "g", 600, "600 g", "8 to 12 months in the freezer", 0),
        new("Red peppers", "pepper", 1, "1", "1 to 2 weeks in the fridge; 3 to 5 days once cut", 4),
        new("Spinach", "g", 142, "142 g", "3 to 5 days once opened", 4),
        new("Cheese", "g", 400, "400 g", "3 to 4 weeks once opened; grated, it freezes", 0, Freezes: true),
        new("Cheese slices", "slice", 16, "16", "2 to 4 weeks once opened", 0),
        new("Sliced turkey", "g", 175, "175 g", "3 to 5 days once opened", 4, Freezes: true),
        new("Lettuce", "head", 1, "1", "1 to 2 weeks in the fridge", 10),
        new("Apples", "apple", 6, "6", "4 to 6 weeks in the fridge", 0),
        new("Canned tuna", "can", 1, "1", "years in the cupboard; 3 to 4 days once opened", 0),
        new("Baby carrots", "g", 340, "340 g", "2 to 3 weeks in the fridge", 14),
        new("Tortillas", "tortilla", 10, "10", "a week once opened, 3 to 4 in the fridge; they freeze", 7, Freezes: true),
        new("Canned beans", "can", 1, "1", "years in the cupboard; 3 to 4 days once opened", 0),
        new("Tomato soup", "can", 1, "1", "years in the cupboard; 3 to 4 days once opened", 0),
        new("Rice", "g", 2000, "2 kg", "a year or more in the cupboard; cooked, 3 to 4 days in the fridge", 0),
        new("Frozen mixed vegetables", "g", 1000, "1 kg", "8 to 12 months in the freezer", 0),
        new("Chicken breasts", "breast", 4, "4", "fresh, 1 to 2 days in the fridge: freeze them one to a bag the day you buy them; 9 months frozen", 2, Freezes: true),
        new("Salad greens", "g", 142, "142 g", "5 to 7 days; 3 to 5 once opened", 4),
        new("Cucumbers", "cucumber", 1, "1", "about a week in the fridge", 6),
        new("Tomatoes", "tomato", 4, "4", "3 to 5 days on the counter", 5),
        new("Dinner rolls", "roll", 12, "12", "3 to 4 days on the counter; they freeze", 4, Freezes: true),
        new("Frozen broccoli", "g", 500, "500 g", "8 to 12 months in the freezer", 0),
        new("Steak", "steak", 2, "2", "fresh, 3 to 5 days in the fridge: freeze them one to a bag; 6 to 12 months frozen", 4, Freezes: true),
        new("Potatoes", "potato", 10, "2.27 kg", "2 to 3 weeks in a cool, dark place", 0),
        new("Frozen green beans", "g", 500, "500 g", "8 to 12 months in the freezer", 0),
        new("Salmon fillets", "fillet", 4, "4", "frozen, 2 to 3 months at their best", 0),
        new("Lemons", "lemon", 1, "1", "3 to 4 weeks in the fridge", 0),
        new("Ground beef", "g", 454, "454 g", "fresh, 1 to 2 days in the fridge: freeze it in 100 g bags the day you buy it; 3 to 4 months frozen", 2, Freezes: true),
        new("Pasta", "g", 900, "900 g", "1 to 2 years in the cupboard", 0),
        new("Pasta sauce", "mL", 650, "650 mL", "5 to 7 days once opened; it freezes in portions", 6, Freezes: true),
        new("Pork chops", "chop", 4, "4", "fresh, 3 to 5 days in the fridge: freeze them one to a bag; 4 to 6 months frozen", 4, Freezes: true),
        new("Onions", "onion", 3, "3", "1 to 2 months in a cool, dark place; 7 to 10 days once cut", 0),
        new("Canned tomatoes", "can", 1, "1", "years in the cupboard; 5 to 7 days once opened", 0),
        new("Hamburger patties", "patty", 8, "8", "3 to 4 months in the freezer", 0),
        new("Hamburger buns", "bun", 8, "8", "5 to 7 days on the counter; they freeze", 6, Freezes: true),
        new("Sausages", "sausage", 6, "6", "fresh, 1 to 2 days in the fridge: freeze them two to a bag; 1 to 2 months frozen", 2, Freezes: true),
        new("Frozen corn", "g", 750, "750 g", "8 to 12 months in the freezer", 0),
        // Staples: they last, and a recipe uses a little.
        new("Cinnamon", "", 1, "", "years", 0, Staple: true),
        new("Paprika", "", 1, "", "years", 0, Staple: true),
        new("Chili powder", "", 1, "", "years", 0, Staple: true),
        new("Chili flakes", "", 1, "", "years", 0, Staple: true),
        new("Cooking oil", "", 1, "", "months", 0, Staple: true),
        new("Soy sauce", "", 1, "", "months", 0, Staple: true),
        new("Mayonnaise", "", 1, "", "2 months once opened", 0, Staple: true),
        new("Salad dressing", "", 1, "", "1 to 3 months once opened", 0, Staple: true),
        new("Salsa", "", 1, "", "2 weeks once opened", 0, Staple: true),
        new("Peanut butter", "", 1, "", "2 to 3 months once opened", 0, Staple: true),
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
