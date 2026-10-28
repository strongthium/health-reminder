namespace Strongthium.HealthReminder.Tests;

public sealed class KitchenTests
{
    private static readonly TimeOnly Wake = new(7, 0), Sleep = new(23, 0);
    private static readonly DateTime Dinner = new(2026, 10, 28, 18, 0, 0);

    private static PlannedMeal Meal(string recipe, DateTime? decided = null) =>
        new("Dinner", Dinner, MealSlot.Dinner, Recipes.Get(recipe), Decided: decided);

    [Fact]
    public void ChickenThawsOvernightInTheFridge()
    {
        // 35 minutes to make: start at 17:25. 12 hours in the fridge would be 5:25, in the night: before bed instead.
        var events = Kitchen.Events(Meal("chicken-rice"), ThawMethod.Fridge, Wake, Sleep).ToList();
        Assert.Equal(
            [(new DateTime(2026, 10, 27, 22, 30, 0), KitchenStep.TakeOut), (new DateTime(2026, 10, 28, 10, 30, 0), KitchenStep.Thawed),
             (new DateTime(2026, 10, 28, 17, 25, 0), KitchenStep.Prep), (new DateTime(2026, 10, 28, 17, 35, 0), KitchenStep.Cook)],
            events.Select(e => (e.At, e.Step)));
        Assert.Contains("chicken breast", events[0].Title);
        Assert.Contains("fridge", events[0].Detail);
    }

    [Fact]
    public void ColdWaterTakesAnHourAndAChange()
    {
        var events = Kitchen.Events(Meal("chicken-rice"), ThawMethod.ColdWater, Wake, Sleep).ToList();
        Assert.Equal(
            [(new DateTime(2026, 10, 28, 16, 25, 0), KitchenStep.TakeOut), (new DateTime(2026, 10, 28, 16, 55, 0), KitchenStep.ChangeWater),
             (new DateTime(2026, 10, 28, 17, 25, 0), KitchenStep.Prep), (new DateTime(2026, 10, 28, 17, 35, 0), KitchenStep.Cook)],
            events.Select(e => (e.At, e.Step)));
        Assert.StartsWith("Thawed", events[2].Title);
    }

    [Fact]
    public void PickedTooLateForTheFridgeItThawsInWater()
    {
        // Picked at 3 PM for 6 PM: there's no time for the fridge.
        var events = Kitchen.Events(Meal("steak", decided: new DateTime(2026, 10, 28, 15, 0, 0)), ThawMethod.Fridge, Wake, Sleep).ToList();
        Assert.Equal(ThawMethod.ColdWater, events[0].Thaw);
        Assert.Contains(events, e => e.Step == KitchenStep.ChangeWater);
    }

    [Fact]
    public void NothingFrozenNothingToThaw()
    {
        var events = Kitchen.Events(Meal("burger"), ThawMethod.Fridge, Wake, Sleep).ToList();
        Assert.DoesNotContain(events, e => e.Step is KitchenStep.TakeOut or KitchenStep.Thawed);
        Assert.Contains(events, e => e.Step == KitchenStep.Prep);
    }

    [Fact]
    public void DecidingOnTheDayAsksInTheMorning()
    {
        var meal = new PlannedMeal("Dinner", Dinner, MealSlot.Dinner, null, DecideOnTheDay: true);
        var ask = Assert.Single(Kitchen.Events(meal, ThawMethod.Fridge, Wake, Sleep));
        Assert.Equal((new DateTime(2026, 10, 28, 7, 15, 0), KitchenStep.Choose), (ask.At, ask.Step));
        Assert.Empty(Kitchen.Events(meal with { DecideOnTheDay = false }, ThawMethod.Fridge, Wake, Sleep));
    }

    [Fact]
    public void FeelingLikeSteakFindsSteak()
    {
        var beef = Recipes.For(MealSlot.Dinner, Feel.Beef).ToList();
        Assert.Contains(beef, r => r.Id == "steak");
        Assert.All(beef, r => Assert.Contains(Feel.Beef, r.Feels));
        Assert.All(Recipes.For(MealSlot.Breakfast), r => Assert.Contains(MealSlot.Breakfast, r.Slots));
    }

    [Fact]
    public void EveryIngredientIsInTheCatalog()
    {
        var missing = Recipes.All.SelectMany(r => r.Ingredients).Select(i => i.Grocery).Distinct().Where(g => GroceryCatalog.Get(g) is null).ToList();
        Assert.Empty(missing);
        Assert.All(Recipes.All.SelectMany(r => r.Ingredients), i => Assert.True(GroceryCatalog.Get(i.Grocery)!.Staple || i.Use > 0, i.Name));
        Assert.Equal(Recipes.All.Count, Recipes.All.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void ShoppingIsInWholePacksAndSaysWhatWontKeep()
    {
        // Three chicken dinners: 3 breasts of a pack of 4; salad greens for one salad, of a box.
        var week = new[] { Recipes.Get("chicken-rice")!, Recipes.Get("stir-fry")!, Recipes.Get("chicken-salad")! };
        var lines = GroceryCatalog.Shopping(week);
        var chicken = lines.Single(l => l.Info.Name == "Chicken breasts");
        Assert.Equal((3.0, 1), (chicken.Needed, chicken.Packs));
        var rice = lines.Single(l => l.Info.Name == "Rice");
        Assert.Equal((190.0, 1), (rice.Needed, rice.Packs));
        var greens = lines.Single(l => l.Info.Name == "Salad greens");
        Assert.Contains("keeps about 4 days", greens.Note);
        Assert.Equal("keep some on hand", lines.Single(l => l.Info.Name == "Cooking oil").Note);
    }

    [Fact]
    public void ALeafOfLettuceIsALittle()
    {
        var lettuce = GroceryCatalog.Get("Lettuce")!;
        Assert.Equal("a little", GroceryCatalog.Amount(0.1, lettuce));
        Assert.Equal("½ head", GroceryCatalog.Amount(0.5, lettuce));
        Assert.Equal("¾ can", GroceryCatalog.Amount(0.75, GroceryCatalog.Get("Canned beans")!));
    }

    [Fact]
    public void WhatWontKeepIsToUseSoon()
    {
        var now = new DateTime(2026, 10, 28, 12, 0, 0);
        var bought = new[]
        {
            new GroceryItem { Name = "Salad greens", Bought = now.AddDays(-2) },   // keeps 4 days: by the 30th
            new GroceryItem { Name = "Rice", Bought = now.AddDays(-2) },           // keeps
            new GroceryItem { Name = "Bananas", Bought = now.AddDays(-20) },       // long gone
        };
        var soon = Assert.Single(GroceryCatalog.UseSoon(bought, now));
        Assert.Equal(("Salad greens", new DateTime(2026, 10, 30)), (soon.Info.Name, soon.UseBy));
    }

    [Fact]
    public void KeepsTheMenuAndTheDaysPicks()
    {
        using var database = new Database();
        database.SetMenu(DayOfWeek.Wednesday, "Dinner", "steak");
        database.SetMenu(DayOfWeek.Thursday, "Dinner", "decide");
        Assert.Equal("steak", database.Menu()[(DayOfWeek.Wednesday, "Dinner")].Recipe);
        database.SetMenu(DayOfWeek.Wednesday, "Dinner", null);
        Assert.False(database.Menu().ContainsKey((DayOfWeek.Wednesday, "Dinner")));

        var day = new DateOnly(2026, 10, 29);
        Assert.Null(database.Choice(day, "Dinner"));
        database.SetChoice(day, "Dinner", "salmon");
        Assert.Equal("salmon", database.Choice(day, "Dinner")!.Value.Recipe);

        database.SetKitchenDone("2026-10-29T18:00|Dinner|Prep");
        Assert.Contains("2026-10-29T18:00|Dinner|Prep", database.KitchenDone(new DateTime(2026, 10, 29)));
        Assert.Empty(database.KitchenDone(new DateTime(2026, 10, 30)));
    }

    [Fact]
    public void PluralsOfThingsBoughtByTheOne()
    {
        Assert.Equal("potatoes", Amounts.Plural("potato"));
        Assert.Equal("patties", Amounts.Plural("patty"));
        Assert.Equal("sprays", Amounts.Plural("spray"));
    }
}
