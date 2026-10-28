using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>The groups the kitchen's stock is shown in, within each place it's kept, in the order they're shown.</summary>
public enum FoodGroup { Produce, MeatAndFish, DairyAndEggs, Beverages, Bakery, Meals, DryGoods, Snacks, Condiments, Spices, Other }

/// <summary>What something in the kitchen is taken to be from its name: its group, its picture (one of
/// <see cref="Foods.Icons"/>), the picture's colour ("#RRGGBB", or null for the picture's own) and where it's kept.</summary>
public sealed record FoodLook(FoodGroup Group, string Icon, string? Color, Kept Kept);

/// <summary>
/// What a food is, from its name: "Coke" is a drink in a red can in the pantry, "Garlic powder" a spice in the cupboard,
/// "Frozen broccoli" a bag of vegetables in the freezer. The word that matches last in the name wins, as the last word
/// says what a thing is ("chocolate milk" is milk, "milk chocolate" is chocolate); at the same place, the longer match
/// ("peanut butter" over "butter"). Canned or frozen says where it's kept, whatever it is.
/// </summary>
public static partial class Foods
{
    /// <summary>The pictures there are, by the kind of thing they show, in the order they're offered, with what each is
    /// called.</summary>
    public static readonly IReadOnlyList<(string Section, IReadOnlyList<(string Key, string Label)> Icons)> IconSections =
    [
        ("Containers",
        [
            ("bottle", "Bottle"), ("jug", "Jug"), ("carafe", "Carafe"), ("milk", "Carton"), ("bag", "Bag"), ("box", "Box"), ("sack", "Sack"),
            ("envelope", "Envelope"), ("container", "Glass container"), ("tub", "Tub"), ("tin", "Can"), ("jar", "Jar"),
            ("sauce", "Squeeze bottle"), ("spice", "Shaker"),
        ]),
        ("Drinks", [("can", "Pop can"), ("juice", "Juice box"), ("glass", "Glass"), ("coffee", "Coffee or tea"), ("wine", "Wine")]),
        ("Dairy & eggs", [("eggs", "Eggs"), ("cheese", "Cheese"), ("butter", "Butter"), ("yogurt", "Yogurt")]),
        ("Meat & fish",
        [
            ("chicken", "Chicken"), ("strips", "Chicken strips"), ("beef", "Steak"), ("groundbeef", "Ground beef"), ("bacon", "Bacon"),
            ("sausage", "Sausage"), ("hotdog", "Hot dog"), ("burger", "Burger"), ("fish", "Fish"), ("shrimp", "Shrimp"),
        ]),
        ("Fruit",
        [
            ("apple", "Apple"), ("banana", "Banana"), ("citrus", "Lemon or orange"), ("berries", "Berries"), ("strawberry", "Strawberry"),
            ("grapes", "Grapes"), ("watermelon", "Watermelon"), ("avocado", "Avocado"),
        ]),
        ("Vegetables",
        [
            ("tomato", "Tomato"), ("pepper", "Pepper"), ("carrot", "Carrot"), ("cucumber", "Cucumber"), ("leafy", "Greens"), ("broccoli", "Broccoli"),
            ("corn", "Corn"), ("potato", "Potato"), ("onion", "Onion or garlic"), ("mushroom", "Mushroom"),
        ]),
        ("Bread & bakery",
        [
            ("bread", "Sliced bread"), ("loaf", "Loaf"), ("baguette", "Baguette"), ("bun", "Bun"), ("bagel", "Bagel"), ("croissant", "Croissant"),
            ("tortilla", "Tortilla or wrap"), ("muffin", "Muffin"), ("donut", "Doughnut"), ("pancakes", "Pancakes"), ("cake", "Cake"), ("pie", "Pie"),
        ]),
        ("Meals", [("pizza", "Pizza"), ("noodles", "Noodles"), ("soup", "Soup")]),
        ("Snacks & sweets", [("cookie", "Cookie"), ("chocolate", "Chocolate bar"), ("popcorn", "Popcorn"), ("icecream", "Ice cream")]),
        ("Other", [("ice", "Frozen"), ("food", "Food")]),
    ];

    /// <summary>All the pictures, in the order they're offered.</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> Icons = IconSections.SelectMany(s => s.Icons).ToList();

    public static string Name(FoodGroup group) => group switch
    {
        FoodGroup.Produce => "Fruit & vegetables",
        FoodGroup.MeatAndFish => "Meat & fish",
        FoodGroup.DairyAndEggs => "Dairy & eggs",
        FoodGroup.Beverages => "Beverages",
        FoodGroup.Bakery => "Bread & bakery",
        FoodGroup.Meals => "Meals & leftovers",
        FoodGroup.DryGoods => "Dry goods",
        FoodGroup.Snacks => "Snacks & sweets",
        FoodGroup.Condiments => "Condiments & spreads",
        FoodGroup.Spices => "Spices",
        _ => "Other",
    };

    public static string Name(Kept kind) => kind switch
    {
        Kept.Fridge => "Fridge",
        Kept.Freezer => "Freezer",
        Kept.Pantry => "Pantry",
        Kept.Cupboard => "Cupboard",
        _ => "Counter",
    };

    private sealed record Rule(Regex Match, FoodGroup Group, string Icon, string? Color, Kept Kept);

    private static Rule R(string pattern, FoodGroup group, string icon, Kept kept, string? color = null) =>
        new(new Regex(@"\b(?:" + pattern + @")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), group, icon, color, kept);

    // Specific before general: at the same place in a name, the longer match wins, and then the first.
    private static readonly Rule[] Rules =
    [
        // Frozen treats
        R("ice cream|gelato|popsicles?|freezies?|frozen yogurt|sherbet|sorbet", FoodGroup.Snacks, "icecream", Kept.Freezer),
        // Condiments and spreads
        R("cheez whiz|cheese spread", FoodGroup.Condiments, "jar", Kept.Fridge, "#F28C28"),
        R("peanut butter|almond butter|nut butter|nutella|hazelnut spread|cookie butter", FoodGroup.Condiments, "jar", Kept.Cupboard, "#A0522D"),
        R(@"jam\b|jelly|marmalade|preserves", FoodGroup.Condiments, "jar", Kept.Fridge, "#9B1B30"),
        R("honey|maple syrup|syrup", FoodGroup.Condiments, "jar", Kept.Cupboard, "#E0A030"),
        R("salsa", FoodGroup.Condiments, "jar", Kept.Fridge, "#C0392B"),
        R("ketchup|catsup", FoodGroup.Condiments, "sauce", Kept.Fridge, "#C62828"),
        R("mustard", FoodGroup.Condiments, "sauce", Kept.Fridge, "#E8B923"),
        R("mayo|mayonnaise|miracle whip|aioli", FoodGroup.Condiments, "sauce", Kept.Fridge, "#EFE3BC"),
        R("relish", FoodGroup.Condiments, "sauce", Kept.Fridge, "#4E9A2E"),
        R("hot sauce|sriracha|tabasco", FoodGroup.Condiments, "sauce", Kept.Cupboard, "#D32F2F"),
        R("bbq|barbecue|steak sauce|hp sauce|worcestershire", FoodGroup.Condiments, "sauce", Kept.Fridge, "#5D2E1A"),
        R("soy sauce|teriyaki|fish sauce|hoisin|oyster sauce", FoodGroup.Condiments, "sauce", Kept.Cupboard, "#3E2723"),
        R("dressing|vinaigrette|ranch", FoodGroup.Condiments, "sauce", Kept.Fridge, "#E8C66A"),
        R("pickles?|olives|sauerkraut|capers", FoodGroup.Condiments, "jar", Kept.Fridge, "#6B8E23"),
        R(@"hummus|tzatziki|guacamole|dip\b", FoodGroup.Condiments, "tub", Kept.Fridge, "#C9B37E"),
        R("pasta sauce|tomato sauce|marinara|alfredo|pizza sauce|tomato paste", FoodGroup.DryGoods, "jar", Kept.Pantry, "#C62828"),
        R("sauce", FoodGroup.Condiments, "sauce", Kept.Fridge),
        // Spices
        R(@"(?:taco|fajita|chili|chilli) seasoning|seasoning (?:mix|packets?)|dip mix", FoodGroup.Spices, "envelope", Kept.Pantry, "#E8B923"),
        R(@"gravy mix|gravy|soup mix|sauce mix", FoodGroup.DryGoods, "envelope", Kept.Pantry, "#8D5A1B"),
        R(@"salt\b|sea salt", FoodGroup.Spices, "spice", Kept.Cupboard, "#CFD8DC"),
        R("black pepper|white pepper|peppercorns?", FoodGroup.Spices, "spice", Kept.Cupboard, "#424242"),
        R(@"oregano|basil|thyme|rosemary|sage\b|dill\b|dried parsley|parsley flakes|italian seasoning|herbes", FoodGroup.Spices, "spice", Kept.Cupboard, "#6B8E23"),
        R(@"cinnamon|nutmeg|allspice|(?<!garlic )cloves\b|ground ginger|ginger powder", FoodGroup.Spices, "spice", Kept.Cupboard, "#8B4A2B"),
        R("turmeric|curry powder|cumin|garam masala|coriander", FoodGroup.Spices, "spice", Kept.Cupboard, "#D4A017"),
        R(@"spices?\b|seasoning|garlic powder|onion powder|chil(?:l)?i powder|paprika|chil(?:l)?i flakes|pepper flakes|cayenne|bay lea", FoodGroup.Spices, "spice", Kept.Cupboard),
        // Beverages
        R(@"dr\.? ?pepper", FoodGroup.Beverages, "can", Kept.Pantry, "#7B1E2B"),
        R(@"coke zero|pepsi zero|diet coke|diet pepsi", FoodGroup.Beverages, "can", Kept.Pantry, "#9EA4AA"),
        R(@"coke\b|coca.?cola|cola\b", FoodGroup.Beverages, "can", Kept.Pantry, "#D32F2F"),
        R("pepsi", FoodGroup.Beverages, "can", Kept.Pantry, "#1E4FA3"),
        R(@"sprite|7.?up\b|seven.?up|mountain dew", FoodGroup.Beverages, "can", Kept.Pantry, "#2DA34A"),
        R("ginger ale|canada dry|schweppes", FoodGroup.Beverages, "can", Kept.Pantry, "#2E7D32"),
        R(@"root beer|a&w\b|a & w\b", FoodGroup.Beverages, "can", Kept.Pantry, "#6D3B1F"),
        R(@"fanta|orange soda|orange crush|crush\b", FoodGroup.Beverages, "can", Kept.Pantry, "#F57C00"),
        R(@"pop\b|soda\b|soft drinks?|cream soda|tonic|club soda|sparkling water|la croix|bubly|energy drinks?|red bull|monster\b", FoodGroup.Beverages, "can", Kept.Pantry),
        R(@"orange juice|\boj\b", FoodGroup.Beverages, "juice", Kept.Fridge, "#F7A21B"),
        R("apple juice|apple cider", FoodGroup.Beverages, "juice", Kept.Fridge, "#E3B23C"),
        R("grape juice", FoodGroup.Beverages, "juice", Kept.Fridge, "#6A2C91"),
        R("cranberry", FoodGroup.Beverages, "juice", Kept.Fridge, "#B0122C"),
        R("juice|lemonade|iced tea", FoodGroup.Beverages, "juice", Kept.Fridge),
        R("sour cream|cream cheese|cottage cheese|ricotta", FoodGroup.DairyAndEggs, "tub", Kept.Fridge, "#F1F1EC"),
        R(@"milk\b|homo\b|homogeni[sz]ed|skim\b|cream\b|half.and.half|creamer|kefir|eggnog|buttermilk", FoodGroup.Beverages, "milk", Kept.Fridge),
        R(@"water\b", FoodGroup.Beverages, "bottle", Kept.Pantry, "#4FA3E0"),
        R("beer|lager|cider", FoodGroup.Beverages, "bottle", Kept.Fridge, "#8D5A1B"),
        R("wine", FoodGroup.Beverages, "wine", Kept.Pantry, "#7B1E3B"),
        R("white wine|ros[eé]", FoodGroup.Beverages, "wine", Kept.Fridge, "#E8D27A"),
        R("gatorade|powerade|sports drinks?|kombucha", FoodGroup.Beverages, "bottle", Kept.Fridge, "#26A69A"),
        R(@"coffee|espresso|\btea\b|hot chocolate|cocoa\b", FoodGroup.Beverages, "coffee", Kept.Cupboard),
        // Dairy and eggs
        R(@"eggs?\b", FoodGroup.DairyAndEggs, "eggs", Kept.Fridge),
        R("butter", FoodGroup.DairyAndEggs, "butter", Kept.Fridge),
        R("margarine|becel", FoodGroup.DairyAndEggs, "tub", Kept.Fridge, "#F4D35E"),
        R("yogh?urt|skyr", FoodGroup.DairyAndEggs, "yogurt", Kept.Fridge),
        R("cheese|cheddar|mozzarella|parmesan|feta|brie|gouda|swiss|havarti|provolone", FoodGroup.DairyAndEggs, "cheese", Kept.Fridge),
        // Meat and fish
        R(@"sliced turkey|sliced chicken|deli\b|ham\b|salami|bologna|pastrami|prosciutto|pepperoni|cold cuts|lunch meat", FoodGroup.MeatAndFish, "beef", Kept.Fridge, "#E58F8A"),
        R(@"chicken|turkey|wings?\b|drumsticks?|thighs?", FoodGroup.MeatAndFish, "chicken", Kept.Fridge),
        R("bacon", FoodGroup.MeatAndFish, "bacon", Kept.Fridge),
        R(@"chicken (?:strips|fingers|tenders|bites)|tenders\b|nuggets|popcorn chicken", FoodGroup.MeatAndFish, "strips", Kept.Freezer),
        R(@"ground (?:beef|pork|lamb|veal|chuck|round|sirloin)|lean ground|hamburger meat|mince\b|minced", FoodGroup.MeatAndFish, "groundbeef", Kept.Fridge),
        R(@"ground (?:turkey|chicken)", FoodGroup.MeatAndFish, "groundbeef", Kept.Fridge, "#E8A49A"),
        R(@"pork|chops?\b|ribs\b|tenderloin", FoodGroup.MeatAndFish, "beef", Kept.Fridge, "#E58F8A"),
        R("beef|steaks?|roast|brisket|veal|lamb|meatballs?", FoodGroup.MeatAndFish, "beef", Kept.Fridge),
        R("sausages?|bratwurst|kielbasa|chorizo", FoodGroup.MeatAndFish, "sausage", Kept.Fridge),
        R(@"hot ?dogs?|wieners?|franks\b|frankfurters?", FoodGroup.MeatAndFish, "hotdog", Kept.Fridge),
        R("burgers?|patt(?:y|ies)", FoodGroup.MeatAndFish, "burger", Kept.Fridge),
        R("salmon|trout", FoodGroup.MeatAndFish, "fish", Kept.Fridge, "#F08A5D"),
        R("fish|tuna|cod\b|haddock|tilapia|halibut|sardines?|scallops?|crab|lobster|seafood|fillets?", FoodGroup.MeatAndFish, "fish", Kept.Fridge),
        R("shrimp|prawns?", FoodGroup.MeatAndFish, "shrimp", Kept.Freezer),
        // Meals
        R("pizza", FoodGroup.Meals, "pizza", Kept.Fridge),
        R(@"lasagna\b|perogies|pierogi|dumplings|egg rolls|spring rolls|french fries|fries\b|fish sticks|burritos?|frozen dinners?|tv dinners?", FoodGroup.Meals, "noodles", Kept.Fridge, "#8D6E63"),
        R(@"leftovers?|casserole|stew\b|chili\b|curry\b|meal prep|lunch(?:es)?\b", FoodGroup.Meals, "container", Kept.Fridge),
        R(@"pot pies?|meat pies?|tourti[eè]re|shepherd.?s pie|chicken pies?|steak pies?|mince pies?", FoodGroup.Meals, "pie", Kept.Freezer, "#C68B4E"),
        R("soups?|broth|stock\b|bouillon", FoodGroup.DryGoods, "tin", Kept.Pantry, "#C62828"),
        // Bread and bakery
        R(@"bread\b|toast\b", FoodGroup.Bakery, "bread", Kept.Counter),
        R(@"loaf|loaves|(?:sourdough|rye|french|italian|crusty|artisan|pumpernickel|banana|raisin) bread|sourdough|ciabatta|brioche", FoodGroup.Bakery, "loaf", Kept.Counter),
        R(@"baguettes?", FoodGroup.Bakery, "baguette", Kept.Counter),
        R(@"bagels?", FoodGroup.Bakery, "bagel", Kept.Counter),
        R(@"buns?\b|rolls?\b|kaisers?", FoodGroup.Bakery, "bun", Kept.Counter),
        R(@"croissants?", FoodGroup.Bakery, "croissant", Kept.Counter),
        R(@"pitas?\b|naan|tortillas?|wraps?\b|flatbreads?", FoodGroup.Bakery, "tortilla", Kept.Counter),
        R(@"muffins?|cupcakes?|scones?", FoodGroup.Bakery, "muffin", Kept.Counter),
        R(@"donuts?|doughnuts?|timbits", FoodGroup.Bakery, "donut", Kept.Counter),
        R(@"pancakes?|waffles?|cr[eê]pes?|eggos?", FoodGroup.Bakery, "pancakes", Kept.Freezer),
        R(@"cakes?\b|cheesecakes?|brownies?", FoodGroup.Bakery, "cake", Kept.Counter),
        R(@"pies?\b|tarts?\b", FoodGroup.Bakery, "pie", Kept.Counter),
        // Dry goods
        R(@"spaghetti|macaroni|penne|lasagna noodles|fettuccine|linguine|rotini|fusilli|pasta|kraft dinner|\bkd\b|mac (?:and|&|n) cheese", FoodGroup.DryGoods, "box", Kept.Pantry, "#1E63B5"),
        R(@"noodles|ramen|udon|vermicelli", FoodGroup.DryGoods, "noodles", Kept.Pantry),
        R("cereal|granola|corn ?flakes|cheerios|muesli|bran flakes", FoodGroup.DryGoods, "box", Kept.Pantry, "#F2A93B"),
        R(@"oats|oatmeal|porridge", FoodGroup.DryGoods, "box", Kept.Pantry, "#C68B4E"),
        R(@"bread ?crumbs|crackers?|quinoa|couscous|barley|lentils|chick ?peas|beans\b|baking soda|baking powder|yeast|cornstarch|cake mix|pancake mix|cocoa powder|chocolate chips", FoodGroup.DryGoods, "bag", Kept.Pantry, "#E8DCC4"),
        R(@"flour|sugar|rice\b|cornmeal", FoodGroup.DryGoods, "sack", Kept.Pantry),
        R(@"oil\b|vinegar", FoodGroup.DryGoods, "bottle", Kept.Cupboard, "#D4A017"),
        // Snacks and sweets
        R(@"chips|crisps|doritos|tostitos|pringles|cheetos|ruffles|lays\b|pretzels", FoodGroup.Snacks, "bag", Kept.Pantry),
        R(@"popcorn", FoodGroup.Snacks, "popcorn", Kept.Pantry),
        R(@"cookies?|biscuits|oreos?|wafers|granola bars?|protein bars?|snack bars?", FoodGroup.Snacks, "cookie", Kept.Pantry),
        R(@"candy|gummies|licorice|jujubes|gum\b|marshmallows", FoodGroup.Snacks, "cookie", Kept.Pantry, "#5D4037"),
        R(@"chocolate|chocolate bars?|candy bars?|kit ?kats?|snickers|mars bars?|aero\b|coffee crisp|caramilk", FoodGroup.Snacks, "chocolate", Kept.Pantry),
        R(@"nuts\b|peanuts|almonds|cashews|walnuts|pecans|pistachios|seeds\b|raisins|dried fruit|trail mix", FoodGroup.Snacks, "bag", Kept.Pantry, "#A1887F"),
        // Fruit and vegetables
        R(@"green beans|peas\b|brussels sprouts|asparagus|mixed vegetables|vegetables|veggies|stir.?fry", FoodGroup.Produce, "leafy", Kept.Fridge, "#4E9A2E"),
        R(@"broccoli|broccolini", FoodGroup.Produce, "broccoli", Kept.Fridge),
        R(@"cauliflower", FoodGroup.Produce, "broccoli", Kept.Fridge, "#EDE4CC"),
        R(@"corn\b|corn on the cob|cobs? of corn", FoodGroup.Produce, "corn", Kept.Fridge),
        R(@"lettuce|spinach|kale|greens\b|salad\b|arugula|cabbage|romaine|chard|herbs\b|cilantro|parsley|celery|bok choy", FoodGroup.Produce, "leafy", Kept.Fridge),
        R(@"cucumbers?|zucchini|squash|eggplant", FoodGroup.Produce, "cucumber", Kept.Fridge),
        R(@"avocados?", FoodGroup.Produce, "avocado", Kept.Counter),
        R(@"carrots?|parsnips?|beets?|radish(?:es)?|turnips?", FoodGroup.Produce, "carrot", Kept.Fridge),
        R(@"tomato(?:es)?", FoodGroup.Produce, "tomato", Kept.Counter),
        R(@"peppers?\b|jalape[nñ]os?", FoodGroup.Produce, "pepper", Kept.Fridge),
        R(@"potato(?:es)?|yams?\b", FoodGroup.Produce, "potato", Kept.Pantry),
        R(@"garlic|shallots?", FoodGroup.Produce, "onion", Kept.Counter, "#E8DFCF"),
        R(@"onions?|leeks?|scallions?", FoodGroup.Produce, "onion", Kept.Pantry),
        R(@"mushrooms?", FoodGroup.Produce, "mushroom", Kept.Fridge),
        R(@"apples?\b|pears?\b|peach(?:es)?|plums?\b|nectarines?|mangos?|mangoes|kiwis?|fruit\b", FoodGroup.Produce, "apple", Kept.Fridge),
        R(@"bananas?|plantains?", FoodGroup.Produce, "banana", Kept.Counter),
        R(@"lemons?|limes?\b", FoodGroup.Produce, "citrus", Kept.Fridge),
        R(@"oranges?|clementines?|mandarins?|tangerines?|grapefruits?", FoodGroup.Produce, "citrus", Kept.Fridge, "#F57C00"),
        R(@"strawberr(?:y|ies)", FoodGroup.Produce, "strawberry", Kept.Fridge),
        R(@"raspberr(?:y|ies)|cherr(?:y|ies)", FoodGroup.Produce, "berries", Kept.Fridge, "#C62828"),
        R(@"grapes?\b", FoodGroup.Produce, "grapes", Kept.Fridge),
        R(@"berr(?:y|ies)|blueberr(?:y|ies)|blackberr(?:y|ies)", FoodGroup.Produce, "berries", Kept.Fridge),
        R(@"watermelons?", FoodGroup.Produce, "watermelon", Kept.Counter),
        R(@"melons?|cantaloupes?|honeydews?", FoodGroup.Produce, "watermelon", Kept.Counter, "#F5A25D"),
        R(@"pineapples?", FoodGroup.Produce, "apple", Kept.Counter, "#F2C230"),
    ];

    [GeneratedRegex(@"\b(?:canned|tinned|cans? of)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Canned();

    [GeneratedRegex(@"\bfrozen\b", RegexOptions.IgnoreCase)]
    private static partial Regex Frozen();

    [GeneratedRegex(@"\b(red|green|yellow|orange|purple)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ColorWord();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthesis();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, FoodLook> Guessed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What a food is taken to be from its name (and the catalog's word on where it's kept, for what it knows).</summary>
    public static FoodLook Guess(string name) => Guessed.GetOrAdd(name.Trim(), Read);

    private static FoodLook Read(string name)
    {
        var text = Parenthesis().Replace(name, " ").Trim();
        Rule? best = null;
        int bestEnd = -1, bestStart = 0;
        foreach (var rule in Rules)
            foreach (Match m in rule.Match.Matches(text))
            {
                var end = m.Index + m.Length;
                if (end > bestEnd || end == bestEnd && m.Index < bestStart)
                    (best, bestEnd, bestStart) = (rule, end, m.Index);
            }

        var look = best is null
            ? new FoodLook(FoodGroup.Other, "food", null, Kept.Pantry)
            : new FoodLook(best.Group, best.Icon, best.Color, best.Kept);

        // A pepper, an apple or an onion of a colour.
        if (look.Icon is "pepper" or "apple" or "onion" or "berries" or "grapes" && ColorWord().Match(text) is { Success: true } word)
            look = look with
            {
                Color = (word.Value.ToLowerInvariant(), look.Icon) switch
                {
                    ("red", "onion") => "#8E244D",
                    ("red", _) => "#D32F2F",
                    ("green", "apple") => "#8BC34A",
                    ("green", _) => "#43A047",
                    ("yellow", "onion") => "#D9A441",
                    ("yellow", _) => "#F2C230",
                    ("orange", _) => "#F57C00",
                    _ => "#6A2C91",
                },
            };
        if (Canned().IsMatch(text))
            look = look with { Group = look.Group is FoodGroup.Beverages ? look.Group : FoodGroup.DryGoods, Icon = look.Icon is "can" ? "can" : "tin", Kept = Kept.Pantry };
        if (Frozen().IsMatch(text))
            look = look with
            {
                Kept = Kept.Freezer,
                // Frozen vegetables and fruit come in a bag.
                Icon = look.Group == FoodGroup.Produce ? "bag" : look.Icon,
                Color = look.Group == FoodGroup.Produce ? look.Color ?? "#43A047" : look.Color,
            };
        // The catalog knows where what it has goes, unless it's the cupboard: which one is the rule's to say.
        else if (GroceryCatalog.Get(name) is { Kept: not Kept.Cupboard } info)
            look = look with { Kept = info.Kept };
        return look;
    }
}
