namespace Strongthium.HealthReminder;

/// <summary>A bag hung on an IV drip: when it was started, and when the reminder to change it rings.</summary>
public readonly record struct IvBag(DateTime Started, DateTime Due);

/// <summary>
/// An IV drip: the line it runs through and where it is, and the bags. Each bag runs for <see cref="Hours"/> (its volume
/// over the rate, when both are known), and the reminder rings <see cref="WarnMinutes"/> before it runs out. Marking it
/// changed hangs the next bag then, so a bag changed late or early moves the ones after it.
/// </summary>
public sealed class IvDrip
{
    public static readonly string[] Lines = ["PICC line", "Midline", "Peripheral IV (cannula)", "Implanted port", "Central line"];
    public static readonly string[] Sites = ["Left arm", "Right arm", "Left hand", "Right hand", "Left chest", "Right chest", "Neck"];
    /// <summary>The shortest a bag can run, in hours.</summary>
    public const double ShortestRun = 0.5;
    /// <summary>The bags kept: enough for the stats and the history.</summary>
    private const int Kept = 200;

    /// <summary>The kind of line ("PICC line") and where it goes in ("Left arm"); "" when not said.</summary>
    public string Line { get; set; } = "";
    public string Site { get; set; } = "";
    /// <summary>When the line was put in, if it was said.</summary>
    public DateTime? LineSince { get; set; }
    /// <summary>A bag's volume and the rate it runs at; 0 when not said.</summary>
    public double BagMl { get; set; }
    public double MlPerHour { get; set; }
    /// <summary>How long a bag runs.</summary>
    public double Hours { get; set; } = 12;
    public int WarnMinutes { get; set; } = 15;
    /// <summary>The bags hung, oldest first; the last is the one running.</summary>
    public List<IvBag> Bags { get; set; } = [];

    public IvBag? Current => Bags.Count > 0 ? Bags[^1] : null;

    /// <summary>"PICC line, left arm": the line and where it is, as much as was said.</summary>
    public string Where => string.Join(", ", new[] { Line, Line.Length > 0 ? Site.ToLowerInvariant() : Site }.Where(s => s.Length > 0));

    /// <summary>How long a bag of this volume runs at this rate; null unless both are known.</summary>
    public static double? RunHours(double bagMl, double mlPerHour) => bagMl > 0 && mlPerHour > 0 ? bagMl / mlPerHour : null;

    /// <summary>When a bag started then runs out.</summary>
    public DateTime RunsOut(DateTime started) => started + Run;

    /// <summary>How long a bag runs, to the minute.</summary>
    private TimeSpan Run => TimeSpan.FromMinutes(Math.Max(1, Math.Round(Hours * 60)));

    /// <summary>A bag started then: it rings <see cref="WarnMinutes"/> before it runs out, to the minute, and not
    /// before a minute after it was started.</summary>
    public IvBag Bag(DateTime started)
    {
        var due = RunsOut(started).AddMinutes(-WarnMinutes);
        if (due < started.AddMinutes(1)) due = started.AddMinutes(1);
        return new IvBag(started, due.AddTicks(-(due.Ticks % TimeSpan.TicksPerMinute)));
    }

    /// <summary>A new bag hung at <paramref name="started"/>.</summary>
    public void Hang(DateTime started)
    {
        Bags.Add(Bag(started));
        if (Bags.Count > Kept) Bags.RemoveRange(0, Bags.Count - Kept);
    }

    /// <summary>The bag running was started at another time, or runs for another time: put right, rather than a new
    /// one hung.</summary>
    public void Restart(DateTime started)
    {
        if (Bags.Count > 0) Bags[^1] = Bag(started);
        else Hang(started);
    }

    /// <summary>
    /// When it rings, from <paramref name="from"/> to before <paramref name="to"/>. For a bag that was followed by
    /// another: when it was due, and again each run after that until the next was hung (changed when a later reminder
    /// rang). For the bag running: when it's due, and each run after that, as if each bag were changed on time.
    /// </summary>
    public IEnumerable<DateTime> Dues(DateTime from, DateTime to)
    {
        var run = Run;
        for (var i = 0; i < Bags.Count; i++)
        {
            var running = i == Bags.Count - 1;
            for (var due = Bags[i].Due; due < to; due += run)
            {
                if (!running && due > Bags[i].Due && due >= Bags[i + 1].Started) break;
                if (due >= from) yield return due;
            }
        }
    }
}
