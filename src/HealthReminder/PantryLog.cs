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

    /// <summary>What there is of a name, in the order it's listed: two bags of it kept apart are two.</summary>
    public static List<PantryItem> Named(string name) =>
        Database.Pantry().Where(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    private static PantryItem? Find(string name) => Named(name).FirstOrDefault();

    private static ColdStorage Cold => App.Current.Cold;

    /// <summary>Bought (from a photo): added to what there is of it; <paramref name="inFreezer"/> when it went straight
    /// in the freezer.</summary>
    public static void Bought(string name, string quantity, int useWithinDays = 0, bool unsure = false, bool inFreezer = false)
    {
        if (name.Trim().Length == 0 || !Pantry.Tracked(name)) return;
        Database.Save(Pantry.Add(Find(name), name.Trim(), quantity, DateTime.Now, useWithinDays, unsure, inFreezer, Cold));
    }

    /// <summary>Something new as it would go in, where its name says, to ask where it goes.</summary>
    public static PantryItem New(string name, string quantity) => Pantry.Add(null, name.Trim(), quantity, DateTime.Now, cold: Cold);

    private static string PutKey(string name) => "put:" + name.Trim().ToLowerInvariant();

    /// <summary>How something was put away the last time, by hand: the place (while it's there) and the group, and
    /// whether, with some of it there already, it was kept apart or added to that (null when that wasn't asked).</summary>
    public static (KitchenPlace Place, FoodGroup Group, bool? Apart)? PutBefore(string name)
    {
        var saved = (Database.Setting(PutKey(name)) ?? "").Split('|');
        if (saved.Length is not (2 or 3) || !long.TryParse(saved[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || !Enum.TryParse<FoodGroup>(saved[1], out var group))
            return null;
        bool? apart = saved.Length == 3 ? saved[2] == "apart" : null;
        return Places().FirstOrDefault(p => p.Id == id) is { } place ? (place, group, apart) : null;
    }

    /// <summary>
    /// Bought by hand and put away: added to <paramref name="into"/>, some of it there already; or else a thing of its
    /// own, beside any of its name, in a place picked for it (frozen in a freezer, thawed out of one) and shown with a
    /// group. How it was put away is kept for next time; <paramref name="apart"/> says, when some was there, whether it
    /// was kept apart.
    /// </summary>
    public static void PutAway(string name, string quantity, KitchenPlace place, FoodGroup group, PantryItem? into = null, bool? apart = null)
    {
        if (name.Trim().Length == 0 || !Pantry.Tracked(name)) return;
        var before = PutBefore(name);
        apart ??= before?.Apart;
        Database.SetSetting(PutKey(name), $"{place.Id.ToString(CultureInfo.InvariantCulture)}|{group}" + (apart is { } a ? a ? "|apart" : "|together" : ""));
        var now = DateTime.Now;
        if (into is not null)
        {
            Database.Save(Pantry.Add(into, name.Trim(), quantity, now, cold: Cold));
            return;
        }
        var item = Pantry.Move(Pantry.Add(null, name.Trim(), quantity, now, cold: Cold), place, now, Cold);
        Database.Save(item with { Group = group == item.Guess.Group ? null : group });
    }

    /// <summary>Counted (or seen in a photo of where it's kept): this much there is now.</summary>
    public static void Counted(string name, string quantity, int useWithinDays = 0, bool unsure = false, bool inFreezer = false)
    {
        if (name.Trim().Length == 0 || !Pantry.Tracked(name)) return;
        Database.Save(Pantry.Set(name.Trim(), quantity, DateTime.Now, useWithinDays, unsure, Find(name), inFreezer, Cold));
    }

    public static List<KitchenPlace> Places() => Database.Places();

    /// <summary>Changed in the kitchen: its picture, group, best-before or place (which freezes or thaws it).</summary>
    public static void Changed(PantryItem item) => Database.Save(item);

    /// <summary>Part of something made a thing of its own, beside it; null, with the problem, when that can't be.</summary>
    public static PantryItem? Split(PantryItem item, string amount, out string problem)
    {
        if (Pantry.Split(item, amount, out problem) is not { } split) return null;
        Database.Save(split.Left);
        RememberApart(item, true);
        return Database.Save(split.Part);
    }

    /// <summary>Split or put together by hand: what's bought of it next time is kept apart, or added, the same way.</summary>
    private static void RememberApart(PantryItem item, bool apart)
    {
        var (place, group) = PutBefore(item.Name) is { } before ? (before.Place, before.Group) : (Pantry.PlaceOf(item, Places()), item.ShownGroup);
        Database.SetSetting(PutKey(item.Name), $"{place.Id.ToString(CultureInfo.InvariantCulture)}|{group}|{(apart ? "apart" : "together")}");
    }

    /// <summary>The others of its name, in the same unit, put together with it as one.</summary>
    public static PantryItem Join(PantryItem item)
    {
        var others = Named(item.Name).Where(p => p.Id != item.Id && p.Unit == item.Unit).ToList();
        var joined = Database.Save(Pantry.Join(item, others));
        foreach (var other in others) Database.DeletePantry(other);
        RememberApart(item, false);
        return joined;
    }

    /// <summary>Some of it used up: eaten, which goes in today's food log too, with about its kcal (and goes back on
    /// if it's taken out of the log), or thrown out. Null, with the problem, when the amount isn't one.</summary>
    public static (PantryItem Left, Portion Portion, string Amount)? Used(PantryItem item, string amount, bool eaten, out string problem)
    {
        if (Pantry.Use(item, amount, out problem) is not { } use) return null;
        if (eaten) FoodLog.FromKitchen(item, use.Amount, use.Portion.Kcal, DateTime.Now, null);
        else Database.Save(use.Left);
        return (Database.Pantry().FirstOrDefault(p => p.Id == item.Id) ?? use.Left, use.Portion, use.Amount);
    }

    /// <summary>Things moved to a place together: into a freezer they're frozen from now, out of one thawed.</summary>
    public static void Move(IEnumerable<PantryItem> items, KitchenPlace to)
    {
        var now = DateTime.Now;
        foreach (var item in items) Database.Save(Pantry.Move(item, to, now, Cold));
    }

    /// <summary>The order things are shown in, within each group.</summary>
    public static KitchenSort Sort
    {
        get => Enum.TryParse<KitchenSort>(Database.Setting("kitchen_sort"), out var sort) ? sort : KitchenSort.Name;
        set => Database.SetSetting("kitchen_sort", value.ToString());
    }

    /// <summary>Put in, or into the freezer, on another day: its best-before follows, unless set by hand.</summary>
    public static PantryItem Added(PantryItem item, DateTime day) => Pantry.SetAdded(item, day, Cold);

    public static PantryItem FrozenOn(PantryItem item, DateTime day) => Pantry.SetFrozen(item, day, Cold);

    public static PantryItem UsualUseBy(PantryItem item) => Pantry.UsualUseBy(item, Cold);

    /// <summary>Moved, as one is in its row: frozen or thawed as it goes.</summary>
    public static PantryItem MovedTo(PantryItem item, KitchenPlace to) => Pantry.Move(item, to, DateTime.Now, Cold);

    /// <summary>Counted, as typed in its row: this much there is now, all else as it was.</summary>
    public static PantryItem Recounted(PantryItem item, string quantity) => Pantry.Set(item.Name, quantity, DateTime.Now, existing: item, cold: Cold);

    /// <summary>A place added at the end.</summary>
    public static void AddPlace(string name, Kept kind)
    {
        var places = Database.Places();
        Database.Save(new KitchenPlace(0, name.Trim().Length > 0 ? name.Trim() : Foods.Name(kind), kind, places.Count == 0 ? 0 : places.Max(p => p.Sort) + 1));
    }

    /// <summary>A place renamed, or made another kind: what was put in it is frozen or thawed to match.</summary>
    public static void ChangePlace(KitchenPlace place)
    {
        Database.Save(place);
        var now = DateTime.Now;
        foreach (var item in Database.Pantry().Where(p => p.Place == place.Id))
            Database.Save(Pantry.Move(item, place, now, Cold));
    }

    /// <summary>A place moved up or down one.</summary>
    public static void MovePlace(KitchenPlace place, int by)
    {
        var places = Database.Places();
        var at = places.FindIndex(p => p.Id == place.Id);
        if (at < 0 || at + by < 0 || at + by >= places.Count) return;
        (places[at], places[at + by]) = (places[at + by], places[at]);
        for (var i = 0; i < places.Count; i++) Database.Save(places[i] with { Sort = i });
    }

    /// <summary>A place taken away, unless it's the last: what was in it goes where its kind goes.</summary>
    public static void DeletePlace(KitchenPlace place)
    {
        if (Database.Places().Count > 1) Database.DeletePlace(place.Id);
    }

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

    public static void Gone(PantryItem item) => Database.DeletePantry(item);

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
