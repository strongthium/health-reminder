namespace Strongthium.HealthReminder;

/// <summary>A cigarette: when it was lit and, when it's timed, when it was put out.</summary>
public sealed class Smoke
{
    public long Id { get; set; }
    public DateTime Started { get; set; }
    /// <summary>Null when it wasn't timed, or is still going.</summary>
    public DateTime? Finished { get; set; }
    public string Brand { get; set; } = "";

    /// <summary>How long it took, when it was timed and took a believable time (a minute to 20): a cigarette left
    /// unfinished on the button isn't counted.</summary>
    public TimeSpan? Duration => Finished - Started is { } d && d >= TimeSpan.FromMinutes(1) && d <= TimeSpan.FromMinutes(20) ? d : null;
}

/// <summary>What's smoked: the brand, how many a pack and its price (for what it costs), and the tar, nicotine and
/// carbon monoxide printed on the pack, where packs still print them.</summary>
public sealed record SmokeBrand(string Name = "", int PerPack = 20, decimal? PackPrice = null, double? TarMg = null, double? NicotineMg = null, double? CoMg = null);

/// <summary>What the log says, worked out at <c>now</c>.</summary>
/// <param name="PerDay">Over the last 7 days (or since the first one logged, if that's sooner).</param>
/// <param name="Duration">The average time a cigarette took over the last 14 days, and over the 14 before.</param>
/// <param name="FirstOfDay">The average time of the day's first cigarette over the last 7 days.</param>
public sealed record SmokeStats(int Today, double PerDay, TimeSpan? SinceLast, TimeSpan? Duration, TimeSpan? DurationBefore, TimeOnly? FirstOfDay, DateTime? Last);

/// <summary>Something in cigarette smoke: how much in one cigarette, roughly, and what it does.</summary>
public sealed record Constituent(string Name, string Amount, string What);

/// <summary>A way to get nicotine without smoke, at a strength to match how much is smoked now.</summary>
public sealed record NicotineOption(string Name, string Dose, string How);

/// <summary>
/// Cigarettes: the log's numbers, what's in the smoke, and nicotine replacement matched to how much is smoked, the
/// usual way to cut down and stop. Nicotine is what the body is hooked on; nearly all the harm is from the smoke, so the
/// same nicotine from a patch, gum, lozenge, inhaler or spray stands in for cigarettes, and is then stepped down.
/// The doses are the ones on the packs of nicotine replacement sold in Canada and the U.S.; a pharmacist can help pick.
/// </summary>
public static class Smoking
{
    /// <summary>How long smoke stays in the air of a room after a cigarette, roughly: two hours with a window open,
    /// five or more in a closed room. On clothes, hair and soft furnishings the smell stays until they're washed.</summary>
    public static readonly (TimeSpan Ventilated, TimeSpan Closed) AirClears = (TimeSpan.FromHours(2), TimeSpan.FromHours(5));

    /// <summary>
    /// Some of what's in the smoke of one cigarette, roughly, across brands, as measured by Health Canada's intense
    /// machine-smoking method (which smokes harder than the old ISO method that packs used to print). What a person takes
    /// in depends on how they smoke, so these are an order of magnitude, not a measurement.
    /// </summary>
    public static readonly IReadOnlyList<Constituent> Constituents =
    [
        new("Nicotine", "1.5 to 3 mg", "The addictive part. It isn't what causes cancer; it raises heart rate and blood pressure."),
        new("Tar", "20 to 35 mg", "The sticky particles; they carry most of the chemicals that cause cancer and coat the lungs."),
        new("Carbon monoxide", "20 to 30 mg", "Takes the place of oxygen in the blood, so the heart works harder."),
        new("Acetaldehyde", "about 1 to 2 mg", "Irritates the airways, and may make nicotine more addictive."),
        new("Hydrogen cyanide", "about 0.2 to 0.5 mg", "Damages the tiny hairs that clear the lungs."),
        new("Acrolein", "about 0.1 to 0.2 mg", "Irritates the eyes and airways; damages the lungs' lining."),
        new("Benzene", "about 50 to 100 µg", "Causes leukemia."),
        new("1,3-Butadiene", "about 50 to 120 µg", "Causes cancer."),
        new("Formaldehyde", "about 30 to 100 µg", "Causes cancer of the nose and throat."),
        new("Cadmium", "about 0.1 to 0.2 µg", "A metal that builds up in the kidneys."),
        new("NNK and NNN", "about 0.1 to 0.3 µg each", "Nitrosamines made from tobacco; among the strongest causes of lung and mouth cancer."),
        new("Benzo[a]pyrene", "about 10 to 20 ng", "Causes lung cancer."),
    ];

    public static SmokeStats Stats(IReadOnlyList<Smoke> smokes, DateTime now)
    {
        var today = now.Date;
        var first = smokes.Count > 0 ? smokes.Min(s => s.Started).Date : today;
        var weekStart = today.AddDays(-7) > first ? today.AddDays(-7) : first;
        var days = Math.Max(1, (today - weekStart).Days);
        // Full days only, unless today is the only one there is.
        var perDay = today > weekStart
            ? smokes.Count(s => s.Started >= weekStart && s.Started < today) / (double)days
            : smokes.Count(s => s.Started >= today);
        TimeSpan? Average(IEnumerable<Smoke> some) =>
            some.Select(s => s.Duration).OfType<TimeSpan>().ToList() is { Count: > 0 } d ? TimeSpan.FromSeconds(d.Average(t => t.TotalSeconds)) : null;
        var firsts = smokes.Where(s => s.Started >= today.AddDays(-7) && s.Started < today)
            .GroupBy(s => s.Started.Date)
            .Select(g => g.Min(s => s.Started).TimeOfDay.TotalMinutes)
            .ToList();
        var last = smokes.Count > 0 ? smokes.Max(s => s.Started) : (DateTime?)null;
        return new SmokeStats(
            smokes.Count(s => s.Started >= today),
            Math.Round(perDay, 1),
            last is { } l ? now - l : null,
            Average(smokes.Where(s => s.Started >= now.AddDays(-14))),
            Average(smokes.Where(s => s.Started >= now.AddDays(-28) && s.Started < now.AddDays(-14))),
            firsts.Count > 0 ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(firsts.Average())) : null,
            last);
    }

    /// <summary>
    /// Nicotine replacement to match how much is smoked, as the packs dose it: a patch of 21 mg a day for more than 10
    /// cigarettes a day (14 mg for 10 or fewer), stepped down; gum or lozenges of 4 mg when the first cigarette is within
    /// 30 minutes of getting up (a sign of stronger dependence), 2 mg otherwise. A patch with gum or a lozenge for
    /// cravings works better than either alone.
    /// </summary>
    public static IReadOnlyList<NicotineOption> Replacement(double perDay, TimeSpan? firstAfterWaking)
    {
        var strong = firstAfterWaking is { } first && first <= TimeSpan.FromMinutes(30);
        var gum = strong ? "4 mg" : "2 mg";
        var options = new List<NicotineOption>();
        if (perDay > 10)
            options.Add(new("Patch", "21 mg a day",
                "One a day, put on in the morning: 21 mg for 6 weeks, then 14 mg for 2 weeks, then 7 mg for 2 weeks, then stop."));
        else if (perDay >= 5)
            options.Add(new("Patch", "14 mg a day",
                "One a day, put on in the morning: 14 mg for 6 weeks, then 7 mg for 2 weeks, then stop."));
        options.Add(new("Gum or lozenge", gum,
            "One when a craving comes, instead of a cigarette; at most 20 lozenges or 24 pieces of gum a day. Over 12 weeks, " +
            "use fewer: about one every 1 to 2 hours at first, then every 2 to 4 hours, then every 4 to 8 hours. Chew gum " +
            "until it tingles, then park it in the cheek."));
        options.Add(new("Inhaler or mouth spray", "1 mg a spray",
            "For the hand-to-mouth habit and quick cravings: a spray or a few puffs on the inhaler in place of a cigarette."));
        return options;
    }

    /// <summary>
    /// A goal of cutting down: from the number a day there was at the start, so many fewer each week, never below zero.
    /// </summary>
    public static int Target(double startPerDay, DateOnly start, int fewerEachWeek, DateOnly today)
    {
        var weeks = Math.Max(0, (today.DayNumber - start.DayNumber) / 7);
        return Math.Max(0, (int)Math.Round(startPerDay) - weeks * fewerEachWeek);
    }
}
