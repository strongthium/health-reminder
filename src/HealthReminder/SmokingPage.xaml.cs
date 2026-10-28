using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>A cigarette in the log: when, and how long it took if it was timed.</summary>
public sealed class SmokeRow(Smoke smoke)
{
    public Smoke Smoke => smoke;
    public string When => (smoke.Started.Date == DateTime.Today ? "Today" : smoke.Started.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : smoke.Started.ToString("ddd MMM d", CultureInfo.CurrentCulture))
        + " " + Format.Time(smoke.Started);
    public string Detail => smoke.Finished is null ? "lit, not yet out"
        : smoke.Duration is { } d ? $"{SmokingPage.Minutes(d)}"
        : smoke.Finished == smoke.Started ? "" : "not timed";
}

/// <summary>A step of recalibrating the body: started, due, or to come.</summary>
public sealed class StepRow(RecalibrationStep step, int number, DateOnly? started, DateOnly? due, bool lastStarted, DateOnly today)
{
    private static readonly SolidColorBrush Green = Frozen(0x2E, 0x7D, 0x32), Amber = Frozen(0x8A, 0x5A, 0x00), Slate = Frozen(0x5F, 0x6B, 0x76),
        Teal = Frozen(0x00, 0x79, 0x6B), Blue = Frozen(0x15, 0x65, 0xC0);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public RecalibrationStep Step => step;
    public string Number => $"{number}.";
    public string EvidenceText => step.Evidence switch { EvidenceLevel.Strong => "Well studied", EvidenceLevel.Some => "Some evidence", _ => "Early evidence" };
    public Brush EvidenceBrush => step.Evidence switch { EvidenceLevel.Strong => Green, EvidenceLevel.Some => Amber, _ => Slate };
    public bool IsDue => started is null && due is { } d && d <= today;
    public bool CanUndo => lastStarted;
    public string StateText => started is { } s ? $"Started {s:MMM d}" : due is { } d ? (d <= today ? "This week" : $"From {d:MMM d}") : "";
    public Brush StateBrush => started is not null ? Teal : Blue;
    public string ButtonText => IsDue ? "Start it" : CanUndo ? "Undo" : "";
    public double Opacity => started is null && due is null ? 0.7 : 1;
}

/// <summary>
/// Smoking: logging cigarettes (with the on-top button), the numbers from the log, the brand and what's in its smoke,
/// and cutting down with nicotine replacement matched to how much is smoked.
/// </summary>
public sealed partial class SmokingPage : UserControl, IPage
{
    private bool loading;

    public SmokingPage()
    {
        InitializeComponent();
        loading = true;
        var brand = SmokeLog.Brand;
        BrandBox.Text = brand.Name;
        PerPackBox.Value = brand.PerPack;
        PriceBox.Text = brand.PackPrice?.ToString("0.00", CultureInfo.CurrentCulture) ?? "";
        TarBox.Value = brand.TarMg;
        NicotineBox.Value = brand.NicotineMg;
        CoBox.Value = brand.CoMg;
        var goal = SmokeLog.Goal;
        GoalBox.IsChecked = goal is not null;
        FewerBox.SelectedIndex = Math.Max(0, Array.IndexOf([1, 2, 3, 5], goal?.Fewer ?? 1));
        loading = false;
    }

    private static App App => App.Current;

    /// <summary>"5 min 40 s".</summary>
    public static string Minutes(TimeSpan time) => time.TotalMinutes >= 1 ? $"{(int)time.TotalMinutes} min {time.Seconds} s" : $"{time.Seconds} s";

    private static string Since(TimeSpan time) =>
        time.TotalDays >= 2 ? $"{(int)time.TotalDays} days" : time.TotalHours >= 1 ? $"{(int)time.TotalHours} h {time.Minutes} min" : $"{(int)time.TotalMinutes} min";

    public void Refresh()
    {
        loading = true;
        ButtonBox.IsChecked = App.SmokeButtonShown;
        TimedBox.IsChecked = App.SmokeTimed;
        loading = false;

        var now = DateTime.Now;
        var smokes = App.Database.Smokes(now.Date.AddDays(-28));
        var stats = Smoking.Stats(smokes, now);
        var open = SmokeLog.Open();
        LogButton.Content = open is not null ? "It's out" : App.SmokeTimed ? "Lighting one now" : "Log one now";

        var target = SmokeLog.TargetToday();
        TodayText.Text = stats.Today.ToString(CultureInfo.CurrentCulture);
        TodayLabel.Text = target is { } t ? $"today, of a goal of {t}" : "today";
        PerDayText.Text = stats.PerDay.ToString("0.#", CultureInfo.CurrentCulture);
        SinceText.Text = stats.SinceLast is { } since ? Since(since) : "–";
        var brand = SmokeLog.Brand;
        CostTile.Show(brand.PackPrice is not null && brand.PerPack > 0);
        if (brand.PackPrice is { } price && brand.PerPack > 0)
            CostText.Text = ((decimal)(stats.PerDay * 7) * price / brand.PerPack).ToString("C0", CultureInfo.CurrentCulture);

        DurationText.Text = stats.Duration is { } duration
            ? $"{Minutes(duration)} on average over the last two weeks" + (stats.DurationBefore is { } before
                ? $"; {Minutes(before)} the two weeks before ({(duration < before ? "quicker now" : duration > before ? "slower now" : "the same")})."
                : ".")
            : App.SmokeTimed ? "Not enough timed yet: click the button when you light one and again when it's out."
            : "Turn on Time each one, above, to see this.";

        var wake = WakeTime();
        FirstText.Text = stats.FirstOfDay is { } first
            ? $"Around {Format.Time(first)} on average." + (wake is { } up && first > up
                ? first - up <= TimeSpan.FromMinutes(30)
                    ? $" That's within half an hour of getting up ({Format.Time(up)}), a sign of strong dependence: the body wants nicotine back by morning."
                    : $" That's {Since(first - up)} after getting up ({Format.Time(up)}); the longer the wait, the less dependent."
                : "")
            : "Not enough logged yet.";

        AirText.Text = stats.Last is { } last
            ? AirAfter(smokes.Last(), last)
            : "Nothing logged yet.";

        ConstituentsNote.Text = (brand.TarMg ?? brand.NicotineMg ?? brand.CoMg) is not null
            ? $"Your pack says {Measured(brand)} a cigarette, measured by the older, gentler ISO method; how you smoke changes what you take in. No public database lists every chemical by brand, so the rest are typical amounts across brands, by Health Canada's intense method."
            : "No public database lists every chemical by brand, so these are typical amounts across brands, as measured by Health Canada's intense machine-smoking method. What you take in depends on how you smoke. If your pack prints tar, nicotine and carbon monoxide, put them in above.";
        ConstituentList.ItemsSource = Smoking.Constituents;

        var firstAfterWaking = stats.FirstOfDay is { } f && wake is { } w && f > w ? f - w : (TimeSpan?)null;
        MatchText.Text = stats.PerDay > 0
            ? $"Matched to about {stats.PerDay:0.#} a day" + (firstAfterWaking is { } fw ? $", the first {Since(fw)} after getting up:" : ":")
            : "Log your cigarettes for a few days to match these to how much you smoke. To start with:";
        ReplacementList.ItemsSource = Smoking.Replacement(stats.PerDay, firstAfterWaking);

        var goal = SmokeLog.Goal;
        GoalText.Text = goal is { } g
            ? $"Started {g.Start:MMM d} at about {g.From:0} a day. This week, no more than {target} a day"
              + (target > 0 ? $"; none from about {g.Start.AddDays(7 * (int)Math.Ceiling(g.From / g.Fewer)):MMM d}." : ". You've reached none: keep it there.")
            : "Pick how fast, then tick it: it starts from how much you smoke now, and the on-top button shows the day's count against it.";

        ShowSteps();

        var recent =smokes.Where(s => s.Started >= now.Date.AddDays(-1)).OrderByDescending(s => s.Started).Select(s => new SmokeRow(s)).ToList();
        LogList.ItemsSource = recent;
        NoLog.Show(recent.Count == 0);
        WeekText.Text = "Each day this week: " + string.Join(" · ", Enumerable.Range(0, 7).Select(back => now.Date.AddDays(-back))
            .Select(day => $"{(day == now.Date ? "today" : day.ToString("ddd", CultureInfo.CurrentCulture))} {smokes.Count(s => s.Started.Date == day)}"));
    }

    /// <summary>What the smoke does, and the steps against it: those started, the one due, and the rest.</summary>
    private void ShowSteps()
    {
        EffectList.ItemsSource ??= Recalibration.Effects;
        CautionText.Text = Recalibration.Caution;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var started = SmokeLog.StepsStarted;
        var next = Recalibration.Next(started, today);
        var last = started.Count > 0 ? started.MaxBy(s => s.Value).Key : null;
        StepList.ItemsSource = Recalibration.Steps
            .Select((step, i) => new StepRow(step, i + 1, started.TryGetValue(step.Id, out var day) ? day : null,
                next is { } n && n.Step == step ? n.From : null, step.Id == last, today))
            .ToList();
        StepsIntro.Text = $"One at a time, a week apart, so each has time to settle and you can tell what helps; keep each one going as you add the next. "
            + (started.Count == 0 ? "Start with the first this week."
               : next is { } upcoming ? $"{started.Count} of {Recalibration.Steps.Count} started; the next {(upcoming.From <= today ? "is due now" : $"from {upcoming.From:MMM d}")}."
               : "All of them started: keep them going.");
    }

    private void Step_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StepRow row) return;
        var started = SmokeLog.StepsStarted;
        if (row.IsDue) started[row.Step.Id] = DateOnly.FromDateTime(DateTime.Today);
        else if (row.CanUndo) started.Remove(row.Step.Id);
        SmokeLog.StepsStarted = started;
        ShowSteps();
    }

    private static string Measured(SmokeBrand brand) =>
        string.Join(", ", new[] { (brand.TarMg, "tar"), (brand.NicotineMg, "nicotine"), (brand.CoMg, "carbon monoxide") }
            .Where(m => m.Item1 is not null).Select(m => $"{m.Item1:0.#} mg {m.Item2}"));

    /// <summary>When the smoke clears from a room, from when the last one was put out (or lit, if not timed).</summary>
    private static string AirAfter(Smoke smoke, DateTime lit)
    {
        var from = smoke.Finished ?? lit;
        var (open, closed) = Smoking.AirClears;
        return $"If it was smoked indoors, smoke stays in the room's air for roughly {open.TotalHours:0} hours with a window open (until about {Format.Time(from + open)}), "
            + $"and {closed.TotalHours:0} or more with it closed (until {Format.Time(from + closed)} or later). The smell stays on clothes, hair and fabric until they're washed, "
            + "and smoke left on surfaces lingers for weeks.";
    }

    /// <summary>When the user gets up, from the Eat &amp; drink page, if it's been filled in.</summary>
    private static TimeOnly? WakeTime()
    {
        var profile = App.Database.Setting("profile") ?? "";
        var wake = profile.Split(';').Select(p => p.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == "wake");
        return wake is not null && TimeOnly.TryParse(wake[1], CultureInfo.InvariantCulture, out var time) ? time : null;
    }

    private void Log_Click(object sender, RoutedEventArgs e) => SmokeLog.Click();

    private void ButtonBox_Click(object sender, RoutedEventArgs e)
    {
        if (!loading) App.SmokeButtonShown = ButtonBox.IsChecked == true;
    }

    private void TimedBox_Click(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        App.SmokeTimed = TimedBox.IsChecked == true;
        App.SmokeChanged();
    }

    private void SaveBrand_Click(object sender, RoutedEventArgs e)
    {
        decimal? price = null;
        var text = PriceBox.Text.Trim();
        if (text.Length > 0)
        {
            if (!decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out var p)
                && !decimal.TryParse(text.TrimStart('$', '€', '£'), NumberStyles.Number, CultureInfo.InvariantCulture, out p))
            {
                BrandSaved.Text = "The price isn't a number.";
                return;
            }
            price = p;
        }
        SmokeLog.Brand = new SmokeBrand(BrandBox.Text.Trim(), (int)(PerPackBox.Value ?? 20), price, TarBox.Value, NicotineBox.Value, CoBox.Value);
        BrandSaved.Text = "Saved.";
        Refresh();
    }

    private void Goal_Changed(object sender, RoutedEventArgs e)
    {
        if (loading || GoalBox is null || FewerBox is null) return;
        int[] fewer = [1, 2, 3, 5];
        if (GoalBox.IsChecked != true)
        {
            SmokeLog.Goal = null;
        }
        else
        {
            // Starting from how much is smoked now; changing the pace keeps the start.
            var now = DateTime.Now;
            var current = SmokeLog.Goal;
            var from = current?.From ?? Math.Max(1, Smoking.Stats(App.Database.Smokes(now.Date.AddDays(-28)), now).PerDay);
            SmokeLog.Goal = (from, current?.Start ?? DateOnly.FromDateTime(now), fewer[Math.Max(0, FewerBox.SelectedIndex)]);
        }
        App.SmokeChanged();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SmokeRow row) return;
        App.Database.Delete(row.Smoke);
        App.SmokeChanged();
    }
}
