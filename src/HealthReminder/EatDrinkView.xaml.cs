using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>A line of the plan's preview; a meal has an idea of what to eat.</summary>
public sealed class PlanRow(PlanItem item, MealIdea? idea)
{
    public ReminderKind Kind => item.Kind;
    public string Time => Format.Time(item.Time);
    public string Name => item.Name;
    public string Amount => Format.Amount(item.Amount, item.Kind == ReminderKind.Meal ? "kcal" : "mL", item.Kind);
    public MealIdea? MealIdea => idea;
    public string Idea => idea is null ? "" : "For example, " + idea;
}

/// <summary>
/// The Eat &amp; drink tab: the body's measurements, the plan worked out from them (<see cref="Planner"/>), and the
/// button that turns it into reminders. The measurements are kept, so the plan can be redone when they change.
/// </summary>
public sealed partial class EatDrinkView : UserControl, IPage
{
    private const double CmPerInch = 2.54, KgPerPound = 0.45359237;
    private static readonly (Activity Activity, string Text)[] Activities =
    [
        (Activity.Sedentary, "Mostly sitting, little exercise"),
        (Activity.Light, "Light exercise 1 to 3 days a week"),
        (Activity.Moderate, "Moderate exercise 3 to 5 days a week"),
        (Activity.Active, "Hard exercise 6 or 7 days a week"),
        (Activity.VeryActive, "Physical job, or training twice a day"),
    ];
    private static readonly (Pregnancy Pregnancy, string Text)[] Pregnancies =
    [
        (Pregnancy.None, "Not pregnant or breastfeeding"),
        (Pregnancy.Pregnant, "Pregnant (second or third trimester)"),
        (Pregnancy.Breastfeeding, "Breastfeeding"),
    ];

    private EatingPlan? plan;
    private List<PlanRow> rows = [];
    private int otherIdeas;
    private bool imperial, loaded;

    public EatDrinkView()
    {
        InitializeComponent();
        ActivityBox.ItemsSource = Activities.Select(a => a.Text).ToList();
        PregnancyBox.ItemsSource = Pregnancies.Select(p => p.Text).ToList();
        foreach (var box in new[] { HeightCm, HeightFt, HeightIn, WeightBox }) box.ValueChanged += (_, _) => ShowPaces();
        PregnancyBox.SelectionChanged += (_, _) => ShowPaces();
        Loaded += (_, _) =>
        {
            // The tab's content is loaded each time the tab is shown: what was typed stays.
            if (loaded) return;
            loaded = true;
            Load();
        };
    }

    /// <summary>Shows whether an eating plan is in use.</summary>
    public void Refresh()
    {
        if (Application.Current is not App app || app.Database is null) return;
        var planned = app.Reminders.Count(r => r.Planned);
        RemoveButton.Show(planned > 0);
        UsedText.Text = planned > 0 ? "Using it replaces the eating plan you have now." : "";
    }

    private static Database Database => App.Current.Database;

    private void Load()
    {
        var saved = (Database.Setting("profile") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1]);
        string Get(string key, string fallback) => saved.GetValueOrDefault(key, fallback);
        double Number(string key, double fallback) => double.TryParse(Get(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        imperial = Get("units", RegionInfo.CurrentRegion.IsMetric ? "metric" : "imperial") == "imperial";
        (imperial ? Imperial : Metric).IsChecked = true;
        if (saved.ContainsKey("sex")) (Get("sex", "") == "Male" ? Male : Female).IsChecked = true;
        SetHeight(Number("height", 0));
        SetWeight(Number("weight", 0));
        AgeBox.Value = saved.ContainsKey("age") ? Number("age", 40) : null;
        ActivityBox.SelectedIndex = Math.Max(0, Array.FindIndex(Activities, a => a.Activity.ToString() == Get("activity", "Light")));
        PregnancyBox.SelectedIndex = Math.Max(0, Array.FindIndex(Pregnancies, p => p.Pregnancy.ToString() == Get("pregnancy", "None")));
        WakeBox.Time = TimeOnly.TryParse(Get("wake", "07:00"), CultureInfo.InvariantCulture, out var wake) ? wake.ToTimeSpan() : new TimeSpan(7, 0, 0);
        SleepBox.Time = TimeOnly.TryParse(Get("sleep", "23:00"), CultureInfo.InvariantCulture, out var sleep) ? sleep.ToTimeSpan() : new TimeSpan(23, 0, 0);
        (Get("goal", "Maintain") switch { "Lose" => GoalLose, "Gain" => GoalGain, _ => GoalKeep }).IsChecked = true;
        ShowUnits();
        ShowPregnancy();
        ShowPaces();
        PaceBox.SelectedIndex = Enum.TryParse<Pace>(Get("pace", "Safe"), out var pace) ? (int)pace : 0;
        Refresh();
    }

    private WeightGoal Goal => GoalLose.IsChecked == true ? WeightGoal.Lose : GoalGain.IsChecked == true ? WeightGoal.Gain : WeightGoal.Maintain;

    private void Goal_Changed(object sender, RoutedEventArgs e) => ShowPaces();

    /// <summary>The paces, with how fast each goes at this BMI, or why the goal isn't planned for it.</summary>
    private void ShowPaces()
    {
        if (PaceBox is null) return;
        var goal = Goal;
        var index = Math.Max(0, PaceBox.SelectedIndex);
        var (height, weight) = (ReadHeight(), ReadWeight());
        var bmi = height is > 0 && weight is > 0 ? weight.Value / Math.Pow(height.Value / 100, 2) : (double?)null;
        var pregnancy = Female.IsChecked == true ? Pregnancies[Math.Max(0, PregnancyBox.SelectedIndex)].Pregnancy : Pregnancy.None;
        var why = bmi is null ? null : Planner.WhyNot(new BodyProfile(Sex.Female, height!.Value, weight!.Value, 40, Activity.Light, pregnancy, default, default), goal);
        PacePanel.Show(goal != WeightGoal.Maintain && why is null);
        GoalProblem.Text = why ?? "";
        GoalProblem.Show(why is not null);
        PaceBox.ItemsSource = Enum.GetValues<Pace>().Select(p =>
        {
            var rate = bmi is { } b ? Planner.KgPerWeek(b, goal, p) : null;
            return rate is { } kg ? $"{p}: about {WeightText(kg)} a week" : p.ToString();
        }).ToList();
        PaceBox.SelectedIndex = index;
        PaceHint.Text = bmi is { } value
            ? $"Your BMI is {value:0.0}. The further it is from the healthy range (18.5 to 24.9), the faster it can safely change."
            : "Fill in your height and weight to see how fast each goes.";
    }

    /// <summary>A rate to two places ("0.35 kg"), a weight to one ("67.8 kg"); pounds to one.</summary>
    private string WeightText(double kg, bool rate = true) => imperial ? $"{kg / KgPerPound:0.#} lb" : rate ? $"{kg:0.##} kg" : $"{kg:0.#} kg";

    private void SetHeight(double cm)
    {
        HeightCm.Value = cm > 0 ? Math.Round(cm) : null;
        var inches = Math.Round(cm / CmPerInch);
        HeightFt.Value = cm > 0 ? Math.Floor(inches / 12) : null;
        HeightIn.Value = cm > 0 ? inches % 12 : null;
    }

    private void SetWeight(double kg) =>
        WeightBox.Value = kg > 0 ? Math.Round(imperial ? kg / KgPerPound : kg, 1) : null;

    private double? ReadHeight() => imperial
        ? HeightFt.Value is null ? null : ((HeightFt.Value ?? 0) * 12 + (HeightIn.Value ?? 0)) * CmPerInch
        : HeightCm.Value;

    private double? ReadWeight() => WeightBox.Value is { } w ? w * (imperial ? KgPerPound : 1) : null;

    private void ShowUnits()
    {
        HeightCm.Show(!imperial);
        CmLabel.Show(!imperial);
        HeightFt.Show(imperial);
        FtLabel.Show(imperial);
        HeightIn.Show(imperial);
        InLabel.Show(imperial);
        WeightUnit.Text = imperial ? "lb" : "kg";
    }

    private void Units_Changed(object sender, RoutedEventArgs e)
    {
        if (Imperial.IsChecked == imperial) return;
        var (height, weight) = (ReadHeight(), ReadWeight());
        imperial = Imperial.IsChecked == true;
        SetHeight(height ?? 0);
        SetWeight(weight ?? 0);
        ShowUnits();
        ShowPaces();
    }

    private void Sex_Changed(object sender, RoutedEventArgs e) => ShowPregnancy();

    private void ShowPregnancy()
    {
        var female = Female.IsChecked == true;
        PregnancyBox.Show(female);
        PregnancyLabel.Show(female);
        if (!female) PregnancyBox.SelectedIndex = 0;
    }

    private BodyProfile? ReadProfile()
    {
        var missing =
            Female.IsChecked != true && Male.IsChecked != true ? "Choose female or male."
            : ReadHeight() is null ? "Fill in your height."
            : ReadWeight() is null ? "Fill in your weight."
            : AgeBox.Value is null ? "Fill in your age."
            : WakeBox.Time is null || SleepBox.Time is null ? "Fill in when you get up and go to bed."
            : null;
        if (missing is not null)
        {
            ProblemText.Text = missing;
            return null;
        }
        return new BodyProfile(
            Male.IsChecked == true ? Sex.Male : Sex.Female,
            ReadHeight()!.Value, ReadWeight()!.Value, (int)AgeBox.Value!.Value,
            Activities[Math.Max(0, ActivityBox.SelectedIndex)].Activity,
            Pregnancies[Math.Max(0, PregnancyBox.SelectedIndex)].Pregnancy,
            TimeOnly.FromTimeSpan(WakeBox.Time!.Value), TimeOnly.FromTimeSpan(SleepBox.Time!.Value),
            Goal, (Pace)Math.Max(0, PaceBox.SelectedIndex));
    }

    private void Save(BodyProfile p)
    {
        var values = new Dictionary<string, string>
        {
            ["units"] = imperial ? "imperial" : "metric",
            ["sex"] = p.Sex.ToString(),
            ["height"] = p.HeightCm.ToString("0.#", CultureInfo.InvariantCulture),
            ["weight"] = p.WeightKg.ToString("0.##", CultureInfo.InvariantCulture),
            ["age"] = p.Age.ToString(CultureInfo.InvariantCulture),
            ["activity"] = p.Activity.ToString(),
            ["pregnancy"] = p.Pregnancy.ToString(),
            ["wake"] = p.Wake.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["sleep"] = p.Sleep.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["goal"] = p.Goal.ToString(),
            ["pace"] = p.Pace.ToString(),
        };
        Database.SetSetting("profile", string.Join(';', values.Select(v => v.Key + "=" + v.Value)));
    }

    public void Plan_Click(object sender, RoutedEventArgs e)
    {
        ProblemText.Text = "";
        if (ReadProfile() is not { } profile) return;
        if (Planner.Check(profile) is { } problem)
        {
            ProblemText.Text = problem;
            return;
        }
        Save(profile);
        plan = Planner.Make(profile);
        CaloriesText.Text = $"{plan.Calories:N0} kcal";
        var difference = plan.Calories - plan.Maintenance;
        CaloriesLabel.Text = difference == 0 ? "to eat a day, which keeps your weight"
            : $"to eat a day: {Math.Abs(difference):N0} {(difference < 0 ? "less" : "more")} than the {plan.Maintenance:N0} that keeps your weight";
        WaterText.Text = Quantities.Drink(plan.DrinksMl, App.Current.DrinkUnit);
        GoalBox.Show(profile.Goal != WeightGoal.Maintain);
        GoalText.Text = profile.Goal == WeightGoal.Maintain ? ""
            : $"{(plan.KgPerWeek < 0 ? "Losing" : "Gaining")} about {WeightText(Math.Abs(plan.KgPerWeek))} a week ({profile.Pace.ToString().ToLowerInvariant()} for a BMI of {plan.Bmi:0.0})."
              + (plan.Weeks is { } weeks && plan.HealthyKg is { } healthy
                  ? $" In about {weeks} weeks you'd reach {WeightText(healthy, rate: false)}, a BMI of {(plan.KgPerWeek < 0 ? "24.9, the top" : "18.5, the bottom")} of the healthy range."
                  : "");
        BasisText.Text = $"Resting energy {plan.Bmr:N0} kcal (Mifflin-St Jeor) × {Planner.ActivityFactor[profile.Activity]} for activity"
            + (profile.Pregnancy != Pregnancy.None ? $", plus {(profile.Pregnancy == Pregnancy.Pregnant ? 350 : 400)} kcal" : "")
            + $" keeps your weight. BMI {plan.Bmi:0.0}."
            + (profile.Goal != WeightGoal.Maintain ? $" A kilogram of body weight is taken as {Planner.KcalPerKg:N0} kcal." : "");
        otherIdeas = 0;
        ShowRows();
        NotesList.ItemsSource = plan.Notes;
        GroceryText.Text = "";
        NoPlan.Show(false);
        PlanPanel.Show(true);
        Refresh();
    }

    /// <summary>The plan's lines, with an idea for each meal: today's, or another.</summary>
    private void ShowRows()
    {
        if (plan is null) return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        rows = plan.Items
            .Select(i => new PlanRow(i, i.Kind == ReminderKind.Meal ? Meals.Suggest(Meals.SlotOf(i.Name), i.Amount, today, otherIdeas) : null))
            .ToList();
        PlanList.ItemsSource = rows;
    }

    private void OtherIdeas_Click(object sender, RoutedEventArgs e)
    {
        otherIdeas++;
        ShowRows();
    }

    /// <summary>Puts what the meal ideas need on the grocery list, leaving out what's on it already.</summary>
    private void AddGroceries_Click(object sender, RoutedEventArgs e)
    {
        var onList = Database.Groceries().Where(g => g.Bought is null).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var needed = rows.Where(r => r.MealIdea is not null).SelectMany(r => r.MealIdea!.Groceries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var added = needed.Where(n => !onList.Contains(n)).ToList();
        foreach (var name in added) Database.Save(new GroceryItem { Name = name, Added = DateTime.Now });
        GroceryText.Text = added.Count == 0 ? "They're all on your grocery list already."
            : $"Added to your grocery list: {string.Join(", ", added)}." + (added.Count < needed.Count ? $" ({needed.Count - added.Count} were on it already.)" : "");
        App.Current.Changed();
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if (plan is null) return;
        RemovePlanned();
        foreach (var reminder in Planner.Reminders(plan, DateTime.Now)) Database.Save(reminder);
        App.Current.Changed();
        UsedText.Text = "Done: the reminders are on the Today and Reminders tabs.";
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        RemovePlanned();
        App.Current.Changed();
    }

    private static void RemovePlanned()
    {
        foreach (var reminder in App.Current.Reminders.Where(r => r.Planned).ToList()) Database.Delete(reminder);
    }
}
