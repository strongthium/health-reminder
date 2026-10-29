namespace Strongthium.HealthReminder;

/// <summary>
/// Food logged by hand: from the kitchen (what it uses comes off the stock, and goes back if it's taken out of the
/// log) or eaten out, on its own or in place of a meal reminder, which then counts as eaten without its recipe coming
/// off the stock.
/// </summary>
internal static class FoodLog
{
    private static Database Database => App.Current.Database;

    public static List<FoodEaten> Since(DateTime from) => Database.FoodLog(from);

    /// <summary>Some of something in the kitchen, eaten: so much comes off it, as it is now.</summary>
    public static void FromKitchen(string pantry, string amount, int kcal, DateTime at, string? meal)
    {
        var item = Database.Pantry().FirstOrDefault(p => p.Name.Equals(pantry, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            Database.Save(new FoodEaten(0, at, pantry, amount, kcal, Meal: meal));
            return;
        }
        var portion = Calories.Eat(item, amount);
        Database.Save(Calories.Left(item, portion));
        Database.Save(new FoodEaten(0, at, item.Name, amount, kcal, item.Name, portion.Used, item.Unit, meal));
    }

    public static void EatenOut(string name, string amount, int kcal, DateTime at, string? meal) =>
        Database.Save(new FoodEaten(0, at, name, amount, kcal, Meal: meal));

    /// <summary>Had in place of a meal: the meal is eaten, as far as the reminders go.</summary>
    public static void ForMeal(Dose dose, DateTime at) => App.Current.Schedule.Done(dose, at);

    /// <summary>Taken out of the log: what came off the stock goes back, while the stock is in the same unit.</summary>
    public static void Remove(FoodEaten food)
    {
        Database.Delete(food);
        if (food.Pantry is not { } name || food.Used <= 0) return;
        var item = Database.Pantry().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item is not null && item.Unit == food.Unit) Database.Save(item with { Amount = Math.Max(0, item.Amount) + food.Used });
    }

    /// <summary>The day's numbers from <paramref name="first"/> to today, as the Stats page and the Today page show them.
    /// A meal marked eaten counts its recipe's kcal if the menu had one, or the plan's.</summary>
    public static List<DayTally> Days(DateOnly first, DateTime now)
    {
        var app = App.Current;
        var from = first.ToDateTime(TimeOnly.MinValue);
        var doses = app.Schedule.Days(app.Reminders, from, now);
        // The menu's recipes, a day at a time rather than a meal at a time.
        var recipes = new Dictionary<DateOnly, List<PlannedMeal>>();
        int Kcal(ScheduledDose d)
        {
            if (d.Dose.Reminder.Kind != ReminderKind.Meal) return 0;
            var day = DateOnly.FromDateTime(d.Dose.Due);
            if (!recipes.TryGetValue(day, out var meals)) recipes[day] = meals = KitchenLog.Meals(day);
            var meal = meals.FirstOrDefault(m => m.Meal == d.Dose.Reminder.Name && m.At == d.Dose.Due);
            return meal?.Recipe is { Kcal: > 0 } recipe ? recipe.Kcal : DailyStats.PlannedKcal(d);
        }
        return DailyStats.Days(doses, app.Database.Smokes(from), Database.FoodLog(from), Kcal, first, now);
    }
}
