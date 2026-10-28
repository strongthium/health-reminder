using System.Globalization;

namespace Strongthium.HealthReminder;

public enum MealSlot { Breakfast, Lunch, Dinner, Snack }

/// <summary>A plain food in a household amount: "egg" / "eggs", about 72 kcal each. <paramref name="Step"/> is the
/// smallest amount it's eaten in (½ cup of rice); <paramref name="Groceries"/> what to buy for it.</summary>
public sealed record Food(string One, string Many, double Kcal, string[] Groceries, double Step = 1, double Max = 3);

/// <summary>An amount of a food in a meal: "2 eggs", "a glass of orange juice", "1½ cups of rice".</summary>
public sealed record MealPart(Food Food, double Count)
{
    public double Kcal => Food.Kcal * Count;

    public override string ToString()
    {
        if (Count == 1) return (StartsWithVowelSound(Food.One) ? "an " : "a ") + Food.One;
        var whole = Math.Floor(Count);
        var half = Count - whole >= 0.5;
        var number = (whole > 0 ? whole.ToString(CultureInfo.CurrentCulture) : "") + (half ? "½" : "");
        return $"{number} {(Count > 1 ? Food.Many : Food.One)}";
    }

    private static bool StartsWithVowelSound(string word) => word.Length > 0 && "aeio".Contains(char.ToLowerInvariant(word[0])) && !word.StartsWith("one", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A meal as plain food: "2 eggs, 2 slices of buttered toast and a glass of orange juice", about 520 kcal.</summary>
public sealed record MealIdea(IReadOnlyList<MealPart> Parts)
{
    public int Kcal => (int)(Math.Round(Parts.Sum(p => p.Kcal) / 10) * 10);

    public override string ToString()
    {
        var parts = Parts.Select(p => p.ToString()).ToList();
        return parts.Count switch
        {
            0 => "",
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }

    /// <summary>What to buy for it, without amounts: "Eggs", "Bread", "Butter".</summary>
    public IEnumerable<string> Groceries => Parts.SelectMany(p => p.Food.Groceries).Distinct();
}

/// <summary>
/// Suggests meals made of plain, everyday food in household amounts (2 eggs, a slice of toast, a cup of rice, a glass of
/// milk), close to a meal's calories, so the plan needs no special shopping. Calories are typical values for the
/// usual size (a large egg, a slice of sandwich bread, a medium apple, a cup of 2% milk), from the USDA's FoodData
/// Central, rounded; brands differ, so they're a guide. A day's ideas stay the same all day, and change from day to
/// day; <c>another</c> asks for a different one.
/// </summary>
public static class Meals
{
    // Breakfast
    private static readonly Food Egg = new("egg", "eggs", 72, ["Eggs"]);
    private static readonly Food Toast = new("slice of buttered toast", "slices of buttered toast", 110, ["Bread", "Butter"]);
    private static readonly Food PeanutButterToast = new("slice of toast with peanut butter", "slices of toast with peanut butter", 180, ["Bread", "Peanut butter"]);
    private static readonly Food Oatmeal = new("bowl of oatmeal", "bowls of oatmeal", 160, ["Oats"], 1, 2);
    private static readonly Food Cereal = new("bowl of cereal with milk", "bowls of cereal with milk", 220, ["Cereal", "Milk"], 1, 2);
    private static readonly Food Yogurt = new("cup of yogurt", "cups of yogurt", 150, ["Yogurt"], 1, 2);
    private static readonly Food Granola = new("handful of granola", "handfuls of granola", 140, ["Granola"], 1, 2);
    private static readonly Food EnglishMuffin = new("egg and cheese English muffin", "egg and cheese English muffins", 300, ["English muffins", "Eggs", "Cheese slices"], 1, 2);
    private static readonly Food OrangeJuice = new("glass of orange juice", "glasses of orange juice", 110, ["Orange juice"], 1, 1);
    private static readonly Food Milk = new("glass of milk", "glasses of milk", 120, ["Milk"], 1, 1);
    private static readonly Food Coffee = new("coffee or tea with milk", "coffees or teas with milk", 20, ["Coffee or tea", "Milk"], 1, 1);

    // Fruit and vegetables
    private static readonly Food Banana = new("banana", "bananas", 105, ["Bananas"], 1, 2);
    private static readonly Food Apple = new("apple", "apples", 95, ["Apples"], 1, 2);
    private static readonly Food Orange = new("orange", "oranges", 62, ["Oranges"], 1, 2);
    private static readonly Food Berries = new("cup of berries", "cups of berries", 70, ["Berries"], 1, 2);
    private static readonly Food Carrots = new("handful of baby carrots", "handfuls of baby carrots", 30, ["Baby carrots"], 1, 2);
    private static readonly Food SideSalad = new("side salad with dressing", "side salads with dressing", 100, ["Salad greens", "Salad dressing"], 1, 1);
    private static readonly Food Broccoli = new("cup of broccoli", "cups of broccoli", 55, ["Broccoli"], 1, 2);
    private static readonly Food GreenBeans = new("cup of green beans", "cups of green beans", 40, ["Green beans"], 1, 2);
    private static readonly Food MixedVegetables = new("cup of mixed vegetables", "cups of mixed vegetables", 80, ["Frozen mixed vegetables"], 1, 2);
    private static readonly Food Corn = new("ear of corn", "ears of corn", 90, ["Corn"], 1, 2);

    // Lunch
    private static readonly Food HamSandwich = new("ham and cheese sandwich", "ham and cheese sandwiches", 350, ["Bread", "Ham", "Cheese slices"], 1, 2);
    private static readonly Food TurkeySandwich = new("turkey sandwich", "turkey sandwiches", 300, ["Bread", "Sliced turkey", "Lettuce"], 1, 2);
    private static readonly Food TunaSandwich = new("tuna sandwich", "tuna sandwiches", 380, ["Bread", "Canned tuna", "Mayonnaise"], 1, 2);
    private static readonly Food EggSandwich = new("egg salad sandwich", "egg salad sandwiches", 360, ["Bread", "Eggs", "Mayonnaise"], 1, 2);
    private static readonly Food PbjSandwich = new("peanut butter and jam sandwich", "peanut butter and jam sandwiches", 380, ["Bread", "Peanut butter", "Jam"], 1, 2);
    private static readonly Food Soup = new("bowl of chicken noodle soup", "bowls of chicken noodle soup", 150, ["Chicken noodle soup"], 1, 2);
    private static readonly Food VegetableSoup = new("bowl of vegetable soup", "bowls of vegetable soup", 120, ["Vegetable soup"], 1, 2);
    private static readonly Food Bread = new("slice of bread", "slices of bread", 80, ["Bread"]);
    private static readonly Food Crackers = new("handful of crackers", "handfuls of crackers", 70, ["Crackers"], 1, 2);
    private static readonly Food Cheese = new("slice of cheese", "slices of cheese", 110, ["Cheese slices"], 1, 2);
    private static readonly Food ChickenSalad = new("chicken salad with dressing", "chicken salads with dressing", 350, ["Chicken breasts", "Salad greens", "Salad dressing"], 1, 2);
    private static readonly Food ChickenWrap = new("chicken wrap", "chicken wraps", 350, ["Tortillas", "Chicken breasts", "Lettuce"], 1, 2);
    private static readonly Food Pizza = new("slice of pizza", "slices of pizza", 285, ["Pizza"], 1, 3);
    private static readonly Food Roll = new("dinner roll", "dinner rolls", 120, ["Dinner rolls"], 1, 2);

    // Dinner
    private static readonly Food Chicken = new("chicken breast", "chicken breasts", 200, ["Chicken breasts"], 1, 2);
    private static readonly Food Salmon = new("salmon fillet", "salmon fillets", 230, ["Salmon fillets"], 1, 2);
    private static readonly Food PorkChop = new("pork chop", "pork chops", 200, ["Pork chops"], 1, 2);
    private static readonly Food Burger = new("hamburger patty", "hamburger patties", 230, ["Ground beef"], 1, 2);
    private static readonly Food Steak = new("small steak", "small steaks", 280, ["Steak"], 1, 2);
    private static readonly Food Sausage = new("sausage", "sausages", 170, ["Sausages"], 1, 3);
    private static readonly Food Potato = new("baked potato", "baked potatoes", 160, ["Potatoes"], 1, 2);
    private static readonly Food Mashed = new("cup of mashed potatoes", "cups of mashed potatoes", 210, ["Potatoes", "Milk", "Butter"], 0.5, 2);
    private static readonly Food Rice = new("cup of rice", "cups of rice", 205, ["Rice"], 0.5, 2);
    private static readonly Food Pasta = new("plate of pasta (85 g dry)", "plates of pasta (85 g dry each)", 300, ["Pasta"], 0.5, 1.5);
    private static readonly Food Spaghetti = new("plate of spaghetti with meat sauce", "plates of spaghetti with meat sauce", 600, ["Pasta", "Pasta sauce", "Ground beef"], 0.5, 1.5);
    private static readonly Food Chili = new("bowl of chili", "bowls of chili", 300, ["Ground beef", "Canned beans", "Canned tomatoes"], 1, 2);
    private static readonly Food StirFry = new("cup of chicken stir-fry", "cups of chicken stir-fry", 250, ["Chicken breasts", "Frozen mixed vegetables", "Soy sauce"], 1, 2);

    // Snacks
    private static readonly Food Almonds = new("handful of almonds", "handfuls of almonds", 165, ["Almonds"], 1, 2);
    private static readonly Food CheeseAndCrackers = new("slice of cheese with crackers", "slices of cheese with crackers", 180, ["Cheese slices", "Crackers"], 1, 2);
    private static readonly Food GranolaBar = new("granola bar", "granola bars", 120, ["Granola bars"], 1, 2);
    private static readonly Food CarrotsAndHummus = new("handful of baby carrots with hummus", "handfuls of baby carrots with hummus", 100, ["Baby carrots", "Hummus"], 1, 2);

    private static readonly Food[] Fruit = [Banana, Apple, Orange, Berries];
    private static readonly Food[] Vegetables = [Broccoli, GreenBeans, MixedVegetables, Corn, SideSalad];
    private static readonly Food[] BreakfastDrinks = [OrangeJuice, Milk, Coffee];

    /// <summary>A part of a meal: one of the foods, between a least and a most (0 leaves it out).</summary>
    private sealed record Course(Food[] Choices, double Least, double Most);

    private static Course C(double least, double most, params Food[] choices) => new(choices, least, most);

    private static readonly Dictionary<MealSlot, Course[][]> Templates = new()
    {
        [MealSlot.Breakfast] =
        [
            [C(1, 3, Egg), C(1, 3, Toast), C(0, 1, BreakfastDrinks)],
            [C(1, 2, Oatmeal), C(0, 1, Banana, Berries, Apple), C(0, 1, BreakfastDrinks)],
            [C(1, 2, Cereal), C(0, 1, Banana, Berries), C(0, 1, OrangeJuice, Coffee)],
            [C(1, 2, Yogurt), C(0, 2, Granola), C(0, 1, Fruit)],
            [C(1, 3, PeanutButterToast), C(0, 1, Banana, Apple), C(0, 1, Milk, Coffee)],
            [C(1, 2, EnglishMuffin), C(0, 1, Fruit), C(0, 1, OrangeJuice, Coffee)],
        ],
        [MealSlot.Lunch] =
        [
            [C(1, 2, HamSandwich, TurkeySandwich, TunaSandwich, EggSandwich, PbjSandwich), C(0, 1, Fruit), C(0, 1, Carrots, SideSalad), C(0, 1, Milk)],
            [C(1, 2, Soup, VegetableSoup), C(0, 2, Bread, Crackers), C(0, 2, Cheese), C(0, 1, Fruit)],
            [C(1, 2, ChickenSalad), C(0, 2, Roll, Bread), C(0, 1, Fruit)],
            [C(1, 2, ChickenWrap), C(0, 1, Carrots, SideSalad), C(0, 1, Fruit)],
            [C(1, 3, Pizza), C(0, 1, SideSalad), C(0, 1, Fruit)],
        ],
        [MealSlot.Dinner] =
        [
            [C(1, 2, Chicken, Salmon, PorkChop, Steak), C(0.5, 2, Potato, Rice, Mashed), C(1, 2, Vegetables), C(0, 1, Milk, Roll)],
            [C(1, 2, Burger), C(0, 2, Roll, Bread), C(0, 1, SideSalad, Corn), C(0, 1, Potato)],
            [C(0.5, 1.5, Spaghetti), C(0, 1, SideSalad, Broccoli), C(0, 2, Bread)],
            [C(1, 2, Chili), C(0, 2, Bread, Crackers, Roll), C(0, 1, SideSalad)],
            [C(1, 2, StirFry), C(0.5, 2, Rice)],
            [C(1, 3, Sausage), C(0.5, 2, Mashed, Potato), C(1, 2, Vegetables)],
            [C(1, 2, Chicken, Salmon), C(0.5, 2.5, Pasta, Rice), C(1, 2, Broccoli, GreenBeans, MixedVegetables)],
        ],
        [MealSlot.Snack] =
        [
            [C(1, 2, Fruit)],
            [C(1, 1, Yogurt), C(0, 1, Berries)],
            [C(1, 1, Almonds)],
            [C(1, 1, CheeseAndCrackers)],
            [C(1, 1, GranolaBar), C(0, 1, Milk)],
            [C(1, 1, CarrotsAndHummus), C(0, 1, Apple, Orange)],
            [C(1, 2, PeanutButterToast)],
            [C(1, 2, Egg), C(0, 1, Fruit)],
        ],
    };

    /// <summary>"Breakfast", "Lunch", "Dinner"; anything with "snack" in it is a snack.</summary>
    public static MealSlot SlotOf(string mealName) =>
        mealName.Contains("snack", StringComparison.OrdinalIgnoreCase) ? MealSlot.Snack
        : mealName.Contains("breakfast", StringComparison.OrdinalIgnoreCase) ? MealSlot.Breakfast
        : mealName.Contains("lunch", StringComparison.OrdinalIgnoreCase) ? MealSlot.Lunch
        : MealSlot.Dinner;

    /// <summary>A meal of about <paramref name="kcal"/> for the slot. The same day, meal and <paramref name="another"/>
    /// give the same idea.</summary>
    public static MealIdea Suggest(MealSlot slot, double kcal, DateOnly day, int another = 0)
    {
        var random = new Random(unchecked(day.DayNumber * 31 + (int)slot * 7919 + another * 104729));
        var candidates = Templates[slot]
            .Select(template => Fit(template.Select(c => (c.Choices[random.Next(c.Choices.Length)], c)).ToList(), kcal))
            .ToList();
        // Any that comes within 15% will do; the day picks which. Failing that, the closest.
        var close = candidates.Where(c => Math.Abs(c.Kcal - kcal) <= kcal * 0.15).ToList();
        return close.Count > 0 ? close[random.Next(close.Count)] : candidates.MinBy(c => Math.Abs(c.Kcal - kcal))!;
    }

    /// <summary>The amounts of the chosen foods closest to the calories; between equals, the one with less food.</summary>
    private static MealIdea Fit(List<(Food Food, Course Course)> chosen, double kcal)
    {
        var options = chosen.Select(c => Amounts(c.Food, c.Course)).ToList();
        double[]? best = null;
        double bestScore = double.MaxValue;
        var counts = new double[chosen.Count];
        void Search(int index, double total)
        {
            if (index == chosen.Count)
            {
                var score = Math.Abs(total - kcal) + counts.Sum() * 5;
                if (score < bestScore) (bestScore, best) = (score, (double[])counts.Clone());
                return;
            }
            foreach (var count in options[index])
            {
                counts[index] = count;
                Search(index + 1, total + count * chosen[index].Food.Kcal);
            }
        }
        Search(0, 0);
        return new MealIdea(chosen.Select((c, i) => new MealPart(c.Food, best![i])).Where(p => p.Count > 0).ToList());
    }

    private static List<double> Amounts(Food food, Course course)
    {
        var amounts = new List<double>();
        var most = Math.Min(course.Most, food.Max);
        if (course.Least == 0) amounts.Add(0);
        for (var amount = Math.Max(food.Step, course.Least); amount <= most + 1e-9; amount += food.Step) amounts.Add(amount);
        return amounts;
    }
}
