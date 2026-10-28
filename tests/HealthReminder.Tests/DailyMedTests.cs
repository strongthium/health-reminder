using System.Text.Json;
using System.Xml.Linq;

namespace Strongthium.HealthReminder.Tests;

public sealed class DailyMedTests
{
    private static List<PillProduct> Products() =>
        DailyMed.ParseProducts(XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "label.xml")));

    [Fact]
    public void ReadsEachProductWithItsAppearance()
    {
        var products = Products();
        Assert.Equal(["12345-101", "12345-102", "12345-201", "12345-301"], products.Select(p => p.Ndc));

        var tablet = products[0];
        Assert.Equal("Examplopril", tablet.Name);
        Assert.Equal("TABLET", tablet.Form);
        Assert.Equal("10 mg", tablet.Strength);
        Assert.Equal(["WHITE"], tablet.Appearance.Colors);
        Assert.Equal("ROUND", tablet.Appearance.Shape);
        Assert.Equal(8, tablet.Appearance.SizeMm);
        Assert.Equal(2, tablet.Appearance.Score);
        Assert.Equal("EX;10", tablet.Appearance.Imprint);
        Assert.Equal(["EX", "10"], tablet.Appearance.ImprintSides);

        Assert.Equal(11.5, products[1].Appearance.SizeMm);
        Assert.Equal(1, products[1].Appearance.Score);
    }

    [Fact]
    public void ReadsAProductsPhotoPackagesAndTheProductItRepackages()
    {
        var products = Products();
        Assert.Equal("EX10.jpg", products[0].Image);
        Assert.Equal(["12345-101-30"], products[0].PackageNdcs);
        Assert.Equal("", products[0].SourceNdc);
        Assert.Equal("", products[1].Image);
        Assert.Equal("99999-202", products[1].SourceNdc);
    }

    [Fact]
    public void ReadsBothColoursOfATwoToneCapsuleAndNamesEachIngredientOfACombination()
    {
        var capsule = Products()[2];
        Assert.Equal(["BLUE", "WHITE"], capsule.Appearance.Colors);
        Assert.Equal("Examplopril and Diureticide", capsule.Name);
        Assert.Equal("Examplopril 20 mg, Diureticide 12.5 mg", capsule.Strength);
        Assert.Equal("blue and white capsule, 18 mm", capsule.Appearance.Describe());
    }

    [Fact]
    public void ALiquidHasAConcentrationAndNoAppearance()
    {
        var liquid = Products()[3];
        Assert.Equal("1 mg/mL", liquid.Strength);
        Assert.False(liquid.Appearance.IsKnown);
    }

    [Fact]
    public void DescribesAPillInWords()
    {
        var tablet = Products()[0];
        Assert.Equal("white round pill, 8 mm, scored in half, marked EX / 10", tablet.Appearance.Describe());
        Assert.Equal("white round tablet, 8 mm, scored in half, marked EX / 10", tablet.Appearance.Describe("TABLET"));
        Assert.Equal("white capsule-shaped tablet, 8 mm, marked H / 144",
            new PillAppearance(["WHITE"], "CAPSULE", 8, "H;144", 1).Describe("TABLET, FILM COATED"));
        Assert.Equal("green pentagon pill", new PillAppearance(["GREEN"], "PENTAGON (5 SIDED)", 0, "", 1).Describe());
    }

    [Fact]
    public void TheSamePillHasTheSameImprintShapeAndAColourInCommon()
    {
        var pill = new PillAppearance(["WHITE"], "ROUND", 8, "LUPIN;2;5", 1);
        Assert.True(pill.SamePillAs(new PillAppearance(["WHITE"], "ROUND", 7, "2;5;LUPIN", 1)));
        Assert.True(pill.SamePillAs(new PillAppearance(["WHITE", "BLUE"], "", 0, "LU PIN;2;5", 1)));
        Assert.False(pill.SamePillAs(new PillAppearance(["PINK"], "ROUND", 8, "LUPIN;2;5", 1)));
        Assert.False(pill.SamePillAs(new PillAppearance(["WHITE"], "OVAL", 8, "LUPIN;2;5", 1)));
        Assert.False(pill.SamePillAs(new PillAppearance(["WHITE"], "ROUND", 8, "LUPIN;5", 1)));
        Assert.False(new PillAppearance(["WHITE"], "ROUND", 8, "", 1).SamePillAs(new PillAppearance(["WHITE"], "ROUND", 8, "", 1)));
    }

    [Theory]
    [InlineData("0093-7663-56", true)]
    [InlineData("72789-101", true)]
    [InlineData("72789101001", true)]
    [InlineData("lisinopril", false)]
    [InlineData("10", false)]
    [InlineData("12-34", false)]
    public void TellsAnNdcFromAName(string text, bool isNdc) => Assert.Equal(isNdc, DailyMed.IsNdc(text));

    [Theory]
    [InlineData("12345-101-30", true)]
    [InlineData("1234510130", true)]
    [InlineData("12345-101", true)]
    [InlineData("12345-102-30", false)]
    public void MatchesAPackageNdcToItsProduct(string searched, bool matches) =>
        Assert.Equal(matches, DailyMed.NdcMatches(Products()[0], searched));

    [Fact]
    public void ReadsSearchResultsImagesAndRxCui()
    {
        using var search = JsonDocument.Parse("""
            {"data": [{"spl_version": 6, "published_date": "Oct 19, 2026", "title": "EXAMPLOPRIL TABLET [EXAMPLE PHARMA]", "setid": "aaaa-1"}],
             "metadata": {"total_elements": 1}}
            """);
        Assert.Equal([new LabelSummary("aaaa-1", "EXAMPLOPRIL TABLET [EXAMPLE PHARMA]", "Oct 19, 2026")], DailyMed.ParseSearch(search));

        using var media = JsonDocument.Parse("""
            {"data": {"media": [
                {"mime_type": "image/jpeg", "url": "https://dailymed.nlm.nih.gov/dailymed/image.cfm?setid=aaaa-1&name=pill.jpg", "name": "pill.jpg"},
                {"mime_type": "application/pdf", "url": "https://dailymed.nlm.nih.gov/x.pdf", "name": "x.pdf"},
                {"mime_type": "image/jpeg", "url": "http://insecure.example/pill.jpg", "name": "insecure.jpg"}
            ]}}
            """);
        Assert.Equal(["pill.jpg"], DailyMed.ParseImages(media).Select(i => i.Name));

        using var status = JsonDocument.Parse("""{"ndcStatus":{"ndc11":"72789010101","status":"ACTIVE","rxcui":"311353","conceptName":"lisinopril 2.5 MG Oral Tablet"}}""");
        Assert.Equal("311353", DailyMed.ParseRxCui(status));
        using var unknown = JsonDocument.Parse("""{"ndcStatus":{"status":"UNKNOWN"}}""");
        Assert.Equal("", DailyMed.ParseRxCui(unknown));
    }
}
