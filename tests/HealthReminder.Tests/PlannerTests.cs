namespace Strongthium.HealthReminder.Tests;

public sealed class PlannerTests
{
    private static BodyProfile Person(Sex sex = Sex.Female, double cm = 165, double kg = 60, int age = 40, Activity activity = Activity.Sedentary,
        Pregnancy pregnancy = Pregnancy.None, string wake = "07:00", string sleep = "23:00", WeightGoal goal = WeightGoal.Maintain, Pace pace = Pace.Safe) =>
        new(sex, cm, kg, age, activity, pregnancy, TimeOnly.Parse(wake), TimeOnly.Parse(sleep), goal, pace);

    [Theory]
    [InlineData(32, WeightGoal.Lose, Pace.Safe, 0.5)]
    [InlineData(32, WeightGoal.Lose, Pace.Aggressive, 1.0)]
    [InlineData(27, WeightGoal.Lose, Pace.Medium, 0.5)]
    [InlineData(22, WeightGoal.Lose, Pace.Aggressive, 0.5)]
    [InlineData(17, WeightGoal.Gain, Pace.Medium, 0.5)]
    [InlineData(22, WeightGoal.Gain, Pace.Safe, 0.15)]
    public void ThePaceFollowsTheBmi(double bmi, WeightGoal goal, Pace pace, double kgPerWeek) =>
        Assert.Equal(kgPerWeek, Planner.KgPerWeek(bmi, goal, pace));

    [Fact]
    public void DoesntPlanToLeaveTheHealthyRangeTheWrongWay()
    {
        Assert.Null(Planner.KgPerWeek(19, WeightGoal.Lose, Pace.Safe));
        Assert.Null(Planner.KgPerWeek(26, WeightGoal.Gain, Pace.Safe));
        // 165 cm, 50 kg: BMI 18.4.
        Assert.Contains("under the healthy range", Planner.Check(Person(kg: 50, goal: WeightGoal.Lose)));
        Assert.Contains("pregnancy", Planner.Check(Person(kg: 80, pregnancy: Pregnancy.Pregnant, goal: WeightGoal.Lose)));
    }

    [Fact]
    public void LosingEatsLessAndSaysHowLong()
    {
        // 165 cm, 90 kg (BMI 33.1), moderate: keeping it takes 1.55 × 1570.25 = 2434 → 2450 kcal.
        var plan = Planner.Make(Person(kg: 90, activity: Activity.Moderate, goal: WeightGoal.Lose, pace: Pace.Medium));
        Assert.Equal(2450, plan.Maintenance);
        // Medium at that BMI is 0.75 kg a week: 825 → 800 kcal a day less.
        Assert.Equal(1650, plan.Calories);
        Assert.Equal(-0.73, plan.KgPerWeek);
        // To BMI 24.9: 67.8 kg, 22.2 kg away: 31 weeks.
        Assert.Equal(67.8, plan.HealthyKg!.Value, 1);
        Assert.Equal(31, plan.Weeks);
        Assert.InRange(plan.Items.Where(i => i.Kind == ReminderKind.Meal).Sum(i => i.Amount), 1600, 1700);
    }

    [Fact]
    public void LosingStopsAtTheFloor()
    {
        // 165 cm, 70 kg (BMI 25.7), sedentary: 1644 → 1650 kcal to keep; aggressive (0.75 kg) would be 800 a day less.
        var plan = Planner.Make(Person(kg: 70, goal: WeightGoal.Lose, pace: Pace.Aggressive));
        Assert.Equal(1200, plan.Calories);
        Assert.Contains(plan.Notes, n => n.Contains("slower than the pace"));
    }

    [Fact]
    public void GainingEatsMore()
    {
        // 165 cm, 48 kg (BMI 17.6): safe is 0.25 kg a week, 275 → 300 kcal a day more; the healthy range starts at 50.4 kg.
        var keep = Planner.Calories(Person(kg: 48));
        var plan = Planner.Make(Person(kg: 48, goal: WeightGoal.Gain, pace: Pace.Safe));
        Assert.Equal(keep + 300, plan.Calories);
        Assert.Equal(50.4, plan.HealthyKg!.Value, 1);
    }

    [Fact]
    public void RestingEnergyIsMifflinStJeor()
    {
        // 10 × 60 + 6.25 × 165 − 5 × 40 − 161 = 1270.25
        Assert.Equal(1270, Planner.Bmr(Person()));
        // 10 × 80 + 6.25 × 180 − 5 × 40 + 5 = 1730
        Assert.Equal(1730, Planner.Bmr(Person(Sex.Male, 180, 80)));
    }

    [Fact]
    public void EnergyGrowsWithActivityAndPregnancy()
    {
        Assert.Equal(1500, Planner.Calories(Person()));                                     // 1270.25 × 1.2 = 1524 → 1500
        Assert.Equal(1950, Planner.Calories(Person(activity: Activity.Moderate)));          // × 1.55 = 1969 → 1950
        Assert.Equal(1850, Planner.Calories(Person(pregnancy: Pregnancy.Pregnant)));        // 1524 + 350 = 1874 → 1850
        Assert.Equal(2100, Planner.Calories(Person(Sex.Male, 180, 80)));                    // 1730 × 1.2 = 2076 → 2100
    }

    [Fact]
    public void DrinksFollowSexWeightAndActivity()
    {
        Assert.Equal(2200, Planner.DrinksMl(Person()));                                     // 80% of 2.7 L
        Assert.Equal(3000, Planner.DrinksMl(Person(Sex.Male, 180, 80)));                    // 80% of 3.7 L
        Assert.Equal(3000, Planner.DrinksMl(Person(Sex.Male, 180, 120)));                   // 3.7 L is still more than 30 mL × 120 kg
        Assert.Equal(3600, Planner.DrinksMl(Person(Sex.Male, 190, 150)));                   // 80% of 30 mL × 150 kg
        // 6 ft 2 in, 260 lb, moderate: 80% of 3.7 L, + 250 mL; 108 fl oz, not the 128 it was when 30 mL a kg was taken as drinks alone.
        Assert.Equal(3200, Planner.DrinksMl(Person(Sex.Male, 188, 117.9, activity: Activity.Moderate)));
        Assert.Equal(2700, Planner.DrinksMl(Person(activity: Activity.Active)));            // + 500
        Assert.Equal(3000, Planner.DrinksMl(Person(pregnancy: Pregnancy.Breastfeeding)));   // 80% of 3.8 L
        Assert.Equal(4500, Planner.DrinksMl(Person(Sex.Male, 190, 160, activity: Activity.VeryActive)));
    }

    [Fact]
    public void MealsFitTheWakingDayAndAddUpToTheDaysEnergy()
    {
        var plan = Planner.Make(Person());
        var meals = plan.Items.Where(i => i.Kind == ReminderKind.Meal).ToList();
        Assert.Equal(["Breakfast", "Morning snack", "Lunch", "Afternoon snack", "Dinner"], meals.Select(m => m.Name));
        Assert.Equal(new TimeOnly(7, 30), meals[0].Time);
        Assert.Equal(new TimeOnly(20, 0), meals[^1].Time);
        Assert.InRange(meals.Sum(m => m.Amount), plan.Calories - 30, plan.Calories + 30);
    }

    [Fact]
    public void DrinksAreSpreadFromGettingUpToAnHourBeforeBed()
    {
        var plan = Planner.Make(Person(wake: "06:00", sleep: "00:30"));
        var drinks = plan.Items.Where(i => i.Kind == ReminderKind.Drink).ToList();
        Assert.Equal(new TimeOnly(6, 15), drinks[0].Time);
        Assert.Equal(new TimeOnly(23, 30), drinks[^1].Time);
        Assert.Equal(9, drinks.Count);                                                      // 2200 / 250
        Assert.InRange(drinks.Sum(d => d.Amount), plan.DrinksMl - 50, plan.DrinksMl + 50);
    }

    [Fact]
    public void AShortDayHasNoSnacks()
    {
        var meals = Planner.Make(Person(wake: "09:00", sleep: "21:00")).Items.Where(i => i.Kind == ReminderKind.Meal);
        Assert.Equal(["Breakfast", "Lunch", "Dinner"], meals.Select(m => m.Name));
    }

    [Fact]
    public void BecomesPlannedReminders()
    {
        var plan = Planner.Make(Person());
        var reminders = Planner.Reminders(plan, new DateTime(2026, 10, 27));
        Assert.All(reminders, r => Assert.True(r.Planned));
        var water = Assert.Single(reminders, r => r.Kind == ReminderKind.Drink);
        Assert.Equal(plan.Items.Count(i => i.Kind == ReminderKind.Drink), water.Times.Count);
        Assert.Equal(5, reminders.Count(r => r.Kind == ReminderKind.Meal));
    }

    [Theory]
    [InlineData(16, 165, 60, "07:00", "23:00", "adults")]
    [InlineData(40, 30, 60, "07:00", "23:00", "height")]
    [InlineData(40, 165, 10, "07:00", "23:00", "weight")]
    [InlineData(40, 165, 60, "07:00", "15:00", "under 10 hours")]
    public void RefusesWhatTheFormulasDontCover(int age, double cm, double kg, string wake, string sleep, string problem)
    {
        var person = Person(age: age, cm: cm, kg: kg, wake: wake, sleep: sleep);
        Assert.Contains(problem, Planner.Check(person));
        Assert.Throws<ArgumentException>(() => Planner.Make(person));
    }

    [Fact]
    public void PregnancyIsForFemalePhysiology() =>
        Assert.NotNull(Planner.Check(Person(Sex.Male, 180, 80, pregnancy: Pregnancy.Pregnant)));
}
