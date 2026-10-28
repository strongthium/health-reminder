namespace Strongthium.HealthReminder;

/// <summary>
/// A line of the grocery list: what, how much as it was typed ("2 L", "1 gallon", "500 g", "12"), and once it's bought,
/// when and for how much. Bought lines stay, out of sight once the shopping is done, so the next time the same thing is
/// on the list its last price shows.
/// </summary>
public sealed class GroceryItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>The amount as typed; <see cref="Amount"/> reads it.</summary>
    public string Quantity { get; set; } = "";
    /// <summary>What it cost, as the user typed it in when it was bought; null if they didn't.</summary>
    public decimal? Price { get; set; }
    public DateTime? Bought { get; set; }
    public DateTime Added { get; set; }
    /// <summary>Done with: bought on an earlier shopping trip.</summary>
    public bool Cleared { get; set; }
    /// <summary>When it was used up; until then it's in the kitchen.</summary>
    public DateTime? Used { get; set; }
    /// <summary>When it's best used by, when that was said (read from a photo); otherwise the catalog knows.</summary>
    public DateTime? UseBy { get; set; }

    public Quantity? Amount => Quantities.Parse(Quantity);

    /// <summary>"1.89 L", "2 lb"; a count, or what isn't an amount, as typed ("2 loaves"); "" without one.</summary>
    public string Show(MeasureSystem system) =>
        Amount is { Dimension: not Dimension.Count } amount ? Quantities.Format(amount, system) : Quantity.Trim();

    /// <summary>"$1.85/L", "$0.42 each"; "" without a price or an amount.</summary>
    public string UnitPrice(MeasureSystem system, IFormatProvider? culture = null)
    {
        if (Price is not { } price || Amount is not { } amount || Quantities.UnitPrice((double)price, amount, system) is not { } each) return "";
        var unit = Quantities.PriceUnit(amount.Dimension, system).Label;
        return each.ToString("C", culture) + (amount.Dimension == Dimension.Count ? " each" : "/" + unit);
    }
}
