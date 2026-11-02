namespace Strongthium.HealthReminder.Tests;

public sealed class KitchenSheetTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0);

    private static readonly List<KitchenPlace> Places =
    [
        new(1, "Freezer", Kept.Freezer, 0),
        new(2, "Fridge", Kept.Fridge, 1),
        new(3, "Pantry", Kept.Pantry, 2),
    ];

    private static PantryItem Item(string name, long place, bool unsure = false, int countedDaysAgo = 1, FoodGroup? group = null, long id = 0) =>
        new(name, 1, "", null, Now.AddDays(-countedDaysAgo), unsure, Now.AddDays(-countedDaysAgo), place == 1 ? Now.AddDays(-countedDaysAgo) : null,
            Place: place, Group: group, Id: id);

    private static readonly List<PantryItem> Kitchen =
    [
        Item("Peas", 1, id: 1),
        Item("Milk", 2, unsure: true, group: FoodGroup.DairyAndEggs, id: 2),
        Item("Yogurt", 2, countedDaysAgo: 20, group: FoodGroup.DairyAndEggs, id: 3),
        Item("Ham", 2, group: FoodGroup.MeatAndFish, id: 4),
        Item("Rice", 3, id: 5),
    ];

    [Fact]
    public void ToCheckHasJustWhatsInDoubtAndLeavesOffPlacesWithNone()
    {
        var sheet = KitchenSheet.Build(Kitchen, Places, new HashSet<long> { 1, 2, 3 }, toCheck: true, KitchenSort.Name, Now);
        var fridge = Assert.Single(sheet);
        Assert.Equal("Fridge", fridge.Place.Name);
        var dairy = Assert.Single(fridge.Groups);
        Assert.Equal(FoodGroup.DairyAndEggs, dairy.Group);
        Assert.Equal(["Milk", "Yogurt"], dairy.Lines.Select(l => l.Item.Name));
        Assert.Equal(["the amount is a guess", "last counted Oct 8"], dairy.Lines.Select(l => l.Why));
    }

    [Fact]
    public void EverythingIsByPlaceInTheirOrderAndThenByGroup()
    {
        var sheet = KitchenSheet.Build(Kitchen, Places, new HashSet<long> { 3, 2 }, toCheck: false, KitchenSort.Name, Now);
        Assert.Equal(["Fridge", "Pantry"], sheet.Select(p => p.Place.Name));
        Assert.Equal(3, sheet[0].Count);
        // In the groups' order, as the Groceries page has them.
        Assert.Equal([FoodGroup.MeatAndFish, FoodGroup.DairyAndEggs], sheet[0].Groups.Select(g => g.Group));
        // What isn't in doubt has no reason to check it.
        Assert.Equal("", sheet[0].Groups.SelectMany(g => g.Lines).Single(l => l.Item.Name == "Ham").Why);
    }

    [Fact]
    public void AnEmptyPlaceStaysOnEverythingToWriteIn()
    {
        var sheet = KitchenSheet.Build([Kitchen[0]], Places, new HashSet<long> { 1, 3 }, toCheck: false, KitchenSort.Name, Now);
        Assert.Equal(["Freezer", "Pantry"], sheet.Select(p => p.Place.Name));
        Assert.Equal(0, sheet[1].Count);
    }

    [Fact]
    public void CountsAreByPlace()
    {
        var counts = KitchenSheet.Counts(Kitchen, Places, Now);
        Assert.Equal((1, 0), counts[1]);
        Assert.Equal((3, 2), counts[2]);
        Assert.Equal((1, 0), counts[3]);
    }
}
