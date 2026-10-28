using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Strongthium.HealthReminder.Tests;

public sealed class HealthCanadaTests
{
    [Theory]
    [InlineData("02155907", true)]
    [InlineData("DIN 02155907", true)]
    [InlineData("din: 2155907", true)]
    [InlineData("2155907", false)]
    [InlineData("0093-7663-56", false)]
    [InlineData("adalat", false)]
    public void RecognizesADin(string text, bool din) => Assert.Equal(din, HealthCanada.IsDin(text));

    [Fact]
    public void PadsAShortDinAfterTheWordDin() => Assert.Equal("02155907", HealthCanada.Din("DIN 2155907"));

    [Fact]
    public void ReadsProductsAndIngredients()
    {
        using var products = JsonDocument.Parse("""
            [{"drug_code":19099,"class_name":"Human","drug_identification_number":"02155907","brand_name":"ADALAT XL","descriptor":"",
              "number_of_ais":"1","ai_group_no":"0108507002","company_name":"BAYER INC","last_update_date":"2023-07-04"}]
            """);
        var product = Assert.Single(HealthCanada.ParseProducts(products));
        Assert.Equal(new CanadianProduct(19099, "02155907", "ADALAT XL", "BAYER INC"), product);

        using var ingredients = JsonDocument.Parse("""
            [{"drug_code":19099,"ingredient_name":"NIFEDIPINE","strength":"30","strength_unit":"MG","dosage_value":"","dosage_unit":""}]
            """);
        var ingredient = Assert.Single(HealthCanada.ParseIngredients(ingredients));
        Assert.Equal("30 mg", ingredient.StrengthText);
        var drug = new CanadianDrug(product, [ingredient], "Tablet (Extended-Release)", "Oral", "Marketed");
        Assert.Equal("Adalat XL 30 mg", drug.Name);
        Assert.True(drug.Marketed);
    }

    [Fact]
    public void FindsTheMonographLink() =>
        Assert.Equal("https://pdf.hres.ca/dpd_pm/00071434.PDF",
            HealthCanada.ParseMonographUrl("""<p><a href="https://pdf.hres.ca/dpd_pm/00071434.PDF" target="_blank">Product Monograph</a></p>"""));

    private static readonly Ingredient Nifedipine30 = new("NIFEDIPINE", "30", "MG");

    [Fact]
    public void ReadsTheDescriptionOfTheStrength()
    {
        const string text = """
            ADALAT XL is a tablet for oral administration. Nonmedicinal ingredients: cellulose acetate, red ferric oxide and
            titanium dioxide.
            ADALAT XL 20 mg tablet description: Round, convex, pink in color and imprinted with “ADALAT 20” on one side .
            ADALAT XL 30 mg tablet description: Round, convex, dusty rose in color and are imprinted with
            “ADALAT 30” on one side . ADALAT XL 30 mg are available in blister packs of 28 and 98 tablets.
            """;
        var look = Monograph.Describe(text, [Nifedipine30], "Tablet (Extended-Release)");
        Assert.NotNull(look);
        Assert.StartsWith("ADALAT XL 30 mg tablet description", look.Sentence, StringComparison.Ordinal);
        Assert.Equal(["PINK"], look.Appearance.Colors);
        Assert.Equal(("ROUND", "ADALAT 30", 1), (look.Appearance.Shape, look.Appearance.Imprint, look.Appearance.Score));
    }

    [Fact]
    public void ReadsTwoSidesColoursSizeAndScore()
    {
        var appearance = Monograph.Read("Each 500 mg tablet is a white to off-white, oval, film-coated, bisected tablet, 17 mm long, engraved with “M” on one side and “500” on the other.");
        Assert.Equal(["WHITE"], appearance.Colors);
        Assert.Equal(("OVAL", 17.0, "M;500", 2), (appearance.Shape, appearance.SizeMm, appearance.Imprint, appearance.Score));
        var capsule = Monograph.Read("Opaque blue and pink capsule, printed with \"AMOX 500\" in black ink.", "CAPSULE");
        Assert.Equal(["BLUE", "PINK"], capsule.Colors);
        Assert.Equal(("CAPSULE", "AMOX 500"), (capsule.Shape, capsule.Imprint));
    }

    [Fact]
    public void FindsNoDescriptionInTextWithout() =>
        Assert.Null(Monograph.Describe("Take one tablet daily. Store at room temperature.", [Nifedipine30], "Tablet"));

    [Fact]
    public void ReadsTheTextOfAPdf()
    {
        var content = Encoding.Latin1.GetBytes(
            "BT /F1 12 Tf 72 700 Td [(ADALAT XL 30 mg tablet des)-12(cription: Round, convex, dusty rose in color and are imprinted with)] TJ " +
            "0 -14 Td (\\223ADALAT 30\\224 on one side.) Tj ET");
        using var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(content);
        var pdf = new MemoryStream();
        void Write(string s) => pdf.Write(Encoding.Latin1.GetBytes(s));
        Write("%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");
        Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        Write($"5 0 obj\n<< /Length {deflated.Length} /Filter /FlateDecode >>\nstream\n");
        pdf.Write(deflated.ToArray());
        Write("\nendstream\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

        var text = PdfText.Extract(pdf.ToArray());
        Assert.Contains("ADALAT XL 30 mg tablet description: Round, convex, dusty rose", text, StringComparison.Ordinal);
        Assert.Contains("\n“ADALAT 30” on one side.", text, StringComparison.Ordinal);
        Assert.Equal("ADALAT 30", Monograph.Describe(text, [Nifedipine30], "Tablet")?.Appearance.Imprint);
    }

    [Fact]
    public void ReadsBothKindsOfCMapRange()
    {
        var cmap = PdfText.ParseCMap("""
            begincodespacerange <0000> <FFFF> endcodespacerange
            1 beginbfrange <0003> <0004> [<0020> <0041>] endbfrange
            1 beginbfrange <010F> <0110> <0062> endbfrange
            1 beginbfchar <0018> <0044> endbfchar
            """);
        Assert.True(cmap.Wide);
        Assert.Equal((" ", "A", "b", "c", "D"), (cmap.Map[3], cmap.Map[4], cmap.Map[0x10F], cmap.Map[0x110], cmap.Map[0x18]));
    }

    [Fact]
    public void MatchesTheClinicalDrugOfTheSameStrengthAndForm()
    {
        (string, string)[] drugs =
        [
            ("198034", "nifedipine 30 MG 24HR Extended Release Oral Tablet"),
            ("1812011", "nifedipine 30 MG Osmotic 24 HR Extended Release Oral Tablet"),
            ("198036", "nifedipine 60 MG 24HR Extended Release Oral Tablet"),
            ("198037", "nifedipine 10 MG Oral Capsule"),
        ];
        Assert.Equal(["198034", "1812011"], DailyMed.MatchClinicalDrugs(drugs, [Nifedipine30], "Tablet (Extended-Release)"));
        Assert.Equal(["198037"], DailyMed.MatchClinicalDrugs(drugs, [new Ingredient("NIFEDIPINE", "10", "MG")], "Capsule"));
        Assert.Empty(DailyMed.MatchClinicalDrugs(drugs, [Nifedipine30], "Tablet"));
    }

    [Fact]
    public void GroupsTheSamePillFromSeveralLabels()
    {
        PillProduct Pill(string color, string imprint, string image = "") =>
            new("1-1", "Nifedipine", "TABLET, EXTENDED RELEASE", "30 mg", new PillAppearance([color], "ROUND", 9, imprint, 1), [], "", image);
        LabelSummary Label(string id) => new(id, "NIFEDIPINE " + id, "2024");
        var grouped = LookAlike.Group([
            (Pill("PINK", "KU;260"), Label("a")), (Pill("YELLOW", "30"), Label("b")), (Pill("PINK", "260; KU", "x.jpg"), Label("c")),
        ]);
        Assert.Equal(2, grouped.Count);
        Assert.Equal((2, "x.jpg"), (grouped[0].Count, grouped[0].Product.Image));
    }
}
