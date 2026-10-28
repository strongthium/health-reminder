namespace Strongthium.HealthReminder.Tests;

public sealed class ScheduleTests : IDisposable
{
    // A Wednesday.
    private static readonly DateTime Today = new(2026, 10, 28);
    private readonly Database database = new();

    public void Dispose() => database.Dispose();

    private Reminder Add(string name, string times, DaySet days = DaySet.Every, DateTime? created = null, ReminderKind kind = ReminderKind.Pill)
    {
        var reminder = new Reminder
        {
            Kind = kind,
            Name = name,
            Unit = kind.DefaultUnit(),
            Times = Database.ParseTimes(times),
            Days = days,
            Created = created ?? Today.AddDays(-30),
            Appearance = new PillAppearance(["WHITE"], "ROUND", 8, "EX;10", 2),
        };
        database.Save(reminder);
        return reminder;
    }

    [Fact]
    public void ADoseIsDueFromItsTimeUntilItIsTaken()
    {
        var reminder = Add("Examplopril", "08:00*2;20:00*1");
        var schedule = new Schedule(database);
        var all = database.Reminders();

        static bool IsToday(Dose d) => d.Due.Date == Today;
        Assert.DoesNotContain(schedule.Due(all, Today.AddHours(7.9)), IsToday);
        var due = Assert.Single(schedule.Due(all, Today.AddHours(8)), IsToday);
        Assert.Equal(Today.AddHours(8), due.Due);
        Assert.Equal(reminder.Id, due.Reminder.Id);
        Assert.Equal(2, due.Amount);

        schedule.Done(due, Today.AddHours(8.1));
        Assert.DoesNotContain(schedule.Due(all, Today.AddHours(9)), IsToday);
        Assert.Equal(1, Assert.Single(schedule.Due(all, Today.AddHours(20)), IsToday).Amount);
    }

    [Fact]
    public void SeveralRemindersAtTheSameTimeAreDueTogether()
    {
        Add("Examplopril", "08:00");
        Add("Insulin", "08:00*12", kind: ReminderKind.Injection);
        Add("Blood sugar", "08:00", kind: ReminderKind.BloodSugar);
        var due = new Schedule(database).Due(database.Reminders(), Today.AddHours(8.05)).Where(d => d.Due.Date == Today).ToList();
        Assert.Equal(3, due.Count);
        Assert.All(due, d => Assert.Equal(Today.AddHours(8), d.Due));
    }

    [Fact]
    public void ASnoozedDoseComesBack()
    {
        Add("Examplopril", "08:00", created: Today);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        var due = Assert.Single(schedule.Due(all, Today.AddHours(8)));

        schedule.Snooze(due, Today.AddHours(8).AddMinutes(10));
        Assert.Empty(schedule.Due(all, Today.AddHours(8).AddMinutes(9)));
        Assert.Single(schedule.Due(all, Today.AddHours(8).AddMinutes(10)));
        Assert.Equal(DoseState.Snoozed, schedule.Day(all, Today.AddHours(8).AddMinutes(5)).Single().State);
    }

    [Fact]
    public void ADoseNotTakenForTwelveHoursIsMissedAndNoLongerRings()
    {
        Add("Examplopril", "08:00", created: Today);
        var schedule = new Schedule(database);
        var all = database.Reminders();

        Assert.Single(schedule.Due(all, Today.AddHours(19.9)));
        Assert.Empty(schedule.Due(all, Today.AddHours(20)));
        Assert.Equal(DoseState.Missed, schedule.Day(all, Today.AddHours(20)).Single().State);
    }

    [Fact]
    public void ADoseNotTakenBeforeTheNextOneIsDueIsMissedSoTheyArentTakenTogether()
    {
        Add("Examplopril", "08:00;12:00", created: Today);
        var schedule = new Schedule(database);
        var all = database.Reminders();

        var due = Assert.Single(schedule.Due(all, Today.AddHours(12)));
        Assert.Equal(Today.AddHours(12), due.Due);
        Assert.Equal([DoseState.Missed, DoseState.Due], schedule.Day(all, Today.AddHours(12)).Select(d => d.State));
    }

    [Fact]
    public void ALateEveningDoseStillRingsAfterMidnight()
    {
        Add("Examplopril", "23:00");
        var due = Assert.Single(new Schedule(database).Due(database.Reminders(), Today.AddHours(0.5)));
        Assert.Equal(Today.AddDays(-1).AddHours(23), due.Due);
    }

    [Fact]
    public void DosesBeforeTheReminderWasAddedDontRing()
    {
        Add("Examplopril", "08:00;20:00", created: Today.AddHours(12));
        var schedule = new Schedule(database);
        var all = database.Reminders();
        Assert.Empty(schedule.Due(all, Today.AddHours(13)));
        Assert.Equal([Today.AddHours(20)], schedule.Day(all, Today.AddHours(13)).Select(d => d.Dose.Due));
    }

    [Fact]
    public void OnlyTheChosenDaysAndActiveRemindersCount()
    {
        Add("Weekly", "09:00", DaySet.Monday);
        var paused = Add("Paused", "09:00");
        paused.Active = false;
        database.Save(paused);
        var all = database.Reminders();

        Assert.Empty(new Schedule(database).Due(all, Today.AddHours(10)));
        var next = Schedule.Next(all, Today.AddHours(10));
        Assert.NotNull(next);
        Assert.Equal("Weekly", next.Reminder.Name);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), next.Due);
    }

    [Fact]
    public void TheDayListsEachDoseWithWhatBecameOfIt()
    {
        Add("Blood sugar", "08:00;12:00;20:00", kind: ReminderKind.BloodSugar);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        var doses = schedule.Day(all, Today.AddHours(13));
        schedule.Done(doses[0].Dose, Today.AddHours(8.2), value: "6.4");
        schedule.Skipped(doses[1].Dose, Today.AddHours(12.5));

        var day = schedule.Day(all, Today.AddHours(13));
        Assert.Equal([DoseState.Taken, DoseState.Skipped, DoseState.Upcoming], day.Select(d => d.State));
        Assert.Equal("6.4", day[0].Record!.Value);
    }

    [Fact]
    public void InjectionSitesAreSuggestedInTurn()
    {
        var insulin = Add("Insulin", "08:00*10;20:00*12", kind: ReminderKind.Injection);
        insulin.Sites = ["Abdomen, left", "Abdomen, right", "Thigh, left"];
        database.Save(insulin);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        insulin = all.Single();

        Assert.Equal("Abdomen, left", schedule.NextSite(insulin));
        var doses = schedule.Day(all, Today.AddHours(21)).Select(d => d.Dose).ToList();
        schedule.Done(doses[0], Today.AddHours(8), site: "Abdomen, left");
        Assert.Equal("Abdomen, right", schedule.NextSite(insulin));
        schedule.Done(doses[1], Today.AddHours(20), site: "Thigh, left");
        Assert.Equal("Abdomen, left", schedule.NextSite(insulin));
        Assert.Equal(12, database.History(Today).First().Record.Amount);
    }

    [Fact]
    public void CountsWhatWasDrunkToday()
    {
        Add("Water", "08:00*250;10:00*300;12:00*250", kind: ReminderKind.Drink);
        var schedule = new Schedule(database);
        var all = database.Reminders();
        var doses = schedule.Day(all, Today.AddHours(13)).Select(d => d.Dose).ToList();
        schedule.Done(doses[0], Today.AddHours(8));
        schedule.Done(doses[1], Today.AddHours(10));
        schedule.Skipped(doses[2], Today.AddHours(12));
        Assert.Equal(550, schedule.DrankToday(all, Today.AddHours(13)));
    }
}
