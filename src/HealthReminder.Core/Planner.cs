namespace Strongthium.HealthReminder;

/// <summary>Sex as in the body: the energy and water formulas differ between female and male physiology.</summary>
public enum Sex { Female, Male }

public enum Activity { Sedentary, Light, Moderate, Active, VeryActive }

public enum Pregnancy { None, Pregnant, Breastfeeding }

/// <summary>What the planner needs to know about someone. Height in cm, weight in kg; times are when they get up and
/// go to bed (bedtime may be after midnight).</summary>
public sealed record BodyProfile(Sex Sex, double HeightCm, double WeightKg, int Age, Activity Activity, Pregnancy Pregnancy, TimeOnly Wake, TimeOnly Sleep,
    WeightGoal Goal = WeightGoal.Maintain, Pace Pace = Pace.Safe)
{
    public double Bmi => WeightKg / Math.Pow(HeightCm / 100, 2);
}

/// <summary>Whether the plan keeps the weight where it is, or loses or gains some.</summary>
public enum WeightGoal { Maintain, Lose, Gain }

/// <summary>How fast to lose or gain; what each means depends on the BMI (see <see cref="Planner.KgPerWeek"/>).</summary>
public enum Pace { Safe, Medium, Aggressive }

/// <summary>One reminder of a plan: a meal (amount in kcal) or a drink (amount in mL).</summary>
public sealed record PlanItem(ReminderKind Kind, string Name, TimeOnly Time, double Amount);

/// <summary>A day of eating and drinking worked out from a <see cref="BodyProfile"/>.</summary>
/// <param name="Notes">What to bear in mind about this plan, for this person.</param>
/// <param name="Maintenance">The calories that keep the weight where it is; <paramref name="Calories"/> is that less or
/// more for a goal.</param>
/// <param name="KgPerWeek">How fast the weight should change on these calories: below zero to lose, above to gain.</param>
/// <param name="HealthyKg">The weight at the edge of the healthy BMI range (18.5 to 24.9) the goal heads for, if it
/// starts outside it; <paramref name="Weeks"/> is about how long it takes to get there.</param>
public sealed record EatingPlan(BodyProfile Profile, int Bmr, int Calories, int DrinksMl, double Bmi, IReadOnlyList<PlanItem> Items, IReadOnlyList<string> Notes,
    int Maintenance = 0, double KgPerWeek = 0, double? HealthyKg = null, int? Weeks = null);

/// <summary>
/// Works out when to eat and drink, and how much, from height, weight, sex, age and activity:
/// <list type="bullet">
/// <item>Energy: the Mifflin-St Jeor equation for resting energy (BMR = 10 × kg + 6.25 × cm − 5 × age, + 5 for men,
/// − 161 for women) times an activity factor from 1.2 (sedentary) to 1.9 (very active), plus 350 kcal when pregnant
/// and 400 when breastfeeding.</item>
/// <item>Water from drinks: 80% of the total water a day (the rest comes from food), which is the U.S. National
/// Academies' adequate intake (3.7 L for men, 2.7 L for women, 3.0 L pregnant, 3.8 L breastfeeding) or 30 mL per kg
/// of body weight if that is more; plus 250 to 750 mL for activity; at most 4.5 L.</item>
/// <item>Losing or gaining: a kilogram of body weight is taken as 7,700 kcal, so each 0.1 kg a week is 110 kcal a day
/// less or more. How fast depends on the BMI (<see cref="KgPerWeek"/>): the further from the healthy range, the faster
/// it can safely go. A plan to lose never goes under 1,200 kcal a day for a woman or 1,500 for a man.</item>
/// <item>Times: breakfast half an hour after getting up, dinner three hours before bed, lunch between them, and a
/// snack in a gap of 4½ hours or more; drinks spread evenly from getting up to an hour before bed.</item>
/// </list>
/// It is a starting point for a healthy adult, not dietary or medical advice.
/// </summary>
public static class Planner
{
    public const double KcalPerKg = 7700;

    public static readonly IReadOnlyDictionary<Activity, double> ActivityFactor = new Dictionary<Activity, double>
    {
        [Activity.Sedentary] = 1.2,
        [Activity.Light] = 1.375,
        [Activity.Moderate] = 1.55,
        [Activity.Active] = 1.725,
        [Activity.VeryActive] = 1.9,
    };

    /// <summary>
    /// How many kilograms a week a pace loses or gains at a BMI, or null when that goal isn't planned at that BMI:
    /// <list type="table">
    /// <item>Lose, BMI 30 and over: 0.5, 0.75, 1 kg (safe, medium, aggressive); 25 to 30: 0.25, 0.5, 0.75; 20 to 25:
    /// 0.25, 0.35, 0.5; under 20 it isn't planned (it would head under the healthy range).</item>
    /// <item>Gain, BMI under 18.5: 0.25, 0.5, 0.75 kg; 18.5 to 25: 0.15, 0.25, 0.35; 25 and over it isn't planned.</item>
    /// </list>
    /// </summary>
    public static double? KgPerWeek(double bmi, WeightGoal goal, Pace pace)
    {
        double[]? rates = goal switch
        {
            WeightGoal.Lose when bmi >= 30 => [0.5, 0.75, 1],
            WeightGoal.Lose when bmi >= 25 => [0.25, 0.5, 0.75],
            WeightGoal.Lose when bmi >= 20 => [0.25, 0.35, 0.5],
            WeightGoal.Gain when bmi < 18.5 => [0.25, 0.5, 0.75],
            WeightGoal.Gain when bmi < 25 => [0.15, 0.25, 0.35],
            WeightGoal.Maintain => [0, 0, 0],
            _ => null,
        };
        return rates?[(int)pace];
    }

    /// <summary>Why a goal isn't planned for this person, or null when it is.</summary>
    public static string? WhyNot(BodyProfile p, WeightGoal goal)
    {
        if (goal == WeightGoal.Maintain) return null;
        if (p.Pregnancy == Pregnancy.Pregnant) return "Losing or gaining weight isn't planned during pregnancy: your midwife or doctor will say what's right.";
        if (p.Pregnancy == Pregnancy.Breastfeeding) return "Losing or gaining weight isn't planned while breastfeeding: ask your midwife, doctor or dietitian.";
        return KgPerWeek(p.Bmi, goal, Pace.Safe) is not null ? null
            : goal == WeightGoal.Lose ? $"At a BMI of {p.Bmi:0.0}, losing weight would head under the healthy range (18.5 to 24.9), so it isn't planned here."
            : $"At a BMI of {p.Bmi:0.0}, gaining weight isn't planned here: it's above the healthy range (18.5 to 24.9).";
    }

    /// <summary>Why a profile can't be planned for, or null when it can.</summary>
    public static string? Check(BodyProfile p)
    {
        if (p.Age < 18) return "The formulas are for adults (18 and over). For a child or teenager, ask a doctor or dietitian.";
        if (p.Age > 120) return "Check the age.";
        if (p.HeightCm is < 100 or > 250) return "Check the height.";
        if (p.WeightKg is < 30 or > 350) return "Check the weight.";
        if (p.Sex == Sex.Male && p.Pregnancy != Pregnancy.None) return "Pregnancy and breastfeeding apply to female physiology.";
        var awake = Awake(p);
        if (awake < TimeSpan.FromHours(10)) return "The time between getting up and going to bed is under 10 hours.";
        if (awake > TimeSpan.FromHours(20)) return "The time between getting up and going to bed is over 20 hours.";
        return WhyNot(p, p.Goal);
    }

    /// <summary>The least a plan to lose weight eats in a day.</summary>
    public static int Floor(BodyProfile p) => p.Sex == Sex.Male ? 1500 : 1200;

    private static TimeSpan Awake(BodyProfile p)
    {
        var awake = p.Sleep - p.Wake;
        return awake <= TimeSpan.Zero ? awake + TimeSpan.FromDays(1) : awake;
    }

    public static int Bmr(BodyProfile p) =>
        (int)Math.Round(10 * p.WeightKg + 6.25 * p.HeightCm - 5 * p.Age + (p.Sex == Sex.Male ? 5 : -161));

    public static int Calories(BodyProfile p)
    {
        var calories = Bmr(p) * ActivityFactor[p.Activity] + p.Pregnancy switch
        {
            Pregnancy.Pregnant => 350,
            Pregnancy.Breastfeeding => 400,
            _ => 0,
        };
        return (int)(Math.Round(calories / 50) * 50);
    }

    public static int DrinksMl(BodyProfile p)
    {
        var totalWater = p.Pregnancy switch
        {
            Pregnancy.Pregnant => 3000,
            Pregnancy.Breastfeeding => 3800,
            _ => p.Sex == Sex.Male ? 3700 : 2700,
        };
        // 30 mL a kilogram is all the water, food's included, like the adequate intake: drinks are 80% of either.
        var drinks = Math.Max(totalWater, 30 * p.WeightKg) * 0.8 + p.Activity switch
        {
            Activity.Moderate => 250,
            Activity.Active => 500,
            Activity.VeryActive => 750,
            _ => 0,
        };
        return (int)(Math.Round(Math.Min(drinks, 4500) / 100) * 100);
    }

    public static EatingPlan Make(BodyProfile p)
    {
        if (Check(p) is { } problem) throw new ArgumentException(problem, nameof(p));
        var maintenance = Calories(p);
        var bmi = p.Bmi;
        var rate = KgPerWeek(bmi, p.Goal, p.Pace) ?? 0;
        var change = (int)(Math.Round(rate * KcalPerKg / 7 / 50) * 50);
        var calories = p.Goal switch
        {
            WeightGoal.Lose => Math.Max(maintenance - change, Floor(p)),
            WeightGoal.Gain => maintenance + change,
            _ => maintenance,
        };
        // Below the floor it loses more slowly than the pace says: the rate is what these calories give.
        var kgPerWeek = Math.Round((calories - maintenance) * 7 / KcalPerKg, 2);
        double? healthyKg = p.Goal switch
        {
            WeightGoal.Lose when bmi >= 25 => 24.9 * Math.Pow(p.HeightCm / 100, 2),
            WeightGoal.Gain when bmi < 18.5 => 18.5 * Math.Pow(p.HeightCm / 100, 2),
            _ => null,
        };
        int? weeks = healthyKg is { } goalKg && kgPerWeek != 0 ? (int)Math.Ceiling(Math.Abs(p.WeightKg - goalKg) / Math.Abs(kgPerWeek)) : null;
        var drinksMl = DrinksMl(p);
        var awake = Awake(p);
        TimeOnly At(TimeSpan sinceWaking) => p.Wake.Add(TimeSpan.FromMinutes(Math.Round(sinceWaking.TotalMinutes / 15) * 15));

        // Meals: breakfast, lunch, dinner, and a snack in each long gap. Share of the day's energy: 25 / 30 / 30, and
        // 7.5 for a snack, scaled to add up.
        var breakfast = TimeSpan.FromMinutes(30);
        var dinner = awake - TimeSpan.FromHours(3);
        var lunch = (breakfast + dinner) / 2;
        var meals = new List<(string Name, TimeSpan At, double Share)> { ("Breakfast", breakfast, 25), ("Lunch", lunch, 30), ("Dinner", dinner, 30) };
        if (lunch - breakfast >= TimeSpan.FromHours(4.5)) meals.Add(("Morning snack", (breakfast + lunch) / 2, 7.5));
        if (dinner - lunch >= TimeSpan.FromHours(4.5)) meals.Add(("Afternoon snack", (lunch + dinner) / 2, 7.5));
        var shares = meals.Sum(m => m.Share);
        var items = meals
            .Select(m => new PlanItem(ReminderKind.Meal, m.Name, At(m.At), Math.Round(calories * m.Share / shares / 10) * 10))
            .ToList();

        // Drinks: about a glass (250 mL) at a time, from a quarter of an hour after getting up to an hour before bed.
        var count = Math.Clamp((int)Math.Round(drinksMl / 250.0), 4, 16);
        var first = TimeSpan.FromMinutes(15);
        var last = awake - TimeSpan.FromHours(1);
        var glass = Math.Round(drinksMl / (double)count / 10) * 10;
        for (var i = 0; i < count; i++)
            items.Add(new PlanItem(ReminderKind.Drink, "Water", At(first + (last - first) * i / (count - 1)), glass));

        var notes = new List<string>
        {
            "If a doctor has told you to limit fluids (for heart failure or kidney disease, for example), follow that instead of this plan.",
        };
        if (p.Goal == WeightGoal.Maintain && bmi < 18.5) notes.Add($"Your BMI is {bmi:0.0}, under the healthy range: these calories keep your weight where it is. Choose Gain weight for a plan to gain, and see a doctor or dietitian about it.");
        else if (p.Goal == WeightGoal.Maintain && bmi >= 30) notes.Add($"Your BMI is {bmi:0.0}: these calories keep your weight where it is. Choose Lose weight for a plan to lose it, and a doctor or dietitian can help.");
        if (p.Goal != WeightGoal.Maintain)
        {
            notes.Add($"Weigh yourself once a week and work the plan out again every few weeks: as your weight changes, so do the calories you need.");
            if (p.Goal == WeightGoal.Lose && calories == Floor(p) && maintenance - calories < change)
                notes.Add($"It doesn't go under {Floor(p):N0} kcal a day, so this is slower than the pace chosen.");
            notes.Add("Losing or gaining weight can change the doses some medication needs, such as for diabetes or blood pressure: tell your doctor about this plan before you start.");
        }
        if (p.Pace == Pace.Aggressive && p.Goal != WeightGoal.Maintain)
            notes.Add("The aggressive pace is the fastest that's usually advised for your BMI; it's harder to keep to, and best done with a doctor or dietitian.");
        if (p.Pregnancy != Pregnancy.None) notes.Add("Needs change through pregnancy and breastfeeding; check this plan with your midwife or doctor.");
        notes.Add("If you have diabetes, eat to the plan your care team gave you; these times can still remind you.");

        return new EatingPlan(p, Bmr(p), calories, drinksMl, bmi, items.OrderBy(i => i.Time).ThenBy(i => i.Kind).ToList(), notes,
            maintenance, kgPerWeek, healthyKg, weeks);
    }

    /// <summary>The plan as reminders: one a meal, and one "Water" reminder with every drink time.</summary>
    public static List<Reminder> Reminders(EatingPlan plan, DateTime now)
    {
        var reminders = plan.Items
            .Where(i => i.Kind == ReminderKind.Meal)
            .Select(i => new Reminder
            {
                Kind = ReminderKind.Meal, Name = i.Name, Unit = "kcal", Times = [new ReminderTime(i.Time, i.Amount)], Created = now, Planned = true,
            })
            .ToList();
        var drinks = plan.Items.Where(i => i.Kind == ReminderKind.Drink).Select(i => new ReminderTime(i.Time, i.Amount)).ToList();
        if (drinks.Count > 0)
            reminders.Add(new Reminder
            {
                Kind = ReminderKind.Drink, Name = "Water", Unit = "mL", Times = drinks, Created = now, Planned = true,
                Notes = $"About {plan.DrinksMl / 1000.0:0.0} L of drinks a day. Water, milk, tea and coffee all count.",
            });
        return reminders;
    }
}
