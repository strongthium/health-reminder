namespace Strongthium.HealthReminder.Tests;

public sealed class GroceryEntryTests
{
    private static readonly string[] Names = ["7 Up", "Dill Pickles", "Heinz 57"];
    private static readonly string[] Brands = ["Bick's", "Farm Boy", "Tilda"];

    private static GroceryEntry Read(string typed, string quantity = "") => GroceryEntry.Read(typed, quantity, Names, Brands);

    [Theory]
    [InlineData("Bick's Gourmet Tangy Dill Relish 370 ml", "Gourmet Tangy Dill Relish", "Bick's", "370 ml")]
    [InlineData("bick’s Gourmet Tangy Dill Relish 370ml", "Gourmet Tangy Dill Relish", "Bick's", "370ml")]
    [InlineData("Farm Boy Macaroni & Cheese 2 lb", "Macaroni & Cheese", "Farm Boy", "2 lb")]
    [InlineData("Rice Tilda 900 g", "Rice", "Tilda", "900 g")]
    [InlineData("Dill Pickles Bick's", "Dill Pickles", "Bick's", "")]
    [InlineData("dill pickles, 1 L", "Dill Pickles", null, "1 L")]
    [InlineData("Coke Zero 2 L", "Coke Zero", null, "2 L")]
    [InlineData("Relish (370 mL)", "Relish", null, "370 mL")]
    [InlineData("Juice 16 fl oz", "Juice", null, "16 fl oz")]
    [InlineData("Pepsi 12 x 355 mL", "Pepsi", null, "12 x 355 mL")]
    [InlineData("2 x Coke 355 mL", "Coke", null, "2 x 355 mL")]
    [InlineData("Coke 12-pack", "Coke", null, "12 pack")]
    [InlineData("Flour 1½ kg", "Flour", null, "1½ kg")]
    [InlineData("2 L milk", "Milk", null, "2 L")]
    [InlineData("500g flour", "flour", null, "500g")]
    [InlineData("2 bags of rice", "Rice", null, "2 bags")]
    [InlineData("a bag of rice", "Rice", null, "a bag")]
    [InlineData("a dozen eggs", "Eggs", null, "a dozen")]
    [InlineData("eggs dozen", "Eggs", null, "a dozen")]
    [InlineData("eggs x 12", "Eggs", null, "12")]
    [InlineData("12 eggs", "Eggs", null, "12")]
    [InlineData("Whole milk", "Whole milk", null, "")]
    [InlineData("Milk 2%", "Milk 2%", null, "")]
    [InlineData("7 up", "7 Up", null, "")]
    [InlineData("Heinz 57", "Heinz 57", null, "")]
    [InlineData("1 Lemon", "Lemon", null, "1")]
    public void ReadsItWhenItCanBeSure(string typed, string name, string? note, string quantity)
    {
        var entry = Read(typed);
        // "1 Lemon": the number's a guess, but it's still read this way when asked.
        Assert.Equal((name, note, quantity), (entry.Name, entry.Note, entry.Quantity));
        Assert.Equal(typed != "1 Lemon", entry.Sure);
    }

    [Theory]
    [InlineData("Kellogg's Corn Flakes 750 g", "Corn Flakes", "Kellogg's", "750 g")]  // A first word like a brand, not seen before.
    [InlineData("Shepherd's pie", "pie", "Shepherd's", "")]
    [InlineData("Area 51", "Area", null, "51")]                                        // A bare number.
    [InlineData("3 Musketeers", "Musketeers", null, "3")]
    public void AsksWhenItCouldBeReadAnotherWay(string typed, string name, string? note, string quantity)
    {
        var entry = Read(typed);
        Assert.False(entry.Sure);
        Assert.Equal((name, note, quantity), (entry.Name, entry.Note, entry.Quantity));
    }

    [Fact]
    public void WhatsInTheBoxForHowMuchCounts()
    {
        Assert.Equal(new GroceryEntry("Milk", null, "4 L", true), Read("Milk", "4 L"));
        Assert.Equal(new GroceryEntry("Relish", "Bick's", "2", true), Read("Bick's Relish", " 2 "));
        // Said twice: it's asked about.
        Assert.Equal(new GroceryEntry("Relish", null, "2", false), Read("Relish 370 ml", "2"));
    }

    [Fact]
    public void AnAmountWithNothingElseIsLeftAlone() => Assert.Equal(new GroceryEntry("370 ml", null, "", true), Read("370 ml"));

    [Fact]
    public void WhatsReadIsAnAmountTheListCanCompare()
    {
        Assert.Equal(new Quantity(370, Dimension.Volume), Quantities.Parse(Read("Relish 370 ml").Quantity));
        Assert.Equal(new Quantity(12 * 355, Dimension.Volume), Quantities.Parse(Read("Pepsi 12 x 355 mL").Quantity));
        Assert.Equal(new Quantity(12, Dimension.Count), Quantities.Parse(Read("eggs dozen").Quantity));
        Assert.Equal(new Quantity(12, Dimension.Count), Quantities.Parse(Read("Coke 12-pack").Quantity));
    }
}
