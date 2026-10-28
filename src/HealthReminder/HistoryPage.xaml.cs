using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>A day's heading in the History list.</summary>
public sealed record HistoryDay(string Text);

/// <summary>One line of the History list.</summary>
public sealed class HistoryItem(HistoryEntry entry, Reminder? reminder)
{
    public HistoryEntry Entry => entry;
    public bool HasVideo => entry.Record.Video.Length > 0;
    public bool Taken => entry.Record.Status == DoseStatus.Taken;
    public ReminderKind Kind => entry.Kind;
    public PillAppearance? Appearance => reminder?.Appearance;
    public string Form => reminder?.Form ?? "";
    public string Name => entry.Name;
    public string Time => Format.Time(entry.Due);
    public string When => entry.Due.ToString("ddd d MMM", CultureInfo.CurrentCulture) + " " + Format.Time(entry.Due);

    public string Result
    {
        get
        {
            var record = entry.Record;
            if (record.Status == DoseStatus.Skipped) return "Skipped";
            var parts = new List<string>();
            if (record.Value.Length > 0) parts.Add(Format.Reading(record.Value, reminder?.ValueUnit ?? ""));
            else if (entry.Kind.HasAmount() && entry.Unit.Length > 0 && record.Amount > 0) parts.Add(Format.Amount(record.Amount, entry.Unit, entry.Kind));
            else parts.Add("Done");
            if (record.Site.Length > 0) parts.Add(record.Site);
            if (Math.Abs((record.At - entry.Due).TotalMinutes) >= 30) parts.Add("at " + Format.Time(record.At));
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>What was done, skipped and read, day by day, with the videos.</summary>
public sealed partial class HistoryPage : UserControl, IPage
{
    private static readonly string[] Filters = ["Everything", "Medication", "Checks and readings", "Meals and drinks"];
    private static readonly int[] Days = [7, 30, 90, 365];

    public HistoryPage()
    {
        InitializeComponent();
        Filter.ItemsSource = Filters;
        Filter.SelectedIndex = 0;
        Range.ItemsSource = Days.Select(d => d == 365 ? "Last year" : $"Last {d} days").ToList();
        Range.SelectedIndex = 0;
    }

    public void Refresh()
    {
        if (Filter.SelectedIndex < 0 || Range.SelectedIndex < 0) return;
        var app = App.Current;
        var reminders = app.Reminders.ToDictionary(r => r.Id);
        Func<ReminderKind, bool> keep = Filter.SelectedIndex switch
        {
            1 => k => k.Group() == AlertGroup.Medication,
            2 => k => k.Group() == AlertGroup.Check,
            3 => k => k.Group() == AlertGroup.EatDrink,
            _ => _ => true,
        };
        var entries = app.Database.History(DateTime.Today.AddDays(-Days[Range.SelectedIndex] + 1))
            .Where(e => keep(e.Kind))
            .ToList();
        var today = DateTime.Today;
        List<object> lines = [];
        foreach (var day in entries.GroupBy(e => e.Due.Date))
        {
            lines.Add(new HistoryDay(day.Key == today ? "Today" : day.Key == today.AddDays(-1) ? "Yesterday" : day.Key.ToString("D", CultureInfo.CurrentCulture)));
            lines.AddRange(day.Select(e => new HistoryItem(e, reminders.GetValueOrDefault(e.ReminderId))));
        }
        List.ItemsSource = lines;
        NoHistory.Show(entries.Count == 0);
        var taken = entries.Count(e => e.Record.Status == DoseStatus.Taken);
        CountText.Text = entries.Count == 0 ? "What was done, skipped and read" : $"{taken} done, {entries.Count - taken} skipped";
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) Refresh();
    }

    /// <summary>Plays a video, after checking that it is the file that was recorded: the same SHA-256.</summary>
    private async void Watch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HistoryItem item) return;
        var record = item.Entry.Record;
        var path = Path.Combine(App.Current.Database.Directory, record.Video);
        VideoCheck.Show(true);
        if (!File.Exists(path))
        {
            VideoCheckText.Text = $"The video of {item.Name} at {item.When} is no longer there ({record.Video}).";
            return;
        }
        var sha256 = await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        });
        VideoCheckText.Text = sha256 == record.VideoSha256
            ? $"The video of {item.Name} at {item.When} is unchanged since it was recorded (SHA-256 {sha256[..16]}…)."
            : $"The video of {item.Name} at {item.When} has been changed since it was recorded: its SHA-256 is {sha256[..16]}…, it was {record.VideoSha256[..Math.Min(16, record.VideoSha256.Length)]}….";
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
