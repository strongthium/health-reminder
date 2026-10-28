namespace Strongthium.HealthReminder;

/// <summary>Where a session is done.</summary>
public enum Place { Outside, Inside }

/// <summary>Where sessions are usually done: asked each time, or always one.</summary>
public enum UsualPlace { Ask, Outside, Inside }

/// <summary>How fit someone is to start with.</summary>
public enum Fitness { Starting, Some, Fit }

/// <summary>Equipment someone may have; none is needed.</summary>
public enum Gear { Bike, JumpRope, Bands, Dumbbells, Kettlebell, PullUpBar, Mat, Stairs, Treadmill, ExerciseBike, Rower }

public enum WorkoutKind { Cardio, Strength, Stretch }

/// <summary>Someone's exercise: what they have, how fit they are, and the days, time and minutes set aside, since
/// <paramref name="Start"/> (sessions get harder every three weeks from it).</summary>
public sealed record ExercisePlan(IReadOnlySet<Gear> Gear, Fitness Level, DaySet Days, TimeOnly At, int Minutes, DateOnly Start, UsualPlace Where);

/// <summary>A day's session: what, where, for how long, what's needed, and its steps (those that take time with their
/// minutes, for a timer). <paramref name="Met"/> is how hard it is, in METs, for the calories.</summary>
public sealed record Workout(DateOnly Day, WorkoutKind Kind, Place Place, string Title, string Summary, int Minutes, string[] Needs, CookStep[] Steps, double Met)
{
    /// <summary>Calories it uses, roughly: METs × kilograms × hours, to the nearest 5.</summary>
    public int Kcal(double kg) => (int)(Math.Round(Met * kg * Minutes / 60 / 5) * 5);
}

/// <summary>A day's session done.</summary>
public sealed record ExerciseDone(DateOnly Day, string Title, Place Place, int Minutes, int Kcal, DateTime Done);

/// <summary>
/// Exercise from no equipment up: on the days set aside, cardio, strength and stretching in turn, each with a version for
/// going out (a walk, a walk-jog or a ride, with its distance) and one for staying in (moves in a room, or the treadmill,
/// exercise bike or rower if there is one), using the equipment there is. It starts at the level given and steps up every
/// three weeks: a walk becomes a brisk walk, then intervals of jogging; wall push-ups become knee push-ups, then full
/// ones. Calories are from the Compendium of Physical Activities' METs. It aims at the WHO's 150 minutes a week.
/// </summary>
public static class Workouts
{
    public const int WeeklyGoalMinutes = 150;

    public static string Label(this Gear gear) => gear switch
    {
        Gear.Bike => "Bike",
        Gear.JumpRope => "Jump rope",
        Gear.Bands => "Resistance bands",
        Gear.Dumbbells => "Dumbbells",
        Gear.Kettlebell => "Kettlebell",
        Gear.PullUpBar => "Pull-up bar",
        Gear.Mat => "Exercise mat",
        Gear.Stairs => "Stairs at home",
        Gear.Treadmill => "Treadmill",
        Gear.ExerciseBike => "Exercise bike",
        _ => "Rowing machine",
    };

    /// <summary>How hard, 0 to 5: from the level started at (0, 2 or 4), a step every three weeks.</summary>
    public static int Stage(ExercisePlan plan, DateOnly day) =>
        Math.Clamp((int)plan.Level * 2 + Math.Max(0, day.DayNumber - plan.Start.DayNumber) / 21, 0, 5);

    /// <summary>The days set aside from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static IEnumerable<DateOnly> Days(ExercisePlan plan, DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
            if (plan.Days.Contains(day.DayOfWeek)) yield return day;
    }

    /// <summary>What a day's session is: cardio, strength and stretching in turn (two days a week or fewer, cardio
    /// and strength), counted from the start.</summary>
    public static WorkoutKind KindOn(ExercisePlan plan, DateOnly day)
    {
        var start = plan.Start <= day ? plan.Start : day;
        var index = Days(plan, start, day.AddDays(-1)).Count();
        WorkoutKind[] turn = plan.Days.Count() <= 2 ? [WorkoutKind.Cardio, WorkoutKind.Strength] : [WorkoutKind.Cardio, WorkoutKind.Strength, WorkoutKind.Cardio, WorkoutKind.Stretch];
        return turn[index % turn.Length];
    }

    private static int Count(this DaySet days) => Enum.GetValues<DayOfWeek>().Count(d => days.Contains(d));

    /// <summary>A day's session, done at <paramref name="place"/>; distances in km or miles first as
    /// <paramref name="system"/> says.</summary>
    public static Workout For(ExercisePlan plan, DateOnly day, Place place, MeasureSystem system)
    {
        var stage = Stage(plan, day);
        var kind = KindOn(plan, day);
        return kind switch
        {
            WorkoutKind.Cardio => place == Place.Outside ? CardioOutside(plan, day, stage, system) : CardioInside(plan, day, stage, system),
            WorkoutKind.Strength => Strength(plan, day, stage, place),
            _ => Stretch(plan, day, place),
        };
    }

    private static string Distance(double km, MeasureSystem system) =>
        system == MeasureSystem.Metric ? $"about {km:0.0} km ({km / 1.609344:0.0} mi)" : $"about {km / 1.609344:0.0} mi ({km:0.0} km)";

    private static Workout CardioOutside(ExercisePlan plan, DateOnly day, int stage, MeasureSystem system)
    {
        var minutes = plan.Minutes;
        var main = Math.Max(5, minutes - 10);
        var needs = new List<string> { "Walking or running shoes", "Water", "Clothes for the weather", "Your phone" };
        // With a bike, every other cardio day is a ride.
        var cardioDays = Days(plan, plan.Start <= day ? plan.Start : day, day.AddDays(-1)).Count(d => KindOn(plan, d) == WorkoutKind.Cardio);
        if (plan.Gear.Contains(Gear.Bike) && cardioDays % 2 == 1)
        {
            var (speed, met, how) = stage >= 3 ? (18.0, 6.8, "a steady ride, breathing harder but able to talk") : (14.0, 4.0, "an easy ride");
            needs.Remove("Walking or running shoes");
            needs.InsertRange(0, ["Bike", "Helmet"]);
            return new(day, WorkoutKind.Cardio, Place.Outside, $"Ride {Distance(speed * main / 60 + 1.5, system)}", $"{how}, {minutes} minutes", minutes, needs.ToArray(),
                [new("Check the tires and brakes."), new("Warm up: ride easy.", 5), new($"Ride: {how}.", main), new("Cool down: ride easy.", 5), new("Stretch your legs for a minute or two.")], met);
        }
        var (pace, mets, title, step) = stage switch
        {
            0 => (4.5, 3.0, "Walk", "Walk at an easy pace."),
            1 => (5.5, 4.3, "Brisk walk", "Walk briskly: breathing faster, still able to talk."),
            2 => (5.8, 5.0, "Brisk walk with faster bursts", "Walk briskly; every 5 minutes, walk as fast as you can for 1 minute."),
            3 => (6.5, 6.0, "Walk and jog", "Repeat: jog gently for 1 minute, walk for 2."),
            4 => (7.5, 7.0, "Jog and walk", "Repeat: jog for 3 minutes, walk for 1."),
            _ => (8.5, 8.0, "Jog", "Jog at a pace you could keep up while talking in short sentences."),
        };
        var km = pace * main / 60 + 4.5 * 10 / 60;
        return new(day, WorkoutKind.Cardio, Place.Outside, $"{title} {Distance(km, system)}", $"{minutes} minutes out of doors", minutes, needs.ToArray(),
            [new("Warm up: walk easy.", 5), new(step, main), new("Cool down: walk easy.", 5), new("Stretch your calves and thighs.")], mets);
    }

    private static Workout CardioInside(ExercisePlan plan, DateOnly day, int stage, MeasureSystem system)
    {
        var minutes = plan.Minutes;
        var main = Math.Max(5, minutes - 8);
        if (plan.Gear.Contains(Gear.Treadmill))
        {
            var (pace, met, how) = stage switch { <= 1 => (5.0, 3.5, "a brisk walk"), <= 3 => (6.0, 5.0, "a fast walk, with a 1% incline"), _ => (8.0, 8.0, "a jog") };
            return new(day, WorkoutKind.Cardio, Place.Inside, $"Treadmill: {how}", $"{Distance(pace * main / 60, system)}, {minutes} minutes", minutes, ["Shoes", "Water", "A towel"],
                [new("Warm up: walk slowly.", 4), new($"At {pace:0.0} km/h ({pace / 1.609344:0.0} mph): {how}.", main), new("Cool down: walk slowly.", 4)], met);
        }
        if (plan.Gear.Contains(Gear.ExerciseBike) || plan.Gear.Contains(Gear.Rower))
        {
            var bike = plan.Gear.Contains(Gear.ExerciseBike);
            var hard = stage >= 3;
            var what = bike ? "Exercise bike" : "Rowing";
            return new(day, WorkoutKind.Cardio, Place.Inside, $"{what}: {(hard ? "intervals" : "steady")}", $"{minutes} minutes", minutes, ["Water", "A towel"],
                [new("Warm up: easy.", 4), new(hard ? "Repeat: 1 minute hard, 2 minutes easy." : "Steady: breathing harder, still able to talk.", main), new("Cool down: easy.", 4)],
                bike ? (hard ? 6.8 : 4.8) : (hard ? 7.0 : 4.8));
        }
        // No machine: moves in a room, in rounds of a minute each.
        string[] moves = stage switch
        {
            0 => ["March on the spot, swinging your arms", plan.Gear.Contains(Gear.Stairs) ? "Step up and down the bottom stair" : "Side steps, side to side", "Knee lifts, one at a time", "Arm circles, forward then back"],
            1 or 2 => ["Jumping jacks (or step-jacks, stepping out instead of jumping)", "High knees, on the spot", plan.Gear.Contains(Gear.Stairs) ? "Step-ups on the bottom stair" : "Fast side steps", "Shadow boxing"],
            _ => [plan.Gear.Contains(Gear.JumpRope) ? "Jump rope" : "Jumping jacks", "High knees", "Mountain climbers", "Squat jumps (or fast squats)"],
        };
        var rounds = Math.Max(1, main / (moves.Length + 1));
        var steps = new List<CookStep> { new("Warm up: march on the spot and roll your shoulders.", 4) };
        steps.Add(new($"Do each move for 1 minute, then rest 1 minute; {rounds} round{(rounds == 1 ? "" : "s")}: " + string.Join("; ", moves) + ".", rounds * (moves.Length + 1)));
        steps.Add(new("Cool down: walk around slowly, then stretch.", 4));
        var mets = stage == 0 ? 3.5 : stage <= 2 ? 4.5 : 6.0;
        var needs = new List<string> { "Shoes", "Water", "Some room to move" };
        if (plan.Gear.Contains(Gear.JumpRope) && stage >= 3) needs.Insert(1, "Jump rope");
        return new(day, WorkoutKind.Cardio, Place.Inside, stage == 0 ? "Easy moves indoors" : "Cardio indoors", $"{moves.Length} moves, {rounds} round{(rounds == 1 ? "" : "s")}, {minutes} minutes",
            minutes, needs.ToArray(), steps.ToArray(), mets);
    }

    private static Workout Strength(ExercisePlan plan, DateOnly day, int stage, Place place)
    {
        var gear = plan.Gear;
        var reps = 8 + stage;
        var moves = new List<string>
        {
            gear.Contains(Gear.Dumbbells) ? $"{reps} goblet squats, holding a dumbbell at your chest"
                : stage == 0 ? $"{reps} sit-to-stands from a chair" : $"{reps + 2} squats",
            stage <= 1 ? $"{reps} wall push-ups" : stage <= 3 ? $"{reps} knee push-ups{(place == Place.Outside ? " (or on a park bench)" : "")}" : $"{reps} push-ups",
            gear.Contains(Gear.Dumbbells) ? $"{reps} dumbbell rows each arm" : gear.Contains(Gear.Bands) ? $"{reps + 4} band rows" : $"{reps} bird-dogs each side",
            $"{reps + 4} glute bridges",
        };
        if (stage >= 2) moves.Add(place == Place.Outside ? $"{reps - 2} step-ups each leg on a bench" : $"{reps - 2} reverse lunges each leg");
        if (gear.Contains(Gear.Kettlebell) && stage >= 2) moves.Add($"{reps + 4} kettlebell swings");
        if (gear.Contains(Gear.Bands)) moves.Add($"{reps + 4} band chest presses");
        if (gear.Contains(Gear.PullUpBar)) moves.Add(stage >= 4 ? $"{Math.Max(1, stage - 2)} chin-ups (or as many as you can)" : $"a {15 + stage * 5}-second hang from the bar");
        moves.Add($"a {15 + stage * 5}-second plank");
        var rounds = Math.Min(4, 2 + stage / 2);
        var minutes = plan.Minutes;
        var needs = new List<string> { "Water", gear.Contains(Gear.Mat) ? "Exercise mat" : "A towel or mat for the floor" };
        if (stage == 0) needs.Add("A sturdy chair");
        needs.AddRange(new[] { Gear.Dumbbells, Gear.Bands, Gear.Kettlebell, Gear.PullUpBar }.Where(gear.Contains).Select(g => g.Label()));
        var round = Math.Max(4, (minutes - 8) / rounds);
        return new(day, WorkoutKind.Strength, place, "Strength", $"{moves.Count} moves, {rounds} rounds, {minutes} minutes{(place == Place.Outside ? ", at a park or in the yard" : "")}", minutes, needs.ToArray(),
            [new("Warm up: march on the spot, arm circles, a few slow squats.", 5),
             .. Enumerable.Range(1, rounds).Select(i => new CookStep($"Round {i} of {rounds}: " + string.Join("; ", moves) + ". Rest a minute after.", round)),
             new("Cool down: stretch your legs, chest and back.", 3)],
            stage >= 3 ? 5.0 : 3.5);
    }

    private static Workout Stretch(ExercisePlan plan, DateOnly day, Place place)
    {
        var minutes = plan.Minutes;
        string[] stretches = ["Calves, against a wall", "Fronts of the thighs, holding a chair", "Backs of the thighs, one heel on a step", "Hips: a lunge, back knee down", "Chest: hands behind your back", "Back: knees to chest, lying down"];
        // Each stretch twice on each side, 30 seconds a time: 2 minutes; whatever's left is easy movement first: a walk,
        // or indoors whatever machine there is, or moving about the home.
        var warmUp = Math.Max(3, minutes - stretches.Length * 2);
        var move = place == Place.Outside ? "Walk at an easy pace."
            : plan.Gear.Contains(Gear.Treadmill) ? "Walk easy on the treadmill."
            : plan.Gear.Contains(Gear.ExerciseBike) ? "Pedal easy on the exercise bike."
            : warmUp > 5 ? "Move easy: walk around your home, step side to side to music, roll your shoulders."
            : "Warm up: march on the spot.";
        return new(day, WorkoutKind.Stretch, place, place == Place.Outside ? "Easy walk and stretch" : "Stretch", $"{minutes} minutes, easy", minutes,
            place == Place.Outside ? ["Walking shoes", "Water"] : [plan.Gear.Contains(Gear.Mat) ? "Exercise mat" : "A towel or mat for the floor", "A chair"],
            [new(move, warmUp),
             .. stretches.Select(s => new CookStep($"{s}: hold 30 seconds, each side, twice, without bouncing.", 2))],
            2.5);
    }
}
