using System.Windows;
using System.Windows.Controls;

namespace Strongthium.HealthReminder;

/// <summary>A recipe to pick: how many calories against the meal's, how long, and whether it thaws first.</summary>
public sealed class RecipeChoice(Recipe recipe, double kcal)
{
    public Recipe Recipe => recipe;
    public string Detail => $"About {recipe.Kcal:N0} kcal" + (kcal > 0 ? $" (the plan has {kcal:N0})" : "")
        + $" · {recipe.PrepMinutes + recipe.CookMinutes} min" + (recipe.ThawHours > 0 ? " · from the freezer" : "")
        + " · " + string.Join(", ", recipe.Feels.Take(3).Select(f => f.Label().ToLowerInvariant()));
}

/// <summary>
/// A kitchen alert: take something out of the freezer, change the thawing water, it's thawed, start preparing, start
/// cooking, or, for a meal left to the day, what you feel like. It rings once; Done or a choice puts it away, and
/// starting to prepare opens the steps when that's set.
/// </summary>
public sealed partial class KitchenAlert : Window
{
    private Feel? feel;

    public KitchenEvent Event { get; }

    public KitchenAlert(KitchenEvent e)
    {
        InitializeComponent();
        Icon = App.Icon;
        Event = e;
        WhenText.Text = $"{Format.Time(e.At)} · {Format.Meal(e)}";
        TitleText.Text = e.Title;
        DetailText.Text = e.Detail;
        var recipe = e.Meal.Recipe;
        var choosing = e.Step == KitchenStep.Choose;
        ChoosePanel.Show(choosing);
        IngredientsPanel.Show(!choosing && recipe is not null);
        StepsButton.Show(recipe is not null && e.Step is KitchenStep.Prep or KitchenStep.Cook);
        DoneButton.Content = e.Step switch
        {
            KitchenStep.Choose => "Not now",
            KitchenStep.TakeOut => "It's out",
            KitchenStep.ChangeWater => "Changed",
            KitchenStep.Prep => "Starting",
            KitchenStep.Cook => "Cooking",
            _ => "OK",
        };
        if (recipe is not null)
        {
            RecipeText.Text = e.Step is KitchenStep.TakeOut or KitchenStep.Thawed or KitchenStep.ChangeWater ? "From the freezer:" : recipe.Name + ":";
            IngredientList.ItemsSource = e.Step is KitchenStep.TakeOut or KitchenStep.Thawed or KitchenStep.ChangeWater ? recipe.Frozen.ToList() : recipe.Ingredients;
        }
        if (choosing)
        {
            ShowFeels();
            ShowChoices();
        }
    }

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

    private void ShowChoices()
    {
        var choices = Recipes.For(Event.Meal.Slot, feel, Event.Meal.Kcal).Select(r => new RecipeChoice(r, Event.Meal.Kcal)).ToList();
        RecipeList.ItemsSource = choices;
        NoneText.Show(choices.Count == 0);
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RecipeChoice choice) return;
        KitchenLog.Pick(Event.Meal, choice.Recipe);
        KitchenLog.Done(Event);
        App.Current.KitchenChanged();
        Close();
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        KitchenLog.Done(Event);
        if (Event.Step == KitchenStep.Prep && KitchenLog.StepsAtStart && Event.Meal.Recipe is { } recipe) App.Current.ShowSteps(recipe, Event.Meal.At);
        App.Current.KitchenChanged();
        Close();
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        KitchenLog.Snooze(Event, TimeSpan.FromMinutes(10));
        Close();
    }

    private void Steps_Click(object sender, RoutedEventArgs e)
    {
        if (Event.Meal.Recipe is { } recipe) App.Current.ShowSteps(recipe, Event.Meal.At);
    }
}
