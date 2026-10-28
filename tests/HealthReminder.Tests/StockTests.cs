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
    public void BestBeforeIsFromWhenItWasAdded()
    {
        var chicken = Pantry.Add(null, "Chicken breasts", "4", Monday);
        Assert.Equal(Monday.Date.AddDays(2), chicken.UseBy);
        Assert.Equal(Monday.Date.AddDays(28), Pantry.Add(null, "Eggs", "12", Monday).UseBy);
        Assert.Null(Pantry.Add(null, "Rice", "2 kg", Monday).UseBy);
        // Counting it later doesn't move it.
        Assert.Equal(Monday.Date.AddDays(2), Pantry.Set("Chicken breasts", "3", Monday.AddDays(1), existing: chicken).UseBy);
        // A photo's date is its own.
        Assert.Equal((Monday.Date.AddDays(5), true), (Pantry.Add(null, "Spinach", "142 g", Monday, useWithinDays: 5).UseBy, Pantry.Add(null, "Spinach", "142 g", Monday, useWithinDays: 5).UseBySet));
    }

    [Fact]
    public void FrozenItKeepsFromWhenItWentIn()
    {
        var chicken = Pantry.Freeze(Pantry.Add(null, "Chicken breasts", "4", Monday), Monday.AddDays(1));
        Assert.True(chicken.InFreezer);
        Assert.Equal(Monday.Date.AddDays(1 + 274), chicken.UseBy);    // 9 months
        // Thawed, it keeps as fresh chicken does: 2 days in the fridge, from then.
        var thawed = Pantry.Thaw(chicken, Monday.AddDays(30));
        Assert.Equal((false, Monday.Date.AddDays(32)), (thawed.InFreezer, thawed.UseBy));
        // Bought frozen, it goes in the freezer; out of it, a few days.
        var broccoli = Pantry.Add(null, "Frozen broccoli", "500 g", Monday);
        Assert.Equal((true, Monday.Date.AddDays(243)), (broccoli.InFreezer, broccoli.UseBy));
        Assert.Equal(Monday.Date.AddDays(13), Pantry.Thaw(broccoli, Monday.AddDays(10)).UseBy);
        // What doesn't freeze well keeps its date; what the catalog doesn't know, about 3 months.
        Assert.Equal(Monday.Date.AddDays(10), Pantry.Freeze(Pantry.Add(null, "Lettuce", "1", Monday), Monday).UseBy);
        Assert.Equal(Monday.Date.AddDays(91), Pantry.Freeze(Pantry.Add(null, "Leftover chili", "1", Monday), Monday).UseBy);
        Assert.Equal("doesn't freeze well", Pantry.FreezerNote("Lettuce"));
    }

    [Fact]
    public void ADateSetByHandStaysUntilPutBack()
    {
        var eggs = Pantry.SetUseBy(Pantry.Add(null, "Eggs", "12", Monday), Monday.AddDays(40));
        Assert.Equal(Monday.Date.AddDays(40), Pantry.Refresh(eggs, new ColdStorage(10, -18)).UseBy);
        Assert.Equal(Monday.Date.AddDays(28), Pantry.UsualUseBy(eggs).UseBy);
    }

    [Fact]
    public void AWarmFridgeOrFreezerShortensTheDates()
    {
        Assert.Equal((40.0, 0.0), (Math.Round(ColdStorage.ToF(ColdStorage.StandardFridgeC), 6), Math.Round(ColdStorage.ToF(ColdStorage.StandardFreezerC), 6)));
        Assert.Empty(ColdStorage.Standard.Warnings(fahrenheit: true));
        var eggs = Pantry.Add(null, "Eggs", "12", Monday);
        var warm = new ColdStorage(ColdStorage.StandardFridgeC + 5, ColdStorage.StandardFreezerC + 4);
        Assert.Equal(Monday.Date.AddDays(14), Pantry.Refresh(eggs, warm).UseBy);
        Assert.Equal(Monday.Date.AddDays(137), Pantry.Freeze(Pantry.Add(null, "Chicken breasts", "4", Monday), Monday, warm).UseBy);
        // Colder doesn't keep food longer.
        Assert.Equal(Monday.Date.AddDays(28), Pantry.Refresh(eggs, new ColdStorage(1, -25)).UseBy);
        Assert.Equal(2, warm.Warnings(fahrenheit: false).Count);
        Assert.Contains("too warm to keep food frozen solid", new ColdStorage(4, -8).Warnings(fahrenheit: false)[0]);
        Assert.Contains("40 °F", new ColdStorage(8, -18).Warnings(fahrenheit: true)[0]);
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
        Assert.Equal((Monday, (DateTime?)null, false), (eggs.Since, eggs.Frozen, eggs.UseBySet));
        database.DeletePantry("Eggs");
        Assert.Empty(database.Pantry());

        var chicken = Pantry.SetUseBy(Pantry.Freeze(Pantry.Add(null, "Chicken breasts", "4", Monday), Monday.AddDays(1)), Monday.AddDays(200));
        database.Save(chicken);
        var read = Assert.Single(database.Pantry());
        Assert.Equal((chicken.Added, chicken.Frozen, chicken.UseBy, true), (read.Added, read.Frozen, read.UseBy, read.UseBySet));
    }

    [Fact]
    public void TheDatesKeptBeforeTheFreezerStayAsTheyWere()
    {
        var folder = Path.Combine(Path.GetTempPath(), "health-reminder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // The stock as version 6 kept it.
            using (var old = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(folder, "health-reminder.db")};Pooling=False"))
            {
                old.Open();
                using var command = old.CreateCommand();
                command.CommandText = """
                    CREATE TABLE pantry (name TEXT PRIMARY KEY COLLATE NOCASE, amount REAL NOT NULL, unit TEXT NOT NULL DEFAULT '', use_by TEXT,
                        counted TEXT NOT NULL, unsure INTEGER NOT NULL DEFAULT 0);
                    INSERT INTO pantry VALUES ('Spinach', 80, 'g', '2026-10-28T00:00:00', '2026-10-26T07:00:00', 1);
                    INSERT INTO pantry VALUES ('Rice', 1650, 'g', NULL, '2026-10-06T07:00:00', 0);
                    PRAGMA user_version = 6;
                    """;
                command.ExecuteNonQuery();
            }
            using var database = new Database(folder);
            var pantry = database.Pantry();
            var spinach = pantry.Single(p => p.Name == "Spinach");
            Assert.Equal((new DateTime(2026, 10, 28), true, Monday), (spinach.UseBy!.Value, spinach.UseBySet, spinach.Since));
            Assert.False(pantry.Single(p => p.Name == "Rice").UseBySet);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }
}
