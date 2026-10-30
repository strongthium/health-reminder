using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>A page of the main window, shown again when the reminders change.</summary>
public interface IPage
{
    void Refresh();
}

internal static class Ui
{
    public static void Show(this UIElement element, bool visible) => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The main window a page is on.</summary>
    public static MainWindow MainWindowOf(this DependencyObject element) => (MainWindow)Window.GetWindow(element);
}

/// <summary>
/// The reminders and the day: Today, Reminders, Eat &amp; drink, Menu, Groceries, Exercise, Smoking, Stats, History, Settings and About, chosen down the side. Closing it
/// hides it; the reminders keep running in the tray.
/// </summary>
public sealed partial class MainWindow : Window
{
    public const int TodayPage = 0, RemindersPage = 1, EatDrinkPage = 2, MenuPage = 3, GroceriesPage = 4, ExercisePage = 5, SmokingPage = 6, StatsPage = 7, HistoryPage = 8, SettingsPage = 9, AboutPage = 10;

    private readonly RadioButton[] nav;
    private readonly Func<FrameworkElement>[] makers =
    [
        () => new TodayPage(), () => new RemindersPage(), () => new EatDrinkView(), () => new MenuPage(), () => new GroceriesPage(), () => new ExercisePage(), () => new SmokingPage(), () => new StatsPage(), () => new HistoryPage(), () => new SettingsPage(), () => new AboutPage(),
    ];
    private readonly FrameworkElement?[] pages = new FrameworkElement?[11];
    private bool allowClose;

    public MainWindow()
    {
        InitializeComponent();
        Icon = App.Icon;
        Logo.Source = App.Icon;
        nav = [TodayNav, RemindersNav, EatDrinkNav, MenuNav, GroceriesNav, ExerciseNav, SmokingNav, StatsNav, HistoryNav, SettingsNav, AboutNav];
        Navigate(TodayPage);
    }

    public void AllowClose() => allowClose = true;

    /// <summary>Shows a page, made the first time.</summary>
    public void Navigate(int index)
    {
        var page = pages[index] ??= makers[index]();
        if (nav[index].IsChecked != true) nav[index].IsChecked = true;
        if (Page.Content != page)
        {
            Page.Content = page;
            (page as IPage)?.Refresh();
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (nav is not null) Navigate(Array.IndexOf(nav, sender));
    }

    /// <summary>Shows the reminders as they are now, on the page that's showing; the others are shown afresh when
    /// they're next picked.</summary>
    public void Refresh() => (Page.Content as IPage)?.Refresh();

    /// <summary>Adds a reminder (null) or changes one.</summary>
    public void Edit(Reminder? reminder)
    {
        var editor = new ReminderWindow(reminder) { Owner = this };
        if (editor.ShowDialog() == true) App.Current.Changed();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // The X hides the window, so that the reminders keep running in the tray, or quits, as Settings says; until
        // it's told, it asks.
        if (!allowClose)
        {
            e.Cancel = true;
            var app = App.Current;
            var action = app.CloseAction;
            if (action == CloseAction.Ask)
            {
                var dialog = new CloseDialog { Owner = this };
                if (dialog.ShowDialog() != true) return;
                action = dialog.Choice;
                if (dialog.Remember) app.CloseAction = action;
            }
            if (action == CloseAction.Quit) Dispatcher.BeginInvoke(app.Quit);
            else Hide();
        }
        base.OnClosing(e);
    }
}
