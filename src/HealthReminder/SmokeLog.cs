using System.Globalization;

namespace Strongthium.HealthReminder;

/// <summary>Logging cigarettes, from the on-top button, the tray or the Smoking page, and what's set about them.</summary>
internal static class SmokeLog
{
    private static Database Database => App.Current.Database;

    /// <summary>The cigarette lit and not yet put out, when they're timed; one lit over 20 minutes ago is let go.</summary>
    public static Smoke? Open() =>
        App.Current.SmokeTimed
            ? Database.Smokes(DateTime.Now.AddMinutes(-20)).LastOrDefault(s => s.Finished is null)
            : null;

    public static int Today() => Database.Smokes(DateTime.Today).Count;

    /// <summary>Timed: lit, or put out. Otherwise one cigarette, logged as it's lit; it isn't timed.</summary>
    public static void Click()
    {
        var now = DateTime.Now;
        if (Open() is { } open)
        {
            open.Finished = now;
            Database.Save(open);
        }
        else
        {
            Database.Save(new Smoke { Started = now, Finished = App.Current.SmokeTimed ? null : now, Brand = Brand.Name });
        }
        App.Current.SmokeChanged();
    }

    public static void UndoLast()
    {
        if (Database.Smokes(DateTime.Now.AddDays(-1)).LastOrDefault() is { } last) Database.Delete(last);
        App.Current.SmokeChanged();
    }

    public static SmokeBrand Brand
    {
        get
        {
            var saved = Values("smoke_brand");
            double? Number(string key) => double.TryParse(saved.GetValueOrDefault(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
            return new SmokeBrand(
                saved.GetValueOrDefault("name", ""),
                (int)(Number("per_pack") ?? 20),
                decimal.TryParse(saved.GetValueOrDefault("price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : null,
                Number("tar"), Number("nicotine"), Number("co"));
        }
        set
        {
            string N(double? n) => n?.ToString(CultureInfo.InvariantCulture) ?? "";
            Store("smoke_brand", new()
            {
                ["name"] = value.Name.Replace(";", ",").Replace("=", "-"), ["per_pack"] = value.PerPack.ToString(CultureInfo.InvariantCulture),
                ["price"] = value.PackPrice?.ToString(CultureInfo.InvariantCulture) ?? "", ["tar"] = N(value.TarMg), ["nicotine"] = N(value.NicotineMg), ["co"] = N(value.CoMg),
            });
        }
    }

    /// <summary>Cutting down: from so many a day, starting on a day, so many fewer each week; null when there's no goal.</summary>
    public static (double From, DateOnly Start, int Fewer)? Goal
    {
        get
        {
            var saved = Values("smoke_goal");
            return double.TryParse(saved.GetValueOrDefault("from"), NumberStyles.Float, CultureInfo.InvariantCulture, out var from)
                && DateOnly.TryParseExact(saved.GetValueOrDefault("start"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                && int.TryParse(saved.GetValueOrDefault("fewer"), CultureInfo.InvariantCulture, out var fewer)
                ? (from, start, fewer) : null;
        }
        set => Store("smoke_goal", value is { } goal
            ? new()
            {
                ["from"] = goal.From.ToString(CultureInfo.InvariantCulture), ["start"] = goal.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["fewer"] = goal.Fewer.ToString(CultureInfo.InvariantCulture),
            }
            : []);
    }

    /// <summary>The recalibration steps started, by id, with the day each was.</summary>
    public static Dictionary<string, DateOnly> StepsStarted
    {
        get => Values("smoke_steps")
            .Where(v => DateOnly.TryParseExact(v.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .ToDictionary(v => v.Key, v => DateOnly.ParseExact(v.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        set => Store("smoke_steps", value.ToDictionary(v => v.Key, v => v.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    /// <summary>Today's most, when cutting down.</summary>
    public static int? TargetToday() =>
        Goal is { } goal ? Smoking.Target(goal.From, goal.Start, goal.Fewer, DateOnly.FromDateTime(DateTime.Today)) : null;

    private static Dictionary<string, string> Values(string key) =>
        (Database.Setting(key) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);

    private static void Store(string key, Dictionary<string, string> values) =>
        Database.SetSetting(key, string.Join(';', values.Select(v => v.Key + "=" + v.Value)));
}
