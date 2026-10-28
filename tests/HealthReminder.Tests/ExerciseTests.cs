namespace Strongthium.HealthReminder.Tests;

public sealed class ExerciseTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);
    private static readonly DaySet MonWedFriSat = DaySet.Monday | DaySet.Wednesday | DaySet.Friday | DaySet.Saturday;

    private static ExercisePlan Plan(Fitness level = Fitness.Starting, int minutes = 30, params Gear[] gear) =>
        new(gear.ToHashSet(), level, MonWedFriSat, new TimeOnly(18, 0), minutes, Monday, UsualPlace.Ask);

    [Fact]
    public void CardioStrengthCardioStretchInTurn()
    {
        var plan = Plan();
        var kinds = Workouts.Days(plan, Monday, Monday.AddDays(6)).Select(d => Workouts.KindOn(plan, d));
        Assert.Equal([WorkoutKind.Cardio, WorkoutKind.Strength, WorkoutKind.Cardio, WorkoutKind.Stretch], kinds);
        // Twice a week: cardio and strength.
        var twice = plan with { Days = DaySet.Tuesday | DaySet.Thursday };
        Assert.Equal([WorkoutKind.Cardio, WorkoutKind.Strength], Workouts.Days(twice, Monday, Monday.AddDays(6)).Select(d => Workouts.KindOn(twice, d)));
    }

    [Fact]
    public void ItGetsHarderEveryThreeWeeks()
    {
        var plan = Plan();
        Assert.Equal(0, Workouts.Stage(plan, Monday));
        Assert.Equal(1, Workouts.Stage(plan, Monday.AddDays(21)));
        Assert.Equal(5, Workouts.Stage(plan, Monday.AddDays(400)));
        Assert.Equal(4, Workouts.Stage(Plan(Fitness.Fit), Monday));
    }

    [Fact]
    public void GoingOutIsAWalkWithItsDistance()
    {
        var walk = Workouts.For(Plan(), Monday, Place.Outside, MeasureSystem.Us);
        // 20 minutes at 4.5 km/h, and 10 of warming up and cooling down: 2.25 km, 1.4 mi.
        Assert.StartsWith("Walk about 1.4 mi (2.3 km)", walk.Title);
        Assert.Contains("Walking or running shoes", walk.Needs);
        Assert.Equal(30, walk.Minutes);
        // Started fit, it's a jog.
        Assert.StartsWith("Jog and walk", Workouts.For(Plan(Fitness.Fit), Monday, Place.Outside, MeasureSystem.Metric).Title);
    }

    [Fact]
    public void StayingInIsMovesInARoomOrTheMachineThereIs()
    {
        var room = Workouts.For(Plan(), Monday, Place.Inside, MeasureSystem.Metric);
        Assert.Contains(room.Steps, s => s.Text.Contains("March on the spot"));
        Assert.All(room.Steps.Where(s => s.Minutes > 0), s => Assert.True(s.Minutes > 0));
        var treadmill = Workouts.For(Plan(gear: Gear.Treadmill), Monday, Place.Inside, MeasureSystem.Metric);
        Assert.StartsWith("Treadmill", treadmill.Title);
    }

    [Fact]
    public void StrengthUsesWhatThereIs()
    {
        var wednesday = Monday.AddDays(2);
        var none = Workouts.For(Plan(), wednesday, Place.Inside, MeasureSystem.Metric);
        Assert.Equal(WorkoutKind.Strength, none.Kind);
        Assert.Contains(none.Steps, s => s.Text.Contains("wall push-ups") && s.Text.Contains("sit-to-stands"));
        var dumbbells = Workouts.For(Plan(gear: Gear.Dumbbells), wednesday, Place.Inside, MeasureSystem.Metric);
        Assert.Contains(dumbbells.Steps, s => s.Text.Contains("goblet squats"));
        Assert.Contains("Dumbbells", dumbbells.Needs);
    }

    [Fact]
    public void ABikeMakesEveryOtherCardioDayARide()
    {
        var plan = Plan(gear: Gear.Bike);
        var friday = Monday.AddDays(4);   // the second cardio day
        var ride = Workouts.For(plan, friday, Place.Outside, MeasureSystem.Metric);
        Assert.StartsWith("Ride", ride.Title);
        Assert.Contains("Helmet", ride.Needs);
    }

    [Fact]
    public void CaloriesAreMetsTimesWeightTimesHours()
    {
        var walk = Workouts.For(Plan(), Monday, Place.Outside, MeasureSystem.Metric);
        // 3.0 METs × 80 kg × 0.5 h = 120.
        Assert.Equal(120, walk.Kcal(80));
    }

    [Fact]
    public void KeepsTheExerciseDone()
    {
        using var database = new Database();
        database.LogExercise(new ExerciseDone(Monday, "Walk", Place.Outside, 30, 120, new DateTime(2026, 10, 26, 18, 35, 0)));
        database.LogExercise(new ExerciseDone(Monday, "Walk", Place.Outside, 25, 100, new DateTime(2026, 10, 26, 18, 30, 0)));
        var done = Assert.Single(database.ExerciseLog(Monday));
        Assert.Equal((25, Place.Outside), (done.Minutes, done.Place));
    }
}
