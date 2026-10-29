using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Strongthium.HealthReminder;

/// <summary>Something in the kitchen to pick: what there is of it, and about what a usual amount has.</summary>
public sealed class KitchenChoice(PantryItem item, MeasureSystem system)
{
    public PantryItem Item => item;
    public string Name => item.Note is { Length: > 0 } note ? $"{item.Name} ({note})" : item.Name;

    public string Detail
    {
        get
        {
            var usual = Calories.UsualAmount(item);
            var kcal = Calories.Eat(item, usual).Kcal;
            return Pantry.Show(item, system) + " there · " + (kcal > 0 ? $"about {kcal} kcal in {(usual == "1" ? "one" : usual)}" : "kcal not known");
        }
    }
}

/// <summary>Something usual eaten out, with about how many kcal.</summary>
public sealed record OutChoice(string Name, int Kcal);

/// <summary>A line of what was eaten: how much, and about how many kcal, worked out again as the amount changes
/// until the kcal are typed.</summary>
public sealed class FoodLine : INotifyPropertyChanged
{
    private string amount;
    private string kcalText = "";
    private bool kcalTyped;

    public FoodLine(string name, string amount, PantryItem? item = null, int? each = null)
    {
        Name = name;
        Item = item;
        Each = each;
        this.amount = amount;
        Work();
    }

    public string Name { get; }
    /// <summary>From the kitchen, or else eaten out.</summary>
    public PantryItem? Item { get; }
    /// <summary>Eaten out: the kcal of one; null when it was typed.</summary>
    public int? Each { get; }
    public string Detail => Item is not null ? "from the kitchen" : "eaten out";

    public string Amount
    {
        get => amount;
        set
        {
            amount = value;
            Notify();
            if (!kcalTyped) Work();
        }
    }

    public string KcalText
    {
        get => kcalText;
        set
        {
            kcalText = value;
            kcalTyped = true;
            Notify();
        }
    }

    public int Kcal => int.TryParse(kcalText.Replace(",", "").Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var k) && k > 0 ? k : 0;

    private void Work()
    {
        var kcal = Item is not null ? Calories.Eat(Item, amount).Kcal
            : Each is { } each ? (int)Math.Round(each * (Quantities.Parse(amount) is { Dimension: Dimension.Count } q ? q.Amount : 1))
            : 0;
        kcalText = kcal > 0 ? kcal.ToString(CultureInfo.CurrentCulture) : "";
        Notify(nameof(KcalText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Logs food eaten by hand, from the kitchen or eaten out, on its own or in place of one of today's meals.</summary>
public sealed partial class FoodWindow : Window
{
    private readonly ObservableCollection<FoodLine> eaten = [];
    private readonly List<KitchenChoice> kitchen;
    private readonly List<Dose> meals;

    /// <param name="meal">The meal it's in place of, when it's logged from the meal's alarm.</param>
    public FoodWindow(Dose? meal = null)
    {
        InitializeComponent();
        var app = App.Current;
        kitchen = PantryLog.Items().Where(p => p.Amount > 0).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new KitchenChoice(p, app.Measures)).ToList();
        OutList.ItemsSource = Calories.EatenOut.Select(o => new OutChoice(o.Name, o.Kcal)).ToList();
        ShowKitchen();

        // Today's meals not yet answered, and the one it's for.
        meals = app.Schedule.Day(app.Reminders, DateTime.Now)
            .Where(d => d.Dose.Reminder.Kind == ReminderKind.Meal && d.State is not (DoseState.Taken or DoseState.Skipped))
            .Select(d => d.Dose).ToList();
        if (meal is not null && !meals.Any(m => m.Key == meal.Key)) meals.Insert(0, meal);
        MealBox.ItemsSource = new[] { "No meal: on its own" }.Concat(meals.Select(m => $"{m.Reminder.Name} at {Format.Time(m.Due)}")).ToList();
        MealBox.SelectedIndex = meal is null ? 0 : meals.FindIndex(m => m.Key == meal.Key) + 1;

        EatenList.ItemsSource = eaten;
        eaten.CollectionChanged += (_, _) => ShowTotal();
        ShowTotal();
    }

    private void ShowKitchen()
    {
        var find = FindBox.Text.Trim();
        var shown = kitchen.Where(k => find.Length == 0 || k.Name.Contains(find, StringComparison.CurrentCultureIgnoreCase)).ToList();
        KitchenList.ItemsSource = shown;
        KitchenEmpty.Show(shown.Count == 0);
    }

    private void Find_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ShowKitchen();
    }

    private void ShowTotal()
    {
        NothingYet.Show(eaten.Count == 0);
        LogButton.IsEnabled = eaten.Count > 0;
        var total = eaten.Sum(l => l.Kcal);
        var unknown = eaten.Count(l => l.Kcal == 0);
        TotalText.Text = eaten.Count == 0 ? "" : $"About {total:N0} kcal" + (unknown > 0 ? $", and {unknown} without kcal" : "");
    }

    private void Add(FoodLine line)
    {
        line.PropertyChanged += (_, _) => ShowTotal();
        eaten.Add(line);
    }

    private void Kitchen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KitchenChoice choice)
            Add(new FoodLine(choice.Name, Calories.UsualAmount(choice.Item), choice.Item));
    }

    private void Out_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is OutChoice choice) Add(new FoodLine(choice.Name, "1", each: choice.Kcal));
    }

    private void Other_Click(object sender, RoutedEventArgs e)
    {
        var name = OtherBox.Text.Trim();
        if (name.Length == 0) return;
        Add(new FoodLine(name, "1"));
        OtherBox.Clear();
    }

    private void Other_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Other_Click(sender, e);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FoodLine line) eaten.Remove(line);
    }

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        var meal = Meal;
        foreach (var line in eaten)
        {
            if (line.Item is { } item) FoodLog.FromKitchen(item.Name, line.Amount, line.Kcal, now, meal?.Key);
            else FoodLog.EatenOut(line.Name, line.Amount, line.Kcal, now, meal?.Key);
        }
        if (meal is not null) FoodLog.ForMeal(meal, now);
        DialogResult = true;
    }

    /// <summary>The meal it was logged in place of, if any.</summary>
    public Dose? Meal => MealBox.SelectedIndex > 0 ? meals[MealBox.SelectedIndex - 1] : null;
}
