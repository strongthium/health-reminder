namespace Strongthium.HealthReminder;

public enum DoseState { Upcoming, Due, Snoozed, Taken, Skipped, Missed }

/// <summary>One line of the day's schedule; <see cref="Record"/> is set once it was taken or skipped.</summary>
public sealed record ScheduledDose(Dose Dose, DoseState State, DateTime? SnoozedUntil, DoseRecord? Record);

/// <summary>
/// Works out which doses are due. A dose is due from its time until it is taken or skipped, for up to
/// <see cref="MissedAfter"/>: after that it counts as missed and the alarm stops asking. It also counts as missed once
/// the next dose of the same reminder is due, so that a late reminder never asks for two doses at once. Snoozing
/// puts a dose off for a while; snoozes aren't saved, so a dose snoozed when the program is closed is due again when
/// it starts.
/// </summary>
public sealed class Schedule(Database database)
{
    public static readonly TimeSpan MissedAfter = TimeSpan.FromHours(12);
    private readonly Dictionary<string, DateTime> snoozed = [];

    public void Snooze(Dose dose, DateTime until) => snoozed[dose.Key] = until;

    /// <summary>Records the dose as taken (eaten, drunk, injected, measured), with the reading, the site and the
    /// video of it if any.</summary>
    public void Done(Dose dose, DateTime at, string value = "", string site = "", string video = "", string videoSha256 = "") =>
        Record(dose, new DoseRecord(DoseStatus.Taken, at, dose.Amount, value, site, video, videoSha256));

    public void Skipped(Dose dose, DateTime at) => Record(dose, new DoseRecord(DoseStatus.Skipped, at, 0, "", ""));

    private void Record(Dose dose, DoseRecord record)
    {
        snoozed.Remove(dose.Key);
        database.Log(dose, record);
    }

    /// <summary>The doses to ring for now, oldest first: at most one a reminder.</summary>
    public List<Dose> Due(IEnumerable<Reminder> reminders, DateTime now)
    {
        var logged = database.LoggedSince(now - MissedAfter - TimeSpan.FromDays(1));
        var doses = Doses(reminders, now.Date.AddDays(-1), now.Date).ToList();
        return doses
            .Where(d => d.Due <= now && now - d.Due < MissedAfter && d.Due >= Since(d.Reminder) && !Superseded(d, doses, now))
            .Where(d => !logged.ContainsKey(d.Key))
            .Where(d => !snoozed.TryGetValue(d.Key, out var until) || until <= now)
            .OrderBy(d => d.Due)
            .ToList();
    }

    /// <summary>Today's doses and what became of each, in order.</summary>
    public List<ScheduledDose> Day(IEnumerable<Reminder> reminders, DateTime now)
    {
        var logged = database.LoggedSince(now.Date);
        var doses = Doses(reminders, now.Date, now.Date).ToList();
        return doses
            .Where(d => d.Due >= Since(d.Reminder))
            .OrderBy(d => d.Due).ThenBy(d => d.Reminder.Kind).ThenBy(d => d.Reminder.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d =>
            {
                if (logged.TryGetValue(d.Key, out var record))
                    return new ScheduledDose(d, record.Status == DoseStatus.Taken ? DoseState.Taken : DoseState.Skipped, null, record);
                if (d.Due > now) return new ScheduledDose(d, DoseState.Upcoming, null, null);
                if (now - d.Due >= MissedAfter || Superseded(d, doses, now)) return new ScheduledDose(d, DoseState.Missed, null, null);
                return snoozed.TryGetValue(d.Key, out var until) && until > now
                    ? new ScheduledDose(d, DoseState.Snoozed, until, null)
                    : new ScheduledDose(d, DoseState.Due, null, null);
            })
            .ToList();
    }

    /// <summary>The dose of the same reminder just before this one, when it was missed: neither taken nor skipped. Only
    /// the two days before count, and only doses since the reminder was added.</summary>
    public Dose? MissedBefore(Dose dose)
    {
        var logged = database.LoggedSince(dose.Due.AddDays(-2).Date);
        var previous = Doses([dose.Reminder], dose.Due.AddDays(-2), dose.Due)
            .Where(d => d.Due < dose.Due && d.Due >= Since(d.Reminder))
            .OrderBy(d => d.Due)
            .LastOrDefault();
        return previous is not null && !logged.ContainsKey(previous.Key) ? previous : null;
    }

    /// <summary>The injection site to use next: the one after the site used last, among the reminder's sites.</summary>
    public string NextSite(Reminder reminder)
    {
        if (reminder.Sites.Count == 0) return "";
        var last = reminder.Sites.IndexOf(database.LastSite(reminder.Id));
        return reminder.Sites[(last + 1) % reminder.Sites.Count];
    }

    /// <summary>How much was drunk today, in mL, from the drink reminders that were answered.</summary>
    public double DrankToday(IEnumerable<Reminder> reminders, DateTime now)
    {
        var drinks = reminders.Where(r => r.Kind == ReminderKind.Drink).Select(r => r.Id).ToHashSet();
        return database.History(now.Date)
            .Where(h => drinks.Contains(h.ReminderId) && h.Record.Status == DoseStatus.Taken && h.Due.Date == now.Date)
            .Sum(h => h.Record.Amount);
    }

    /// <summary>Whether a later dose of the same reminder is due by now.</summary>
    private static bool Superseded(Dose dose, List<Dose> doses, DateTime now) =>
        doses.Any(other => other.Reminder.Id == dose.Reminder.Id && other.Due > dose.Due && other.Due <= now);

    /// <summary>The minute a reminder was added: its doses from then on count, earlier ones of that day don't.</summary>
    private static DateTime Since(Reminder reminder) =>
        reminder.Created.AddTicks(-(reminder.Created.Ticks % TimeSpan.TicksPerMinute));

    /// <summary>When the next dose after <paramref name="now"/> is, if anything is scheduled in the next week.</summary>
    public static Dose? Next(IEnumerable<Reminder> reminders, DateTime now) =>
        Doses(reminders, now.Date, now.Date.AddDays(7)).Where(d => d.Due > now).OrderBy(d => d.Due).FirstOrDefault();

    /// <summary>Every dose of the active reminders on the days from <paramref name="first"/> to <paramref name="last"/>.</summary>
    public static IEnumerable<Dose> Doses(IEnumerable<Reminder> reminders, DateTime first, DateTime last)
    {
        foreach (var reminder in reminders.Where(m => m.Active))
            for (var day = first.Date; day <= last.Date; day = day.AddDays(1))
                if (reminder.Days.Contains(day.DayOfWeek))
                    foreach (var time in reminder.Times)
                        yield return new Dose(reminder, day + time.Time.ToTimeSpan());
    }
}
