namespace Strongthium.HealthReminder.Tests;

public sealed class TrackingTests : IDisposable
{
    // A Wednesday.
    private static readonly DateTime Today = new(2026, 10, 28);
    private readonly Database database = new();

    public void Dispose() => database.Dispose();

    private Reminder Add(string name, ReminderKind kind, string times = "08:00*1")
    {
        var reminder = new Reminder
        {
            Kind = kind,
            Name = name,
            Unit = kind.DefaultUnit(),
            Times = Database.ParseTimes(times),
            Days = DaySet.Every,
            Created = Today.AddDays(-30),
            Appearance = new PillAppearance(["WHITE"], "ROUND", 8, "EX;10", 2),
        };
        database.Save(reminder);
        return reminder;
    }

    private static int Missed(Schedule schedule, List<Reminder> all, DateTime first, DateTime now) =>
        schedule.Days(all, first, now).Count(d => d.State == DoseState.Missed);

    [Fact]
    public void AResetCountsFromThereOn()
    {
        Add("Water", ReminderKind.Drink);
        var pill = Add("Examplopril", ReminderKind.Pill);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        var now = Today.AddHours(21);
        schedule.Done(new Dose(pill, Today.AddDays(-3).AddHours(8)), Today.AddDays(-3).AddHours(8));
        Assert.True(Missed(schedule, all, Today.AddDays(-7), now) > 0);

        schedule.Tracking.Reset(now);
        // Nothing before it is missed, or counted; and it's still there after the program starts again.
        Assert.DoesNotContain(new Schedule(database).Days(all, Today.AddDays(-7), now), d => d.Dose.Due < Today);
        Assert.Equal(0, Missed(schedule, all, Today.AddDays(-7), now));
        Assert.Equal(Today, new Schedule(database).Tracking.Counted(Today.AddDays(-7)));
        Assert.Equal(Today.AddDays(1), new Schedule(database).Tracking.Counted(Today.AddDays(1)));

        // After it, what's unanswered is missed as usual.
        var later = Today.AddDays(1).AddHours(21);
        Assert.Equal(2, Missed(schedule, all, Today, later));
    }

    [Fact]
    public void AResetDeletesNothing()
    {
        var pill = Add("Examplopril", ReminderKind.Pill);
        var schedule = new Schedule(database);
        (pill.StockCount, pill.StockAt) = (30, Today.AddDays(-5));
        database.Save(pill);
        var all = database.Reminders();
        schedule.Done(new Dose(pill, Today.AddDays(-3).AddHours(8)), Today.AddDays(-3).AddHours(8));
        database.Save(new FoodEaten(0, Today.AddDays(-2), "Toast", "1", 80));

        schedule.StopTracking(all, Today.AddHours(21), keepMedication: true, reset: true);

        // What's on hand still counts the dose taken, and the History and the log still have what they had.
        Assert.Equal(29, database.Supply(pill)!.OnHand);
        Assert.Single(database.History(Today.AddDays(-7)));
        Assert.Single(database.FoodLog(Today.AddDays(-7)));

        schedule.Tracking.UndoReset();
        Assert.Null(new Schedule(database).Tracking.From);
    }

    [Fact]
    public void WhatWasTakenTodayStillShowsAfterAReset()
    {
        var pill = Add("Examplopril", ReminderKind.Pill, "08:00*1;20:00*1");
        var schedule = new Schedule(database);
        var all = database.Reminders();
        schedule.Done(new Dose(pill, Today.AddHours(8)), Today.AddHours(8));
        schedule.Tracking.Reset(Today.AddHours(12));
        Assert.Contains(schedule.Day(all, Today.AddHours(12)), d => d.State == DoseState.Taken);
        Assert.Contains(schedule.Day(all, Today.AddHours(12)), d => d.State == DoseState.Upcoming);
    }

    [Fact]
    public void StoppedNothingRingsButMedicationIfKept()
    {
        Add("Water", ReminderKind.Drink);
        Add("Blood sugar", ReminderKind.BloodSugar);
        Add("Examplopril", ReminderKind.Pill);
        var schedule = new Schedule(database);
        var all = database.Reminders();

        schedule.StopTracking(all, Today.AddHours(7), keepMedication: true, reset: false);
        Assert.Equal("Examplopril", Assert.Single(schedule.Due(all, Today.AddHours(8))).Reminder.Name);
        Assert.Equal("Examplopril", Assert.Single(schedule.Day(all, Today.AddHours(8))).Dose.Reminder.Name);
        Assert.Equal("Examplopril", schedule.NextRinging(all, Today.AddHours(7))!.Reminder.Name);

        // Still stopped after the program starts again; and off for medication too.
        schedule.Tracking.Start(Today.AddHours(7));
        schedule.StopTracking(all, Today.AddHours(7), keepMedication: false, reset: false);
        var again = new Schedule(database);
        Assert.True(again.Tracking.Stopped);
        Assert.Empty(again.Due(all, Today.AddHours(8)));
        Assert.Empty(again.Day(all, Today.AddHours(8)));
        Assert.Null(again.NextRinging(all, Today.AddHours(7)));
    }

    [Fact]
    public void NothingUnansweredCountsAsMissedWhileStopped()
    {
        Add("Water", ReminderKind.Drink);
        var pill = Add("Examplopril", ReminderKind.Pill);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        var first = Today.AddDays(1);

        // Stopped for two days, with the pill still ringing, and started again on the third.
        schedule.StopTracking(all, Today.AddHours(21), keepMedication: true, reset: false);
        schedule.Done(new Dose(pill, first.AddHours(8)), first.AddHours(8));
        Assert.Equal(0, Missed(schedule, all, first, first.AddDays(2).AddHours(7)));
        schedule.Tracking.Start(first.AddDays(2).AddHours(7));

        var days = new Schedule(database).Days(all, first, first.AddDays(2).AddHours(21));
        // What was answered while it was stopped counts; what wasn't doesn't; from the start on, it's as usual.
        Assert.Contains(days, d => d.Dose.Due == first.AddHours(8) && d.State == DoseState.Taken);
        Assert.Equal(2, days.Count(d => d.State == DoseState.Missed));
        Assert.All(days.Where(d => d.State == DoseState.Missed), d => Assert.Equal(first.AddDays(2).AddHours(8), d.Dose.Due));
    }

    [Fact]
    public void WhatsDueAsItStopsDoesntGoOnToBeMissed()
    {
        Add("Water", ReminderKind.Drink);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        // Due at 8:00, and not answered by 9:00, when it's stopped.
        Assert.Single(schedule.Due(all, Today.AddHours(9)));
        schedule.StopTracking(all, Today.AddHours(9), keepMedication: false, reset: false);
        Assert.Empty(schedule.Due(all, Today.AddHours(9)));
        Assert.Equal(0, Missed(schedule, all, Today, Today.AddHours(21)));
    }

    [Fact]
    public void MedicationDueAsItsResetStillRingsButIsNeverMissed()
    {
        var pill = Add("Examplopril", ReminderKind.Pill, "08:00*1");
        Add("Water", ReminderKind.Drink);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        schedule.Tracking.Reset(Today.AddHours(9));

        Assert.Equal(pill.Name, Assert.Single(schedule.Due(all, Today.AddHours(9))).Reminder.Name);
        Assert.Equal(0, Missed(schedule, all, Today, Today.AddHours(21)));
        // Nor does tomorrow's say the one before it was missed.
        Assert.Null(schedule.MissedBefore(new Dose(pill, Today.AddDays(1).AddHours(8))));
    }
}
