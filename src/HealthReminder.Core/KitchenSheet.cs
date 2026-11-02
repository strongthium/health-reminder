namespace Strongthium.HealthReminder;

/// <summary>Something on a printed kitchen sheet, and why it's to be checked ("" when it isn't).</summary>
public sealed record SheetLine(PantryItem Item, string Why);

/// <summary>A group of what's in a place on the sheet ("Dairy"), in the kitchen's order.</summary>
public sealed record SheetGroup(FoodGroup Group, IReadOnlyList<SheetLine> Lines);

/// <summary>A place on the sheet and what's in it, by group.</summary>
public sealed record SheetPlace(KitchenPlace Place, IReadOnlyList<SheetGroup> Groups)
{
    public int Count => Groups.Sum(g => g.Lines.Count);
}

/// <summary>
/// A kitchen sheet to print and take to the fridge and cupboards, for what the program can't see: what's in doubt, or
/// everything, from the places picked, by place and then by group, as the Groceries page shows them.
/// </summary>
public static class KitchenSheet
{
    /// <summary>Why something is in doubt: "the amount is a guess", "last counted Oct 12"; "" when it isn't.</summary>
    public static string Why(PantryItem item, DateTime now) =>
        item.Unsure ? "the amount is a guess"
        : Pantry.IsInDoubt(item, now) ? $"last counted {item.Counted:MMM d}"
        : "";

    /// <summary>How many there are in each place: all, and in doubt.</summary>
    public static Dictionary<long, (int All, int ToCheck)> Counts(IReadOnlyList<PantryItem> items, IReadOnlyList<KitchenPlace> places, DateTime now) =>
        places.ToDictionary(p => p.Id, p =>
        {
            var here = items.Where(i => Pantry.PlaceOf(i, places).Id == p.Id).ToList();
            return (here.Count, here.Count(i => Pantry.IsInDoubt(i, now)));
        });

    /// <summary>The places picked, in their order, each with what's in it (just what's in doubt, with
    /// <paramref name="toCheck"/>), by group and in <paramref name="sort"/>'s order within each. A place with nothing
    /// to show is left off what's to check, and kept with everything, to write in what's there.</summary>
    public static List<SheetPlace> Build(IReadOnlyList<PantryItem> items, IReadOnlyList<KitchenPlace> places, ISet<long> picked, bool toCheck,
        KitchenSort sort, DateTime now)
    {
        var sorted = Pantry.Sorted(items, sort).ToList();
        return places.OrderBy(p => p.Sort)
            .Where(p => picked.Contains(p.Id))
            .Select(place => new SheetPlace(place, sorted
                .Where(i => Pantry.PlaceOf(i, places).Id == place.Id && (!toCheck || Pantry.IsInDoubt(i, now)))
                .GroupBy(i => i.ShownGroup).OrderBy(g => g.Key)
                .Select(g => new SheetGroup(g.Key, g.Select(i => new SheetLine(i, Why(i, now))).ToList()))
                .ToList()))
            .Where(s => !toCheck || s.Count > 0)
            .ToList();
    }
}
