namespace Strongthium.HealthReminder.Tests;

public sealed class KitchenPlaceTests
{
    private static readonly DateTime Monday = new(2026, 10, 26, 7, 0, 0);

    private static PantryItem Item(string name, DateTime? frozen = null) => new(name, 1, "", null, Monday, false, Monday, frozen);

    [Theory]
    [InlineData("Cheese", FoodGroup.DairyAndEggs, "cheese", Kept.Fridge)]
    [InlineData("Milk (Homogenized)", FoodGroup.Beverages, "milk", Kept.Fridge)]
    [InlineData("Whipping cream", FoodGroup.Beverages, "milk", Kept.Fridge)]
    [InlineData("Sour cream", FoodGroup.DairyAndEggs, "tub", Kept.Fridge)]
    [InlineData("Garlic", FoodGroup.Produce, "onion", Kept.Counter)]
    [InlineData("Garlic powder", FoodGroup.Spices, "spice", Kept.Cupboard)]
    [InlineData("Potato chips", FoodGroup.Snacks, "bag", Kept.Pantry)]
    [InlineData("Tostitos medium salsa", FoodGroup.Condiments, "jar", Kept.Fridge)]
    [InlineData("Eggs", FoodGroup.DairyAndEggs, "eggs", Kept.Fridge)]
    [InlineData("Cheez Whiz", FoodGroup.Condiments, "jar", Kept.Fridge)]
    [InlineData("Peanut butter", FoodGroup.Condiments, "jar", Kept.Cupboard)]
    [InlineData("Chocolate milk", FoodGroup.Beverages, "milk", Kept.Fridge)]
    [InlineData("Milk chocolate", FoodGroup.Snacks, "chocolate", Kept.Pantry)]
    [InlineData("Canned tuna", FoodGroup.DryGoods, "tin", Kept.Pantry)]
    [InlineData("Tomato soup", FoodGroup.DryGoods, "tin", Kept.Pantry)]
    [InlineData("Spaghetti", FoodGroup.DryGoods, "spaghetti", Kept.Pantry)]
    [InlineData("Elbow macaroni", FoodGroup.DryGoods, "elbow", Kept.Pantry)]
    [InlineData("Kraft Dinner", FoodGroup.DryGoods, "elbow", Kept.Pantry)]
    [InlineData("Penne", FoodGroup.DryGoods, "penne", Kept.Pantry)]
    [InlineData("Pasta", FoodGroup.DryGoods, "penne", Kept.Pantry)]
    [InlineData("Rotini", FoodGroup.DryGoods, "rotini", Kept.Pantry)]
    [InlineData("Pasta shells", FoodGroup.DryGoods, "shells", Kept.Pantry)]
    [InlineData("Taco shells", FoodGroup.Bakery, "tortilla", Kept.Counter)]
    [InlineData("Farfalle", FoodGroup.DryGoods, "bowtie", Kept.Pantry)]
    [InlineData("Pasta sauce", FoodGroup.DryGoods, "jar", Kept.Pantry)]
    [InlineData("Spaghetti and meatballs", FoodGroup.Meals, "pastaplate", Kept.Fridge)]
    [InlineData("Leftover pasta", FoodGroup.Meals, "pastaplate", Kept.Fridge)]
    [InlineData("French fries", FoodGroup.Meals, "fries", Kept.Freezer)]
    [InlineData("Hash brown patties", FoodGroup.Meals, "hashbrown", Kept.Freezer)]
    [InlineData("Hash browns", FoodGroup.Meals, "hashbrowns", Kept.Freezer)]
    [InlineData("Diced potatoes", FoodGroup.Meals, "hashbrowns", Kept.Freezer)]
    [InlineData("Hash brown potatoes", FoodGroup.Meals, "hashbrowns", Kept.Freezer)]
    [InlineData("Meat pies", FoodGroup.Meals, "meatpie", Kept.Freezer)]
    [InlineData("Frozen broccoli", FoodGroup.Produce, "bag", Kept.Freezer)]
    [InlineData("Salmon fillets", FoodGroup.MeatAndFish, "fish", Kept.Freezer)]
    [InlineData("Ground beef", FoodGroup.MeatAndFish, "groundbeef", Kept.Fridge)]
    [InlineData("Hamburger buns", FoodGroup.Bakery, "bun", Kept.Counter)]
    [InlineData("Hamburger patties", FoodGroup.MeatAndFish, "burger", Kept.Freezer)]
    [InlineData("Ice cream", FoodGroup.Snacks, "icecream", Kept.Freezer)]
    [InlineData("Orange juice", FoodGroup.Beverages, "juice", Kept.Fridge)]
    [InlineData("Dr Pepper", FoodGroup.Beverages, "can", Kept.Pantry)]
    [InlineData("Paprika", FoodGroup.Spices, "spice", Kept.Cupboard)]
    [InlineData("Chudleys pies", FoodGroup.Meals, "meatpie", Kept.Freezer)]
    [InlineData("Chicken pot pie", FoodGroup.Meals, "meatpie", Kept.Freezer)]
    [InlineData("Apple pie", FoodGroup.Bakery, "pie", Kept.Counter)]
    [InlineData("Broccoli", FoodGroup.Produce, "broccoli", Kept.Fridge)]
    [InlineData("Blueberry muffins", FoodGroup.Bakery, "muffin", Kept.Counter)]
    [InlineData("Birthday cake", FoodGroup.Bakery, "cake", Kept.Counter)]
    [InlineData("Pancake mix", FoodGroup.DryGoods, "bag", Kept.Pantry)]
    [InlineData("Frozen waffles", FoodGroup.Bakery, "pancakes", Kept.Freezer)]
    [InlineData("Chicken strips", FoodGroup.MeatAndFish, "strips", Kept.Freezer)]
    [InlineData("Ground turkey", FoodGroup.MeatAndFish, "groundbeef", Kept.Fridge)]
    [InlineData("Hot dogs", FoodGroup.MeatAndFish, "hotdog", Kept.Fridge)]
    [InlineData("Hot dog buns", FoodGroup.Bakery, "bun", Kept.Counter)]
    [InlineData("Sourdough bread", FoodGroup.Bakery, "loaf", Kept.Counter)]
    [InlineData("Bagels", FoodGroup.Bakery, "bagel", Kept.Counter)]
    [InlineData("Flour", FoodGroup.DryGoods, "sack", Kept.Pantry)]
    [InlineData("Taco seasoning", FoodGroup.Spices, "envelope", Kept.Pantry)]
    [InlineData("Leftover chili", FoodGroup.Meals, "container", Kept.Fridge)]
    [InlineData("Red wine", FoodGroup.Beverages, "wine", Kept.Pantry)]
    [InlineData("Something odd", FoodGroup.Other, "food", Kept.Pantry)]
    public void WhatAFoodIsComesFromItsName(string name, FoodGroup group, string icon, Kept kept)
    {
        var look = Foods.Guess(name);
        Assert.Equal((group, icon, kept), (look.Group, look.Icon, look.Kept));
    }

    [Fact]
    public void PopIsInItsBrandsColour()
    {
        Assert.Equal("#D32F2F", Foods.Guess("Coke").Color);
        Assert.Equal("#1E4FA3", Foods.Guess("Pepsi 12 pack").Color);
        Assert.Equal("#43A047", Foods.Guess("Green peppers").Color);
        Assert.Null(Foods.Guess("Cheese").Color);
    }

    [Fact]
    public void APictureOrColourPickedReplacesTheGuess()
    {
        var coke = Item("Coke");
        Assert.Equal(("can", "#D32F2F"), (coke.ShownIcon, coke.ShownColor));
        // Another picture: the brand's colour was for the can.
        var bottle = coke with { Icon = "bottle" };
        Assert.Equal(("bottle", (string?)null), (bottle.ShownIcon, bottle.ShownColor));
        Assert.Equal("#000000", (coke with { Color = "#000000" }).ShownColor);
        Assert.Equal(FoodGroup.Snacks, (coke with { Group = FoodGroup.Snacks }).ShownGroup);
    }

    private static readonly List<KitchenPlace> Places =
    [
        new(1, "Freezer", Kept.Freezer, 0),
        new(2, "Fridge", Kept.Fridge, 1),
        new(3, "Pantry", Kept.Pantry, 2),
        new(4, "Deep freezer", Kept.Freezer, 3),
    ];

    [Fact]
    public void ThingsGoWhereTheirKindGoes()
    {
        Assert.Equal("Fridge", Pantry.PlaceOf(Item("Cheese"), Places).Name);
        Assert.Equal("Pantry", Pantry.PlaceOf(Item("Spaghetti"), Places).Name);
        // No cupboard or counter: the pantry is nearest.
        Assert.Equal("Pantry", Pantry.PlaceOf(Item("Paprika"), Places).Name);
        Assert.Equal("Pantry", Pantry.PlaceOf(Item("Bananas"), Places).Name);
        // Frozen, it's in the first freezer.
        Assert.Equal("Freezer", Pantry.PlaceOf(Item("Cheese", Monday), Places).Name);
        Assert.Equal("Deep freezer", Pantry.PlaceOf(Item("Cheese", Monday) with { Place = 4 }, Places).Name);
        // A place taken away, or one that isn't a freezer for what's frozen: where its kind goes.
        Assert.Equal("Fridge", Pantry.PlaceOf(Item("Cheese") with { Place = 9 }, Places).Name);
        Assert.Equal("Freezer", Pantry.PlaceOf(Item("Cheese", Monday) with { Place = 2 }, Places).Name);
    }

    [Fact]
    public void MovingToAFreezerFreezesIt()
    {
        var later = Monday.AddDays(3);
        var steak = Pantry.Refresh(Item("Steak"));
        var frozen = Pantry.Move(steak, Places[3], later);
        Assert.Equal((4L, later), (frozen.Place!.Value, frozen.Frozen!.Value));
        Assert.Equal(later.Date.AddDays(Math.Round(6 * 30.4)), frozen.UseBy);
        // Between freezers it keeps the day it went in; out of one, it's thawed.
        var moved = Pantry.Move(frozen, Places[0], later.AddDays(10));
        Assert.Equal(later, moved.Frozen);
        var thawed = Pantry.Move(moved, Places[1], later.AddDays(20));
        Assert.Null(thawed.Frozen);
        Assert.Equal(later.AddDays(20).Date.AddDays(4), thawed.UseBy);
        // Between places that aren't freezers, nothing else changes.
        Assert.Equal(steak with { Place = 3 }, Pantry.Move(steak, Places[2], later));
    }

    [Fact]
    public void KeepsThePlacesAndWhatWasPicked()
    {
        using var database = new Database();
        Assert.Equal(["Freezer", "Fridge", "Pantry", "Cupboard", "Counter"], database.Places().Select(p => p.Name));
        var id = database.Save(new KitchenPlace(0, "Deep freezer", Kept.Freezer, 5));
        database.Save(database.Places().First(p => p.Name == "Fridge") with { Name = "Kitchen fridge" });
        Assert.Contains(database.Places(), p => p.Name == "Kitchen fridge");

        var coke = Item("Coke") with { Place = id, Icon = "bottle", Color = "#112233", Group = FoodGroup.Snacks };
        coke = database.Save(coke);
        Assert.Equal(coke, database.Pantry().Single());
        // Taken away, what was in it goes where its kind goes.
        database.DeletePlace(id);
        Assert.Null(database.Pantry().Single().Place);
        Assert.DoesNotContain(database.Places(), p => p.Id == id);
    }
}
