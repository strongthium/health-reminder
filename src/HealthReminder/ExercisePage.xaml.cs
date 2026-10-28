using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Strongthium.HealthReminder;

/// <summary>A day of the week's exercise.</summary>
public sealed class WeekRow(DateOnly day, string text, bool done, bool past)
{
    public string Day => day == DateOnly.FromDateTime(DateTime.Today) ? "Today" : day.ToString("dddd", CultureInfo.CurrentCulture);
    public string Text => text;
    public bool Done => done;
    public double Opacity => past && !done ? 0.55 : 1;
}

/// <summary>
/// The Exercise page: today's session, going out or staying in, to start (its steps at the side of the screen) or mark
/// done; the week's minutes against the WHO's 150; and the plan.
/// </summary>
public sealed partial class ExercisePage : UserControl, IPage
{
    private static readonly int[] MinuteChoices = [15, 20, 30, 45, 60];
    private readonly Dictionary<DayOfWeek, ToggleButton> dayButtons = [];
    private readonly Dictionary<Gear, CheckBox> gearBoxes = [];
    private Place? place;

    public ExercisePage()
    {
        InitializeComponent();
        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        foreach (var day in Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)first + i) % 7)))
        {
            var button = new ToggleButton { Content = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day), Width = 64, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(0, 5, 0, 5) };
            dayButtons[day] = button;
            DayPanel.Children.Add(button);
        }
        foreach (var gear in Enum.GetValues<Gear>())
        {
            var box = new CheckBox { Content = gear.Label(), Margin = new Thickness(0, 0, 18, 6), MinWidth = 160 };
            gearBoxes[gear] = box;
            GearPanel.Children.Add(box);
        }
        MinutesBox.ItemsSource = MinuteChoices.Select(m => $"{m} minutes").ToList();
        ShowPlan();
    }

    private static App App => App.Current;

    /// <summary>The plan's settings in the form; a new plan's: three days a week, 30 minutes at 6 PM.</summary>
    private void ShowPlan()
    {
        var plan = ExerciseLog.Plan ?? new ExercisePlan(new HashSet<Gear>(), Fitness.Starting, DaySet.Monday | DaySet.Wednesday | DaySet.Friday,
            new TimeOnly(18, 0), 30, DateOnly.FromDateTime(DateTime.Today), UsualPlace.Ask);
        foreach (var (day, button) in dayButtons) button.IsChecked = plan.Days.Contains(day);
        foreach (var (gear, box) in gearBoxes) box.IsChecked = plan.Gear.Contains(gear);
        AtBox.Time = plan.At.ToTimeSpan();
        MinutesBox.SelectedIndex = Math.Max(0, Array.IndexOf(MinuteChoices, plan.Minutes));
        LevelBox.SelectedIndex = (int)plan.Level;
        WhereBox.SelectedIndex = (int)plan.Where;
        AlertsBox.IsChecked = ExerciseLog.AlertsOn;
        RestartButton.Show(ExerciseLog.Plan is not null);
        SaveButton.Content = ExerciseLog.Plan is null ? "Start this plan" : "Save the plan";
    }

    public void Refresh()
    {
        var plan = ExerciseLog.Plan;
        var today = DateOnly.FromDateTime(DateTime.Today);
        SafetyText.Text = ExerciseLog.Safety;
        InsulinText.Text = ExerciseLog.Diabetes() ? ExerciseLog.Insulin : "";
        InsulinText.Show(InsulinText.Text.Length > 0);
        if (plan is null)
        {
            TodayHeading.Text = "Set up your plan below: the days and time, how long, and what you have. None is needed.";
            TodayPanel.Show(false);
            WeekList.ItemsSource = null;
            WeekText.Text = $"0 of {Workouts.WeeklyGoalMinutes} minutes";
            WeekProgress.Value = 0;
            return;
        }

        var log = App.Database.ExerciseLog(today.AddDays(-7));
        var doneToday = log.FirstOrDefault(d => d.Day == today);
        if (plan.Days.Contains(today.DayOfWeek) || doneToday is not null)
        {
            var at = place ?? doneToday?.Place ?? ExerciseLog.PlaceFor(plan);
            (at == Place.Inside ? InBox : OutBox).IsChecked = true;
            var workout = Workouts.For(plan, today, at, App.Measures);
            TodayHeading.Text = $"Today at {Format.Time(plan.At)}: {workout.Kind switch { WorkoutKind.Cardio => "cardio", WorkoutKind.Strength => "strength", _ => "stretching" }}";
            WorkoutText.Text = workout.Title;
            SummaryText.Text = $"{workout.Summary} · about {workout.Kcal(ExerciseLog.WeightKg()):N0} kcal";
            NeedsText.Text = "You'll need: " + string.Join(", ", workout.Needs).ToLowerInvariant() + ".";
            StepsList.ItemsSource = workout.Steps.Select(s => "• " + s.Text + (s.Minutes > 0 ? $" ({s.Minutes} min)" : "")).ToList();
            DoneText.Text = doneToday is null ? "" : $"Done at {Format.Time(doneToday.Done)}";
            DoneButton.Show(doneToday is null);
            TodayPanel.Show(true);
        }
        else
        {
            var next = Workouts.Days(plan, today.AddDays(1), today.AddDays(7)).FirstOrDefault();
            TodayHeading.Text = next == default ? "No exercise days are set." : $"A day off. Next: {next:dddd} at {Format.Time(plan.At)}, {Workouts.KindOn(plan, next).ToString().ToLowerInvariant()}.";
            TodayPanel.Show(false);
        }

        // The week, from its first day.
        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var start = today.AddDays(-(((int)today.DayOfWeek - (int)first + 7) % 7));
        var week = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();
        var minutes = log.Where(d => d.Day >= start).Sum(d => d.Minutes);
        WeekText.Text = $"{minutes} of {Workouts.WeeklyGoalMinutes} minutes";
        WeekProgress.Value = Math.Min(minutes, Workouts.WeeklyGoalMinutes);
        WeekList.ItemsSource = week.Select(d =>
        {
            var done = log.FirstOrDefault(l => l.Day == d);
            if (done is not null) return new WeekRow(d, $"{done.Title} · {done.Minutes} min · {done.Kcal:N0} kcal", true, d < today);
            if (!plan.Days.Contains(d.DayOfWeek)) return new WeekRow(d, "rest", false, false);
            var w = Workouts.For(plan, d, ExerciseLog.PlaceFor(plan), App.Measures);
            return new WeekRow(d, $"{w.Title}, {w.Minutes} min" + (d < today ? " · not done" : ""), false, d < today);
        }).ToList();
    }

    private void Place_Click(object sender, RoutedEventArgs e)
    {
        place = InBox.IsChecked == true ? Place.Inside : Place.Outside;
        Refresh();
    }

    private Workout? Today => ExerciseLog.Today(DateOnly.FromDateTime(DateTime.Today), place ?? (InBox.IsChecked == true ? Place.Inside : Place.Outside));

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Today is { } workout) ExerciseLog.Start(workout);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        if (Today is { } workout) ExerciseLog.Done(workout);
    }

    private ExercisePlan Read(DateOnly start) => new(
        gearBoxes.Where(g => g.Value.IsChecked == true).Select(g => g.Key).ToHashSet(),
        (Fitness)Math.Max(0, LevelBox.SelectedIndex),
        dayButtons.Aggregate(DaySet.None, (set, b) => set.With(b.Key, b.Value.IsChecked == true)),
        TimeOnly.FromTimeSpan(AtBox.Time ?? new TimeSpan(18, 0, 0)),
        MinuteChoices[Math.Max(0, MinutesBox.SelectedIndex)],
        start,
        (UsualPlace)Math.Max(0, WhereBox.SelectedIndex));

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var current = ExerciseLog.Plan;
        // A new level starts the climb again from it.
        var start = current is null || current.Level != (Fitness)Math.Max(0, LevelBox.SelectedIndex) ? DateOnly.FromDateTime(DateTime.Today) : current.Start;
        var plan = Read(start);
        if (plan.Days == DaySet.None)
        {
            SavedText.Text = "Pick at least one day.";
            return;
        }
        ExerciseLog.Plan = plan;
        ExerciseLog.AlertsOn = AlertsBox.IsChecked == true;
        SavedText.Text = "Saved.";
        ShowPlan();
        App.ExerciseChanged();
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        ExerciseLog.Plan = Read(DateOnly.FromDateTime(DateTime.Today));
        SavedText.Text = "Started over at the level chosen.";
        App.ExerciseChanged();
    }
}
