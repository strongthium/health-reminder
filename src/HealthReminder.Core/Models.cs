using System.Globalization;

namespace Strongthium.HealthReminder;

/// <summary>What a pill looks like, as its manufacturer describes it in the FDA's structured product label (SPL).</summary>
/// <param name="Colors">One colour, or two for a two-tone capsule: SPL's names (WHITE, BLUE, ...).</param>
/// <param name="Shape">SPL's shape name (ROUND, OVAL, CAPSULE, ...).</param>
/// <param name="SizeMm">The longest side in millimetres; 0 when the label doesn't say.</param>
/// <param name="Imprint">What is printed or engraved on it, its sides separated by ';' ("H;144").</param>
/// <param name="Score">How many pieces the score lines split it into; 1 when it has none.</param>
public sealed record PillAppearance(IReadOnlyList<string> Colors, string Shape, double SizeMm, string Imprint, int Score)
{
    public static readonly PillAppearance Unknown = new([], "", 0, "", 1);

    public bool IsKnown => Colors.Count > 0 || Shape.Length > 0 || Imprint.Length > 0;

    /// <summary>"white round tablet, 8 mm, scored in half, marked H / 144": what to look for, in words.</summary>
    /// <param name="form">The label's dosage form ("TABLET, FILM COATED"), which names the pill better than its shape.</param>
    public string Describe(string form = "")
    {
        var parts = new List<string>();
        var colour = string.Join(" and ", Colors.Select(c => c.ToLowerInvariant()));
        var isCapsule = Shape.Equals("CAPSULE", StringComparison.OrdinalIgnoreCase);
        var noun = form.Contains("CAPSULE", StringComparison.OrdinalIgnoreCase) ? "capsule"
            : form.Contains("TABLET", StringComparison.OrdinalIgnoreCase) ? "tablet"
            : isCapsule ? "capsule" : "pill";
        // A tablet the shape of a capsule is "capsule-shaped", not a capsule. "(5 SIDED)" and the like are dropped.
        var shape = Shape.Length == 0 || (isCapsule && noun == "capsule") ? ""
            : isCapsule ? "capsule-shaped"
            : Shape.Split(" (")[0].ToLowerInvariant();
        parts.Add(string.Join(' ', new[] { colour, shape, noun }.Where(s => s.Length > 0)));
        if (SizeMm > 0) parts.Add($"{SizeMm:0.#} mm");
        if (Score > 1) parts.Add(Score == 2 ? "scored in half" : $"scored in {Score}");
        if (Imprint.Length > 0) parts.Add("marked " + string.Join(" / ", ImprintSides));
        return string.Join(", ", parts);
    }

    public IReadOnlyList<string> ImprintSides => Imprint.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Whether this is the same pill as <paramref name="other"/>, as far as the labels tell: the same imprint
    /// (the sides in any order, spaces ignored), and the same shape and a colour in common where both say.</summary>
    public bool SamePillAs(PillAppearance other)
    {
        static string Key(PillAppearance a) => string.Join(';', a.ImprintSides.Select(s => s.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant()).Order(StringComparer.Ordinal));
        if (Imprint.Length == 0 || Key(this) != Key(other)) return false;
        if (Shape.Length > 0 && other.Shape.Length > 0 && !Shape.Equals(other.Shape, StringComparison.OrdinalIgnoreCase)) return false;
        return Colors.Count == 0 || other.Colors.Count == 0 || Colors.Intersect(other.Colors, StringComparer.OrdinalIgnoreCase).Any();
    }
}

/// <summary>What a reminder is for. The medication kinds can be looked up on DailyMed; the checks record a value.</summary>
public enum ReminderKind
{
    Pill, Injection, Liquid, Inhaler, Drops, Patch, OtherMedication,
    BloodSugar, BloodPressure, Weight, OtherCheck,
    Meal, Drink, Other,
}

/// <summary>What each kind is called, counted in and asked for.</summary>
public static class Kinds
{
    public static readonly ReminderKind[] All = Enum.GetValues<ReminderKind>();

    public static string Label(this ReminderKind kind) => kind switch
    {
        ReminderKind.Pill => "Pill (tablet or capsule)",
        ReminderKind.Injection => "Injection (insulin, pen, syringe)",
        ReminderKind.Liquid => "Liquid medicine",
        ReminderKind.Inhaler => "Inhaler",
        ReminderKind.Drops => "Eye or ear drops",
        ReminderKind.Patch => "Patch or cream",
        ReminderKind.OtherMedication => "Other medication",
        ReminderKind.BloodSugar => "Blood sugar test",
        ReminderKind.BloodPressure => "Blood pressure check",
        ReminderKind.Weight => "Weigh myself",
        ReminderKind.OtherCheck => "Other check or measurement",
        ReminderKind.Meal => "Meal or snack",
        ReminderKind.Drink => "Drink water",
        _ => "Something else",
    };

    /// <summary>What each kind is called in a word or two, on its badge: "Pill", "Blood sugar".</summary>
    public static string ShortLabel(this ReminderKind kind) => kind switch
    {
        ReminderKind.Liquid => "Liquid",
        ReminderKind.Drops => "Drops",
        ReminderKind.Patch => "Patch",
        ReminderKind.OtherMedication => "Medication",
        ReminderKind.BloodSugar => "Blood sugar",
        ReminderKind.BloodPressure => "Blood pressure",
        ReminderKind.OtherCheck => "Check",
        ReminderKind.Other => "Reminder",
        _ => kind.ToString(),
    };

    public static bool IsMedication(this ReminderKind kind) => kind <= ReminderKind.OtherMedication;

    /// <summary>A check: the reminder asks for a reading (blood sugar, blood pressure, weight, ...).</summary>
    public static bool IsCheck(this ReminderKind kind) => kind is >= ReminderKind.BloodSugar and <= ReminderKind.OtherCheck;

    /// <summary>Which group the kind rings with: each group has its own sound and colour. Anything else goes with
    /// medication, the group that is most important not to miss.</summary>
    public static AlertGroup Group(this ReminderKind kind) =>
        kind.IsCheck() ? AlertGroup.Check
        : kind is ReminderKind.Meal or ReminderKind.Drink ? AlertGroup.EatDrink
        : AlertGroup.Medication;

    /// <summary>The group of the most important of these kinds (medication, then checks, then eating and drinking),
    /// which a reminder of several rings and looks like.</summary>
    public static AlertGroup Group(this IEnumerable<ReminderKind> kinds) =>
        kinds.Select(k => k.Group()).DefaultIfEmpty(AlertGroup.Medication).Min();

    public static string Label(this AlertGroup group) => group switch
    {
        AlertGroup.Check => "Checks",
        AlertGroup.EatDrink => "Meals and drinks",
        _ => "Medication",
    };

    /// <summary>Whether each time has an amount: how many pills, units, mL, calories.</summary>
    public static bool HasAmount(this ReminderKind kind) => !kind.IsCheck() && kind != ReminderKind.Other;

    /// <summary>What the amount is counted in, unless the user says otherwise.</summary>
    public static string DefaultUnit(this ReminderKind kind, string form = "") => kind switch
    {
        ReminderKind.Pill => form.Contains("CAPSULE", StringComparison.OrdinalIgnoreCase) ? "capsule" : "tablet",
        ReminderKind.Injection => "unit",
        ReminderKind.Liquid => "mL",
        ReminderKind.Inhaler => "puff",
        ReminderKind.Drops => "drop",
        ReminderKind.Patch => "patch",
        ReminderKind.OtherMedication => "dose",
        ReminderKind.Meal => "kcal",
        ReminderKind.Drink => "mL",
        _ => "",
    };

    public static IReadOnlyList<string> Units(this ReminderKind kind) => kind switch
    {
        ReminderKind.Pill => ["tablet", "capsule", "softgel", "lozenge"],
        ReminderKind.Injection => ["unit", "mL", "mg", "pen", "syringe", "dose"],
        ReminderKind.Liquid => ["mL", "teaspoon", "tablespoon", "drop"],
        ReminderKind.Inhaler => ["puff", "dose"],
        ReminderKind.Drops => ["drop"],
        ReminderKind.Patch => ["patch", "application"],
        ReminderKind.OtherMedication => ["dose", "tablet", "sachet", "spray", "mL"],
        ReminderKind.Meal => ["kcal"],
        ReminderKind.Drink => ["mL"],
        _ => [],
    };

    /// <summary>What a check's reading is measured in; the first is the default for the user's country.</summary>
    public static IReadOnlyList<string> ValueUnits(this ReminderKind kind)
    {
        var us = RegionInfo.CurrentRegion.TwoLetterISORegionName is "US" or "LR" or "MM";
        return kind switch
        {
            ReminderKind.BloodSugar => us ? ["mg/dL", "mmol/L"] : ["mmol/L", "mg/dL"],
            ReminderKind.BloodPressure => ["mmHg"],
            ReminderKind.Weight => us ? ["lb", "kg"] : ["kg", "lb"],
            _ => [],
        };
    }

    /// <summary>The name a new reminder of this kind starts with.</summary>
    public static string DefaultName(this ReminderKind kind) => kind switch
    {
        ReminderKind.BloodSugar => "Blood sugar test",
        ReminderKind.BloodPressure => "Blood pressure",
        ReminderKind.Weight => "Weight",
        ReminderKind.Drink => "Water",
        _ => "",
    };

    /// <summary>The verb of the alarm: "Take", "Inject", "Check".</summary>
    public static string Verb(this ReminderKind kind) => kind switch
    {
        ReminderKind.Injection => "Inject",
        ReminderKind.Inhaler => "Inhale",
        ReminderKind.Drops or ReminderKind.Patch => "Use",
        ReminderKind.BloodSugar => "Test your blood sugar",
        ReminderKind.BloodPressure => "Take your blood pressure",
        ReminderKind.Weight => "Weigh yourself",
        ReminderKind.OtherCheck => "Check",
        ReminderKind.Meal => "Eat",
        ReminderKind.Drink => "Drink",
        ReminderKind.Other => "",
        _ => "Take",
    };

    /// <summary>The words of the button that says it's done.</summary>
    public static string DoneText(this ReminderKind kind) => kind switch
    {
        ReminderKind.Injection => "Injected",
        _ when kind.IsMedication() => "I took it",
        _ when kind.IsCheck() => "Save",
        ReminderKind.Meal => "I ate",
        ReminderKind.Drink => "I drank it",
        _ => "Done",
    };
}

/// <summary>Where insulin and other injections can go; the reminder suggests the next one in turn.</summary>
public static class InjectionSites
{
    public static readonly string[] All =
    [
        "Abdomen, left", "Abdomen, right", "Thigh, left", "Thigh, right", "Arm, left", "Arm, right", "Buttock, left", "Buttock, right",
    ];
}

/// <summary>One time of day a reminder rings, and how much to take then.</summary>
public readonly record struct ReminderTime(TimeOnly Time, double Amount);

/// <summary>Something to be reminded of: a medication, a check, a meal or a drink, when, and how much.</summary>
public sealed class Reminder
{
    public long Id { get; set; }
    public ReminderKind Kind { get; set; } = ReminderKind.Pill;
    public string Name { get; set; } = "";
    /// <summary>What the amounts count: "tablet", "unit", "mL", "kcal". Singular; see <see cref="Amounts"/>.</summary>
    public string Unit { get; set; } = "tablet";
    public string Notes { get; set; } = "";
    /// <summary>The times of day it rings, in local time, sorted, each with its amount.</summary>
    public List<ReminderTime> Times { get; set; } = [];
    /// <summary>The days of the week it rings; all of them by default.</summary>
    public DaySet Days { get; set; } = DaySet.Every;
    public bool Active { get; set; } = true;
    /// <summary>When it was added: doses scheduled before then aren't reminded of.</summary>
    public DateTime Created { get; set; } = DateTime.Now;
    /// <summary>Made by the eating and drinking planner, which replaces its reminders when it is run again.</summary>
    public bool Planned { get; set; }

    public PillAppearance Appearance { get; set; } = PillAppearance.Unknown;
    /// <summary>The dosage form from the label: TABLET, CAPSULE, TABLET, FILM COATED, INJECTION, SOLUTION...</summary>
    public string Form { get; set; } = "";
    /// <summary>The DailyMed label it was looked up in, the product (NDC) chosen from it and its RxNorm concept.</summary>
    public string SetId { get; set; } = "";
    public string Ndc { get; set; } = "";
    public string RxCui { get; set; } = "";
    public string LabelTitle { get; set; } = "";
    /// <summary>A photo of the pill, as a file in the data directory's images/; empty for none.</summary>
    public string Photo { get; set; } = "";
    /// <summary>Which label the photo came from, in words.</summary>
    public string PhotoSource { get; set; } = "";

    /// <summary>For an injection: the sites to rotate through, in order.</summary>
    public List<string> Sites { get; set; } = [];
    /// <summary>For a check: what the reading is measured in (mmol/L, mg/dL, mmHg, kg, lb).</summary>
    public string ValueUnit { get; set; } = "";
    /// <summary>Answering it records a video with the webcam, with the date and time on it, as evidence.</summary>
    public bool RecordVideo { get; set; }
    /// <summary>Canada's Drug Identification Number, the 8 digits on a Canadian label or pharmacy sticker.</summary>
    public string Din { get; set; } = "";
    /// <summary>The pharmacy's prescription number ("Rx 4471182"), for refills; it is the pharmacy's own, and can't be
    /// looked up.</summary>
    public string RxNumber { get; set; } = "";
    /// <summary>How many (in <see cref="Unit"/>) there were when counted, at <see cref="StockAt"/>; refills received add to
    /// it and doses marked taken take from it. Null when it isn't counted.</summary>
    public double? StockCount { get; set; }
    public DateTime? StockAt { get; set; }

    /// <summary>How much is taken in a week, on average a day: the amounts at its times on the days it's taken.</summary>
    public double DailyUse => Times.Sum(t => t.Amount) * Enum.GetValues<DayOfWeek>().Count(d => Days.Contains(d)) / 7.0;

    /// <summary>The amount at <paramref name="time"/>, or 1 when it has none.</summary>
    public double AmountAt(TimeOnly time) => Times.FirstOrDefault(t => t.Time == time) is { Amount: > 0 } t ? t.Amount : 1;
}

/// <summary>Writing amounts: "1 tablet", "½ tablet", "2½ tablets", "12 units", "250 mL".</summary>
public static class Amounts
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.Ordinal) { "mL", "mg", "g", "kcal", "IU", "mcg", "L", "fl oz", "oz", "lb", "kg" };

    public static string Format(double amount, string unit)
    {
        var whole = Math.Floor(amount);
        var fraction = amount - whole;
        var number = Math.Abs(fraction - 0.5) < 0.001 ? (whole > 0 ? whole.ToString(CultureInfo.CurrentCulture) : "") + "½"
            : Math.Abs(fraction - 0.25) < 0.001 ? (whole > 0 ? whole.ToString(CultureInfo.CurrentCulture) : "") + "¼"
            : Math.Abs(fraction - 0.75) < 0.001 ? (whole > 0 ? whole.ToString(CultureInfo.CurrentCulture) : "") + "¾"
            : amount.ToString("0.##", CultureInfo.CurrentCulture);
        return unit.Length == 0 ? number : $"{number} {(amount > 1 ? Plural(unit) : unit)}";
    }

    public static string Plural(string unit) =>
        Abbreviations.Contains(unit) || unit.EndsWith('s') ? unit
        : unit.EndsWith("ch", StringComparison.Ordinal) || unit.EndsWith("sh", StringComparison.Ordinal) || unit.EndsWith('o') || unit.EndsWith('x') ? unit + "es"
        : unit.Length > 1 && unit[^1] == 'y' && !"aeiou".Contains(unit[^2]) ? unit[..^1] + "ies"
        : unit + "s";
}

/// <summary>Days of the week, as bits: Sunday is 1, Saturday 64.</summary>
[Flags]
public enum DaySet
{
    None = 0,
    Sunday = 1, Monday = 2, Tuesday = 4, Wednesday = 8, Thursday = 16, Friday = 32, Saturday = 64,
    Every = 127,
}

public static class DaySetExtensions
{
    public static bool Contains(this DaySet days, DayOfWeek day) => (days & (DaySet)(1 << (int)day)) != 0;

    public static DaySet With(this DaySet days, DayOfWeek day, bool on) =>
        on ? days | (DaySet)(1 << (int)day) : days & ~(DaySet)(1 << (int)day);

    /// <summary>"every day", "weekdays", "Mon, Wed, Fri".</summary>
    public static string Describe(this DaySet days) => days switch
    {
        DaySet.Every => "every day",
        DaySet.None => "never",
        DaySet.Monday | DaySet.Tuesday | DaySet.Wednesday | DaySet.Thursday | DaySet.Friday => "weekdays",
        DaySet.Saturday | DaySet.Sunday => "weekends",
        _ => string.Join(", ", Enum.GetValues<DayOfWeek>().Where(d => days.Contains(d)).Select(d => d.ToString()[..3])),
    };
}

public enum DoseStatus { Taken, Skipped }

/// <summary>One scheduled ring of a reminder: the reminder, the moment it was due and the amount then.</summary>
public sealed record Dose(Reminder Reminder, DateTime Due)
{
    public string Key => $"{Reminder.Id}@{Due:yyyy-MM-ddTHH:mm}";
    public double Amount => Reminder.AmountAt(TimeOnly.FromDateTime(Due));
}

/// <summary>What was done about a dose: taken or skipped, when, the reading or injection site, and the video of it
/// (a file in the data directory's videos/) with the SHA-256 it had when it was made.</summary>
public sealed record DoseRecord(DoseStatus Status, DateTime At, double Amount, string Value, string Site, string Video = "", string VideoSha256 = "");

/// <summary>A line of the history.</summary>
public sealed record HistoryEntry(long ReminderId, string Name, ReminderKind Kind, string Unit, DateTime Due, DoseRecord Record);

/// <summary>How hard the reminder window insists until it's answered.</summary>
public enum NagLevel
{
    /// <summary>It comes up once, in the middle of the screen.</summary>
    Gentle,
    /// <summary>Every 30 seconds it comes back to the middle of the screen, in front, and flashes in the taskbar.</summary>
    Persistent,
    /// <summary>Every 10 seconds, shaking, with the rest of the screen darkened; it can't be minimized.</summary>
    Insistent,
}

/// <summary>What closing the main window does.</summary>
public enum CloseAction
{
    /// <summary>It asks: keep running in the tray, or quit.</summary>
    Ask,
    /// <summary>The window goes; the reminders keep running in the tray.</summary>
    Tray,
    /// <summary>The program ends: nothing rings until it's started again.</summary>
    Quit,
}

/// <summary>The groups of reminders that each ring with their own sound, and look their own way.</summary>
public enum AlertGroup { Medication, Check, EatDrink }
