using System.Globalization;

namespace Strongthium.HealthReminder;

/// <summary>A day's numbers: the reminders done, skipped and missed (and still to come, today), the cigarettes and
/// how far apart they were on average, the drinks done of those planned and how much was drunk (mL), and about how
/// many kcal were eaten (the meals marked eaten, and food logged by hand) of the plan's.</summary>
public sealed record DayTally(DateOnly Day, int Done, int Skipped, IReadOnlyList<Dose> Missed, int ToCome, int Smokes, TimeSpan? Between,
    int Drinks, int DrinksPlanned, double DrankMl, int Kcal, int KcalPlanned, bool FoodLogged)
{
    public int Scheduled => Done + Skipped + Missed.Count + ToCome;
}

public enum NoteTone { Good, Heads, Warning }

/// <summary>Something the numbers say worth a word: good news, a heads-up, or a warning.</summary>
public sealed record StatNote(NoteTone Tone, string Text);

/// <summary>
/// The days in numbers, for the Stats page, and what stands out: more missed than usual, more cigarettes than usual by
/// this time of day, fewer drinks than planned. "Usual" is the average of the 7 days before yesterday (or as many of
/// them as there's a log for), so that a bad day doesn't count against itself.
/// </summary>
public static class DailyStats
{
    /// <summary>
    /// Each day from <paramref name="first"/> to today. A meal marked eaten counts <paramref name="mealKcal"/> for it
    /// (its recipe's, or the plan's), unless food was logged in its place; food logged on its own counts as logged.
    /// </summary>
    public static List<DayTally> Days(IReadOnlyList<ScheduledDose> doses, IReadOnlyList<Smoke> smokes, IReadOnlyList<FoodEaten> food,
        Func<ScheduledDose, int> mealKcal, DateOnly first, DateTime now)
    {
        var replaced = food.Where(f => f.Meal is not null).Select(f => f.Meal!).ToHashSet();
        var days = new List<DayTally>();
        for (var day = first; day <= DateOnly.FromDateTime(now); day = day.AddDays(1))
        {
            var date = day.ToDateTime(TimeOnly.MinValue);
            var of = doses.Where(d => d.Dose.Due.Date == date).ToList();
            var lit = smokes.Where(s => s.Started.Date == date).Select(s => s.Started).ToList();
            var drinks = of.Where(d => d.Dose.Reminder.Kind == ReminderKind.Drink).ToList();
            var meals = of.Where(d => d.Dose.Reminder.Kind == ReminderKind.Meal).ToList();
            var eaten = food.Where(f => f.At.Date == date).ToList();
            days.Add(new DayTally(day,
                of.Count(d => d.State == DoseState.Taken),
                of.Count(d => d.State == DoseState.Skipped),
                of.Where(d => d.State == DoseState.Missed).Select(d => d.Dose).ToList(),
                of.Count(d => d.State is DoseState.Upcoming or DoseState.Due or DoseState.Snoozed),
                lit.Count,
                AverageGap(lit),
                drinks.Count(d => d.State == DoseState.Taken),
                drinks.Count,
                drinks.Where(d => d.State == DoseState.Taken).Sum(d => d.Record!.Amount),
                eaten.Sum(f => f.Kcal) + meals.Where(d => d.State == DoseState.Taken && !replaced.Contains(d.Dose.Key)).Sum(mealKcal),
                (int)meals.Where(d => d.Dose.Reminder.Unit == "kcal").Sum(d => d.Dose.Amount),
                eaten.Count > 0));
        }
        return days;
    }

    /// <summary>The kcal of a meal marked eaten as the plan had it: what was recorded for it.</summary>
    public static int PlannedKcal(ScheduledDose dose) => dose.Dose.Reminder.Unit == "kcal" ? (int)(dose.Record?.Amount ?? 0) : 0;

    /// <summary>The average time from one to the next, of times on one day; null with fewer than two.</summary>
    public static TimeSpan? AverageGap(IEnumerable<DateTime> times)
    {
        var sorted = times.Order().ToList();
        return sorted.Count < 2 ? null : (sorted[^1] - sorted[0]) / (sorted.Count - 1);
    }

    /// <summary>How many were lit on the day <paramref name="daysBack"/> before <paramref name="now"/>'s, by the same
    /// time of day.</summary>
    public static int ByThisTime(IEnumerable<Smoke> smokes, DateTime now, int daysBack)
    {
        var start = now.Date.AddDays(-daysBack);
        var end = start + now.TimeOfDay;
        return smokes.Count(s => s.Started >= start && s.Started < end);
    }

    /// <summary>How many are usually lit by this time of day: the average of the 7 days before yesterday that there's
    /// a log for (from the first one logged); null when there's none.</summary>
    public static double? UsualByThisTime(IReadOnlyList<Smoke> smokes, DateTime now)
    {
        var back = UsualDays(smokes, now);
        return back.Count == 0 ? null : back.Average(b => ByThisTime(smokes, now, b));
    }

    /// <summary>The days back (2 to 8) that count as usual: the week before yesterday, from the first cigarette logged.</summary>
    private static List<int> UsualDays(IReadOnlyList<Smoke> smokes, DateTime now)
    {
        if (smokes.Count == 0) return [];
        var firstDay = smokes.Min(s => s.Started).Date;
        return Enumerable.Range(2, 7).Where(b => now.Date.AddDays(-b) >= firstDay).ToList();
    }

    /// <summary>"1 h 20 min", "45 min".</summary>
    public static string Span(TimeSpan time) =>
        time.TotalHours >= 1 ? $"{(int)time.TotalHours} h {time.Minutes} min" : $"{Math.Max(1, (int)Math.Round(time.TotalMinutes))} min";

    private static string Time(DateTime time) => time.ToString("t", CultureInfo.CurrentCulture);

    private static string List(IReadOnlyList<string> names) =>
        names.Count <= 1 ? names.FirstOrDefault() ?? "" : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    /// <summary>What stands out in the days (the last of which is today) and the cigarettes over them, most pressing
    /// first; <paramref name="target"/> is today's most cigarettes, when cutting down.</summary>
    public static List<StatNote> Notes(IReadOnlyList<DayTally> days, IReadOnlyList<Smoke> smokes, DateTime now, int? target = null)
    {
        var notes = new List<StatNote>();
        if (days.Count == 0) return notes;
        var today = days[^1];
        var yesterday = days.Count > 1 ? days[^2] : null;
        var usual = days.Count > 2 ? days.Take(days.Count - 2).TakeLast(7).ToList() : [];

        // Missed: a medication missed today, or several of anything; more than usual yesterday; a lot over the week.
        static List<string> Names(IEnumerable<Dose> doses) => doses.OrderBy(d => d.Due).Select(d => $"{d.Reminder.Name} at {Time(d.Due)}").ToList();
        static string Some(List<string> names) => List(names.Take(4).ToList()) + (names.Count > 4 ? $" and {names.Count - 4} more" : "");
        var medication = Names(today.Missed.Where(d => d.Reminder.Kind.IsMedication()));
        var rest = Names(today.Missed.Where(d => !d.Reminder.Kind.IsMedication()));
        if (medication.Count > 0)
            notes.Add(new(NoteTone.Warning, $"Medication missed today: {Some(medication)}. If a dose is missed often, a pharmacist can say what to do when it happens."));
        if (rest.Count >= 2)
            notes.Add(new(NoteTone.Heads, $"{rest.Count} missed today so far: {Some(rest)}."));
        if (yesterday is { Missed.Count: >= 2 } y)
        {
            var before = usual.Count > 0 ? usual.Average(u => u.Missed.Count) : 0;
            if (y.Missed.Count >= before + 2 || y.Missed.Count >= y.Scheduled / 4.0)
                notes.Add(new(NoteTone.Warning, $"Yesterday {y.Missed.Count} of {y.Scheduled} were missed" +
                    (usual.Count > 0 ? $", more than usual (about {before:0.#} a day)." : ".")));
        }
        var week = days.SkipLast(1).TakeLast(7).ToList();
        if (week.Count >= 3)
        {
            var (missed, scheduled) = (week.Sum(d => d.Missed.Count), week.Sum(d => d.Scheduled));
            if (scheduled > 0 && missed >= 5 && missed >= scheduled * 0.2)
                notes.Add(new(NoteTone.Heads, $"{missed} of {scheduled} missed over the last {week.Count} days ({missed * 100 / scheduled}%). " +
                    "Moving the times to when you're usually free, or a longer snooze, may help."));
        }

        // Cigarettes: over the goal; more (or fewer) than usual by this time of day; more than usual yesterday;
        // closer together.
        if (target is { } goal && today.Smokes > goal)
            notes.Add(new(NoteTone.Warning, $"{today.Smokes} cigarettes today, over today's goal of {goal}."));
        if (UsualByThisTime(smokes, now) is { } byNow)
        {
            var margin = Math.Max(2, byNow * 0.25);
            var diff = today.Smokes - byNow;
            if (diff >= margin)
                notes.Add(new(NoteTone.Warning, $"{today.Smokes} cigarettes by {Time(now)}, about {diff:0} more than usual by this time ({byNow:0.#})."));
            else if (-diff >= margin)
                notes.Add(new(NoteTone.Good, $"{today.Smokes} cigarettes by {Time(now)}, about {-diff:0} fewer than usual by this time ({byNow:0.#})."));
        }
        var usualSmoking = UsualDays(smokes, now).Select(b => days.FirstOrDefault(d => d.Day == DateOnly.FromDateTime(now.Date.AddDays(-b)))).OfType<DayTally>().ToList();
        if (yesterday is not null && usualSmoking.Count > 0)
        {
            var perDay = usualSmoking.Average(d => d.Smokes);
            if (yesterday.Smokes >= perDay + Math.Max(3, perDay * 0.2))
                notes.Add(new(NoteTone.Heads, $"Yesterday {yesterday.Smokes} cigarettes, more than your usual {perDay:0.#} a day."));
        }
        var gaps = usualSmoking.Select(d => d.Between).OfType<TimeSpan>().ToList();
        if (today.Smokes >= 3 && today.Between is { } gap && gaps.Count > 0)
        {
            var usualGap = TimeSpan.FromMinutes(gaps.Average(g => g.TotalMinutes));
            if (gap < usualGap * 0.75)
                notes.Add(new(NoteTone.Heads, $"Closer together today: one every {Span(gap)} on average, against every {Span(usualGap)} usually."));
        }

        // Drinks and food, yesterday's whole day.
        if (yesterday is { DrinksPlanned: >= 3 } dy && dy.Drinks <= dy.DrinksPlanned / 2)
            notes.Add(new(NoteTone.Heads, $"Yesterday {dy.Drinks} of {dy.DrinksPlanned} drinks were done. Keeping a glass where you sit helps."));
        if (yesterday is { FoodLogged: true, KcalPlanned: > 0 } ky && ky.Kcal > ky.KcalPlanned * 1.25)
            notes.Add(new(NoteTone.Heads, $"About {ky.Kcal:N0} kcal yesterday, over the plan's {ky.KcalPlanned:N0}."));
        return notes.OrderByDescending(n => n.Tone).ToList();
    }
}
