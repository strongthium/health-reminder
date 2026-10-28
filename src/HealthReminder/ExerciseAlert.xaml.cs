using System.Windows;

namespace Strongthium.HealthReminder;

/// <summary>
/// An exercise alert. A quarter of an hour ahead it says what the session is and what's needed, so shoes and water
/// are ready; at the time, it asks whether you're going out or staying in (when that's left to the day), shows that
/// session, and Start opens its steps at the side of the screen.
/// </summary>
public sealed partial class ExerciseAlert : Window
{
    private Place place;
    private bool loading;

    public ExerciseEvent Event { get; }

    public ExerciseAlert(ExerciseEvent e, ExercisePlan plan)
    {
        InitializeComponent();
        Icon = App.Icon;
        Event = e;
        place = ExerciseLog.PlaceFor(plan);
        loading = true;
        (place == Place.Inside ? InBox : OutBox).IsChecked = true;
        loading = false;
        PlacePanel.Show(plan.Where == UsualPlace.Ask);
        WhenText.Text = $"Exercise at {Format.Time(e.Day.ToDateTime(plan.At))} · {plan.Minutes} minutes";
        TitleText.Text = e.Ready ? "Exercise in 15 minutes: get ready" : "Time to exercise";
        GoButton.Content = e.Ready ? "Ready" : "Start";
        SnoozeButton.Show(!e.Ready);
        InsulinText.Text = ExerciseLog.Diabetes() ? ExerciseLog.Insulin : "";
        InsulinText.Show(InsulinText.Text.Length > 0);
        Show(plan);
    }

    private Workout? Workout => ExerciseLog.Today(Event.Day, place);

    private void Show(ExercisePlan plan)
    {
        if (Workout is not { } workout) return;
        WorkoutText.Text = workout.Title;
        SummaryText.Text = $"{workout.Summary} · about {workout.Kcal(ExerciseLog.WeightKg()):N0} kcal";
        NeedsText.Text = string.Join(" · ", workout.Needs);
    }

    private void Place_Click(object sender, RoutedEventArgs e)
    {
        if (loading || ExerciseLog.Plan is not { } plan) return;
        place = InBox.IsChecked == true ? Place.Inside : Place.Outside;
        Show(plan);
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        ExerciseLog.Answered(Event);
        if (!Event.Ready && Workout is { } workout) ExerciseLog.Start(workout);
        Close();
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        ExerciseLog.Snooze(Event, TimeSpan.FromMinutes(15));
        Close();
    }

    /// <summary>Not today: neither alert comes back for the day.</summary>
    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        ExerciseLog.Answered(Event);
        ExerciseLog.Answered(Event with { Ready = !Event.Ready });
        App.Current.ExerciseChanged();
        Close();
    }
}
