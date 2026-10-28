using System.Globalization;

namespace Strongthium.HealthReminder.Tests;

public sealed class GroceryAndMealTests
{
    [Theory]
    [InlineData("2 L", Dimension.Volume, 2000)]
    [InlineData("1.5l", Dimension.Volume, 1500)]
    [InlineData("750 ml", Dimension.Volume, 750)]
    [InlineData("1 gallon", Dimension.Volume, 3785.41)]
    [InlineData("2 gallons of milk", Dimension.Volume, 7570.82)]
    [InlineData("1 imperial gallon", Dimension.Volume, 4546.09)]
    [InlineData("64 fl oz", Dimension.Volume, 1892.71)]
    [InlineData("1½ cups", Dimension.Volume, 354.88)]
    [InlineData("1 1/2 cups", Dimension.Volume, 354.88)]
    [InlineData("2 x 355 mL", Dimension.Volume, 710)]
    [InlineData("500g", Dimension.Mass, 500)]
    [InlineData("2 kg", Dimension.Mass, 2000)]
    [InlineData("1 lb", Dimension.Mass, 453.59)]
    [InlineData("12 oz", Dimension.Mass, 340.19)]
    [InlineData("12", Dimension.Count, 12)]
    [InlineData("a dozen", Dimension.Count, 12)]
    [InlineData("2 loaves", Dimension.Count, 2)]
    [InlineData("3 avocados", Dimension.Count, 3)]
    public void ReadsAnyAmount(string text, Dimension dimension, double amount)
    {
        var quantity = Quantities.Parse(text);
        Assert.NotNull(quantity);
        Assert.Equal(dimension, quantity.Value.Dimension);
        Assert.Equal(amount, quantity.Value.Amount, 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("some")]
    [InlineData("milk")]
    public void AnythingElseIsntAnAmount(string text) => Assert.Null(Quantities.Parse(text));

    [Fact]
    public void ShowsLiquidsAndWeightsEachInOneUnit()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Assert.Equal("3.79 L", Quantities.Format(Quantities.Parse("1 gal")!.Value, MeasureSystem.Metric));
        Assert.Equal("750 mL", Quantities.Format(Quantities.Parse("750 ml")!.Value, MeasureSystem.Metric));
        Assert.Equal("67.6 fl oz", Quantities.Format(Quantities.Parse("2 L")!.Value, MeasureSystem.Us));
        Assert.Equal("1.32 gal", Quantities.Format(Quantities.Parse("5 L")!.Value, MeasureSystem.Us));
        Assert.Equal("1.36 kg", Quantities.Format(Quantities.Parse("3 lb")!.Value, MeasureSystem.Metric));
        Assert.Equal("2.2 lb", Quantities.Format(Quantities.Parse("1 kg")!.Value, MeasureSystem.Us));
        Assert.Equal("8.8 oz", Quantities.Format(Quantities.Parse("250 g")!.Value, MeasureSystem.Us));
        Assert.Equal("1.1 lb", Quantities.Format(Quantities.Parse("500 g")!.Value, MeasureSystem.Us));
    }

    [Fact]
    public void DrinksShowInTheChosenUnit()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Assert.Equal("250 mL", Quantities.Drink(250, DrinkUnit.Millilitre));
        Assert.Equal("2.2 L", Quantities.Drink(2200, DrinkUnit.Millilitre));
        Assert.Equal("8½ fl oz", Quantities.Drink(250, DrinkUnit.FluidOunce));
        Assert.Equal("108 fl oz", Quantities.Drink(3200, DrinkUnit.FluidOunce));
        Assert.Equal("1 cup", Quantities.Drink(240, DrinkUnit.Cup));
        Assert.Equal(236.59, Quantities.ToMl(1, DrinkUnit.Cup), 2);
    }

    [Fact]
    public void PricesCompareByTheLitreOrKilogram()
    {
        var culture = CultureInfo.GetCultureInfo("en-CA");
        var milk = new GroceryItem { Name = "Milk", Quantity = "4 L", Price = 6.29m };
        Assert.Equal("$1.57/L", milk.UnitPrice(MeasureSystem.Metric, culture));
        var gallon = new GroceryItem { Name = "Milk", Quantity = "1 gallon", Price = 4.00m };
        Assert.Equal("$1.06/L", gallon.UnitPrice(MeasureSystem.Metric, culture));
        Assert.Equal("$4.00/gal", gallon.UnitPrice(MeasureSystem.Us, culture));
        var eggs = new GroceryItem { Name = "Eggs", Quantity = "a dozen", Price = 4.80m };
        Assert.Equal("$0.40 each", eggs.UnitPrice(MeasureSystem.Metric, culture));
        Assert.Equal("", new GroceryItem { Name = "Bread", Price = 3m }.UnitPrice(MeasureSystem.Metric, culture));
    }

    [Fact]
    public void KeepsTheListAndThePricesPaid()
    {
        using var database = new Database();
        var milk = new GroceryItem { Name = "Milk", Quantity = "4 L" };
        database.Save(milk);
        database.Save(new GroceryItem { Name = "Bread" });
        Assert.Equal(["Milk", "Bread"], database.Groceries().Select(g => g.Name));

        milk.Price = 6.29m;
        milk.Bought = new DateTime(2026, 10, 27, 17, 30, 0);
        database.Save(milk);
        // To buy first, then what's bought.
        Assert.Equal(["Bread", "Milk"], database.Groceries().Select(g => g.Name));

        database.ClearBought();
        Assert.Equal(["Bread"], database.Groceries().Select(g => g.Name));
        var last = database.LastBought("milk");
        Assert.NotNull(last);
        Assert.Equal((6.29m, "4 L"), (last.Price, last.Quantity));
        Assert.Contains("Milk", database.GroceryNames());
    }

    [Fact]
    public void MealIdeasArePlainFoodNearTheCalories()
    {
        var day = new DateOnly(2026, 10, 27);
        foreach (var slot in Enum.GetValues<MealSlot>())
        foreach (var kcal in slot == MealSlot.Snack ? new[] { 120.0, 200 } : [400, 600, 800])
        foreach (var offset in Enumerable.Range(0, 20))
        {
            var idea = Meals.Suggest(slot, kcal, day.AddDays(offset));
            Assert.NotEmpty(idea.Parts);
            Assert.InRange(idea.Kcal, kcal * 0.6, kcal * 1.4);
        }
    }

    [Fact]
    public void AMealIdeaReadsLikeAShoppingList()
    {
        var egg = new Food("egg", "eggs", 72, ["Eggs"]);
        var toast = new Food("slice of buttered toast", "slices of buttered toast", 110, ["Bread", "Butter"]);
        var juice = new Food("glass of orange juice", "glasses of orange juice", 110, ["Orange juice"]);
        var rice = new Food("cup of rice", "cups of rice", 205, ["Rice"], 0.5);
        var idea = new MealIdea([new MealPart(egg, 2), new MealPart(toast, 2), new MealPart(juice, 1)]);
        Assert.Equal("2 eggs, 2 slices of buttered toast and a glass of orange juice", idea.ToString());
        Assert.Equal(470, idea.Kcal);
        Assert.Equal(["Eggs", "Bread", "Butter", "Orange juice"], idea.Groceries);
        Assert.Equal("an egg", new MealPart(egg, 1).ToString());
        Assert.Equal("½ cup of rice", new MealPart(rice, 0.5).ToString());
        Assert.Equal("1½ cups of rice", new MealPart(rice, 1.5).ToString());
    }

    [Fact]
    public void TheSameDayGivesTheSameIdea()
    {
        var day = new DateOnly(2026, 10, 27);
        Assert.Equal(Meals.Suggest(MealSlot.Lunch, 600, day).ToString(), Meals.Suggest(MealSlot.Lunch, 600, day).ToString());
        var ideas = Enumerable.Range(0, 10).Select(i => Meals.Suggest(MealSlot.Lunch, 600, day, i).ToString()).Distinct();
        Assert.True(ideas.Count() > 3);
    }
}
