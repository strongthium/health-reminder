using System.Globalization;

namespace Strongthium.HealthReminder;

/// <summary>A kitchen check: a set time a week to count what's in doubt, when something is.</summary>
public sealed record PantryCheck(DateTime At, IReadOnlyList<PantryItem> Items)
{
    public string Key => $"{At:yyyy-MM-ddTHH:mm}|pantry-check";
}

/// <summary>
/// The kitchen's stock as the app keeps it: what's bought and seen in photos goes in, the meals cooked or eaten take
/// out what their recipes use (once a meal), and a time a week is set aside to count what's in doubt.
/// </summary>
internal static class PantryLog
{
    private static Database Database => App.Current.Database;

    /// <summary>When the weekly check is: a day and a time, Sunday at 10:00 until it's set.</summary>
    public static (DayOfWeek Day, TimeOnly At) CheckTime
    {
        get
        {
            var saved = (Database.Setting("pantry_check") ?? "").Split(' ');
            return saved.Length == 2 && Enum.TryParse<DayOfWeek>(saved[0], out var day) && TimeOnly.TryParse(saved[1], CultureInfo.InvariantCulture, out var at)
                ? (day, at) : (DayOfWeek.Sunday, new TimeOnly(10, 0));
        }
        set => Database.SetSetting("pantry_check", $"{value.Day} {value.At.ToString("HH:mm", CultureInfo.InvariantCulture)}");
    }

    public static bool CheckOn
    {
        get => Database.Setting("pantry_check_on") != "off";
        set => Database.SetSetting("pantry_check_on", value ? "on" : "off");
    }

    public static List<PantryItem> Items() => Database.Pantry();

    private static PantryItem? Find(string name) => Database.Pantry().FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static ColdStorage Cold => App.Current.Cold;

    /// <summary>Bought: added to what there is; <paramref name="inFreezer"/> when it went straight in the freezer.</summary>
    public static void Bought(string name, string quantity, int useWithinDays = 0, bool unsure = false, bool inFreezer = false)
    {
        if (name.Trim().Length == 0 || !Pantry.Tracked(name)) return;
        Database.Save(Pantry.Add(Find(name), name.Trim(), quantity, DateTime.Now, useWithinDays, unsure, inFreezer, Cold));
    }

    /// <summary>Counted (or seen in a photo of where it's kept): this much there is now.</summary>
    public static void Counted(string name, string quantity, int useWithinDays = 0, bool unsure = false, bool inFreezer = false)
    {
        if (name.Trim().Length == 0 || !Pantry.Tracked(name)) return;
        Database.Save(Pantry.Set(name.Trim(), quantity, DateTime.Now, useWithinDays, unsure, Find(name), inFreezer, Cold));
    }

    /// <summary>Put in the freezer, or taken out to thaw.</summary>
    public static void Freezer(PantryItem item, bool inFreezer) =>
        Database.Save(inFreezer ? Pantry.Freeze(item, DateTime.Now, Cold) : Pantry.Thaw(item, DateTime.Now, Cold));

    /// <summary>A best-before set by hand; null puts it back to the usual one.</summary>
    public static void UseBy(PantryItem item, DateTime? useBy) =>
        Database.Save(useBy is null ? Pantry.UsualUseBy(item, Cold) : Pantry.SetUseBy(item, useBy));

    /// <summary>The fridge or freezer changed: the dates worked out from them are worked out again.</summary>
    public static void Redate()
    {
        var cold = Cold;
        foreach (var item in Database.Pantry().Where(p => !p.UseBySet)) Database.Save(Pantry.Refresh(item, cold));
    }

    /// <summary>The count is right as it is.</summary>
    public static void Confirmed(PantryItem item) => Database.Save(item with { Counted = DateTime.Now, Unsure = false });

    public static void Gone(PantryItem item) => Database.DeletePantry(item.Name);

    /// <summary>A recipe cooked: what it uses comes off the stock, once for <paramref name="key"/> (a meal, or a
    /// cooking).</summary>
    public static void Cooked(Recipe recipe, string key)
    {
        var done = "cooked|" + key;
        if (Database.KitchenDone(DateTime.Today.AddDays(-2)).Contains(done)) return;
        Database.SetKitchenDone(done);
        foreach (var item in Pantry.Cook(Database.Pantry(), recipe)) Database.Save(item);
    }

    /// <summary>A meal marked eaten: if the menu had a recipe for it, what it used comes off (unless its cooking
    /// already took it off).</summary>
    public static void Eaten(Dose dose)
    {
        if (dose.Reminder.Kind != ReminderKind.Meal) return;
        var meal = KitchenLog.Meals(DateOnly.FromDateTime(dose.Due)).FirstOrDefault(m => m.Meal == dose.Reminder.Name && m.At == dose.Due);
        if (meal?.Recipe is { } recipe) Cooked(recipe, MealKey(meal.At, recipe));
    }

    /// <summary>The key of a meal's cooking: by when it's eaten, so cooking it and eating it count once.</summary>
    public static string MealKey(DateTime at, Recipe recipe) => $"{at:yyyy-MM-ddTHH:mm}|{recipe.Id}";

    /// <summary>The check that's due now, if the week's time has come, something is in doubt, and it isn't done.</summary>
    public static PantryCheck? Due(DateTime now)
    {
        if (!CheckOn) return null;
        var (day, at) = CheckTime;
        var slot = now.Date.AddDays(-(((int)now.DayOfWeek - (int)day + 7) % 7)) + at.ToTimeSpan();
        if (slot > now || now - slot > TimeSpan.FromHours(12)) return null;
        var doubt = Pantry.InDoubt(Database.Pantry(), now);
        if (doubt.Count == 0) return null;
        var check = new PantryCheck(slot, doubt);
        return Database.KitchenDone(slot.AddDays(-1)).Contains(check.Key) ? null : check;
    }

    public static void CheckDone(PantryCheck check) => Database.SetKitchenDone(check.Key);

    /// <summary>Once, when the stock is new: what was bought in the last 30 days, as it was kept before.</summary>
    public static void SeedOnce()
    {
        if (Database.Setting("pantry_seeded") == "yes") return;
        Database.SetSetting("pantry_seeded", "yes");
        if (Database.Pantry().Count > 0) return;
        foreach (var item in Database.InKitchen(DateTime.Now.AddDays(-30)).OrderBy(i => i.Bought))
            if (Pantry.Tracked(item.Name))
                Database.Save(Pantry.Add(Find(item.Name), item.Name, item.Quantity, item.Bought ?? DateTime.Now, cold: Cold) with { Unsure = true });
    }
}
