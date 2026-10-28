namespace Strongthium.HealthReminder;

/// <summary>What someone may feel like eating; a recipe has a few.</summary>
public enum Feel { Beef, Chicken, Fish, Pork, Pasta, Eggs, Light, Comfort, Spicy, NoMeat, Quick }

/// <summary>Something a recipe needs: "2", "eggs"; <paramref name="Use"/> is how much, in the grocery's unit (see
/// <see cref="GroceryCatalog"/>); <paramref name="Frozen"/> when it's kept in the freezer and has to
/// thaw first. <paramref name="Grocery"/> is what goes on the grocery list.</summary>
public sealed record FoodItem(string Amount, string Name, string Grocery, double Use = 0, bool Frozen = false)
{
    public override string ToString() => Amount.Length == 0 ? Name : $"{Amount} {Name}";
}

/// <summary>A step of a recipe; <paramref name="Minutes"/> above zero gets a timer.</summary>
public sealed record CookStep(string Text, int Minutes = 0);

/// <summary>
/// A meal for one: about <paramref name="Kcal"/>, <paramref name="PrepMinutes"/> of getting ready and
/// <paramref name="CookMinutes"/> of cooking, and, when an ingredient is frozen, <paramref name="ThawHours"/> to thaw
/// in the fridge (a single portion; in cold water it's about an hour).
/// </summary>
public sealed record Recipe(string Id, string Name, MealSlot[] Slots, int Kcal, int PrepMinutes, int CookMinutes, int ThawHours,
    Feel[] Feels, FoodItem[] Ingredients, CookStep[] Steps)
{
    public IEnumerable<FoodItem> Frozen => Ingredients.Where(i => i.Frozen);
    public TimeSpan Time => TimeSpan.FromMinutes(PrepMinutes + CookMinutes);
}

/// <summary>
/// Plain recipes for one person cooking for themself, from everyday groceries, with the cooking temperatures Health
/// Canada and the USDA give for safety: poultry 74 °C (165 °F), ground meat 71 °C (160 °F), whole cuts of beef and pork
/// and fish 63 °C (145 °F), with a rest for beef and pork. Calories are typical values, rounded.
/// </summary>
public static class Recipes
{
    private static readonly MealSlot[] B = [MealSlot.Breakfast], L = [MealSlot.Lunch], D = [MealSlot.Dinner], LD = [MealSlot.Lunch, MealSlot.Dinner];

    public static readonly IReadOnlyList<Recipe> All =
    [
        // Breakfast
        new("scrambled-eggs", "Scrambled eggs on toast", B, 380, 3, 5, 0, [Feel.Eggs, Feel.Quick, Feel.Comfort],
            [new("2", "eggs", "Eggs", 2), new("1 tbsp", "milk", "Milk", 15), new("1 tsp", "butter", "Butter", 5), new("2 slices", "bread", "Bread", 2)],
            [new("Beat the eggs with the milk and a pinch of salt and pepper."), new("Put the bread in the toaster."),
             new("Melt the butter in a non-stick pan over medium-low heat."),
             new("Pour in the eggs and stir gently until just set, no runny egg left.", 3), new("Serve on the toast.")]),
        new("oatmeal", "Oatmeal with banana and cinnamon", B, 380, 2, 5, 0, [Feel.Light, Feel.Quick, Feel.NoMeat, Feel.Comfort],
            [new("½ cup", "rolled oats", "Oats", 40), new("1 cup", "milk", "Milk", 250), new("1", "banana", "Bananas", 1), new("", "cinnamon", "Cinnamon", 0)],
            [new("Put the oats and milk in a small pot."), new("Simmer, stirring now and then, until thick (or microwave 2 to 3 minutes, stirring halfway).", 5),
             new("Slice the banana on top and sprinkle with cinnamon.")]),
        new("yogurt-berries", "Yogurt with granola and berries", B, 330, 3, 0, 8, [Feel.Light, Feel.Quick, Feel.NoMeat],
            [new("¾ cup", "plain yogurt", "Yogurt", 175), new("⅓ cup", "granola", "Granola", 40), new("½ cup", "frozen berries", "Frozen berries", 75, Frozen: true)],
            [new("Spoon the yogurt into a bowl."), new("Top with the thawed berries and their juice, then the granola.")]),
        new("pb-toast", "Peanut butter and banana toast", B, 450, 3, 3, 0, [Feel.Quick, Feel.NoMeat, Feel.Comfort],
            [new("2 slices", "bread", "Bread", 2), new("2 tbsp", "peanut butter", "Peanut butter", 0), new("1", "banana", "Bananas", 1)],
            [new("Toast the bread.", 3), new("Spread with the peanut butter and top with the sliced banana.")]),
        new("omelette", "Vegetable omelette with toast", B, 400, 5, 6, 0, [Feel.Eggs, Feel.NoMeat],
            [new("2", "eggs", "Eggs", 2), new("¼", "red pepper", "Red peppers", 0.25), new("a handful of", "spinach", "Spinach", 30),
             new("2 tbsp", "grated cheese", "Cheese", 15), new("1 tsp", "butter", "Butter", 5), new("1 slice", "bread", "Bread", 1)],
            [new("Dice the pepper; beat the eggs with a pinch of salt."), new("Melt the butter in a non-stick pan over medium heat and soften the pepper and spinach.", 2),
             new("Pour in the eggs; when the edges set, lift them so the rest runs under.", 3), new("Sprinkle the cheese on one half, fold, and cook until set through.", 1),
             new("Toast the bread and serve.")]),

        // Lunch
        new("turkey-sandwich", "Turkey and cheese sandwich with an apple", L, 430, 5, 0, 0, [Feel.Quick, Feel.Light],
            [new("2 slices", "bread", "Bread", 2), new("60 g (2 oz)", "sliced turkey", "Sliced turkey", 60), new("1 slice", "cheese", "Cheese slices", 1),
             new("", "lettuce and mustard", "Lettuce", 0.1), new("1", "apple", "Apples", 1)],
            [new("Spread mustard on the bread."), new("Layer the turkey, cheese and lettuce; close and cut in half."), new("Wash the apple.")]),
        new("tuna-sandwich", "Tuna sandwich and carrots", L, 410, 5, 0, 0, [Feel.Fish, Feel.Quick],
            [new("1 small can (120 g)", "tuna", "Canned tuna", 1), new("1 tbsp", "mayonnaise", "Mayonnaise", 0), new("2 slices", "bread", "Bread", 2),
             new("a handful of", "baby carrots", "Baby carrots", 85)],
            [new("Drain the tuna and mix it with the mayonnaise and a pinch of pepper."), new("Spread on the bread and close the sandwich."), new("Serve with the carrots.")]),
        new("quesadilla", "Bean and cheese quesadilla", L, 420, 5, 6, 0, [Feel.NoMeat, Feel.Comfort, Feel.Quick, Feel.Spicy],
            [new("1 large", "tortilla", "Tortillas", 1), new("½ cup", "canned black beans", "Canned beans", 0.33), new("¼ cup", "grated cheese", "Cheese", 28),
             new("2 tbsp", "salsa", "Salsa", 0)],
            [new("Rinse and drain the beans; mash them lightly with a fork."), new("Spread the beans and cheese on half the tortilla and fold it over."),
             new("Cook in a dry pan over medium heat until golden.", 3), new("Turn it over and cook the other side.", 3), new("Cut in wedges and serve with the salsa.")]),
        new("grilled-cheese", "Grilled cheese and tomato soup", L, 480, 3, 8, 0, [Feel.Comfort, Feel.NoMeat, Feel.Quick],
            [new("2 slices", "bread", "Bread", 2), new("2 slices", "cheese", "Cheese slices", 2), new("1 tsp", "butter", "Butter", 5), new("1 cup", "tomato soup", "Tomato soup", 0.5)],
            [new("Heat the soup in a small pot over low heat, stirring now and then.", 6), new("Butter the outsides of the bread and put the cheese between."),
             new("Cook in a pan over medium heat until golden.", 3), new("Turn it over and cook the other side.", 3)]),
        new("egg-fried-rice", "Egg fried rice", L, 450, 5, 8, 0, [Feel.Eggs, Feel.Quick, Feel.NoMeat],
            [new("1 cup", "cooked rice (cold, from the day before)", "Rice", 65), new("2", "eggs", "Eggs", 2), new("½ cup", "frozen peas and carrots", "Frozen mixed vegetables", 70),
             new("1 tbsp", "soy sauce", "Soy sauce", 0), new("1 tsp", "oil", "Cooking oil", 0)],
            [new("Heat the oil in a pan over medium-high heat and cook the frozen vegetables.", 3), new("Push them aside, scramble the eggs in the space.", 2),
             new("Add the rice and soy sauce and stir-fry until hot all through.", 3)]),
        new("chicken-salad", "Chicken salad", LD, 450, 10, 15, 12, [Feel.Chicken, Feel.Light],
            [new("1", "chicken breast", "Chicken breasts", 1, Frozen: true), new("2 cups", "salad greens", "Salad greens", 60), new("½", "cucumber", "Cucumbers", 0.5),
             new("1", "tomato", "Tomatoes", 1), new("2 tbsp", "salad dressing", "Salad dressing", 0), new("1", "dinner roll", "Dinner rolls", 1)],
            [new("Season the chicken with salt and pepper."), new("Cook in a lightly oiled pan over medium heat.", 7),
             new("Turn it over and cook until 74 °C (165 °F) in the thickest part, no pink.", 7), new("Let it rest, then slice.", 5),
             new("Chop the cucumber and tomato; toss with the greens and dressing."), new("Top with the chicken; serve with the roll.")]),

        // Dinner
        new("chicken-rice", "Chicken, rice and broccoli", D, 500, 10, 25, 12, [Feel.Chicken, Feel.Light],
            [new("1", "chicken breast", "Chicken breasts", 1, Frozen: true), new("½ cup", "rice", "Rice", 95), new("1 cup", "frozen broccoli", "Frozen broccoli", 90),
             new("1 tsp", "oil", "Cooking oil", 0), new("", "salt, pepper and paprika", "Paprika", 0)],
            [new("Rinse the rice; bring 1 cup of water to a boil, add the rice, cover and simmer on low.", 18),
             new("Season the chicken with salt, pepper and paprika."), new("Heat the oil in a pan over medium heat and cook the chicken.", 7),
             new("Turn it over and cook until 74 °C (165 °F) in the thickest part, no pink.", 7), new("Let the chicken rest.", 5),
             new("Microwave the broccoli with a splash of water, covered.", 4), new("Fluff the rice and serve.")]),
        new("steak", "Steak, baked potato and green beans", D, 590, 10, 25, 12, [Feel.Beef, Feel.Comfort],
            [new("1 (170 g, 6 oz)", "sirloin steak", "Steak", 1, Frozen: true), new("1", "potato", "Potatoes", 1), new("1 cup", "frozen green beans", "Frozen green beans", 125),
             new("1 tsp", "oil", "Cooking oil", 0), new("1 tsp", "butter", "Butter", 5)],
            [new("Prick the potato with a fork and microwave it, turning halfway, until soft.", 9),
             new("Pat the steak dry and season it well with salt and pepper."), new("Heat the oil in a pan until very hot and sear the steak.", 4),
             new("Turn it over and cook to at least 63 °C (145 °F) inside for medium-rare; longer for more done.", 4), new("Let the steak rest.", 3),
             new("Microwave the green beans with a splash of water, covered.", 4), new("Split the potato and top it with the butter.")]),
        new("salmon", "Salmon, potato and green beans", D, 490, 5, 20, 10, [Feel.Fish, Feel.Light],
            [new("1 (120 g, 4 oz)", "salmon fillet", "Salmon fillets", 1, Frozen: true), new("1", "potato", "Potatoes", 1), new("1 cup", "frozen green beans", "Frozen green beans", 125),
             new("1 tsp", "oil", "Cooking oil", 0), new("½", "lemon", "Lemons", 0.5)],
            [new("Heat the oven to 200 °C (400 °F)."), new("Prick the potato and microwave it, turning halfway, until soft.", 9),
             new("Put the salmon on a lined tray, brush with the oil, season, and bake until it flakes and reaches 63 °C (145 °F).", 13),
             new("Microwave the green beans with a splash of water, covered.", 4), new("Squeeze the lemon over the salmon and serve.")]),
        new("spaghetti", "Spaghetti with meat sauce", D, 600, 5, 20, 12, [Feel.Beef, Feel.Pasta, Feel.Comfort],
            [new("100 g (¼ lb)", "ground beef", "Ground beef", 100, Frozen: true), new("85 g (3 oz)", "spaghetti", "Pasta", 85), new("½ cup", "pasta sauce", "Pasta sauce", 125),
             new("", "a side salad", "Salad greens", 40)],
            [new("Bring a pot of salted water to a boil."), new("Brown the beef in a pan over medium-high heat, breaking it up, until no pink is left (71 °C, 160 °F).", 7),
             new("Add the sauce and simmer on low.", 10), new("Cook the spaghetti in the boiling water.", 10), new("Drain the pasta, top with the sauce, and serve with the salad.")]),
        new("pork-chop", "Pork chop, mashed potatoes and vegetables", D, 560, 10, 25, 12, [Feel.Pork, Feel.Comfort],
            [new("1", "pork chop", "Pork chops", 1, Frozen: true), new("1", "potato", "Potatoes", 1), new("2 tbsp", "milk", "Milk", 30), new("1 tsp", "butter", "Butter", 5),
             new("1 cup", "frozen mixed vegetables", "Frozen mixed vegetables", 140), new("1 tsp", "oil", "Cooking oil", 0)],
            [new("Peel and cube the potato; boil until soft.", 15), new("Season the pork chop with salt and pepper."),
             new("Heat the oil in a pan over medium-high heat and cook the chop.", 5), new("Turn it over and cook to 63 °C (145 °F).", 5), new("Let it rest.", 3),
             new("Microwave the vegetables with a splash of water, covered.", 4), new("Drain and mash the potato with the milk and butter.")]),
        new("stir-fry", "Chicken stir-fry with rice", D, 580, 10, 20, 12, [Feel.Chicken, Feel.Quick, Feel.Spicy],
            [new("1", "chicken breast", "Chicken breasts", 1, Frozen: true), new("½ cup", "rice", "Rice", 95), new("1½ cups", "frozen stir-fry vegetables", "Frozen mixed vegetables", 210),
             new("1 tbsp", "soy sauce", "Soy sauce", 0), new("1 tsp", "oil", "Cooking oil", 0), new("", "chili flakes, if you like", "Chili flakes", 0)],
            [new("Rinse the rice; bring 1 cup of water to a boil, add the rice, cover and simmer on low.", 18), new("Cut the chicken into thin strips."),
             new("Heat the oil in a pan over high heat and stir-fry the chicken until no pink is left (74 °C, 165 °F).", 6),
             new("Add the vegetables and stir-fry until hot.", 4), new("Stir in the soy sauce and chili flakes and serve over the rice.")]),
        new("chili", "Quick chili", D, 520, 5, 25, 12, [Feel.Beef, Feel.Comfort, Feel.Spicy],
            [new("100 g (¼ lb)", "ground beef", "Ground beef", 100, Frozen: true), new("½ can", "kidney beans", "Canned beans", 0.5), new("½ can", "diced tomatoes", "Canned tomatoes", 0.5),
             new("1 tsp", "chili powder", "Chili powder", 0), new("¼", "onion", "Onions", 0.25)],
            [new("Chop the onion."), new("Brown the beef and onion in a pot, breaking up the beef, until no pink is left (71 °C, 160 °F).", 7),
             new("Add the beans (drained), tomatoes and chili powder; simmer on low, stirring now and then.", 15), new("Season with salt to taste.")]),
        new("burger", "Burger with a side salad", D, 520, 5, 12, 0, [Feel.Beef, Feel.Comfort, Feel.Quick],
            [new("1", "frozen hamburger patty", "Hamburger patties", 1), new("1", "bun", "Hamburger buns", 1), new("", "lettuce, tomato, pickle", "Lettuce", 0.1),
             new("", "a side salad", "Salad greens", 40)],
            [new("Heat a pan over medium heat; the patty cooks from frozen."), new("Cook the patty.", 6), new("Turn it over and cook until 71 °C (160 °F) inside, no pink.", 6),
             new("Toast the bun in the pan for the last minute."), new("Build the burger and serve with the salad.")]),
        new("pasta-veg", "Pasta with tomato sauce and vegetables", D, 520, 5, 15, 0, [Feel.Pasta, Feel.NoMeat, Feel.Quick],
            [new("85 g (3 oz)", "pasta", "Pasta", 85), new("½ cup", "pasta sauce", "Pasta sauce", 125), new("1 cup", "frozen mixed vegetables", "Frozen mixed vegetables", 140),
             new("2 tbsp", "grated cheese", "Cheese", 15)],
            [new("Bring a pot of salted water to a boil and cook the pasta.", 10), new("Add the frozen vegetables to the pasta for the last 3 minutes."),
             new("Warm the sauce in a small pot.", 3), new("Drain, toss with the sauce and top with the cheese.")]),
        new("sausage-tray", "Sausages and vegetables from the oven", D, 600, 10, 30, 12, [Feel.Pork, Feel.Comfort],
            [new("2", "sausages", "Sausages", 2, Frozen: true), new("1", "potato", "Potatoes", 1), new("½", "red pepper", "Red peppers", 0.5), new("½", "onion", "Onions", 0.5),
             new("1 tsp", "oil", "Cooking oil", 0)],
            [new("Heat the oven to 200 °C (400 °F)."), new("Cut the potato, pepper and onion into chunks and toss with the oil, salt and pepper on a tray."),
             new("Roast the vegetables.", 10), new("Add the sausages and roast, turning once, until 71 °C (160 °F) inside.", 20)]),
        new("bean-bowl", "Black bean and rice bowl", D, 520, 5, 10, 0, [Feel.NoMeat, Feel.Spicy, Feel.Quick, Feel.Light],
            [new("1 cup", "cooked rice", "Rice", 65), new("½ can", "black beans", "Canned beans", 0.5), new("½ cup", "frozen corn", "Frozen corn", 80),
             new("2 tbsp", "salsa", "Salsa", 0), new("2 tbsp", "grated cheese", "Cheese", 15)],
            [new("Rinse and drain the beans."), new("Heat the rice, beans and corn together, covered, in the microwave, stirring halfway.", 4),
             new("Top with the salsa and cheese.")]),
    ];

    public static Recipe? Get(string? id) => id is null ? null : All.FirstOrDefault(r => r.Id == id);

    /// <summary>The recipes for a meal, those someone feels like first (all, when <paramref name="feel"/> is null),
    /// and among those, the closest to the meal's calories.</summary>
    public static IEnumerable<Recipe> For(MealSlot slot, Feel? feel = null, double kcal = 0) =>
        All.Where(r => r.Slots.Contains(slot) && (feel is null || r.Feels.Contains(feel.Value)))
            .OrderBy(r => kcal > 0 ? Math.Abs(r.Kcal - kcal) : 0);

    public static string Label(this Feel feel) => feel switch
    {
        Feel.Beef => "Steak or beef",
        Feel.Chicken => "Chicken",
        Feel.Fish => "Fish",
        Feel.Pork => "Pork",
        Feel.Pasta => "Pasta",
        Feel.Eggs => "Eggs",
        Feel.Light => "Something light",
        Feel.Comfort => "Comfort food",
        Feel.Spicy => "Something spicy",
        Feel.NoMeat => "No meat",
        _ => "Quick",
    };
}
