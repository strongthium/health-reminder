using System.Windows;

namespace Strongthium.HealthReminder;

/// <summary>Asks what closing the main window does: keep running in the tray, or quit.</summary>
public sealed partial class CloseDialog : Window
{
    public CloseDialog()
    {
        InitializeComponent();
        Icon = App.Icon;
    }

    public CloseAction Choice { get; private set; } = CloseAction.Tray;

    /// <summary>The choice is kept, and not asked again.</summary>
    public bool Remember => RememberBox.IsChecked == true;

    private void Tray_Click(object sender, RoutedEventArgs e) => Answer(CloseAction.Tray);

    private void Quit_Click(object sender, RoutedEventArgs e) => Answer(CloseAction.Quit);

    private void Answer(CloseAction choice)
    {
        Choice = choice;
        DialogResult = true;
    }
}
