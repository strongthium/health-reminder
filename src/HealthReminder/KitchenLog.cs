using System.Globalization;

namespace Strongthium.HealthReminder;

/// <summary>
/// The meals of a day as the menu has them (each meal reminder's time, with the recipe picked for it), the kitchen
/// alerts that follow, and what's set about them.
/// </summary>
internal static class KitchenLog
{
    private static App App => App.Current;
    private static Database Database => App.Current.Database;
    private static readonly Dictionary<string, DateTime> snoozed = [];

    /// <summary>Whether the kitchen alerts ring at all.</summary>
    public static bool AlertsOn
    {
        get => Database.Setting("kitchen_alerts") != "off";
        set => Database.SetSetting("kitchen_alerts", value ? "on" : "off");
    }

    public static ThawMethod Thaw
    {
        get => Enum.TryParse<ThawMethod>(Database.Setting("thaw"), out var method) ? method : ThawMethod.Fridge;
        set => Database.SetSetting("thaw", value.ToString());
    }

    /// <summary>Whether the steps open by themselves when it's time to start.</summary>
    public static bool StepsAtStart
    {
        get => Database.Setting("steps_at_start") != "off";
        set => Database.SetSetting("steps_at_start", value ? "on" : "off");
    }

    /// <summary>Whether the steps stay in front of other windows.</summary>
    public static bool StepsOnTop
    {
        get => Database.Setting("steps_on_top") != "off";
        set => Database.SetSetting("steps_on_top", value ? "on" : "off");
    }

    /// <summary>When the user gets up and goes to bed, from the Eat &amp; drink page; 7:00 and 23:00 until it's filled in.</summary>
    public static (TimeOnly Wake, TimeOnly Sleep) Day()
    {
        var profile = (Database.Setting("profile") ?? "").Split(';').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        TimeOnly Time(string key, TimeOnly fallback) =>
            profile.TryGetValue(key, out var text) && TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out var t) ? t : fallback;
        return (Time("wake", new TimeOnly(7, 0)), Time("sleep", new TimeOnly(23, 0)));
    }

    /// <summary>The meals of a day that are cooked or decided: each active meal reminder's times that day (snacks
    /// left out), with what the menu says, or what was picked for that day over it.</summary>
    public static List<PlannedMeal> Meals(DateOnly day)
    {
        var menu = Database.Menu();
        var meals = new List<PlannedMeal>();
        foreach (var reminder in App.Reminders.Where(r => r.Kind == ReminderKind.Meal && r.Active && r.Days.Contains(day.DayOfWeek)))
        {
            var slot = HealthReminder.Meals.SlotOf(reminder.Name);
            if (slot == MealSlot.Snack) continue;
            foreach (var time in reminder.Times)
            {
                var at = day.ToDateTime(time.Time);
                var kcal = reminder.Unit == "kcal" ? time.Amount : 0;
                if (Database.Choice(day, reminder.Name) is { } choice)
                    meals.Add(new(reminder.Name, at, slot, Recipes.Get(choice.Recipe), Decided: choice.Decided, Kcal: kcal));
                else if (menu.TryGetValue((day.DayOfWeek, reminder.Name), out var planned))
                    meals.Add(new(reminder.Name, at, slot, Recipes.Get(planned.Recipe), planned.Recipe == "decide", planned.Decided, kcal));
                else
                    meals.Add(new(reminder.Name, at, slot, null, Kcal: kcal));
            }
        }
        return meals.OrderBy(m => m.At).ToList();
    }

    /// <summary>The kitchen alerts from <paramref name="from"/> to <paramref name="to"/>, in time order; a meal's can
    /// start the day before it (thawing overnight).</summary>
    public static List<KitchenEvent> Events(DateTime from, DateTime to)
    {
        var (wake, sleep) = Day();
        var thaw = Thaw;
        var events = new List<KitchenEvent>();
        for (var day = DateOnly.FromDateTime(from); day <= DateOnly.FromDateTime(to).AddDays(1); day = day.AddDays(1))
            foreach (var meal in Meals(day))
                events.AddRange(Kitchen.Events(meal, thaw, wake, sleep).Where(e => e.At >= from && e.At <= to));
        return events.OrderBy(e => e.At).ToList();
    }

    /// <summary>What's due now and not done or snoozed: up to three hours late, after which it's let go.</summary>
    public static List<KitchenEvent> Due(DateTime now)
    {
        if (!AlertsOn) return [];
        var done = Database.KitchenDone(now.AddDays(-2));
        return Events(now.AddHours(-3), now)
            .Where(e => !done.Contains(e.Key) && !(snoozed.TryGetValue(e.Key, out var until) && until > now))
            .ToList();
    }

    public static bool IsDone(KitchenEvent e) => Database.KitchenDone(e.Meal.At.AddDays(-2)).Contains(e.Key);

    public static void Done(KitchenEvent e)
    {
        Database.SetKitchenDone(e.Key);
        snoozed.Remove(e.Key);
    }

    public static void Snooze(KitchenEvent e, TimeSpan time) => snoozed[e.Key] = DateTime.Now + time;

    /// <summary>The recipe picked for a meal on its day, from an alert or the Menu page.</summary>
    public static void Pick(PlannedMeal meal, Recipe recipe) =>
        Database.SetChoice(DateOnly.FromDateTime(meal.At), meal.Meal, recipe.Id);
}
