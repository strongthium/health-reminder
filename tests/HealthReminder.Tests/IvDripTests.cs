namespace Strongthium.HealthReminder.Tests;

public sealed class IvDripTests
{
    private static readonly DateTime Morning = new(2026, 11, 2, 8, 0, 0);

    private static Reminder Drip(double hours = 12, int warn = 15)
    {
        var reminder = new Reminder
        {
            Kind = ReminderKind.IV, Name = "Saline", Unit = "bag", Created = Morning.AddDays(-1),
            Iv = new IvDrip { Line = "PICC line", Site = "Left arm", Hours = hours, WarnMinutes = warn },
        };
        reminder.Iv.Hang(Morning);
        return reminder;
    }

    [Fact]
    public void ABagRingsBeforeItRunsOutAndEachRunAfterAsIfChangedOnTime()
    {
        var iv = Drip().Iv;
        Assert.Equal(new IvBag(Morning, Morning.AddHours(12).AddMinutes(-15)), iv.Current);
        Assert.Equal(Morning.AddHours(12), iv.RunsOut(Morning));
        Assert.Equal([Morning.AddHours(11.75), Morning.AddHours(23.75), Morning.AddHours(35.75)], iv.Dues(Morning, Morning.AddHours(40)));
        Assert.Equal("PICC line, left arm", iv.Where);
        Assert.Equal(12.5, IvDrip.RunHours(1000, 80));
        Assert.Null(IvDrip.RunHours(1000, 0));
    }

    [Fact]
    public void ABagChangedLateMovesTheOnesAfterIt()
    {
        var iv = Drip().Iv;
        // Due at 7:45 PM, changed at 9:00 PM: the next rings at 8:45 AM, not 7:45 AM.
        iv.Hang(Morning.AddHours(13));
        Assert.Equal([Morning.AddHours(11.75), Morning.AddHours(24.75)], iv.Dues(Morning, Morning.AddHours(30)));
    }

    [Fact]
    public void ABagChangedWhenALaterReminderRangKeepsBothReminders()
    {
        var iv = Drip(hours: 4).Iv;
        // Due at 11:45 and again at 3:45 PM, changed at 4:00 PM.
        iv.Hang(Morning.AddHours(8));
        Assert.Equal([Morning.AddHours(3.75), Morning.AddHours(7.75), Morning.AddHours(11.75)], iv.Dues(Morning, Morning.AddHours(12)));
    }

    [Fact]
    public void AWarningLongerThanABagRingsAMinuteAfterItsStarted()
    {
        Assert.Equal(Morning.AddMinutes(1), Drip(hours: 0.5, warn: 60).Iv.Current!.Value.Due);
    }

    [Fact]
    public void MarkingABagChangedHangsTheNextAndRecordsTheLine()
    {
        using var database = new Database();
        var reminder = Drip();
        database.Save(reminder);
        var schedule = new Schedule(database);
        var now = Morning.AddHours(12).AddMinutes(5);

        var due = Assert.Single(schedule.Due([reminder], now));
        Assert.Equal(Morning.AddHours(11.75), due.Due);
        schedule.Done(due, now);

        Assert.Empty(schedule.Due([reminder], now));
        Assert.Equal("PICC line, left arm", Assert.Single(database.History(Morning.Date)).Record.Site);
        var saved = Assert.Single(database.Reminders());
        Assert.Equal([Morning, now], saved.Iv.Bags.Select(b => b.Started));
        Assert.Equal(now.AddHours(12).AddMinutes(-15), Schedule.Next([saved], now)!.Due);
        Assert.Equal(DoseState.Taken, Assert.Single(schedule.Day([saved], now)).State);
    }

    [Fact]
    public void KeepsTheLineAndTheBags()
    {
        using var database = new Database();
        var reminder = Drip(hours: 12.5, warn: 20);
        reminder.Iv.BagMl = 1000;
        reminder.Iv.MlPerHour = 80;
        reminder.Iv.LineSince = new DateTime(2026, 10, 24);
        reminder.Iv.Hang(Morning.AddHours(12).AddSeconds(42));
        database.Save(reminder);

        var iv = Assert.Single(database.Reminders()).Iv;
        Assert.Equal(("PICC line", "Left arm", new DateTime(2026, 10, 24), 1000.0, 80.0, 12.5, 20),
            (iv.Line, iv.Site, iv.LineSince, iv.BagMl, iv.MlPerHour, iv.Hours, iv.WarnMinutes));
        Assert.Equal(reminder.Iv.Bags, iv.Bags);
    }

    [Fact]
    public void ADayIsTwentyFourHoursOfBags()
    {
        Assert.Equal(2, Drip(hours: 12).DailyUse);
        Assert.Equal(3, Drip(hours: 8).DailyUse);
    }

    [Fact]
    public void ADoseStillOnTheDripAfterAnEdit()
    {
        var reminder = Drip();
        Assert.True(Schedule.Has(reminder, Morning.AddHours(11.75)));
        reminder.Iv.Restart(Morning.AddHours(1));
        Assert.False(Schedule.Has(reminder, Morning.AddHours(11.75)));
    }
}
