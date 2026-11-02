namespace Strongthium.HealthReminder.Tests;

public sealed class ShoppingSheetTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0);

    private static GroceryItem Item(string name, string? note = null, bool bought = false) =>
        new() { Name = name, Note = note, Quantity = "1", Added = Now, Bought = bought ? Now : null };

    private static StockedThing Had(string name, FoodGroup group, string? note = null) =>
        new(new PantryItem(name, 0, "", null, Now, false, Now, Group: group, Note: note), "", 1, Now, Now);

    [Fact]
    public void BySectionWhatsStillToBuyByName()
    {
        var list = new[] { Item("Milk"), Item("Bananas"), Item("Apples"), Item("Bread", bought: true) };
        var sheet = ShoppingSheet.Build(list, [], bySection: true);
        Assert.Equal([FoodGroup.Produce, FoodGroup.Beverages], sheet.Select(s => s.Group));
        Assert.Equal(["Apples", "Bananas"], sheet[0].Items.Select(i => i.Name));
        Assert.DoesNotContain(sheet.SelectMany(s => s.Items), i => i.Name == "Bread");
    }

    [Fact]
    public void ASectionIsWhereTheKitchenKeptItWithItsBrandFirst()
    {
        List<StockedThing> history = [Had("Relish", FoodGroup.Snacks), Had("Relish", FoodGroup.Condiments, "Bick's")];
        Assert.Equal(FoodGroup.Condiments, ShoppingSheet.SectionOf(Item("relish", "Bick's"), history));
        Assert.Equal(FoodGroup.Snacks, ShoppingSheet.SectionOf(Item("Relish"), history));
    }

    [Fact]
    public void AsOneListOrNothing()
    {
        var one = Assert.Single(ShoppingSheet.Build([Item("Milk"), Item("Apples")], [], bySection: false));
        Assert.Null(one.Group);
        Assert.Equal(["Apples", "Milk"], one.Items.Select(i => i.Name));
        Assert.Empty(ShoppingSheet.Build([Item("Milk", bought: true)], [], bySection: false));
    }
}
