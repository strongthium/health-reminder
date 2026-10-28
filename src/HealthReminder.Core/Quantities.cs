using System.Globalization;
using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>What a quantity measures: liquids by volume, dry food by weight, the rest by count.</summary>
public enum Dimension { Count, Volume, Mass }

/// <summary>An amount in the dimension's base unit: millilitres, grams, or a count.</summary>
public readonly record struct Quantity(double Amount, Dimension Dimension);

/// <summary>How amounts are shown: metric (mL, L, g, kg) or U.S. (fl oz, gal, oz, lb).</summary>
public enum MeasureSystem { Metric, Us }

/// <summary>What a drink is measured in when it's drunk.</summary>
public enum DrinkUnit { Millilitre, FluidOunce, Cup }

/// <summary>
/// Reads an amount however it's written ("2 L", "1 gallon", "500g", "1½ cups", "2 x 355 mL", "1 lb", "12", "a dozen")
/// and shows every liquid in one unit and every weight in another, so that amounts compare. U.S. measures throughout:
/// a cup is 236.6 mL and a gallon 3.785 L; "imperial gallon" and "imperial pint" are the British ones.
/// </summary>
public static partial class Quantities
{
    public const double MlPerFluidOunce = 29.5735295625, MlPerCup = 236.5882365, MlPerGallon = 3785.411784, GramsPerOunce = 28.349523125, GramsPerPound = 453.59237;

    private static readonly (string[] Names, Dimension Dimension, double Factor)[] Units =
    [
        (["ml", "millilitre", "milliliter", "cc"], Dimension.Volume, 1),
        (["cl", "centilitre", "centiliter"], Dimension.Volume, 10),
        (["dl", "decilitre", "deciliter"], Dimension.Volume, 100),
        (["l", "litre", "liter", "ltr"], Dimension.Volume, 1000),
        (["tsp", "teaspoon"], Dimension.Volume, 4.92892159375),
        (["tbsp", "tablespoon"], Dimension.Volume, 14.78676478125),
        (["fl oz", "floz", "fluid ounce", "fl. oz", "fl.oz"], Dimension.Volume, MlPerFluidOunce),
        (["cup", "c"], Dimension.Volume, MlPerCup),
        (["imperial pint", "imp pint", "imp pt"], Dimension.Volume, 568.26125),
        (["pt", "pint"], Dimension.Volume, 473.176473),
        (["qt", "quart"], Dimension.Volume, 946.352946),
        (["imperial gallon", "imp gal", "imp gallon"], Dimension.Volume, 4546.09),
        (["gal", "gallon"], Dimension.Volume, MlPerGallon),
        (["mg", "milligram"], Dimension.Mass, 0.001),
        (["g", "gram", "gr", "gm"], Dimension.Mass, 1),
        (["kg", "kilogram", "kilo"], Dimension.Mass, 1000),
        (["oz", "ounce"], Dimension.Mass, GramsPerOunce),
        (["lb", "pound", "lbs"], Dimension.Mass, GramsPerPound),
        (["dozen", "doz"], Dimension.Count, 12),
        (["", "x", "each", "ea", "item", "pc", "pcs", "piece", "pack", "package", "box", "bag", "can", "tin", "jar", "bottle", "carton", "loaf", "loaves", "bunch"], Dimension.Count, 1),
    ];

    [GeneratedRegex(@"^\s*(?:(?<times>\d+)\s*[x×*]\s*)?(?<number>\d+(?:[.,]\d+)?(?:\s+\d+/\d+|\s*[½¼¾⅓⅔])?|\d+/\d+|[½¼¾⅓⅔]|an?\b)\s*(?<unit>.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityPattern();

    /// <summary>The amount written, in mL, g or a count; null when it isn't one. A word that isn't a unit ("2 loaves
    /// of bread", "3 avocados") counts.</summary>
    public static Quantity? Parse(string text)
    {
        var match = QuantityPattern().Match(text ?? "");
        if (!match.Success || Number(match.Groups["number"].Value) is not { } number) return null;
        var times = match.Groups["times"].Success ? int.Parse(match.Groups["times"].Value, CultureInfo.InvariantCulture) : 1;
        var unit = match.Groups["unit"].Value.Trim().TrimEnd('.').ToLowerInvariant();
        // "of milk", "bottles of water": the unit is the first words, before "of".
        var of = unit.IndexOf(" of ", StringComparison.Ordinal);
        if (of >= 0) unit = unit[..of];
        foreach (var (names, dimension, factor) in Units)
            if (names.Any(name => Matches(unit, name)))
                return new Quantity(times * number * factor, dimension);
        return new Quantity(times * number, Dimension.Count);
    }

    private static bool Matches(string unit, string name) =>
        unit == name || unit == name + "s" || unit == name + "es" || unit.StartsWith(name + " ", StringComparison.Ordinal) && name.Length > 0 && !name.Contains(' ') && name.Length > 2;

    private static double? Number(string text)
    {
        text = text.Trim().ToLowerInvariant();
        if (text is "a" or "an") return 1;
        double Fraction(char c) => c switch { '½' => 0.5, '¼' => 0.25, '¾' => 0.75, '⅓' => 1 / 3.0, '⅔' => 2 / 3.0, _ => 0 };
        if (text.Length == 1 && Fraction(text[0]) > 0) return Fraction(text[0]);
        if (text.Length > 1 && Fraction(text[^1]) > 0)
            return double.TryParse(text[..^1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w + Fraction(text[^1]) : null;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double total = 0;
        foreach (var part in parts)
        {
            var slash = part.Split('/');
            if (slash.Length == 2 && double.TryParse(slash[0], CultureInfo.InvariantCulture, out var a) && double.TryParse(slash[1], CultureInfo.InvariantCulture, out var b) && b != 0)
                total += a / b;
            else if (double.TryParse(part.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                total += n;
            else
                return null;
        }
        return total > 0 ? total : null;
    }

    /// <summary>"1.5 L", "750 mL", "2 kg", "350 g", "1.5 gal", "64 fl oz", "2 lb", "12 oz", "12".</summary>
    public static string Format(Quantity quantity, MeasureSystem system) => (quantity.Dimension, system) switch
    {
        (Dimension.Volume, MeasureSystem.Metric) => quantity.Amount >= 1000 ? $"{Number(quantity.Amount / 1000, 2)} L" : $"{Number(quantity.Amount, 0)} mL",
        (Dimension.Volume, _) => quantity.Amount >= MlPerGallon ? $"{Number(quantity.Amount / MlPerGallon, 2)} gal" : $"{Number(quantity.Amount / MlPerFluidOunce, 1)} fl oz",
        (Dimension.Mass, MeasureSystem.Metric) => quantity.Amount >= 1000 ? $"{Number(quantity.Amount / 1000, 2)} kg" : $"{Number(quantity.Amount, 0)} g",
        (Dimension.Mass, _) => quantity.Amount >= GramsPerPound ? $"{Number(quantity.Amount / GramsPerPound, 2)} lb" : $"{Number(quantity.Amount / GramsPerOunce, 1)} oz",
        _ => Number(quantity.Amount, 2),
    };

    /// <summary>What prices are compared per: a litre or a kilogram, a gallon or a pound, or an item.</summary>
    public static (double Amount, string Label) PriceUnit(Dimension dimension, MeasureSystem system) => (dimension, system) switch
    {
        (Dimension.Volume, MeasureSystem.Metric) => (1000, "L"),
        (Dimension.Volume, _) => (MlPerGallon, "gal"),
        (Dimension.Mass, MeasureSystem.Metric) => (1000, "kg"),
        (Dimension.Mass, _) => (GramsPerPound, "lb"),
        _ => (1, "each"),
    };

    /// <summary>The price per litre, kilogram, gallon, pound or item; null without an amount.</summary>
    public static double? UnitPrice(double price, Quantity quantity, MeasureSystem system) =>
        quantity.Amount > 0 ? price / quantity.Amount * PriceUnit(quantity.Dimension, system).Amount : null;

    /// <summary>The drink unit's name, for a heading: "mL", "fl oz", "cups".</summary>
    public static string Label(this DrinkUnit unit) => unit switch
    {
        DrinkUnit.FluidOunce => "fl oz",
        DrinkUnit.Cup => "cup",
        _ => "mL",
    };

    public static double MlPer(DrinkUnit unit) => unit switch
    {
        DrinkUnit.FluidOunce => MlPerFluidOunce,
        DrinkUnit.Cup => MlPerCup,
        _ => 1,
    };

    /// <summary>Millilitres in the drink unit, rounded as it's measured: 10 mL, ½ fl oz, ¼ cup.</summary>
    public static double FromMl(double ml, DrinkUnit unit) => unit switch
    {
        DrinkUnit.FluidOunce => Math.Round(ml / MlPerFluidOunce * 2) / 2,
        DrinkUnit.Cup => Math.Round(ml / MlPerCup * 4) / 4,
        _ => Math.Round(ml / 10) * 10,
    };

    public static double ToMl(double amount, DrinkUnit unit) => amount * MlPer(unit);

    /// <summary>"250 mL", "8½ fl oz", "1 cup", "2.5 L" (from 1 L up, in millilitres), "108 fl oz" (a day's: from
    /// 16 fl oz up, whole ounces).</summary>
    public static string Drink(double ml, DrinkUnit unit) =>
        unit == DrinkUnit.Millilitre && ml >= 1000 ? $"{Number(ml / 1000, 2)} L"
        : unit == DrinkUnit.FluidOunce && ml >= 16 * MlPerFluidOunce ? $"{Math.Round(ml / MlPerFluidOunce):N0} fl oz"
        : Amounts.Format(FromMl(ml, unit), unit.Label());

    private static string Number(double value, int decimals) =>
        Math.Round(value, decimals).ToString(decimals == 0 ? "N0" : "#,0." + new string('#', decimals), CultureInfo.CurrentCulture);
}
