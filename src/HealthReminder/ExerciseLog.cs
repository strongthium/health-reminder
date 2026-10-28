using System.Globalization;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>An exercise alert: a quarter of an hour ahead with what's needed, or the session itself.</summary>
public sealed record ExerciseEvent(DateTime At, bool Ready, DateOnly Day)
{
    public string Key => $"{Day:yyyy-MM-dd}T00:00|exercise|{(Ready ? "ready" : "start")}";
}

/// <summary>The exercise plan as it's set, its alerts and what's been done.</summary>
internal static class ExerciseLog
{
    public static readonly Brush Green = Frozen(0x2E, 0x7D, 0x32);
    private static readonly TimeSpan ReadyAhead = TimeSpan.FromMinutes(15);
    private static readonly Dictionary<string, DateTime> snoozed = [];

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static App App => App.Current;
    private static Database Database => App.Current.Database;

    /// <summary>The plan, or null until one is set up on the Exercise page.</summary>
    public static ExercisePlan? Plan
    {
        get
        {
            var saved = (Database.Setting("exercise") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
            if (!saved.TryGetValue("start", out var start) || !DateOnly.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var started)) return null;
            return new ExercisePlan(
                saved.GetValueOrDefault("gear", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => Enum.TryParse<Gear>(g, out var x) ? (Gear?)x : null).OfType<Gear>().ToHashSet(),
                Enum.TryParse<Fitness>(saved.GetValueOrDefault("level"), out var level) ? level : Fitness.Starting,
                int.TryParse(saved.GetValueOrDefault("days"), out var days) ? (DaySet)days : DaySet.Monday | DaySet.Wednesday | DaySet.Friday,
                TimeOnly.TryParse(saved.GetValueOrDefault("at"), CultureInfo.InvariantCulture, out var at) ? at : new TimeOnly(18, 0),
                int.TryParse(saved.GetValueOrDefault("minutes"), out var minutes) ? minutes : 30,
                started,
                Enum.TryParse<UsualPlace>(saved.GetValueOrDefault("where"), out var where) ? where : UsualPlace.Ask);
        }
        set
        {
            if (value is null)
            {
                Database.SetSetting("exercise", "");
                return;
            }
            Database.SetSetting("exercise", string.Join(';', new Dictionary<string, string>
            {
                ["gear"] = string.Join(',', value.Gear), ["level"] = value.Level.ToString(), ["days"] = ((int)value.Days).ToString(CultureInfo.InvariantCulture),
                ["at"] = value.At.ToString("HH:mm", CultureInfo.InvariantCulture), ["minutes"] = value.Minutes.ToString(CultureInfo.InvariantCulture),
                ["start"] = value.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["where"] = value.Where.ToString(),
            }.Select(v => v.Key + "=" + v.Value)));
        }
    }

    public static bool AlertsOn
    {
        get => Database.Setting("exercise_alerts") != "off";
        set => Database.SetSetting("exercise_alerts", value ? "on" : "off");
    }

    /// <summary>The place for a session: the usual one, or outside when it's asked each time and not yet answered.</summary>
    public static Place PlaceFor(ExercisePlan plan) => plan.Where == UsualPlace.Inside ? Place.Inside : Place.Outside;

    public static Workout? Today(DateOnly day, Place? place = null) =>
        Plan is { } plan && plan.Days.Contains(day.DayOfWeek) ? Workouts.For(plan, day, place ?? PlaceFor(plan), App.Measures) : null;

    /// <summary>Today's and tomorrow's alerts from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static List<ExerciseEvent> Events(DateTime from, DateTime to)
    {
        if (Plan is not { } plan) return [];
        var events = new List<ExerciseEvent>();
        foreach (var day in Workouts.Days(plan, DateOnly.FromDateTime(from), DateOnly.FromDateTime(to)))
        {
            var at = day.ToDateTime(plan.At);
            events.Add(new(at - ReadyAhead, true, day));
            events.Add(new(at, false, day));
        }
        return events.Where(e => e.At >= from && e.At <= to).ToList();
    }

    /// <summary>What's due now and not done, skipped or snoozed; up to two hours late.</summary>
    public static List<ExerciseEvent> Due(DateTime now)
    {
        if (!AlertsOn) return [];
        var done = Database.KitchenDone(now.Date.AddDays(-1));
        var logged = Database.ExerciseLog(DateOnly.FromDateTime(now)).Select(d => d.Day).ToHashSet();
        return Events(now.AddHours(-2), now)
            .Where(e => !done.Contains(e.Key) && !logged.Contains(e.Day) && !(snoozed.TryGetValue(e.Key, out var until) && until > now))
            .ToList();
    }

    public static void Answered(ExerciseEvent e)
    {
        Database.SetKitchenDone(e.Key);
        snoozed.Remove(e.Key);
    }

    public static void Snooze(ExerciseEvent e, TimeSpan time) => snoozed[e.Key] = DateTime.Now + time;

    /// <summary>The body weight, from the Eat &amp; drink page, for calories; 70 kg until it's filled in.</summary>
    public static double WeightKg()
    {
        var profile = (Database.Setting("profile") ?? "").Split(';').Select(p => p.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == "weight");
        return profile is not null && double.TryParse(profile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var kg) && kg > 0 ? kg : 70;
    }

    public static void Done(Workout workout)
    {
        Database.LogExercise(new ExerciseDone(workout.Day, workout.Title, workout.Place, workout.Minutes, workout.Kcal(WeightKg()), DateTime.Now));
        App.ExerciseChanged();
    }

    /// <summary>The workout's steps at the side of the screen, with Done to log it.</summary>
    public static void Start(Workout workout) =>
        App.ShowWorkout(workout);

    /// <summary>Whether insulin or blood sugar tests are among the reminders: exercise lowers blood sugar.</summary>
    public static bool Diabetes() =>
        App.Reminders.Any(r => r.Active && (r.Kind == ReminderKind.BloodSugar || r.Kind == ReminderKind.Injection && r.Name.Contains("insulin", StringComparison.OrdinalIgnoreCase)));

    public const string Safety =
        "Start easy and build up. Stop if you feel chest pain, faintness or unusual shortness of breath. If you have a heart, " +
        "lung or joint condition, are pregnant, or haven't been active for a long time, ask your doctor first.";

    public const string Insulin =
        "With insulin or some diabetes pills, exercise can lower your blood sugar: test before and after, and carry fast sugar (glucose tablets, juice).";
}
