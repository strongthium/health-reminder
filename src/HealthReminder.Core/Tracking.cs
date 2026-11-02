using System.Globalization;

namespace Strongthium.HealthReminder;

/// <summary>
/// Whether the days are being kept track of. Stopped, nothing that goes unanswered counts as missed and nothing rings
/// (medication still does, when <see cref="KeepMedication"/> says so). Reset, the stats and what was missed count from
/// <see cref="From"/>. Neither deletes anything: what was taken stays in the History, and medication on hand still
/// counts the doses taken.
/// </summary>
public sealed class Tracking
{
    private readonly Database database;
    private readonly List<(DateTime From, DateTime To)> pauses = [];

    public Tracking(Database database)
    {
        this.database = database;
        From = Read("tracking_from");
        StoppedFrom = Read("tracking_stopped");
        KeepMedication = database.Setting("tracking_medication") != "off";
        foreach (var pause in (database.Setting("tracking_pauses") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var ends = pause.Split('|');
            pauses.Add((DateTime.Parse(ends[0], CultureInfo.InvariantCulture), DateTime.Parse(ends[1], CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>When the stats count from, once they've been reset.</summary>
    public DateTime? From { get; private set; }
    /// <summary>When it was stopped, until it's started again.</summary>
    public DateTime? StoppedFrom { get; private set; }
    public bool Stopped => StoppedFrom is not null;
    /// <summary>Medication still rings while it's stopped.</summary>
    public bool KeepMedication { get; private set; }

    /// <summary>A dose at this time doesn't count as missed: it's from before the reset, or from while it was stopped.</summary>
    public bool Excused(DateTime due) => due < From || due >= StoppedFrom || pauses.Any(p => due >= p.From && due < p.To);

    /// <summary>The first day of those from <paramref name="first"/> on that the stats count.</summary>
    public DateTime Counted(DateTime first) => From is { } from && from.Date > first ? from.Date : first;

    public DateOnly Counted(DateOnly first) => DateOnly.FromDateTime(Counted(first.ToDateTime(TimeOnly.MinValue)));

    public void Stop(DateTime from, bool keepMedication)
    {
        if (Stopped) return;
        (StoppedFrom, KeepMedication) = (from, keepMedication);
        Write("tracking_stopped", from);
        database.SetSetting("tracking_medication", keepMedication ? "on" : "off");
    }

    public void Start(DateTime now)
    {
        if (StoppedFrom is not { } from) return;
        pauses.Add((from, now));
        StoppedFrom = null;
        Write("tracking_stopped", null);
        database.SetSetting("tracking_pauses", string.Join(';', pauses.Select(p => Text(p.From) + "|" + Text(p.To))));
    }

    public void Reset(DateTime from)
    {
        From = from;
        Write("tracking_from", from);
    }

    /// <summary>Back to counting everything, as before the reset.</summary>
    public void UndoReset()
    {
        From = null;
        Write("tracking_from", null);
    }

    private DateTime? Read(string key) => database.Setting(key) is { Length: > 0 } text ? DateTime.Parse(text, CultureInfo.InvariantCulture) : null;

    private void Write(string key, DateTime? value) => database.SetSetting(key, value is { } at ? Text(at) : "");

    private static string Text(DateTime at) => at.ToString("s", CultureInfo.InvariantCulture);
}
