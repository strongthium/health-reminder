namespace Strongthium.HealthReminder;

/// <summary>How frozen food is thawed: in the fridge (the safest, over hours) or sealed in cold water (about an hour
/// for a single portion, the water changed every 30 minutes).</summary>
public enum ThawMethod { Fridge, ColdWater }

/// <summary>What a kitchen alert is for.</summary>
public enum KitchenStep { Choose, TakeOut, ChangeWater, Thawed, Prep, Cook }

/// <summary>A meal on a day: its reminder's name and time, and what's to be cooked for it (null for just an idea), or
/// to be decided that day. <paramref name="Decided"/> is when the recipe was picked: thawing in the fridge can't start
/// before it.</summary>
public sealed record PlannedMeal(string Meal, DateTime At, MealSlot Slot, Recipe? Recipe, bool DecideOnTheDay = false, DateTime? Decided = null, double Kcal = 0);

/// <summary>A kitchen alert: when, what for, and for which meal.</summary>
public sealed record KitchenEvent(DateTime At, KitchenStep Step, PlannedMeal Meal, string Title, string Detail, ThawMethod Thaw = ThawMethod.Fridge)
{
    /// <summary>Kept when it's done, so it doesn't ring again.</summary>
    public string Key => $"{Meal.At:yyyy-MM-ddTHH:mm}|{Meal.Meal}|{Step}";
}

/// <summary>
/// The kitchen's alerts for a meal: deciding what to have (when it's left to the day), taking frozen food out to
/// thaw and when it has, starting to prepare, and starting to cook, timed back from when the meal is eaten. Thawing in the
/// fridge is moved to before bed when it would fall in the night; when there isn't time left for it, the meal thaws in
/// cold water instead.
/// </summary>
public static class Kitchen
{
    public static readonly TimeSpan ColdWater = TimeSpan.FromHours(1);

    public static IEnumerable<KitchenEvent> Events(PlannedMeal meal, ThawMethod method, TimeOnly wake, TimeOnly sleep)
    {
        var day = meal.At.Date;
        if (meal.Recipe is not { } recipe)
        {
            if (!meal.DecideOnTheDay) yield break;
            // Breakfast is chosen the evening before; the rest in the morning.
            var ask = meal.Slot == MealSlot.Breakfast ? LastAwake(day.AddMinutes(-1), wake, sleep).AddHours(-1) : day + wake.ToTimeSpan() + TimeSpan.FromMinutes(15);
            yield return new(ask, KitchenStep.Choose, meal, $"What do you feel like for {meal.Meal.ToLowerInvariant()}?",
                $"For {Format(meal.At)}: pick from what you feel like, and the thawing and cooking alerts follow.");
            yield break;
        }

        var start = meal.At - recipe.Time;
        var frozen = recipe.Frozen.ToList();
        var thaw = method;
        if (frozen.Count > 0)
        {
            var what = string.Join(" and ", frozen.Select(f => f.Name));
            var takeOut = LastAwake(start - TimeSpan.FromHours(recipe.ThawHours), wake, sleep);
            // Too late to thaw it in the fridge: cold water instead.
            if (thaw == ThawMethod.Fridge && meal.Decided is { } decided && takeOut < decided) thaw = ThawMethod.ColdWater;
            if (thaw == ThawMethod.Fridge)
            {
                yield return new(takeOut, KitchenStep.TakeOut, meal, $"Take the {Things(frozen)} out of the freezer",
                    $"Put the {what} in the fridge, on a plate or in a container to catch drips, to thaw for {recipe.Name.ToLowerInvariant()} at {Format(meal.At)}.", thaw);
                var thawed = FirstAwake(takeOut + TimeSpan.FromHours(recipe.ThawHours), wake, sleep);
                if (thawed < start)
                    yield return new(thawed, KitchenStep.Thawed, meal, $"The {Things(frozen)} should be thawed",
                        "Check it's soft all the way through; keep it in the fridge until you cook it. Thawed in the fridge, it keeps a day or two.", thaw);
            }
            else
            {
                var takeOutWater = start - ColdWater;
                yield return new(takeOutWater, KitchenStep.TakeOut, meal, $"Take the {Things(frozen)} out of the freezer",
                    $"Seal the {what} in a leak-proof bag and put it in a bowl of cold tap water, for {recipe.Name.ToLowerInvariant()} at {Format(meal.At)}. Not warm water: it lets bacteria grow.", thaw);
                yield return new(takeOutWater + TimeSpan.FromMinutes(30), KitchenStep.ChangeWater, meal, "Change the thawing water",
                    "Pour it out and fill the bowl with fresh cold water, so it stays cold.", thaw);
            }
        }

        if (recipe.Time >= TimeSpan.FromMinutes(10))
            yield return new(start, KitchenStep.Prep, meal,
                (thaw == ThawMethod.ColdWater && frozen.Count > 0 ? "Thawed: time" : "Time") + $" to start {recipe.Name.ToLowerInvariant()}",
                $"To eat at {Format(meal.At)}: about {recipe.PrepMinutes} minutes to get it ready" + (recipe.CookMinutes > 0 ? $", then {recipe.CookMinutes} of cooking." : "."), thaw);
        if (recipe.CookMinutes > 0 && recipe.PrepMinutes >= 5)
            yield return new(meal.At - TimeSpan.FromMinutes(recipe.CookMinutes), KitchenStep.Cook, meal, $"Time to start cooking",
                $"{recipe.Name}: {recipe.CookMinutes} minutes, to eat at {Format(meal.At)}.", thaw);
    }

    private static string Things(IReadOnlyList<FoodItem> frozen) =>
        frozen.Count == 1 ? frozen[0].Name.Replace("frozen ", "") : "frozen food";

    private static string Format(DateTime time) => time.ToString("t", System.Globalization.CultureInfo.CurrentCulture);

    private static bool Asleep(DateTime time, TimeOnly wake, TimeOnly sleep)
    {
        var t = TimeOnly.FromDateTime(time);
        return sleep > wake ? t >= sleep || t < wake : t >= sleep && t < wake;
    }

    /// <summary>The time, or if it's in the night, half an hour before going to bed that night.</summary>
    public static DateTime LastAwake(DateTime time, TimeOnly wake, TimeOnly sleep)
    {
        if (!Asleep(time, wake, sleep)) return time;
        // The bedtime before it: the same day if it's after bedtime, the day before if it's before getting up.
        var bed = time.Date + sleep.ToTimeSpan();
        if (bed > time) bed = bed.AddDays(-1);
        return bed - TimeSpan.FromMinutes(30);
    }

    /// <summary>The time, or if it's in the night, when getting up.</summary>
    public static DateTime FirstAwake(DateTime time, TimeOnly wake, TimeOnly sleep)
    {
        if (!Asleep(time, wake, sleep)) return time;
        var up = time.Date + wake.ToTimeSpan();
        return up < time ? up.AddDays(1) : up;
    }
}
