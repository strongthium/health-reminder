using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>Something eaten and logged by hand: from the kitchen (<paramref name="Pantry"/>, the stock's name, and
/// <paramref name="PantryId"/>, which one of that name, with <paramref name="Used"/> in its <paramref name="Unit"/> taken
/// off, so it can be put back), or eaten out. When it was had in place of a meal reminder, <paramref name="Meal"/> is
/// that dose's key, and the meal counts as eaten.</summary>
public sealed record FoodEaten(long Id, DateTime At, string Name, string Amount, int Kcal, string? Pantry = null, double Used = 0, string Unit = "",
    string? Meal = null, long? PantryId = null);

/// <summary>What a food has, roughly: kcal in 100 g (or 100 mL, for what's poured or scooped), in one piece of it as
/// it's usually counted (an egg, a slice, a can; 0 when it isn't), and a usual serving, in g or mL.</summary>
public sealed record FoodEnergy(double Per100, double Piece = 0, double Serving = 100);

/// <summary>What eating some of something in the kitchen comes to: about so many kcal (0 when it can't be told), and
/// how much comes off the stock in its unit (<paramref name="Read"/> false when that can't be told either).</summary>
public sealed record Portion(int Kcal, double Used, bool Read);

/// <summary>
/// Calories, roughly: for food in the kitchen, by what its name says (the word that matches last, as
/// <see cref="Foods"/> reads names: "chocolate milk" is milk), and for the usual things eaten out. Values are typical
/// ones from the USDA's FoodData Central and the chains' published nutrition, rounded; sizes and brands differ, so
/// they're a guide, to be changed when the package says otherwise.
/// </summary>
public static partial class Calories
{
    /// <summary>The usual things eaten out, in a usual size, with about how many kcal.</summary>
    public static readonly IReadOnlyList<(string Name, int Kcal)> EatenOut =
    [
        ("Hamburger", 250), ("Cheeseburger", 300), ("Double cheeseburger", 450), ("Big burger (quarter pound or more)", 550),
        ("Chicken sandwich, fried", 500), ("Chicken sandwich, grilled", 380), ("Chicken nuggets, 6", 270), ("Chicken nuggets, 10", 450),
        ("Fried chicken, 2 pieces", 500), ("Chicken wings, 6", 480), ("Fish and chips", 900),
        ("Fries, small", 230), ("Fries, medium", 340), ("Fries, large", 490), ("Onion rings, medium", 400), ("Poutine, regular", 740),
        ("Hot dog", 290), ("Pizza, a slice of cheese", 285), ("Pizza, a slice of pepperoni", 310),
        ("Sub, 6-inch", 400), ("Sub, footlong", 800), ("Wrap", 550), ("Burrito", 750), ("Taco", 170), ("Shawarma or donair", 650),
        ("Chinese takeout, a plate", 900), ("Pad thai", 850), ("Sushi, 8 pieces", 350),
        ("Breakfast sandwich", 400), ("Hash brown", 150), ("Pancakes, a stack of 3, with syrup", 550),
        ("Doughnut", 260), ("Muffin, bakery", 400), ("Cookie, bakery", 220), ("Ice cream cone", 230),
        ("Milkshake, medium", 600), ("Pop, medium", 210), ("Coffee with cream and sugar, medium", 220), ("Iced coffee, sweetened", 260),
        ("Latte, medium", 190), ("Salad with dressing", 350),
    ];

    /// <summary>By words in a food's name; a phrase matches before the words in it. Before <see cref="Patterns"/>,
    /// which is made from it.</summary>
    private static readonly (string[] Words, FoodEnergy Energy)[] Table =
    [
        // Dairy and eggs
        (["egg"], new(143, 72, 100)),
        (["milk"], new(50, 0, 250)), (["chocolate milk"], new(80, 0, 250)), (["cream"], new(195, 0, 15)), (["sour cream"], new(200, 0, 30)),
        (["cream cheese"], new(340, 0, 30)), (["cheese"], new(400, 110, 30)), (["cheez", "cheese spread"], new(290, 0, 30)),
        (["butter", "margarine"], new(717, 0, 10)), (["yogurt", "yoghurt"], new(70, 150, 175)), (["ice cream"], new(110, 0, 125)),
        // Bakery
        (["bread", "loaf", "toast"], new(265, 80, 30)), (["bagel"], new(250, 270)), (["bun", "roll"], new(280, 120)),
        (["tortilla", "wrap"], new(300, 140)), (["english muffin"], new(230, 130)), (["muffin"], new(380, 400)), (["pancake"], new(230, 90)),
        (["waffle"], new(290, 95)), (["croissant"], new(406, 230)), (["donut", "doughnut"], new(420, 260)), (["cookie"], new(480, 80)),
        (["cake"], new(370, 350)), (["pie"], new(260, 500)), (["meat pie", "pot pie", "tourtiere", "tourtière"], new(270, 540, 200)),
        // Meals
        (["fries", "french fries", "wedges", "tater tots"], new(160, 0, 85)), (["hash brown", "hash browns"], new(150, 0, 85)),
        (["hash brown patty", "hash brown patties"], new(210, 130)), (["ravioli", "tortellini"], new(250, 0, 200)),
        // Drinks
        (["juice"], new(45, 0, 250)), (["pop", "soda", "cola", "coke", "pepsi", "sprite", "ginger ale", "root beer"], new(42, 150, 355)),
        (["beer"], new(43, 150, 355)), (["wine"], new(83, 0, 150)), (["coffee", "tea"], new(2, 0, 250)), (["water"], new(0, 0, 500)),
        // Dry goods
        (["rice"], new(360, 0, 60)), (["pasta", "spaghetti", "macaroni", "noodle", "penne", "rotini", "fusilli", "linguine", "fettuccine", "elbows", "farfalle", "bow ties", "rigatoni"], new(370, 0, 85)), (["oat", "oatmeal"], new(380, 0, 40)),
        (["cereal"], new(380, 0, 40)), (["granola"], new(470, 0, 50)), (["granola bar"], new(430, 120)), (["flour"], new(364, 0, 30)), (["sugar"], new(387, 0, 10)),
        (["cracker"], new(440, 0, 30)), (["chip", "crisp"], new(536, 0, 30)), (["popcorn"], new(390, 0, 30)), (["chocolate"], new(535, 230, 45)),
        (["almond", "nut", "peanut"], new(580, 0, 30)), (["peanut butter"], new(590, 0, 32)), (["jam", "honey", "syrup"], new(280, 0, 20)),
        (["ketchup"], new(100, 0, 15)), (["mayonnaise", "mayo"], new(680, 0, 15)), (["salsa"], new(30, 0, 30)), (["hummus"], new(170, 0, 30)),
        // Meat and fish
        (["chicken"], new(200, 0, 150)), (["chicken breast"], new(165, 200, 150)), (["chicken strip", "nugget", "tender"], new(250, 0, 100)),
        (["wing"], new(290, 0, 150)), (["ground beef", "hamburger"], new(250, 0, 113)), (["patty", "patties"], new(250, 230)),
        (["beef", "steak"], new(250, 280, 150)), (["pork", "pork chop"], new(230, 200, 150)), (["bacon"], new(540, 45, 30)), (["ham"], new(145, 0, 60)),
        (["sausage"], new(300, 170)), (["hot dog", "wiener"], new(290, 150)), (["pepperoni", "salami"], new(400, 0, 30)), (["turkey"], new(110, 0, 60)),
        (["salmon"], new(200, 230, 150)), (["fish"], new(150, 0, 120)), (["tuna"], new(120, 120, 80)), (["shrimp"], new(100, 0, 100)),
        // Fruit
        (["banana"], new(89, 105)), (["apple"], new(52, 95)), (["orange"], new(47, 62)), (["pear"], new(57, 100)), (["peach"], new(39, 60)),
        (["grape"], new(69, 0, 150)), (["berry", "berries", "strawberry", "strawberries", "blueberry", "blueberries"], new(40, 0, 150)),
        (["watermelon", "melon"], new(30, 0, 280)), (["avocado"], new(160, 240)), (["lemon", "lime"], new(29, 17)),
        // Vegetables
        (["lettuce", "salad", "greens", "spinach"], new(17, 80, 50)), (["pepper"], new(30, 30)), (["tomato"], new(18, 22)),
        (["cucumber"], new(15, 45)), (["carrot"], new(41, 25, 80)), (["broccoli"], new(35, 0, 90)), (["corn"], new(86, 90, 100)),
        (["potato"], new(77, 160, 150)), (["onion"], new(40, 44)), (["garlic"], new(149, 4, 5)), (["mushroom"], new(22, 0, 70)),
        (["green bean"], new(31, 0, 100)), (["mixed vegetables", "vegetable"], new(65, 0, 90)), (["pea"], new(80, 0, 80)),
        // Canned, and meals
        (["bean", "chickpea", "lentil"], new(90, 380, 130)), (["canned tomatoes"], new(20, 80, 130)), (["soup"], new(40, 180, 250)),
        (["chili"], new(110, 300, 250)), (["pizza"], new(270, 285, 110)), (["lasagna"], new(160, 400, 250)), (["leftover"], new(150, 400, 250)),
    ];

    [GeneratedRegex(@"\b(diet|zero|sugar[- ]free|no sugar)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NoSugar();

    /// <summary>What a food has, by its name; null when the name says nothing known. "Diet" and "zero" drinks have
    /// none to speak of.</summary>
    public static FoodEnergy? Of(string name)
    {
        var text = " " + name.Trim().ToLowerInvariant() + " ";
        if (NoSugar().IsMatch(text) && Of(NoSugar().Replace(text, " ")) is { } drink && drink.Per100 < 60) return drink with { Per100 = 0, Piece = 0 };
        FoodEnergy? best = null;
        (int End, int Length) bestAt = (-1, 0);
        foreach (var (pattern, length, energy) in Patterns)
        {
            var match = pattern.Match(text);
            if (!match.Success) continue;
            var end = match.Index + match.Length;
            if (end > bestAt.End || end == bestAt.End && length > bestAt.Length) (best, bestAt) = (energy, (end, length));
        }
        return best;
    }

    /// <summary>Each word of the table as whole words, and their plurals: "eggs", "tomatoes".</summary>
    private static readonly List<(Regex Pattern, int Length, FoodEnergy Energy)> Patterns =
        Table.SelectMany(t => t.Words.Select(w => (new Regex($@"\b{Regex.Escape(w)}(s|es)?\b", RegexOptions.CultureInvariant), w.Length, t.Energy))).ToList();

    /// <summary>What one piece of something in the kitchen has: from what a piece weighs, if that's set, or else the
    /// usual piece of it; null when it can't be told.</summary>
    private static double? PieceKcal(PantryItem item, FoodEnergy? energy) =>
        item.Packing?.Each is { } each && energy is not null ? each * energy.Per100 / 100
        : energy is { Piece: > 0 } ? energy.Piece
        : null;

    /// <summary>A usual amount to eat of something in the kitchen, as it's typed: "1" of what's counted, a serving of
    /// what's weighed ("60 g" of rice, "250 mL" of milk).</summary>
    public static string UsualAmount(PantryItem item) =>
        item.Unit switch
        {
            "g" => $"{Of(item.Name)?.Serving ?? 100:0} g",
            "mL" => $"{Of(item.Name)?.Serving ?? 250:0} mL",
            _ => "1",
        };

    /// <summary>
    /// Eating <paramref name="amount"/> of something in the kitchen: a count of its pieces ("2" eggs, "1" serving of
    /// what's weighed), or a weight or volume ("150 g", "1 cup"). Volumes are taken as their weight in water. How
    /// much comes off is in the stock's unit; a weight of something counted comes off only when a piece's weight is
    /// known.
    /// </summary>
    public static Portion Eat(PantryItem item, string amount)
    {
        var energy = Of(item.Name);
        var q = Quantities.Parse(amount) ?? new Quantity(1, Dimension.Count);
        double kcal, used;
        var read = true;
        if (item.Unit is "g" or "mL")
        {
            if (q.Dimension == Dimension.Count)
            {
                var each = item.Packing?.Each ?? energy?.Serving;
                used = q.Amount * (each ?? 0);
                read = each is not null;
            }
            else used = q.Amount;
            kcal = energy is null ? 0 : used * energy.Per100 / 100;
        }
        else if (q.Dimension == Dimension.Count)
        {
            used = q.Amount;
            kcal = q.Amount * (PieceKcal(item, energy) ?? 0);
        }
        else
        {
            kcal = energy is null ? 0 : q.Amount * energy.Per100 / 100;
            used = item.Packing?.Each is > 0 and var each ? q.Amount / each : 0;
            read = item.Packing?.Each is > 0;
        }
        return new Portion((int)(Math.Round(kcal / 5) * 5), used, read);
    }

    /// <summary>What eating some leaves of it: the amount taken off (never below zero), and in doubt when more was eaten
    /// than there was, or how much couldn't be told.</summary>
    public static PantryItem Left(PantryItem item, Portion portion)
    {
        var left = item.Amount - portion.Used;
        return item with { Amount = Math.Max(0, left), Unsure = item.Unsure || left < 0 || !portion.Read };
    }
}
