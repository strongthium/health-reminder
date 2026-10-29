namespace Strongthium.HealthReminder.Tests;

/// <summary>The calories of what's eaten, the food log, and the days in numbers with what stands out.</summary>
public sealed class StatsAndFoodTests : IDisposable
{
    // A Wednesday, mid-afternoon.
    private static readonly DateTime Now = new(2026, 10, 28, 15, 0, 0);
    private readonly Database database = new();

    public void Dispose() => database.Dispose();

    private static PantryItem Item(string name, string quantity, Packing? packing = null) =>
        Pantry.Add(null, name, quantity, Now.AddDays(-1)) with { Packing = packing };

    [Theory]
    [InlineData("Eggs", 143, 72)]
    [InlineData("Diet Pepsi", 0, 0)]
    [InlineData("Coke", 42, 150)]
    [InlineData("Peanut butter", 590, 0)]
    [InlineData("Milk (Homogenized)", 50, 0)]
    [InlineData("Chicken strips", 250, 0)]
    [InlineData("Blueberry muffins", 380, 400)]
    [InlineData("Leftover chili", 110, 300)]
    public void AFoodsCaloriesAreReadFromItsName(string name, double per100, double piece)
    {
        var energy = Calories.Of(name);
        Assert.NotNull(energy);
        Assert.Equal((per100, piece), (energy.Per100, energy.Piece));
    }

    [Fact]
    public void ANameThatSaysNothingKnownHasNoCalories() => Assert.Null(Calories.Of("Mystery tin"));

    [Fact]
    public void EatingSomeOfWhatsCountedOrWeighedComesOffTheStock()
    {
        var eggs = Item("Eggs", "12");
        var two = Calories.Eat(eggs, "2");
        Assert.Equal(new Portion(145, 2, true), two);
        Assert.Equal(10, Calories.Left(eggs, two).Amount);

        // A serving of what's weighed, or a weight of it.
        var rice = Item("Rice", "2 kg");
        Assert.Equal("60 g", Calories.UsualAmount(rice));
        Assert.Equal(new Portion(215, 60, true), Calories.Eat(rice, "60 g"));
        Assert.Equal(1880, Calories.Left(rice, Calories.Eat(rice, "2")).Amount);

        // A piece's weight, when it's set, gives what a piece has: 2 pies of 200 g.
        var pies = Item("Pies", "2", new Packing(1, "box", 200));
        Assert.Equal(520, Calories.Eat(pies, "1").Kcal);
        Assert.Equal(new Portion(260, 0.5, true), Calories.Eat(pies, "100 g"));
    }

    [Fact]
    public void WhatCantBeToldIsLeftInDoubt()
    {
        // A weight of something counted, with no weight for a piece: the kcal are known, what comes off isn't.
        var muffins = Item("Blueberry muffins", "6");
        var portion = Calories.Eat(muffins, "50 g");
        Assert.Equal((190, 0.0, false), (portion.Kcal, portion.Used, portion.Read));
        var left = Calories.Left(muffins, portion);
        Assert.Equal(6, left.Amount);
        Assert.True(left.Unsure);
        // More eaten than there was.
        Assert.True(Calories.Left(Item("Eggs", "2"), Calories.Eat(Item("Eggs", "2"), "3")).Unsure);
    }

    [Fact]
    public void TheFoodLogIsKept()
    {
        var saved = database.Save(new FoodEaten(0, Now, "Cheeseburger", "2", 600, Meal: "3@2026-10-28T12:00"));
        database.Save(new FoodEaten(0, Now.AddHours(1), "Eggs", "2", 145, "Eggs", 2, "egg"));
        Assert.True(saved.Id > 0);
        var log = database.FoodLog(Now.Date);
        Assert.Equal(2, log.Count);
        Assert.Equal(saved, log[0]);
        Assert.Equal(("Eggs", 2.0, "egg", (string?)null), (log[1].Pantry, log[1].Used, log[1].Unit, log[1].Meal));
        database.Delete(saved);
        Assert.Single(database.FoodLog(Now.Date));
        Assert.Empty(database.FoodLog(Now.Date.AddDays(1)));
    }

    private Reminder Add(string name, ReminderKind kind, string times, string unit)
    {
        var reminder = new Reminder { Kind = kind, Name = name, Unit = unit, Times = Database.ParseTimes(times), Created = Now.AddDays(-30) };
        database.Save(reminder);
        return reminder;
    }

    [Fact]
    public void ADayCountsWhatWasDoneMissedSmokedDrunkAndEaten()
    {
        var pill = Add("Examplopril", ReminderKind.Pill, "08:00*1;20:00*1", "tablet");
        var water = Add("Water", ReminderKind.Drink, "09:00*250;11:00*250;13:00*250", "mL");
        var lunch = Add("Lunch", ReminderKind.Meal, "12:00*600", "kcal");
        var schedule = new Schedule(database);
        var yesterday = Now.Date.AddDays(-1);
        // Yesterday: the morning pill taken, the evening one missed; two waters; lunch eaten in place of a cheeseburger.
        schedule.Done(new Dose(pill, yesterday.AddHours(8)), yesterday.AddHours(8));
        schedule.Done(new Dose(water, yesterday.AddHours(9)), yesterday.AddHours(9));
        schedule.Done(new Dose(water, yesterday.AddHours(11)), yesterday.AddHours(11));
        schedule.Skipped(new Dose(water, yesterday.AddHours(13)), yesterday.AddHours(13));
        schedule.Done(new Dose(lunch, yesterday.AddHours(12)), yesterday.AddHours(12));
        var lunchKey = new Dose(lunch, yesterday.AddHours(12)).Key;
        List<FoodEaten> food = [new(1, yesterday.AddHours(12), "Cheeseburger", "2", 600, Meal: lunchKey), new(2, yesterday.AddHours(16), "Apple", "1", 95)];
        // Today: lunch eaten as planned.
        schedule.Done(new Dose(lunch, Now.Date.AddHours(12)), Now.Date.AddHours(12));
        List<Smoke> smokes = [new() { Started = yesterday.AddHours(9) }, new() { Started = yesterday.AddHours(10) }, new() { Started = yesterday.AddHours(12) }];

        var doses = schedule.Days(database.Reminders(), yesterday, Now);
        var days = DailyStats.Days(doses, smokes, food, DailyStats.PlannedKcal, DateOnly.FromDateTime(yesterday), Now);
        Assert.Equal(2, days.Count);
        var y = days[0];
        Assert.Equal((4, 1, 1, 0), (y.Done, y.Skipped, y.Missed.Count, y.ToCome));
        Assert.Equal("Examplopril", y.Missed[0].Reminder.Name);
        Assert.Equal((3, TimeSpan.FromHours(1.5)), (y.Smokes, y.Between));
        Assert.Equal((2, 3, 500.0), (y.Drinks, y.DrinksPlanned, y.DrankMl));
        // The cheeseburgers stand for lunch, and the apple is on its own.
        Assert.Equal((695, 600, true), (y.Kcal, y.KcalPlanned, y.FoodLogged));

        var t = days[1];
        // The morning pill is still due; the 9:00 and 11:00 waters are missed, as the next was due.
        Assert.Equal((1, 0, 2, 3), (t.Done, t.Skipped, t.Missed.Count, t.ToCome));
        Assert.Equal(600, t.Kcal);
        Assert.Null(t.Between);
    }

    private static List<DayTally> Quiet(int days, Func<int, int> smokes, Func<int, TimeSpan?>? between = null) =>
        Enumerable.Range(0, days).Select(i => new DayTally(DateOnly.FromDateTime(Now).AddDays(i - days + 1), 5, 0, [], 0, smokes(days - 1 - i),
                between?.Invoke(days - 1 - i), 0, 0, 0, 0, 0, false))
            .ToList();

    private static List<Smoke> Every(int days, int perDay, int today, double firstHour = 7)
    {
        var smokes = new List<Smoke>();
        for (var back = days; back >= 1; back--)
            for (var i = 0; i < perDay; i++)
                smokes.Add(new Smoke { Started = Now.Date.AddDays(-back).AddHours(firstHour + i * 16.0 / perDay) });
        for (var i = 0; i < today; i++) smokes.Add(new Smoke { Started = Now.Date.AddHours(firstHour + i * 0.5) });
        return smokes;
    }

    [Fact]
    public void MoreCigarettesThanUsualByThisTimeIsSaid()
    {
        // 16 a day, one an hour from 7:00: by 15:00, 8 usually. Today, 14 by then.
        var smokes = Every(9, 16, 14);
        Assert.Equal(8, DailyStats.UsualByThisTime(smokes, Now));
        Assert.Equal(14, DailyStats.ByThisTime(smokes, Now, 0));
        Assert.Equal(8, DailyStats.ByThisTime(smokes, Now, 1));
        var days = Quiet(10, back => back == 0 ? 14 : 16, back => TimeSpan.FromHours(back == 0 ? 0.5 : 1));
        var notes = DailyStats.Notes(days, smokes, Now);
        var note = Assert.Single(notes, n => n.Text.Contains("more than usual by this time"));
        Assert.Equal(NoteTone.Warning, note.Tone);
        Assert.Contains("14 cigarettes by", note.Text);
        // Closer together: every half hour, against an hour.
        Assert.Contains(notes, n => n.Text.StartsWith("Closer together"));

        // And fewer is good news.
        var fewer = DailyStats.Notes(Quiet(10, back => back == 0 ? 3 : 16), Every(9, 16, 3), Now);
        Assert.Contains(fewer, n => n.Tone == NoteTone.Good && n.Text.Contains("5 fewer than usual"));
    }

    [Fact]
    public void AQuietDayHasNothingToSay()
    {
        var notes = DailyStats.Notes(Quiet(10, back => back == 0 ? 8 : 16), Every(9, 16, 8), Now);
        Assert.Empty(notes);
    }

    [Fact]
    public void MissingMoreThanUsualIsSaid()
    {
        var pill = new Reminder { Id = 1, Kind = ReminderKind.Pill, Name = "Examplopril" };
        var water = new Reminder { Id = 2, Kind = ReminderKind.Drink, Name = "Water" };
        var days = Quiet(10, _ => 0);
        // Yesterday 4 of 9 missed; today the morning pill.
        days[^2] = days[^2] with { Missed = [.. Enumerable.Range(0, 4).Select(i => new Dose(water, Now.Date.AddDays(-1).AddHours(9 + i)))] };
        days[^1] = days[^1] with { Missed = [new Dose(pill, Now.Date.AddHours(8))] };
        var notes = DailyStats.Notes(days, [], Now);
        Assert.Equal(NoteTone.Warning, notes[0].Tone);
        Assert.Contains(notes, n => n.Text.StartsWith("Medication missed today: Examplopril at "));
        Assert.Contains(notes, n => n.Text.StartsWith("Yesterday 4 of 9 were missed, more than usual (about 0 a day)"));
    }

    [Fact]
    public void TheTimeBetweenIsFromTheFirstToTheLast() =>
        Assert.Equal(TimeSpan.FromMinutes(40), DailyStats.AverageGap([Now.AddHours(-2), Now, Now.AddMinutes(-80), Now.AddMinutes(-20)]));
}
