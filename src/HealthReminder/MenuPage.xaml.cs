using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>A recipe that fits what someone feels like, and whether it uses up something that won't keep.</summary>
public sealed class MenuChoice(Recipe recipe, double kcal, string usesSoon)
{
    public Recipe Recipe => recipe;
    public string Detail => new RecipeChoice(recipe, kcal).Detail;
    public string UsesSoon => usesSoon;
}

/// <summary>A meal idea from Claude, from what's in the kitchen.</summary>
public sealed class IdeaRow(KitchenIdea idea)
{
    public KitchenIdea Idea => idea;
    public bool HasMissing => idea.Missing.Length > 0;
    public string Detail => $"About {idea.Kcal:N0} kcal · {idea.PrepMinutes + idea.CookMinutes} min"
        + (idea.Uses.Length > 0 ? " · uses " + string.Join(", ", idea.Uses) : "")
        + (idea.Missing.Length > 0 ? " · you'd need " + string.Join(", ", idea.Missing) : " · nothing to buy");
}

/// <summary>A line of the week's shopping: what, in whole packs, and what to do with the rest.</summary>
public sealed class ShoppingRow(ShoppingLine line)
{
    public ShoppingLine Line => line;
    public string Name => line.Info.Name;
    public string Buy => line.Info.Staple ? "on hand" : $"{line.Packs} × {PackLabel(line.Info)}";
    public string Note => line.Info.Staple ? line.Note : $"uses {GroceryCatalog.Amount(line.Needed, line.Info)}" + (line.Note.Length > 0 ? "; " + line.Note : "");

    /// <summary>"454 g", "1 loaf", "12 eggs".</summary>
    public static string PackLabel(GroceryInfo info) =>
        info.PackText.Any(char.IsLetter) ? info.PackText : $"{info.PackText} {(info.Pack == 1 ? info.Unit : Amounts.Plural(info.Unit))}";
}

/// <summary>A meal to choose for, in the "I feel like" list.</summary>
public sealed record MealOption(PlannedMeal Meal, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A choice in a cell of the week's menu.</summary>
public sealed record MenuOption(string Label, string? Value)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Menu page: picking a meal by what you feel like, the week's menu (a recipe, "decide on the day", or just the
/// meal's idea, for each weekday's meals), the shopping it needs in whole packs and what won't keep, what was bought
/// that's best used soon, and the kitchen alerts' settings.
/// </summary>
public sealed partial class MenuPage : UserControl, IPage
{
    private static readonly int[] Days = [0, 1, 2, 3, 4, 5, 6];
    private Feel? feel;
    private bool loading;
    private List<PantryItem> useSoon = [];

    public MenuPage() => InitializeComponent();

    private static App App => App.Current;
    private static Database Database => App.Current.Database;

    private PlannedMeal? ChosenMeal => (MealBox.SelectedItem as MealOption)?.Meal;

    public void Refresh()
    {
        loading = true;
        AlertsBox.IsChecked = KitchenLog.AlertsOn;
        (KitchenLog.Thaw == ThawMethod.ColdWater ? ThawWater : ThawFridge).IsChecked = true;
        StepsAtStartBox.IsChecked = KitchenLog.StepsAtStart;
        StepsOnTopBox.IsChecked = KitchenLog.StepsOnTop;

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        useSoon = Pantry.UseSoon(PantryLog.Items(), now);
        UseSoonList.ItemsSource = useSoon.Select(u => $"{u.Name} ({Pantry.Show(u, App.Measures)}): best used by {When(u.UseBy!.Value)}" + (GroceryCatalog.Get(u.Name) is { Freezes: true } && !u.InFreezer ? ", or frozen" : "")).ToList();
        UseSoonPanel.Show(useSoon.Count > 0);

        // The meals to choose for: what's still to come today, and tomorrow's.
        var selected = ChosenMeal;
        var upcoming = KitchenLog.Meals(today).Where(m => m.At > now.AddMinutes(-30)).Concat(KitchenLog.Meals(today.AddDays(1))).ToList();
        MealBox.ItemsSource = upcoming.Select(m => new MealOption(m,
            $"{m.Meal} {(m.At.Date == now.Date ? "today" : "tomorrow")}, {Format.Time(m.At)}" + (m.Recipe is { } r ? $": {r.Name}" : m.DecideOnTheDay ? ": to decide" : ""))).ToList();
        MealBox.SelectedIndex = selected is null ? (upcoming.Count > 0 ? 0 : -1) : Math.Max(0, upcoming.FindIndex(m => m.Meal == selected.Meal && m.At == selected.At));
        NoMeals.Show(upcoming.Count == 0 && !App.Reminders.Any(r => r.Kind == ReminderKind.Meal));
        loading = false;

        ShowFeels();
        ShowChoices();
        ShowWeek();
        ShowShopping();
        IdeasButton.IsEnabled = App.ClaudeOn && !asking;
        if (!asking && IdeaList.Items.Count == 0)
            IdeasText.Text = App.ClaudeOn
                ? "Claude suggests three meals for the meal above from what's in your kitchen (the Groceries page), using first what won't keep."
                : "To get ideas from what's in your kitchen, turn on Help from Claude in Settings.";
    }

    // ---- Ideas from the kitchen -------------------------------------------------------------------------------

    private bool asking;

    private async void Ideas_Click(object sender, RoutedEventArgs e)
    {
        if (App.Assistant is not { } assistant) return;
        var kitchen = Pantry.Kitchen(PantryLog.Items(), App.Measures);
        if (kitchen.Count == 0)
        {
            IdeasText.Text = "There's nothing in your kitchen yet: add groceries on the Groceries page (from photos, or as you buy them).";
            return;
        }
        var meal = ChosenMeal;
        asking = true;
        IdeasButton.IsEnabled = false;
        IdeaList.ItemsSource = null;
        IdeasText.Text = "Asking Claude…";
        try
        {
            var ideas = await assistant.IdeasAsync(kitchen, meal?.Slot ?? MealSlot.Dinner, meal?.Kcal is > 0 ? meal.Kcal : 600, feel?.Label(), DateTime.Now);
            IdeaList.ItemsSource = ideas.Select(i => new IdeaRow(i)).ToList();
            IdeasText.Text = ideas.Count == 0 ? "No ideas came back; try again." : $"For {(meal is null ? "dinner" : meal.Meal.ToLowerInvariant())}" + (feel is { } f ? $", {f.Label().ToLowerInvariant()}" : "") + ":";
        }
        catch (KitchenAssistantException ex)
        {
            IdeasText.Text = ex.Message;
        }
        finally
        {
            asking = false;
            IdeasButton.IsEnabled = App.ClaudeOn;
        }
    }

    private void IdeaSteps_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IdeaRow row) App.ShowSteps(row.Idea.ToRecipe(ChosenMeal?.Slot ?? MealSlot.Dinner), ChosenMeal?.At);
    }

    private void IdeaMissing_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not IdeaRow row) return;
        var onList = Database.Groceries().Where(g => g.Bought is null).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in row.Idea.Missing.Where(m => !onList.Contains(m)))
            Database.Save(new GroceryItem { Name = name, Added = DateTime.Now });
        IdeasText.Text = $"Added {string.Join(", ", row.Idea.Missing)} to the grocery list.";
    }

    private static string When(DateTime day) =>
        day.Date == DateTime.Today ? "today" : day.Date == DateTime.Today.AddDays(1) ? "tomorrow" : day.ToString("dddd", CultureInfo.CurrentCulture);

    // ---- I feel like ------------------------------------------------------------------------------------------

    private void ShowFeels()
    {
        FeelPanel.Children.Clear();
        foreach (var option in new Feel?[] { null }.Concat(Enum.GetValues<Feel>().Cast<Feel?>()))
        {
            var button = new Button { Content = option?.Label() ?? "Anything", Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 5, 12, 5), Tag = option };
            if (option == feel) button.Style = (Style)FindResource("AccentButtonStyle");
            button.Click += (_, _) =>
            {
                feel = (Feel?)button.Tag;
                ShowFeels();
                ShowChoices();
            };
            FeelPanel.Children.Add(button);
        }
    }

    private void Meal_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading) ShowChoices();
    }

    /// <summary>The recipes for the meal that fit what's felt like: those that use up what won't keep first.</summary>
    private void ShowChoices()
    {
        var meal = ChosenMeal;
        var slot = meal?.Slot ?? MealSlot.Dinner;
        PickedText.Text = meal is null ? "" : meal.Recipe is { } r ? $"Picked: {r.Name}. Pick another to change it." : "";
        PickedText.Show(PickedText.Text.Length > 0);
        ChoiceList.ItemsSource = Recipes.For(slot, feel, meal?.Kcal ?? 0)
            .Select(r => new MenuChoice(r, meal?.Kcal ?? 0, UsesSoon(r)))
            .OrderBy(c => c.UsesSoon.Length == 0)
            .ToList();
    }

    private string UsesSoon(Recipe recipe)
    {
        var used = useSoon.Where(u => recipe.Ingredients.Any(i => i.Grocery.Equals(u.Name, StringComparison.OrdinalIgnoreCase))).ToList();
        return used.Count == 0 ? "" : "Uses up " + string.Join(" and ", used.Select(u => $"your {u.Name.ToLowerInvariant()} (best by {When(u.UseBy!.Value)})"));
    }

    private void Have_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MenuChoice choice || ChosenMeal is not { } meal) return;
        KitchenLog.Pick(meal, choice.Recipe);
        App.KitchenChanged();
    }

    private void Steps_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MenuChoice choice) App.ShowSteps(choice.Recipe, ChosenMeal?.At);
    }

    // ---- The week ---------------------------------------------------------------------------------------------

    /// <summary>A row a day from today, a column a meal (by the time of day it's at), a choice in each.</summary>
    private void ShowWeek()
    {
        WeekGrid.Children.Clear();
        WeekGrid.ColumnDefinitions.Clear();
        WeekGrid.RowDefinitions.Clear();
        var meals = App.Reminders
            .Where(r => r.Kind == ReminderKind.Meal && r.Active && HealthReminder.Meals.SlotOf(r.Name) != MealSlot.Snack && r.Times.Count > 0)
            .OrderBy(r => r.Times.Min(t => t.Time))
            .ToList();
        if (meals.Count == 0) return;
        var menu = Database.Menu();
        WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        foreach (var _ in meals) WeekGrid.ColumnDefinitions.Add(new ColumnDefinition());
        WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < meals.Count; c++)
            Place(new TextBlock { Text = meals[c].Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 6) }, 0, c + 1);

        var today = DateTime.Today;
        foreach (var offset in Days)
        {
            var date = today.AddDays(offset);
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = offset + 1;
            Place(new TextBlock
            {
                Text = offset == 0 ? "Today" : date.ToString("dddd", CultureInfo.CurrentCulture),
                FontWeight = offset == 0 ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center,
            }, row, 0);
            for (var c = 0; c < meals.Count; c++)
            {
                var reminder = meals[c];
                if (!reminder.Days.Contains(date.DayOfWeek))
                {
                    Place(new TextBlock { Text = "–", Style = (Style)FindResource("Muted"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }, row, c + 1);
                    continue;
                }
                var slot = HealthReminder.Meals.SlotOf(reminder.Name);
                var kcal = reminder.Unit == "kcal" ? reminder.Times[0].Amount : 0;
                var options = new List<MenuOption> { new("Just an idea", null), new("Decide on the day", "decide") };
                options.AddRange(Recipes.For(slot, null, kcal).Select(r => new MenuOption($"{r.Name} · {r.Kcal:N0} kcal", r.Id)));
                var current = menu.TryGetValue((date.DayOfWeek, reminder.Name), out var set) ? set.Recipe : null;
                var box = new ComboBox { ItemsSource = options, Margin = new Thickness(4, 3, 4, 3), MinWidth = 150 };
                box.SelectedIndex = Math.Max(0, options.FindIndex(o => o.Value == current));
                var (day, name) = (date.DayOfWeek, reminder.Name);
                box.SelectionChanged += (_, _) =>
                {
                    if (box.SelectedItem is not MenuOption option) return;
                    // Not a refresh of the page: that would rebuild the grid this box is in.
                    Database.SetMenu(day, name, option.Value);
                    ShowShopping();
                };
                Place(box, row, c + 1);
            }
        }

        var picks = Days.Select(d => DateOnly.FromDateTime(today.AddDays(d)))
            .SelectMany(d => meals.Select(m => (Day: d, m.Name, Choice: Database.Choice(d, m.Name))))
            .Where(p => p.Choice is not null && Recipes.Get(p.Choice.Value.Recipe) is not null)
            .Select(p => $"{(p.Day == DateOnly.FromDateTime(today) ? "today" : p.Day.ToString("dddd", CultureInfo.CurrentCulture))}'s {p.Name.ToLowerInvariant()}: {Recipes.Get(p.Choice!.Value.Recipe)!.Name}")
            .ToList();
        PicksText.Text = picks.Count == 0 ? "" : "Picked for a single day, over the menu: " + string.Join("; ", picks) + ".";
        PicksText.Show(picks.Count > 0);
    }

    private void Place(UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        WeekGrid.Children.Add(element);
    }

    // ---- Shopping ---------------------------------------------------------------------------------------------

    private List<ShoppingRow> shopping = [];

    /// <summary>What the next 7 days' recipes need, in whole packs.</summary>
    private void ShowShopping()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var recipes = Days.SelectMany(d => KitchenLog.Meals(today.AddDays(d))).Select(m => m.Recipe).OfType<Recipe>().ToList();
        shopping = GroceryCatalog.Shopping(recipes).OrderBy(l => l.Info.Staple).Select(l => new ShoppingRow(l)).ToList();
        ShoppingList.ItemsSource = shopping;
        NoShopping.Show(shopping.Count == 0);
        ShoppingButtons.Show(shopping.Count > 0);
        ShoppingAdded.Text = "";
    }

    /// <summary>Puts the packs on the grocery list, leaving out staples and what's on it already.</summary>
    private void AddShopping_Click(object sender, RoutedEventArgs e)
    {
        var onList = Database.Groceries().Where(g => g.Bought is null).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var row in shopping.Where(r => !r.Line.Info.Staple && r.Line.Packs > 0 && !onList.Contains(r.Name)))
        {
            var info = row.Line.Info;
            Database.Save(new GroceryItem { Name = info.Name, Quantity = row.Line.Packs == 1 ? info.PackText : $"{row.Line.Packs} x {info.PackText}", Added = DateTime.Now });
            added++;
        }
        ShoppingAdded.Text = added == 0 ? "They're on your grocery list already." : $"Added {added} to your grocery list.";
        App.Changed();
    }

    // ---- Settings ---------------------------------------------------------------------------------------------

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        KitchenLog.AlertsOn = AlertsBox.IsChecked == true;
        KitchenLog.Thaw = ThawWater.IsChecked == true ? ThawMethod.ColdWater : ThawMethod.Fridge;
        KitchenLog.StepsAtStart = StepsAtStartBox.IsChecked == true;
        KitchenLog.StepsOnTop = StepsOnTopBox.IsChecked == true;
    }

    private void EatDrink_Click(object sender, RoutedEventArgs e) => this.MainWindowOf().Navigate(MainWindow.EatDrinkPage);
}
