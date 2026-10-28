namespace Strongthium.HealthReminder.Tests;

public sealed class StockTests
{
    private static readonly DateTime Monday = new(2026, 10, 26, 7, 0, 0);

    private static Reminder Pills(Database database, double count)
    {
        var reminder = new Reminder
        {
            Kind = ReminderKind.Pill, Name = "Adalat XL 30 mg", Unit = "tablet", Created = Monday.AddDays(-1),
            Times = [new ReminderTime(new TimeOnly(8, 0), 1), new ReminderTime(new TimeOnly(20, 0), 1)],
            StockCount = count, StockAt = Monday,
        };
        database.Save(reminder);
        return reminder;
    }

    [Fact]
    public void OnlyDosesMarkedTakenComeOffTheCount()
    {
        using var database = new Database();
        var schedule = new Schedule(database);
        var pills = Pills(database, 30);
        schedule.Done(new Dose(pills, Monday.Date.AddHours(8)), Monday.Date.AddHours(8.1));
        schedule.Skipped(new Dose(pills, Monday.Date.AddHours(20)), Monday.Date.AddHours(20.2));
        // Tuesday morning's isn't answered at all.
        var supply = database.Supply(pills)!;
        Assert.Equal(29, supply.OnHand);
        Assert.Equal(2, supply.DailyUse);
        Assert.Equal(14, supply.DaysLeft);
        Assert.False(supply.Low);
    }

    [Fact]
    public void RefillsAddToTheCount()
    {
        using var database = new Database();
        var pills = Pills(database, 6);
        Assert.True(database.Supply(pills)!.Low);
        database.AddRefill(pills.Id, 60, Monday.AddDays(1));
        database.AddRefill(pills.Id, 60, Monday.AddDays(-5));   // before the count: already in it
        var supply = database.Supply(pills)!;
        Assert.Equal(66, supply.OnHand);
        Assert.False(supply.Low);
        Assert.Equal(2, database.Refills(pills.Id).Count);
    }

    [Fact]
    public void WithoutACountThereIsNoSupply()
    {
        using var database = new Database();
        var pills = Pills(database, 0);
        pills.StockCount = null;
        pills.StockAt = null;
        database.Save(pills);
        Assert.Null(database.Supply(database.Reminders().Single()));
    }

    [Fact]
    public void ADoseAfterAMissedOneIsKnown()
    {
        using var database = new Database();
        var schedule = new Schedule(database);
        var pills = Pills(database, 30);
        var evening = new Dose(pills, Monday.Date.AddHours(20));
        var morning = new Dose(pills, Monday.Date.AddDays(1).AddHours(8));
        // Monday morning's wasn't answered: the evening's follows a missed dose.
        Assert.Equal(Monday.Date.AddHours(8), schedule.MissedBefore(evening)!.Due);
        schedule.Done(evening, evening.Due);
        Assert.Null(schedule.MissedBefore(morning));
        // A skipped dose isn't a missed one.
        schedule.Skipped(morning, morning.Due);
        Assert.Null(schedule.MissedBefore(new Dose(pills, Monday.Date.AddDays(1).AddHours(20))));
    }

    [Theory]
    [InlineData("Eggs", "a dozen", 12, "egg")]
    [InlineData("Eggs", "6", 6, "egg")]
    [InlineData("Bread", "1 loaf", 20, "slice")]
    [InlineData("Bread", "", 20, "slice")]
    [InlineData("Chicken breasts", "1 pack", 4, "breast")]
    [InlineData("Chicken breasts", "2", 2, "breast")]
    [InlineData("Milk", "4 L", 4000, "mL")]
    [InlineData("Ground beef", "1 lb", 453.59, "g")]
    [InlineData("Ground beef", "2", 908, "g")]
    [InlineData("Leftover curry", "2 bowls", 2, "")]
    [InlineData("Olive oil", "500 mL", 500, "mL")]
    public void AmountsAreKeptInTheirOwnUnit(string name, string quantity, double amount, string unit)
    {
        var (a, u) = Pantry.Amount(name, quantity);
        Assert.Equal(amount, a, 2);
        Assert.Equal(unit, u);
    }

    [Fact]
    public void BuyingAddsAndCountingSets()
    {
        var eggs = Pantry.Add(null, "Eggs", "a dozen", Monday);
        eggs = Pantry.Add(eggs, "Eggs", "6", Monday);
        Assert.Equal(18, eggs.Amount);
        var counted = Pantry.Set("Eggs", "4", Monday.AddDays(3), existing: eggs);
        Assert.Equal((4.0, Monday.AddDays(3)), (counted.Amount, counted.Counted));
        Assert.False(Pantry.Tracked("Cooking oil"));
    }

    [Fact]
    public void CookingTakesOffWhatTheRecipeUses()
    {
        var pantry = new List<PantryItem>
        {
            new("Eggs", 12, "egg", null, Monday, false),
            new("Bread", 20, "slice", null, Monday, false),
            new("Butter", 454, "g", null, Monday, false),
            new("Milk", 10, "mL", null, Monday, false),
        };
        var left = Pantry.Cook(pantry, Recipes.Get("scrambled-eggs")!).ToDictionary(p => p.Name);
        Assert.Equal(10, left["Eggs"].Amount);
        Assert.Equal(18, left["Bread"].Amount);
        Assert.Equal(449, left["Butter"].Amount);
        // 15 mL of milk from 10: none left, and the count is a guess now.
        Assert.Equal((0.0, true), (left["Milk"].Amount, left["Milk"].Unsure));
    }

    [Fact]
    public void ClaudesIdeasComeOffByName()
    {
        var pantry = new List<PantryItem> { new("Eggs", 12, "egg", null, Monday, false), new("Spinach", 142, "g", null, Monday, false) };
        var idea = new KitchenIdea("Spinach omelette", "", 420, 5, 6, [], [], [new("3", "large eggs, beaten"), new("a handful of", "spinach")], []);
        var left = Pantry.Cook(pantry, idea.ToRecipe(MealSlot.Breakfast)).ToDictionary(p => p.Name);
        Assert.Equal(9, left["Eggs"].Amount);
        // "a handful of" isn't grams: the spinach is left to be checked.
        Assert.True(left["Spinach"].Unsure);
    }

    [Fact]
    public void WhatsInDoubtIsAGuessOrLongUncounted()
    {
        var now = Monday.AddDays(20);
        var pantry = new List<PantryItem>
        {
            new("Eggs", 6, "egg", null, now.AddDays(-1), false),
            new("Rice", 1500, "g", null, Monday, false),            // 20 days uncounted
            new("Spinach", 70, "g", null, now, true),               // from a photo
            new("Flour", 0, "g", null, Monday, false),              // none, nothing to count
        };
        Assert.Equal(["Rice", "Spinach"], Pantry.InDoubt(pantry, now).Select(p => p.Name));
    }

    [Fact]
    public void KeepsTheStock()
    {
        using var database = new Database();
        database.Save(new PantryItem("Eggs", 12, "egg", null, Monday, false));
        database.Save(new PantryItem("eggs", 8, "egg", null, Monday, true));     // the same, by name
        var eggs = Assert.Single(database.Pantry());
        Assert.Equal((8.0, true), (eggs.Amount, eggs.Unsure));
        database.DeletePantry("Eggs");
        Assert.Empty(database.Pantry());
    }
}
