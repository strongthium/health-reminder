using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>A note of what stands out, with its colour and mark: a warning, a heads-up or good news.</summary>
public sealed class NoteRow(StatNote note)
{
    public string Text => note.Text;
    public string Label => note.Tone switch { NoteTone.Warning => "Worth a look", NoteTone.Heads => "Heads-up", _ => "Good" };
    public string Icon => note.Tone switch { NoteTone.Warning => "", NoteTone.Heads => "", _ => "" };
    public Brush Brush => note.Tone switch { NoteTone.Warning => StatsReport.MissedBrush, NoteTone.Heads => StatsReport.HeadsBrush, _ => StatsReport.DoneBrush };
}

/// <summary>
/// The days in numbers and bars (<see cref="StatsReport"/>), with a word on what stands out at the top; and printed.
/// </summary>
public sealed partial class StatsPage : UserControl, IPage
{
    private bool loading = true;

    public StatsPage()
    {
        InitializeComponent();
        RangeBox.ItemsSource = StatsReport.Ranges.Select(d => $"Last {d} days").ToList();
        RangeBox.SelectedIndex = Math.Max(0, Array.IndexOf(StatsReport.Ranges, SavedDays));
        loading = false;
    }

    /// <summary>How many days are shown: 7 until it's changed.</summary>
    public static int SavedDays => int.TryParse(App.Current.Database.Setting("stats_days"), CultureInfo.InvariantCulture, out var d) ? d : 7;

    private int Days => StatsReport.Ranges[Math.Max(0, RangeBox.SelectedIndex)];

    private void Range_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        App.Current.Database.SetSetting("stats_days", Days.ToString(CultureInfo.InvariantCulture));
        Refresh();
    }

    public void Refresh()
    {
        var report = StatsReport.For(Days, DateTime.Now);
        CountingText.Text = report.Counting;
        CountingText.Show(report.Counting.Length > 0);
        NoteList.ItemsSource = report.Notes.Select(n => new NoteRow(n)).ToList();

        Tile(DoneText, DoneLabel, report.Done);
        Tile(MissedText, MissedLabel, report.Missed);
        Tile(SmokesText, null, report.Smokes);
        Tile(GapText, null, report.Gap);
        Tile(DrinksText, DrinksLabel, report.Drinks);
        Tile(KcalText, null, report.Kcal);

        Chart(DoneSummary, DoneChart, report.DoneChart);
        Chart(SmokeSummary, SmokeChart, report.SmokeChart);
        Chart(GapSummary, GapChart, report.GapChart);
        Chart(DrinkSummary, DrinkChart, report.DrinkChart);
        Chart(KcalSummary, KcalChart, report.KcalChart);
    }

    private static void Tile(TextBlock value, TextBlock? label, StatsTile tile)
    {
        value.Text = tile.Value;
        if (label is not null) label.Text = tile.Label;
    }

    private static void Chart(TextBlock summary, DayBars bars, StatsChart chart)
    {
        summary.Text = chart.Summary;
        bars.Show(chart.Bars, chart.Line, chart.Format);
    }

    private void Print_Click(object sender, RoutedEventArgs e) => Printing.Stats(Window.GetWindow(this), Days);
}
