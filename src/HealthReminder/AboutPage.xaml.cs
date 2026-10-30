using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Strongthium, this program, and whether there's a newer version of it. GitHub is asked when the page is first shown
/// (and again if it's shown an hour later), and at no other time; nothing is installed until Update is pressed.
/// </summary>
public sealed partial class AboutPage : UserControl
{
    private static readonly Updater Updater = new(Updater.NewClient("health-reminder"), "strongthium/health-reminder",
        version => $"health-reminder-{version}-win-x64-needs-dotnet.zip", "health-reminder.exe");

    private static readonly TimeSpan AskAgainAfter = TimeSpan.FromHours(1);
    private const string Info = "\uE946", Working = "\uE895", Problem = "\uE783", Newer = "\uE896", UpToDate = "\uE73E";

    private Release? release;
    private DateTime? asked;
    private bool busy;

    public AboutPage()
    {
        InitializeComponent();
        ProgramIcon.Source = App.Icon;
        VersionText.Text = $"Version {Updater.RunningVersion.ToString(3)}";
        Loaded += async (_, _) =>
        {
            if (!busy && (asked is null || DateTime.UtcNow - asked > AskAgainAfter)) await CheckAsync();
        };
    }

    private async Task CheckAsync()
    {
        busy = true;
        release = null;
        Show(Working, "Asking GitHub for the latest version…", "");
        UpdateBar.IsIndeterminate = true;
        UpdateBar.Visibility = Visibility.Visible;
        CheckButton.IsEnabled = false;
        NotesButton.Visibility = UpdateButton.Visibility = Visibility.Collapsed;
        try
        {
            release = await Updater.LatestAsync();
            asked = DateTime.UtcNow;
            ShowAnswer();
        }
        catch (Exception exception) when (Updater.IsExpected(exception))
        {
            Show(Problem, "Couldn't ask GitHub for the latest version", Updater.Explain(exception, ""));
        }
        finally
        {
            busy = false;
            UpdateBar.Visibility = Visibility.Collapsed;
            CheckButton.IsEnabled = true;
        }
    }

    private void ShowAnswer()
    {
        if (release is null) return;
        var latest = release.Version.ToString(3);
        NotesButton.Visibility = Visibility.Visible;
        if (release.Version > Updater.RunningVersion)
        {
            Show(Newer, $"Version {latest} is out. You have {Updater.RunningVersion.ToString(3)}.",
                "Updating downloads it from GitHub, checks it against the release's checksums, replaces this program and starts it again.");
            UpdateButton.Content = $"Update to {latest} and restart";
            UpdateButton.Visibility = Visibility.Visible;
        }
        else if (release.Version == Updater.RunningVersion) Show(UpToDate, "You have the latest version.", "");
        else Show(UpToDate, $"This is newer than the latest release, {latest}.", "");
    }

    private void Show(string icon, string title, string detail)
    {
        UpdateIcon.Text = icon;
        UpdateIcon.Foreground = icon is Newer or UpToDate ? (Brush)FindResource("AccentTextFillColorPrimaryBrush") : (Brush)FindResource("TextFillColorSecondaryBrush");
        UpdateTitle.Text = title;
        UpdateDetail.Text = detail;
        UpdateDetail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (release is null || Environment.ProcessPath is not { } running) return;
        var latest = release.Version.ToString(3);
        busy = true;
        UpdateButton.Visibility = CheckButton.Visibility = Visibility.Collapsed;
        UpdateBar.IsIndeterminate = false;
        UpdateBar.Value = 0;
        UpdateBar.Visibility = Visibility.Visible;
        Show(Newer, $"Downloading version {latest}…", "");
        try
        {
            await Updater.InstallAsync(release, running, new Progress<double>(share => UpdateBar.Value = share * 100));
            Show(Working, $"Starting version {latest}…", "");
            Restart(running);
        }
        catch (Exception exception) when (Updater.IsExpected(exception) || exception is System.ComponentModel.Win32Exception)
        {
            busy = false;
            UpdateBar.Visibility = Visibility.Collapsed;
            CheckButton.Visibility = UpdateButton.Visibility = Visibility.Visible;
            Show(Problem, "The update wasn't installed", Updater.Explain(exception, running) + " You can download it from the release's page instead: What's new.");
        }
    }

    /// <summary>Starts the new program, which waits for this one to quit. If it won't start, the old one goes back.</summary>
    private static void Restart(string running)
    {
        var start = new ProcessStartInfo(running) { UseShellExecute = false };
        start.ArgumentList.Add("--wait-for");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        if (Program.OwnDataDirectory)
        {
            start.ArgumentList.Add("--data");
            start.ArgumentList.Add(Program.DataDirectory);
        }
        try
        {
            Process.Start(start)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Updater.Undo(running);
            throw;
        }
        App.Current.Quit();
    }

    private async void Check_Click(object sender, RoutedEventArgs e) => await CheckAsync();

    private void Notes_Click(object sender, RoutedEventArgs e) => Open(release?.Page ?? Updater.ReleasesPage);

    private void Banner_Click(object sender, RoutedEventArgs e) => Open((string)((Button)sender).Tag);

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
}
