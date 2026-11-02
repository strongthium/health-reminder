using System.Windows;

namespace Strongthium.HealthReminder;

/// <summary>Asked when tracking is stopped: whether medication still rings, and whether to reset the stats.</summary>
public sealed partial class StopTrackingWindow : Window
{
    /// <summary>What a reset leaves as it is.</summary>
    public const string Stays = "Your kitchen, groceries, reminders and settings stay as they are, and so does what's on the History page. "
        + "Nothing is deleted: Settings can count everything again.";

    public StopTrackingWindow(bool hasMedication)
    {
        InitializeComponent();
        Icon = App.Icon;
        MedicationBox.Show(hasMedication);
        MedicationNote.Show(hasMedication);
        StaysText.Text = "The stats and what's been missed count from now on. " + Stays;
    }

    public bool KeepMedication => MedicationBox.IsChecked == true;

    public bool Reset => ResetBox.IsChecked == true;

    private void Stop_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
