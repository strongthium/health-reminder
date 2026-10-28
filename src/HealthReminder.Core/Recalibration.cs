namespace Strongthium.HealthReminder;

/// <summary>How well a step is backed by studies: <c>Strong</c>, several good studies; <c>Some</c>, a few or surveys,
/// or a well-known reason with no trial of its effect on cravings; <c>Early</c>, a small trial or mixed results.</summary>
public enum EvidenceLevel { Strong, Some, Early }

/// <summary>Something smoking does to the body besides the habit, and what follows from it.</summary>
public sealed record BodyEffect(string Title, string What);

/// <summary>A change to eating, drinking or moving that works against one of the ways smoking acts on the body.</summary>
/// <param name="Id">Kept with the date it was started; never changes.</param>
/// <param name="Evidence">What the studies show <paramref name="Why"/> it helps, said plainly in <paramref name="Studies"/>.</param>
public sealed record RecalibrationStep(string Id, string Title, string Do, string Why, EvidenceLevel Evidence, string Studies, string Caution = "");

/// <summary>
/// Recalibrating the body while cutting down: one change at a time, a week apart, each working against a way smoking
/// acts on the body (hunger it hides, nutrients it uses up, caffeine it clears, the taste it depends on, the low mood of
/// the enzymes it blocks), rather than only replacing the nicotine. Each step says how strong its evidence is: some
/// are well studied, some rest on one small trial, and none is a cure on its own.
/// </summary>
public static class Recalibration
{
    /// <summary>How long to give a step before the next.</summary>
    public static readonly TimeSpan StepApart = TimeSpan.FromDays(7);

    /// <summary>What cigarette smoke does in the body that a craving can come from, beyond the habit.</summary>
    public static readonly IReadOnlyList<BodyEffect> Effects =
    [
        new("More than nicotine",
            "Smoke also has harman and norharman, which block the enzymes (MAO-A and MAO-B) that break down dopamine and serotonin, " +
            "so a smoker's brain runs with more of both. Brain scans show the enzymes about a third lower in smokers; they come back over " +
            "the weeks after stopping. Part of the low mood of cutting down is that recovery, and it passes. Acetaldehyde in smoke is " +
            "being studied for making nicotine more rewarding. Formaldehyde is harmful but isn't one of the addictive ones."),
        new("It hides hunger",
            "Nicotine dulls appetite and releases sugar into the blood. Cutting down brings hunger back, and a dip in blood sugar can " +
            "feel like a craving."),
        new("It uses up vitamin C, and lowers others",
            "Smoke's oxidants use up vitamin C: smokers are advised 35 mg a day more than non-smokers. Smokers also tend to have less " +
            "folate, vitamin B6, B12 and E in the blood. Restoring them is good for the body; no study has shown that a shortage is " +
            "what makes someone smoke."),
        new("It speeds up caffeine",
            "Smoke makes the liver clear caffeine about twice as fast. Cut down and keep the same coffee, and caffeine builds up: " +
            "jitters, a racing heart and poor sleep that feel like withdrawal."),
        new("Some food and drink make it taste better",
            "Smokers say coffee, alcohol and meat make a cigarette taste better, and milk, water, fruit and vegetables make it taste worse."),
    ];

    /// <summary>The steps, in the order to take them: the best backed and simplest first.</summary>
    public static readonly IReadOnlyList<RecalibrationStep> Steps =
    [
        new("regular-meals", "Eat before you get hungry",
            "Breakfast, lunch and dinner at set times, with protein in each (eggs, yogurt, meat, fish, beans), and a snack between if " +
            "it's over four hours. The Eat & drink page can remind you.",
            "Nicotine hid hunger; with less of it, an empty stomach or a dip in blood sugar can feel like a craving.",
            EvidenceLevel.Some, "Nicotine's effect on appetite and blood sugar is well known; that eating regularly eases cravings rests on smaller studies."),
        new("caffeine", "Halve the coffee as you cut down",
            "For each cigarette a day you stop, drink a bit less coffee, tea or energy drink: about half as much once you're down by half. " +
            "None after mid-afternoon.",
            "Smoke made your liver clear caffeine twice as fast; without it, the same coffee builds up into jitters and poor sleep that feel like withdrawal. " +
            "Coffee also makes a cigarette taste better.",
            EvidenceLevel.Strong, "That smoking makes caffeine clear up to twice as fast, and that this goes back to normal within about a week of stopping, is well established."),
        new("taste", "Swap what goes with a cigarette",
            "After a meal or with a break, have a glass of milk, water, or a piece of fruit or vegetable instead of coffee or alcohol.",
            "Smokers say milk, water, fruit and vegetables make a cigarette taste worse, and coffee, alcohol and meat make it taste better.",
            EvidenceLevel.Some, "A Duke University survey of smokers (2007); it hasn't been tested in a trial."),
        new("vitamin-c", "Vitamin C every day",
            "Two servings of fruit or vegetables rich in it: an orange, a kiwi, a cup of strawberries, a red or green pepper, broccoli. " +
            "Food first; a 250 to 500 mg tablet if you can't.",
            "Smoke uses vitamin C up: smokers are advised 35 mg a day more than others, and most have less in their blood.",
            EvidenceLevel.Some, "Smokers' lower vitamin C, and the extra 35 mg a day, are well established; that it eases cravings has not been shown."),
        new("move", "Move when a craving comes",
            "A brisk walk, stairs or a few minutes of exercise when the urge comes; it passes in about 10 minutes.",
            "Exercise lifts dopamine and mood, a little of what the smoke was doing, without it.",
            EvidenceLevel.Strong, "Several trials found a short bout of exercise cuts a craving and withdrawal symptoms for a while afterwards."),
        new("b-vitamins", "Leafy greens, beans and whole grains",
            "Spinach, lettuce, broccoli, beans, lentils, eggs and whole-grain bread most days.",
            "Smokers have less folate, vitamin B6 and B12 in their blood; these foods bring them back.",
            EvidenceLevel.Some, "Smokers' lower B vitamins are well established; that restoring them eases cravings has not been shown."),
        new("omega-3", "Fish twice a week",
            "Salmon, sardines, mackerel or trout twice a week; or, after asking your doctor or pharmacist, a fish oil supplement.",
            "Omega-3 fats are part of brain cell membranes and of how dopamine signals; smokers tend to have less of them.",
            EvidenceLevel.Early, "One small trial (Israel, 2014): 2.7 g of omega-3 a day for a month cut cravings and cigarettes a day, against a placebo. It needs repeating.",
            "Fish oil can add to the effect of blood thinners such as warfarin."),
        new("sweet", "Something sweet for a sudden craving",
            "A piece of fruit, or a glucose tablet, when a strong craving comes.",
            "A quick rise in blood sugar answers the dip that can feel like a craving.",
            EvidenceLevel.Early, "Small trials of glucose tablets had mixed results: some found less craving, others no difference.",
            "With diabetes, ask your care team first."),
    ];

    /// <summary>What not to do, whatever the step.</summary>
    public const string Caution =
        "Don't take beta-carotene or vitamin A supplements: in two large trials they raised the risk of lung cancer in smokers. " +
        "Food first; ask your doctor or pharmacist before any supplement, since some change how medication works.";

    /// <summary>The next step to take: the first not started, once the last one started has had its week (or now, if
    /// none has been). Null when all have been started.</summary>
    public static (RecalibrationStep Step, DateOnly From)? Next(IReadOnlyDictionary<string, DateOnly> started, DateOnly today)
    {
        var next = Steps.FirstOrDefault(s => !started.ContainsKey(s.Id));
        if (next is null) return null;
        var from = started.Count == 0 ? today : DateOnly.FromDateTime(started.Values.Max().ToDateTime(TimeOnly.MinValue) + StepApart);
        return (next, from < today ? today : from);
    }
}
