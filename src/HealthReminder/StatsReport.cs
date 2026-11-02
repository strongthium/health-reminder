using System.Globalization;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>A number on its own, and what it is: "82%", "done of what was due".</summary>
public sealed record StatsTile(string Value, string Label);

/// <summary>A chart of the days: its title, what it says, a bar a day, a goal or plan to draw across, how a value is
/// written, and what its colours are.</summary>
public sealed record StatsChart(string Title, string Summary, IReadOnlyList<Bar> Bars, (double Value, string Text)? Line = null,
    Func<double, string>? Format = null, IReadOnlyList<(string Name, Brush Brush)>? Legend = null);

/// <summary>
/// The days in numbers and bars, worked out once for the Stats page and for printing: reminders done, skipped and
/// missed, cigarettes and how far apart, drinks done of those planned, and about how many kcal were eaten; with what
/// stands out.
/// </summary>
public sealed class StatsReport
{
    public static readonly Brush DoneBrush = KindLook.Brush(Color.FromRgb(0x2E, 0x7D, 0x32)), SkippedBrush = KindLook.Brush(Color.FromRgb(0x8A, 0x93, 0x9B)),
        MissedBrush = KindLook.Brush(Color.FromRgb(0xC6, 0x28, 0x28)), HeadsBrush = KindLook.Brush(Color.FromRgb(0x8A, 0x5A, 0x00)),
        SmokeBrush = KindLook.Brush(Color.FromRgb(0x54, 0x6E, 0x7A)), DrinkBrush = KindLook.Brush(ReminderKind.Drink),
        DrinkLeftBrush = KindLook.Brush(KindLook.ColorOf(ReminderKind.Drink), 0x55), MealBrush = KindLook.Brush(ReminderKind.Meal);

    /// <summary>The days that can be shown.</summary>
    public static readonly int[] Ranges = [7, 14, 30];

    /// <summary>The days shown, today last; fewer than asked for when the stats were reset since.</summary>
    public required IReadOnlyList<DayTally> Days { get; init; }
    /// <summary>"Counting from Oct 28.", and whether tracking is stopped.</summary>
    public required string Counting { get; init; }
    public required IReadOnlyList<StatNote> Notes { get; init; }
    public required StatsTile Done, Missed, Smokes, Gap, Drinks, Kcal;
    public required StatsChart DoneChart, SmokeChart, GapChart, DrinkChart, KcalChart;

    /// <summary>The last <paramref name="count"/> days, today last.</summary>
    public static StatsReport For(int count, DateTime now)
    {
        // A week more than shown, for what's usual.
        var all = FoodLog.Days(DateOnly.FromDateTime(now).AddDays(-(count + 8)), now);
        var days = all.TakeLast(count).ToList();
        var today = days[^1];
        var whole = days.Count > 1 ? days.SkipLast(1).ToList() : days;
        var tracking = App.Current.Schedule.Tracking;
        var smokes = App.Current.Database.Smokes(tracking.Counted(now.Date.AddDays(-(count + 8))));
        var counting = string.Join(" ", new[]
        {
            tracking.From is { } from ? $"Counting from {from.ToString("MMM d", CultureInfo.CurrentCulture)}." : "",
            tracking.Stopped ? "Tracking is stopped: nothing counts as missed until it's started again." : "",
        }.Where(s => s.Length > 0));

        var notes = DailyStats.Notes(all, smokes, now, SmokeLog.TargetToday());
        if (notes.Count == 0)
            notes.Add(new StatNote(NoteTone.Good, all.Any(d => d.Scheduled > 0 || d.Smokes > 0)
                ? "Nothing stands out: what's missed, smoked and drunk is about as usual."
                : "Not much logged yet: notes come as the days fill in."));

        string Label(DayTally d) => d.Day == DateOnly.FromDateTime(now) ? "Today"
            : count <= 7 ? d.Day.ToString("ddd", CultureInfo.CurrentCulture) : d.Day.ToString("MMM d", CultureInfo.CurrentCulture);
        string Long(DayTally d) => d.Day == DateOnly.FromDateTime(now) ? "Today" : d.Day.ToString("dddd MMM d", CultureInfo.CurrentCulture);

        // Reminders
        var (done, skipped, missed) = (days.Sum(d => d.Done), days.Sum(d => d.Skipped), days.Sum(d => d.Missed.Count));
        var due = done + skipped + missed;
        var doneChart = new StatsChart("Reminders each day",
            due > 0
                ? $"{done} of {due} done, {skipped} skipped and {missed} missed " + (days.Count == 1 ? "today" : $"over the {days.Count} days") + (today.ToCome > 0 ? $"; {today.ToCome} still to come today." : ".")
                : "Nothing was due over these days.",
            days.Select(d => new Bar(Label(d), [(d.Done, DoneBrush), (d.Skipped, SkippedBrush), (d.Missed.Count, MissedBrush)],
                $"{Long(d)}: {d.Done} done, {d.Skipped} skipped, {d.Missed.Count} missed" + (d.ToCome > 0 ? $", {d.ToCome} to come" : "")
                + (d.Missed.Count > 0 ? "\nMissed: " + string.Join(", ", d.Missed.Select(m => $"{m.Reminder.Name} at {Format.Time(m.Due)}")) : ""))).ToList(),
            Legend: [("Done", DoneBrush), ("Skipped", SkippedBrush), ("Missed", MissedBrush)]);

        // Cigarettes
        var perDay = whole.Average(d => d.Smokes);
        var target = SmokeLog.TargetToday();
        var smokeChart = new StatsChart("Cigarettes each day",
            $"{perDay:0.#} a day on average before today; {today.Smokes} today"
                + (target is { } goal ? $", of a goal of {goal}." : ".")
                + (DailyStats.UsualByThisTime(smokes, now) is { } usual ? $" Usually {usual:0.#} by this time of day." : ""),
            days.Select(d => new Bar(Label(d), [(d.Smokes, SmokeBrush)], $"{Long(d)}: {d.Smokes} cigarette{(d.Smokes == 1 ? "" : "s")}")).ToList(),
            target is { } t ? (t, $"goal {t}") : null);

        var gaps = whole.Select(d => d.Between).OfType<TimeSpan>().ToList();
        var averageGap = gaps.Count > 0 ? TimeSpan.FromMinutes(gaps.Average(g => g.TotalMinutes)) : (TimeSpan?)null;
        var gapChart = new StatsChart("Average time between cigarettes",
            (today.Between is { } todayGap ? $"Today, one every {DailyStats.Span(todayGap)} on average" : "Today, not enough yet")
                + (averageGap is { } ag ? $"; every {DailyStats.Span(ag)} over the days before." : ".")
                + " Longer is better: from the first of a day to the last, divided by the gaps between.",
            days.Select(d => new Bar(Label(d), [(d.Between?.TotalMinutes ?? 0, SmokeBrush)],
                $"{Long(d)}: " + (d.Between is { } b ? $"one every {DailyStats.Span(b)}" : "fewer than two"))).ToList(),
            Format: v => v >= 60 ? $"{v / 60:0.#} h" : $"{v:0} min");

        // Drinks
        var (drinks, planned) = (days.Sum(d => d.Drinks), days.Sum(d => d.DrinksPlanned));
        var ml = whole.Average(d => d.DrankMl);
        var drinkChart = new StatsChart("Drinks each day",
            planned > 0
                ? $"{drinks} of {planned} done; about {Format.Amount(ml, "mL", ReminderKind.Drink)} a day before today, {Format.Amount(today.DrankMl, "mL", ReminderKind.Drink)} today."
                : "No drink reminders over these days. The Eat & drink page makes a plan with them.",
            days.Select(d => new Bar(Label(d), [(d.Drinks, DrinkBrush), (Math.Max(0, d.DrinksPlanned - d.Drinks), DrinkLeftBrush)],
                $"{Long(d)}: {d.Drinks} of {d.DrinksPlanned} done, {Format.Amount(d.DrankMl, "mL", ReminderKind.Drink)}")).ToList(),
            Legend: [("Done", DrinkBrush), ("Not done", DrinkLeftBrush)]);

        // Calories
        var eatenDays = whole.Where(d => d.Kcal > 0).ToList();
        var kcal = eatenDays.Count > 0 ? eatenDays.Average(d => d.Kcal) : 0;
        var plans = days.Where(d => d.KcalPlanned > 0).ToList();
        var plan = plans.Count > 0 ? plans.Average(d => d.KcalPlanned) : 0;
        var kcalChart = new StatsChart("Calories each day",
            (kcal > 0 ? $"About {kcal:N0} a day before today, {today.Kcal:N0} today" : $"About {today.Kcal:N0} today")
                + (plan > 0 ? $"; the plan is {plan:N0} a day." : ".")
                + " From the meals marked eaten (their recipe's, or the plan's) and the food logged on the Today page.",
            days.Select(d => new Bar(Label(d), [(d.Kcal, MealBrush)], $"{Long(d)}: about {d.Kcal:N0} kcal"
                + (d.KcalPlanned > 0 ? $" of {d.KcalPlanned:N0} planned" : "") + (d.FoodLogged ? ", with food logged" : ""))).ToList(),
            plan > 0 ? (plan, $"plan {plan:N0}") : null);

        return new StatsReport
        {
            Days = days,
            Counting = counting,
            Notes = notes,
            Done = new(due > 0 ? $"{done * 100 / due}%" : "–", "done of what was due"),
            Missed = new(missed.ToString(CultureInfo.CurrentCulture), days.Count == 1 ? "missed today" : $"missed in {days.Count} days"),
            Smokes = new(perDay.ToString("0.#", CultureInfo.CurrentCulture), "cigarettes a day"),
            Gap = new(averageGap is { } g ? DailyStats.Span(g) : "–", "between cigarettes"),
            Drinks = new(planned > 0 ? $"{drinks * 100 / planned}%" : "–", "of drinks done"),
            Kcal = new(kcal > 0 ? kcal.ToString("N0", CultureInfo.CurrentCulture) : "–", "kcal a day, about"),
            DoneChart = doneChart,
            SmokeChart = smokeChart,
            GapChart = gapChart,
            DrinkChart = drinkChart,
            KcalChart = kcalChart,
        };
    }
}
