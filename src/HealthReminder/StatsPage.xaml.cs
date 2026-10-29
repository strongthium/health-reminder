using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>A note of what stands out, with its colour and mark: a warning, a heads-up or good news.</summary>
public sealed class NoteRow(StatNote note)
{
    public string Text => note.Text;
    public string Label => note.Tone switch { NoteTone.Warning => "Worth a look", NoteTone.Heads => "Heads-up", _ => "Good" };
    public string Icon => note.Tone switch { NoteTone.Warning => "", NoteTone.Heads => "", _ => "" };
    public Brush Brush => note.Tone switch { NoteTone.Warning => StatsPage.MissedBrush, NoteTone.Heads => StatsPage.HeadsBrush, _ => StatsPage.DoneBrush };
}

/// <summary>
/// The days in numbers and bars: reminders done, skipped and missed, cigarettes and how far apart, drinks done of those
/// planned, and about how many kcal were eaten; with a word on what stands out at the top.
/// </summary>
public sealed partial class StatsPage : UserControl, IPage
{
    public static readonly Brush DoneBrush = KindLook.Brush(Color.FromRgb(0x2E, 0x7D, 0x32)), SkippedBrush = KindLook.Brush(Color.FromRgb(0x8A, 0x93, 0x9B)),
        MissedBrush = KindLook.Brush(Color.FromRgb(0xC6, 0x28, 0x28)), HeadsBrush = KindLook.Brush(Color.FromRgb(0x8A, 0x5A, 0x00)),
        SmokeBrush = KindLook.Brush(Color.FromRgb(0x54, 0x6E, 0x7A)), DrinkBrush = KindLook.Brush(ReminderKind.Drink),
        DrinkLeftBrush = KindLook.Brush(KindLook.ColorOf(ReminderKind.Drink), 0x55), MealBrush = KindLook.Brush(ReminderKind.Meal);

    private static readonly int[] Ranges = [7, 14, 30];
    private bool loading = true;

    public StatsPage()
    {
        InitializeComponent();
        RangeBox.ItemsSource = Ranges.Select(d => $"Last {d} days").ToList();
        var saved = int.TryParse(App.Current.Database.Setting("stats_days"), CultureInfo.InvariantCulture, out var d) ? d : 7;
        RangeBox.SelectedIndex = Math.Max(0, Array.IndexOf(Ranges, saved));
        loading = false;
    }

    private void Range_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        App.Current.Database.SetSetting("stats_days", Ranges[Math.Max(0, RangeBox.SelectedIndex)].ToString(CultureInfo.InvariantCulture));
        Refresh();
    }

    public void Refresh()
    {
        var now = DateTime.Now;
        var count = Ranges[Math.Max(0, RangeBox.SelectedIndex)];
        // A week more than shown, for what's usual.
        var all = FoodLog.Days(DateOnly.FromDateTime(now).AddDays(-(count + 8)), now);
        var days = all.TakeLast(count).ToList();
        var today = days[^1];
        var whole = days.Count > 1 ? days.SkipLast(1).ToList() : days;
        var smokes = App.Current.Database.Smokes(now.Date.AddDays(-(count + 8)));

        var notes = DailyStats.Notes(all, smokes, now, SmokeLog.TargetToday());
        if (notes.Count == 0)
            notes.Add(new StatNote(NoteTone.Good, all.Any(d => d.Scheduled > 0 || d.Smokes > 0)
                ? "Nothing stands out: what's missed, smoked and drunk is about as usual."
                : "Not much logged yet: notes come as the days fill in."));
        NoteList.ItemsSource = notes.Select(n => new NoteRow(n)).ToList();

        string Label(DayTally d) => d.Day == DateOnly.FromDateTime(now) ? "Today"
            : count <= 7 ? d.Day.ToString("ddd", CultureInfo.CurrentCulture) : d.Day.ToString("MMM d", CultureInfo.CurrentCulture);
        string Long(DayTally d) => d.Day == DateOnly.FromDateTime(now) ? "Today" : d.Day.ToString("dddd MMM d", CultureInfo.CurrentCulture);

        // Reminders
        var (done, skipped, missed) = (days.Sum(d => d.Done), days.Sum(d => d.Skipped), days.Sum(d => d.Missed.Count));
        var due = done + skipped + missed;
        DoneText.Text = due > 0 ? $"{done * 100 / due}%" : "–";
        DoneLabel.Text = "done of what was due";
        MissedText.Text = missed.ToString(CultureInfo.CurrentCulture);
        MissedLabel.Text = $"missed in {count} days";
        DoneSummary.Text = due > 0
            ? $"{done} of {due} done, {skipped} skipped and {missed} missed over the {count} days" + (today.ToCome > 0 ? $"; {today.ToCome} still to come today." : ".")
            : "Nothing was due over these days.";
        DoneChart.Show(days.Select(d => new Bar(Label(d), [(d.Done, DoneBrush), (d.Skipped, SkippedBrush), (d.Missed.Count, MissedBrush)],
            $"{Long(d)}: {d.Done} done, {d.Skipped} skipped, {d.Missed.Count} missed" + (d.ToCome > 0 ? $", {d.ToCome} to come" : "")
            + (d.Missed.Count > 0 ? "\nMissed: " + string.Join(", ", d.Missed.Select(m => $"{m.Reminder.Name} at {Format.Time(m.Due)}")) : ""))).ToList());

        // Cigarettes
        var perDay = whole.Average(d => d.Smokes);
        SmokesText.Text = perDay.ToString("0.#", CultureInfo.CurrentCulture);
        var target = SmokeLog.TargetToday();
        SmokeSummary.Text = $"{perDay:0.#} a day on average before today; {today.Smokes} today"
            + (target is { } goal ? $", of a goal of {goal}." : ".")
            + (DailyStats.UsualByThisTime(smokes, now) is { } usual ? $" Usually {usual:0.#} by this time of day." : "");
        SmokeChart.Show(days.Select(d => new Bar(Label(d), [(d.Smokes, SmokeBrush)], $"{Long(d)}: {d.Smokes} cigarette{(d.Smokes == 1 ? "" : "s")}")).ToList(),
            target is { } t ? (t, $"goal {t}") : null);

        var gaps = whole.Select(d => d.Between).OfType<TimeSpan>().ToList();
        var averageGap = gaps.Count > 0 ? TimeSpan.FromMinutes(gaps.Average(g => g.TotalMinutes)) : (TimeSpan?)null;
        GapText.Text = averageGap is { } g ? DailyStats.Span(g) : "–";
        GapSummary.Text = (today.Between is { } todayGap ? $"Today, one every {DailyStats.Span(todayGap)} on average" : "Today, not enough yet")
            + (averageGap is { } ag ? $"; every {DailyStats.Span(ag)} over the days before." : ".")
            + " Longer is better: from the first of a day to the last, divided by the gaps between.";
        GapChart.Show(days.Select(d => new Bar(Label(d), [(d.Between?.TotalMinutes ?? 0, SmokeBrush)],
                $"{Long(d)}: " + (d.Between is { } b ? $"one every {DailyStats.Span(b)}" : "fewer than two"))).ToList(),
            format: v => v >= 60 ? $"{v / 60:0.#} h" : $"{v:0} min");

        // Drinks
        var (drinks, planned) = (days.Sum(d => d.Drinks), days.Sum(d => d.DrinksPlanned));
        DrinksText.Text = planned > 0 ? $"{drinks * 100 / planned}%" : "–";
        DrinksLabel.Text = "of drinks done";
        var ml = whole.Average(d => d.DrankMl);
        DrinkSummary.Text = planned > 0
            ? $"{drinks} of {planned} done; about {Format.Amount(ml, "mL", ReminderKind.Drink)} a day before today, {Format.Amount(today.DrankMl, "mL", ReminderKind.Drink)} today."
            : "No drink reminders over these days. The Eat & drink page makes a plan with them.";
        DrinkChart.Show(days.Select(d => new Bar(Label(d), [(d.Drinks, DrinkBrush), (Math.Max(0, d.DrinksPlanned - d.Drinks), DrinkLeftBrush)],
            $"{Long(d)}: {d.Drinks} of {d.DrinksPlanned} done, {Format.Amount(d.DrankMl, "mL", ReminderKind.Drink)}")).ToList());

        // Calories
        var eatenDays = whole.Where(d => d.Kcal > 0).ToList();
        var kcal = eatenDays.Count > 0 ? eatenDays.Average(d => d.Kcal) : 0;
        KcalText.Text = kcal > 0 ? kcal.ToString("N0", CultureInfo.CurrentCulture) : "–";
        var plans = days.Where(d => d.KcalPlanned > 0).ToList();
        var plan = plans.Count > 0 ? plans.Average(d => d.KcalPlanned) : 0;
        KcalSummary.Text = (kcal > 0 ? $"About {kcal:N0} a day before today, {today.Kcal:N0} today" : $"About {today.Kcal:N0} today")
            + (plan > 0 ? $"; the plan is {plan:N0} a day." : ".")
            + " From the meals marked eaten (their recipe's, or the plan's) and the food logged on the Today page.";
        KcalChart.Show(days.Select(d => new Bar(Label(d), [(d.Kcal, MealBrush)], $"{Long(d)}: about {d.Kcal:N0} kcal"
                + (d.KcalPlanned > 0 ? $" of {d.KcalPlanned:N0} planned" : "") + (d.FoodLogged ? ", with food logged" : ""))).ToList(),
            plan > 0 ? (plan, $"plan {plan:N0}") : null);
    }
}
