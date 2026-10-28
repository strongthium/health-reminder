using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>One card of the Reminders page.</summary>
public sealed class ReminderItem(Reminder reminder, string imageDirectory, Supply? supply = null)
{
    public Reminder Reminder { get; } = reminder;
    public ReminderKind Kind => Reminder.Kind;
    public string Name => Reminder.Name;
    public string KindText => Kind.ShortLabel();
    public string Form => Reminder.Form;
    public bool Planned => Reminder.Planned;
    public bool RecordVideo => Reminder.RecordVideo;
    public bool Paused => !Reminder.Active;
    public PillAppearance Appearance => Reminder.Appearance;
    public double Opacity => Reminder.Active ? 1 : 0.6;
    public ImageSource? Photo { get; } = Photos.Load(imageDirectory, reminder.Photo);
    public bool HasPhoto => Photo is not null;
    public string Look => Kind == ReminderKind.Pill && Appearance.IsKnown ? Appearance.Describe(Form) : "";

    /// <summary>The numbers it's known by: "DIN 02237618 · Rx 4471182".</summary>
    /// <summary>How many are left by the count, and for how many days; "" when it isn't counted.</summary>
    public string SupplyText => supply is null ? ""
        : (supply.OnHand <= 0 ? "None left by the count" : $"{Amounts.Format(Math.Round(supply.OnHand, 2), Reminder.Unit)} left")
          + (supply.DaysLeft is { } days && supply.OnHand > 0 ? $", about {days} day{(days == 1 ? "" : "s")}" : "") + (supply.Low ? ": time to refill" : "");
    public bool SupplyLow => supply is { Low: true };

    public string Ids => string.Join(" · ", new[]
    {
        Reminder.Din.Length > 0 ? "DIN " + Reminder.Din : "",
        Reminder.Din.Length == 0 && Reminder.Ndc.Length > 0 ? "NDC " + Reminder.Ndc : "",
        Reminder.RxNumber.Length > 0 ? "Rx " + Reminder.RxNumber : "",
    }.Where(s => s.Length > 0));

    public string ScheduleText
    {
        get
        {
            if (!Reminder.Active) return "Paused: it doesn't ring";
            if (Reminder.Times.Count == 0) return "No times set";
            var amounts = Kind.HasAmount() && Reminder.Unit.Length > 0;
            // Many times with the same amount (a day of drinks) are summed up rather than listed.
            if (Reminder.Times.Count > 6)
                return $"{Reminder.Times.Count} times from {Format.Time(Reminder.Times[0].Time)} to {Format.Time(Reminder.Times[^1].Time)}"
                    + (amounts ? ", " + Format.Amount(Reminder.Times.Sum(t => t.Amount), Reminder.Unit, Kind) + " a day" : "") + " · " + Reminder.Days.Describe();
            return string.Join(", ", Reminder.Times.Select(t => Format.Time(t.Time) + (amounts ? " (" + Format.Amount(t.Amount, Reminder.Unit, Kind) + ")" : "")))
                + " · " + Reminder.Days.Describe();
        }
    }
}

/// <summary>The reminders of one group: medication, checks, or meals and drinks.</summary>
public sealed class ReminderSection(AlertGroup group, List<ReminderItem> items)
{
    public AlertGroup Group => group;
    public string Title => group.Label();
    public string Count => items.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    public List<ReminderItem> Items => items;
}

/// <summary>Every reminder, by group, each with its picture, its times and what it looks like.</summary>
public sealed partial class RemindersPage : UserControl, IPage
{
    public RemindersPage() => InitializeComponent();

    public void Refresh()
    {
        var app = App.Current;
        var reminders = app.Reminders;
        Sections.ItemsSource = reminders
            .GroupBy(r => r.Kind.Group())
            .OrderBy(g => g.Key)
            .Select(g => new ReminderSection(g.Key, g
                .OrderBy(r => !r.Active).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(r => new ReminderItem(r, app.Database.ImageDirectory, r.Kind.IsMedication() ? app.Database.Supply(r) : null)).ToList()))
            .ToList();
        NoReminders.Show(reminders.Count == 0);
        var active = reminders.Count(r => r.Active);
        CountText.Text = reminders.Count == 0 ? "What to remind you of, and when"
            : active == reminders.Count ? $"{reminders.Count} in all" : $"{reminders.Count} in all, {reminders.Count - active} paused";
    }

    private void Add_Click(object sender, RoutedEventArgs e) => this.MainWindowOf().Edit(null);

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ReminderItem item) this.MainWindowOf().Edit(item.Reminder);
    }
}
