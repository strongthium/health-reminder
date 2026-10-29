namespace Strongthium.HealthReminder.Tests;

public sealed class PantryPackingTests
{
    private static readonly DateTime Monday = new(2026, 10, 26, 7, 0, 0);

    private static PantryItem Item(string name, double amount = 1, string unit = "") => new(name, amount, unit, null, Monday, false, Monday);

    [Fact]
    public void PiecesOfAWeightAddUp()
    {
        // 1 box of Chudleys, 2 pies, 200 g each.
        var (packing, problem) = Pantry.ReadPacking("1", "box", "200 g", false, "");
        Assert.Equal("", problem);
        var pies = Item("Pies", 2) with { Note = "Chudleys", Packing = packing };
        Assert.Equal("2 in 1 box · 200 g each", Pantry.Describe(pies, MeasureSystem.Metric));
        Assert.Equal("400 g in all", Pantry.InAll(pies, MeasureSystem.Metric));
    }

    [Fact]
    public void APacksWeightIsSharedByWhatAFullOneHolds()
    {
        // 1 pack of hot dogs, 8 left, a 500 g pack of 12.
        var (packing, _) = Pantry.ReadPacking("1", "pack", "500g", true, "12");
        var hotDogs = Item("Hot dogs", 8) with { Packing = packing };
        Assert.Equal(500 / 12.0, packing!.Each!.Value, 6);
        Assert.Equal("8 in 1 pack · 42 g each", Pantry.Describe(hotDogs, MeasureSystem.Metric));
        Assert.Equal("333 g in all", Pantry.InAll(hotDogs, MeasureSystem.Metric));
        // Without how many a full one holds, there's only the pack's weight.
        var unknown = hotDogs with { Packing = packing with { PerPack = null } };
        Assert.Equal("8 in 1 pack · a 500 g pack", Pantry.Describe(unknown, MeasureSystem.Metric));
        Assert.Equal("", Pantry.InAll(unknown, MeasureSystem.Metric));
    }

    [Fact]
    public void AWeightIsCountedInPieces()
    {
        var (packing, _) = Pantry.ReadPacking("", "", "50 g", false, "");
        Assert.Equal("about 6", Pantry.InAll(Item("Cookies", 300, "g") with { Packing = packing }, MeasureSystem.Metric));
        Assert.Equal("2 boxes", Amounts.Format(2, "box"));
    }

    [Theory]
    [InlineData("two", "", "", false, "", "How many packs is a number: 1, 2, ½.")]
    [InlineData("", "", "3", false, "", "A size is a weight or an amount: 200 g, 1 lb, 355 mL.")]
    [InlineData("", "", "", true, "lots", "What a full pack holds is a number: 12.")]
    public void WhatCantBeReadIsSaid(string packs, string word, string size, bool ofPack, string perPack, string problem) =>
        Assert.Equal(problem, Pantry.ReadPacking(packs, word, size, ofPack, perPack).Problem);

    [Fact]
    public void NothingTypedIsNoPacking() => Assert.Null(Pantry.ReadPacking(" ", "box", "", false, "").Packing);

    [Fact]
    public void SortsWithinAGroup()
    {
        var items = new[]
        {
            Item("Milk") with { UseBy = Monday.AddDays(5), Added = Monday.AddDays(-1), Note = "Natrel" },
            Item("Apples") with { UseBy = Monday.AddDays(20), Added = Monday.AddDays(-3) },
            Item("Bread") with { UseBy = Monday.AddDays(2), Added = Monday, Note = "Dempster's" },
            Item("Rice"),
        };
        string Order(KitchenSort sort) => string.Join(",", Pantry.Sorted(items, sort).Select(i => i.Name));
        Assert.Equal("Apples,Bread,Milk,Rice", Order(KitchenSort.Name));
        Assert.Equal("Bread,Milk,Apples,Rice", Order(KitchenSort.BestBefore));
        Assert.Equal("Bread,Rice,Milk,Apples", Order(KitchenSort.Newest));
        Assert.Equal("Apples,Milk,Bread,Rice", Order(KitchenSort.Oldest));
        Assert.Equal("Bread,Milk,Apples,Rice", Order(KitchenSort.Brand));
    }

    [Fact]
    public void DatesMovedMoveTheBestBefore()
    {
        var steak = Pantry.Freeze(Pantry.Refresh(Item("Steak")), Monday);
        var earlier = Pantry.SetFrozen(steak, Monday.AddDays(-30));
        Assert.Equal(Monday.Date.AddDays(-30), earlier.Frozen!.Value.Date);
        Assert.Equal(steak.UseBy!.Value.AddDays(-30), earlier.UseBy);
        // Set by hand, the best-before stays.
        var byHand = Pantry.SetUseBy(steak, Monday.AddDays(3));
        Assert.Equal(Monday.Date.AddDays(3), Pantry.SetFrozen(byHand, Monday.AddDays(-30)).UseBy);
        // Fresh, from when it was added.
        var cheese = Pantry.Refresh(Item("Cheese"));
        Assert.Equal(cheese.UseBy!.Value.AddDays(-7), Pantry.SetAdded(cheese, Monday.AddDays(-7)).UseBy);
        // Not frozen: no freezer date to set.
        Assert.Equal(cheese, Pantry.SetFrozen(cheese, Monday));
    }

    [Fact]
    public void KeepsTheBrandAndPackingAndRenames()
    {
        using var database = new Database();
        var (packing, _) = Pantry.ReadPacking("1", "pack", "500 g", true, "12");
        var hotDogs = Item("Hot dogs", 8) with { Note = "Schneiders", Packing = packing };
        hotDogs = database.Save(hotDogs);
        database.Save(Item("Buns"));
        Assert.Equal(hotDogs, database.Pantry().Single(p => p.Name == "Hot dogs"));
        database.Save(hotDogs with { Name = "Wieners" });
        Assert.Equal(["Buns", "Wieners"], database.Pantry().Select(p => p.Name));
        Assert.Equal(hotDogs with { Name = "Wieners" }, database.Pantry().Single(p => p.Name == "Wieners"));
    }
}
