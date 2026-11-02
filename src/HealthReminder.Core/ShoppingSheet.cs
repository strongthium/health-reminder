namespace Strongthium.HealthReminder;

/// <summary>A section of a printed grocery list ("Dairy & eggs"), and what's in it; null for the list as one.</summary>
public sealed record ShoppingSection(FoodGroup? Group, IReadOnlyList<GroceryItem> Items);

/// <summary>
/// A grocery list to print and take to the store: what's still to buy, by name, in the sections a store has them in (the
/// groups the kitchen keeps them in), or as one list.
/// </summary>
public static class ShoppingSheet
{
    /// <summary>The section something goes in: its group as the kitchen kept it the last time it was there (its brand
    /// first), or what its name says.</summary>
    public static FoodGroup SectionOf(GroceryItem item, IReadOnlyList<StockedThing> history)
    {
        var same = StringComparer.OrdinalIgnoreCase;
        var named = history.Where(t => same.Equals(t.Name, item.Name.Trim())).ToList();
        var kept = named.FirstOrDefault(t => same.Equals(t.Note, item.Note ?? "")) ?? named.FirstOrDefault();
        return kept?.Like.ShownGroup ?? Foods.Guess(item.Name).Group;
    }

    public static List<ShoppingSection> Build(IEnumerable<GroceryItem> list, IReadOnlyList<StockedThing> history, bool bySection)
    {
        var toBuy = list.Where(i => i.Bought is null).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (!bySection) return toBuy.Count == 0 ? [] : [new ShoppingSection(null, toBuy)];
        return toBuy.GroupBy(i => SectionOf(i, history)).OrderBy(g => g.Key)
            .Select(g => new ShoppingSection(g.Key, g.ToList()))
            .ToList();
    }
}
