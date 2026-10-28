using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>One reminder of the Today page.</summary>
public sealed class TodayItem(ScheduledDose scheduled)
{
    public ScheduledDose Scheduled { get; } = scheduled;
    private Reminder Reminder => Scheduled.Dose.Reminder;
    public ReminderKind Kind => Reminder.Kind;
    public string KindText => Kind.ShortLabel();
    public string Name => Reminder.Name;
    public string Form => Reminder.Form;
    public PillAppearance Appearance => Reminder.Appearance;
    public double Count => Kind == ReminderKind.Pill && Reminder.Unit is "tablet" or "capsule" or "softgel" or "lozenge" ? Scheduled.Dose.Amount : 1;
    public bool CanTake => Scheduled.State is not (DoseState.Taken or DoseState.Skipped);
    public bool IsCheck => Kind.IsCheck();
    public string Value { get; set; } = "";
    public string DoneText => Reminder.RecordVideo ? Evidence.ButtonText(Kind) : Kind.IsCheck() ? "Save" : Kind.DoneText();

    /// <summary>What to do, and once done, what was recorded: "Take 2 tablets · Abdomen, left", "6.4 mmol/L".</summary>
    public string Detail
    {
        get
        {
            var record = Scheduled.Record;
            if (record is { Status: DoseStatus.Taken } && record.Value.Length > 0) return Format.Reading(record.Value, Reminder.ValueUnit);
            var detail = Format.Instruction(Reminder, Scheduled.Dose.Amount);
            if (record?.Site.Length > 0) detail += " · " + record.Site;
            return detail;
        }
    }

    public string StateText => Scheduled.State switch
    {
        DoseState.Taken => "Done " + Format.Time(Scheduled.Record!.At),
        DoseState.Skipped => "Skipped",
        DoseState.Snoozed => "Snoozed to " + Format.Time(Scheduled.SnoozedUntil!.Value),
        DoseState.Due => "Due now",
        DoseState.Missed => "Missed",
        _ => "Later",
    };

    /// <summary>What's still to come is said plainly; everything else has a coloured badge.</summary>
    public bool HasStateChip => Scheduled.State != DoseState.Upcoming;

    public Brush StateBrush => KindLook.Brush(Scheduled.State switch
    {
        DoseState.Taken => Color.FromRgb(0x2E, 0x7D, 0x32),
        DoseState.Skipped => Color.FromRgb(0x5F, 0x6B, 0x76),
        DoseState.Snoozed => Color.FromRgb(0x8A, 0x5A, 0x00),
        DoseState.Due => Color.FromRgb(0xC6, 0x28, 0x28),
        _ => Color.FromRgb(0x8E, 0x24, 0x20),
    });
}

/// <summary>Everything due at one time of the day, together.</summary>
public sealed class TodayGroup(DateTime due, List<TodayItem> items)
{
    public string Time => Format.Time(due);
    public List<TodayItem> Items { get; } = items;
    public bool CanDoAll => Items.Count(i => i.CanTake) > 1;

    public string Summary
    {
        get
        {
            var open = Items.Count(i => i.CanTake);
            return open == 0 ? "all done" : Items.Count == 1 ? "" : open == Items.Count ? $"{Items.Count} things" : $"{open} of {Items.Count} left";
        }
    }
}

/// <summary>The day: how it's going, and everything due, by time, each answered here as well as on the alarm.</summary>
public sealed partial class TodayPage : UserControl, IPage
{
    /// <summary>The pill drawn on the page when there are no reminders yet.</summary>
    public static PillAppearance SamplePill { get; } = new(["WHITE", "TURQUOISE"], "CAPSULE", 0, "", 1);

    private string? signature;

    public TodayPage() => InitializeComponent();

    public void Refresh()
    {
        var app = App.Current;
        var now = DateTime.Now;
        var reminders = app.Reminders;

        DateText.Text = now.ToString("D", CultureInfo.CurrentCulture);
        var day = app.Schedule.Day(reminders, now);
        // Rebuilt only when something changed, so that a reading being typed in isn't wiped by the minute's refresh.
        var newSignature = string.Join('|', day.Select(d => $"{d.Dose.Key}:{d.State}:{d.Dose.Reminder.Name}:{d.Dose.Amount}:{d.Dose.Reminder.RecordVideo}"));
        if (newSignature != signature)
        {
            signature = newSignature;
            TodayList.ItemsSource = day
                .GroupBy(d => d.Dose.Due)
                .Select(g => new TodayGroup(g.Key, g.Select(d => new TodayItem(d)).ToList()))
                .ToList();
        }

        Summary.Show(day.Count > 0);
        Empty.Show(reminders.Count == 0);
        NothingToday.Show(reminders.Count > 0 && day.Count == 0);
        var done = day.Count(d => d.State == DoseState.Taken);
        ProgressText.Text = $"{done} of {day.Count} done";
        DayProgress.Maximum = Math.Max(1, day.Count);
        DayProgress.Value = day.Count(d => d.State is DoseState.Taken or DoseState.Skipped);
        var counts = new List<string>();
        void Count(DoseState state, string text)
        {
            var n = day.Count(d => d.State == state);
            if (n > 0) counts.Add($"{n} {text}");
        }
        Count(DoseState.Due, "due");
        Count(DoseState.Snoozed, "snoozed");
        Count(DoseState.Missed, "missed");
        Count(DoseState.Skipped, "skipped");
        Count(DoseState.Upcoming, "to come");
        CountsText.Text = string.Join(" · ", counts);
        var next = Schedule.Next(reminders, now);
        NextText.Text = next is null ? "Nothing more is scheduled" : $"Next: {next.Reminder.Name} at {Format.When(next.Due, now)}";

        var today = DateOnly.FromDateTime(now);
        var workout = ExerciseLog.Today(today);
        var doneExercise = app.Database.ExerciseLog(today).FirstOrDefault();
        ExerciseCard.Show(workout is not null || doneExercise is not null);
        if (doneExercise is not null)
        {
            ExerciseTitle.Text = $"Exercise done at {Format.Time(doneExercise.Done)}";
            ExerciseDetail.Text = $"{doneExercise.Title} · {doneExercise.Minutes} min · {doneExercise.Kcal:N0} kcal";
            ExerciseButton.Show(false);
        }
        else if (workout is not null && ExerciseLog.Plan is { } plan)
        {
            ExerciseTitle.Text = $"Exercise at {Format.Time(plan.At)}: {workout.Title}";
            ExerciseDetail.Text = $"{workout.Summary}. You'll need: {string.Join(", ", workout.Needs).ToLowerInvariant()}.";
            ExerciseButton.Show(true);
        }

        var kitchen = KitchenLog.AlertsOn ? KitchenLog.Events(now.Date, now.Date.AddDays(1).AddTicks(-1)) : [];
        KitchenList.ItemsSource = kitchen.Select(e => new KitchenRow(e, KitchenLog.IsDone(e), now)).ToList();
        KitchenCard.Show(kitchen.Count > 0);
    }

    private void Exercise_Click(object sender, RoutedEventArgs e)
    {
        if (ExerciseLog.Today(DateOnly.FromDateTime(DateTime.Today)) is { } workout) ExerciseLog.Start(workout);
    }

    private void KitchenSteps_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KitchenRow { Event.Meal.Recipe: { } recipe } row) App.Current.ShowSteps(recipe, row.Event.Meal.At);
    }

    private void Add_Click(object sender, RoutedEventArgs e) => this.MainWindowOf().Edit(null);

    private void Plan_Click(object sender, RoutedEventArgs e) => this.MainWindowOf().Navigate(MainWindow.EatDrinkPage);

    private static void MarkDone(TodayItem item, RecordOutcome outcome)
    {
        var app = App.Current;
        var dose = item.Scheduled.Dose;
        app.Schedule.Done(dose, DateTime.Now, item.IsCheck ? item.Value : "",
            item.Kind == ReminderKind.Injection ? app.Schedule.NextSite(dose.Reminder) : "",
            dose.Reminder.RecordVideo ? outcome.Video : "", dose.Reminder.RecordVideo ? outcome.Sha256 : "");
        PantryLog.Eaten(dose);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TodayItem item) return;
        if (Evidence.Record(this.MainWindowOf(), [item.Scheduled.Dose]) is not { } outcome) return;
        MarkDone(item, outcome);
        App.Current.Changed();
    }

    private void AllDone_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TodayGroup group) return;
        var open = group.Items.Where(i => i.CanTake).ToList();
        if (Evidence.Record(this.MainWindowOf(), open.Select(i => i.Scheduled.Dose).ToList()) is not { } outcome) return;
        foreach (var item in open) MarkDone(item, outcome);
        App.Current.Changed();
    }
}

/// <summary>A kitchen alert of today's, done or to come.</summary>
public sealed class KitchenRow(KitchenEvent e, bool done, DateTime now)
{
    public KitchenEvent Event => e;
    public string Time => Format.Time(e.At);
    public string Title => e.Title;
    public string Detail => e.Meal.Recipe is { } r ? $"{Format.Meal(e)}: {r.Name}" : Format.Meal(e);
    public bool Done => done;
    public bool HasSteps => e.Meal.Recipe is not null && e.Step is KitchenStep.Prep or KitchenStep.Cook;
    public double Opacity => done || e.At < now.AddHours(-3) ? 0.6 : 1;
}
