using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>A product in Health Canada's Drug Product Database, under its DIN.</summary>
public sealed record CanadianProduct(int DrugCode, string Din, string Brand, string Company);

/// <summary>An active ingredient and its strength: "NIFEDIPINE", "30", "MG".</summary>
public sealed record Ingredient(string Name, string Strength, string Unit)
{
    /// <summary>"30 mg", "0.125 mg", "100 unit/mL".</summary>
    public string StrengthText => $"{Strength} {Unit.ToLowerInvariant()}".Trim();
}

/// <summary>A Canadian product with what's in it, its form and whether it's still sold.</summary>
public sealed record CanadianDrug(CanadianProduct Product, IReadOnlyList<Ingredient> Ingredients, string Form, string Route, string Status)
{
    public string Strength => string.Join(", ", Ingredients.Select(i => Ingredients.Count > 1 ? $"{Titled(i.Name)} {i.StrengthText}" : i.StrengthText));
    public string Name => $"{Titled(Product.Brand)} {Strength}".Trim();
    public bool Marketed => Status.Equals("Marketed", StringComparison.OrdinalIgnoreCase);

    /// <summary>"ADALAT XL" → "Adalat XL": the database shouts, but "XL", "CR" and "HCT" stay as they are.</summary>
    public static string Titled(string text) =>
        text.Any(char.IsLower) ? text.Trim() : string.Join(' ', text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word =>
            word.Length <= 4 && (!word.Any(c => "AEIOUY".Contains(c)) || word is "ER" or "OD" or "LA" or "ODT" or "DS" or "EC")
                ? word
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant())));
}

/// <summary>What a product monograph says the pill looks like: its sentence, and the appearance read out of it.</summary>
public sealed record MonographLook(string Sentence, PillAppearance Appearance, string Url);

/// <summary>
/// Looks Canadian products up in Health Canada's Drug Product Database, by DIN (the 8 digits on a Canadian label or
/// pharmacy sticker) or brand name. The database says what is in a product and its form, but not what it looks like;
/// its product monograph does, in a sentence ("Round, convex, dusty rose in color and are imprinted with "ADALAT 30"
/// on one side"), which <see cref="LookFromMonographAsync"/> finds and reads. No key is needed.
/// https://health-products.canada.ca/api/documentation/dpd-documentation-en.html
/// </summary>
public sealed partial class HealthCanada(HttpClient http)
{
    private const string ApiUrl = "https://health-products.canada.ca/api/drug/";
    private const string InfoUrl = "https://health-products.canada.ca/dpd-bdpp/info?lang=eng&code=";
    /// <summary>How many products of a brand-name search are looked at, at most.</summary>
    private const int MaxResults = 40;

    /// <summary>A DIN: 8 digits, perhaps after "DIN".</summary>
    public static bool IsDin(string text) => DinPattern().IsMatch(text.Trim());

    /// <summary>The 8 digits of a DIN, with the leading zeros a label may leave out.</summary>
    public static string Din(string text) =>
        DinPattern().Match(text.Trim()) is { Success: true } m ? (m.Groups[1].Success ? m.Groups[1] : m.Groups[2]).Value.PadLeft(8, '0') : "";

    [GeneratedRegex(@"^(?:DIN[\s:#-]*)?(\d{8})$|^DIN[\s:#-]*(\d{6,7})$", RegexOptions.IgnoreCase)]
    private static partial Regex DinPattern();

    /// <summary>Products for a DIN or a brand name (any part of it), with their details; the ones still sold first.</summary>
    public async Task<List<CanadianDrug>> SearchAsync(string query, CancellationToken cancel = default)
    {
        query = query.Trim();
        var din = Din(query);
        var url = din.Length > 0 ? $"drugproduct/?din={din}" : $"drugproduct/?brandname={Uri.EscapeDataString(query)}";
        var products = ParseProducts(await GetJsonAsync(url, cancel).ConfigureAwait(false)).Take(MaxResults).ToList();
        using var limit = new SemaphoreSlim(8);
        var drugs = await Task.WhenAll(products.Select(async product =>
        {
            await limit.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                return await DetailsAsync(product, cancel).ConfigureAwait(false);
            }
            finally
            {
                limit.Release();
            }
        })).ConfigureAwait(false);
        return drugs.OrderBy(d => !d.Marketed).ThenBy(d => d.Product.Brand, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Strength, StringComparer.Ordinal).ToList();
    }

    /// <summary>What's in a product, its form, how it's taken and whether it's still sold.</summary>
    public async Task<CanadianDrug> DetailsAsync(CanadianProduct product, CancellationToken cancel = default)
    {
        var code = product.DrugCode.ToString(CultureInfo.InvariantCulture);
        var ingredients = GetJsonAsync($"activeingredient/?id={code}", cancel);
        var form = GetJsonAsync($"form/?id={code}", cancel);
        var route = GetJsonAsync($"route/?id={code}", cancel);
        var status = GetJsonAsync($"status/?id={code}", cancel);
        return new CanadianDrug(product,
            ParseIngredients(await ingredients.ConfigureAwait(false)),
            Field(await form.ConfigureAwait(false), "pharmaceutical_form_name"),
            Field(await route.ConfigureAwait(false), "route_of_administration_name"),
            Field(await status.ConfigureAwait(false), "status"));
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancel)
    {
        using var stream = await http.GetStreamAsync(new Uri(ApiUrl + path + (path.Contains('?') ? "&" : "?") + "lang=en&type=json"), cancel).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
    }

    private static IEnumerable<JsonElement> Items(JsonDocument json) =>
        json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement.EnumerateArray() : json.RootElement.ValueKind == JsonValueKind.Object ? [json.RootElement] : [];

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) ? p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? "",
            JsonValueKind.Number => p.GetRawText(),
            _ => "",
        } : "";

    internal static List<CanadianProduct> ParseProducts(JsonDocument json) =>
        Items(json)
            .Select(e => new CanadianProduct(int.TryParse(Text(e, "drug_code"), CultureInfo.InvariantCulture, out var code) ? code : 0,
                Text(e, "drug_identification_number"), Text(e, "brand_name").Trim(), Text(e, "company_name").Trim()))
            .Where(p => p.DrugCode > 0)
            .ToList();

    internal static List<Ingredient> ParseIngredients(JsonDocument json) =>
        Items(json).Select(e => new Ingredient(Text(e, "ingredient_name").Trim(), Text(e, "strength").Trim(), Text(e, "strength_unit").Trim()))
            .Where(i => i.Name.Length > 0)
            .ToList();

    private static string Field(JsonDocument json, string name) => string.Join(", ", Items(json).Select(e => Text(e, name)).Where(t => t.Length > 0).Distinct());

    /// <summary>The address of a product's monograph (a PDF), from its page on the database's site; empty if none.</summary>
    public async Task<string> MonographUrlAsync(CanadianProduct product, CancellationToken cancel = default)
    {
        var page = await http.GetStringAsync(new Uri(InfoUrl + product.DrugCode.ToString(CultureInfo.InvariantCulture)), cancel).ConfigureAwait(false);
        return ParseMonographUrl(page);
    }

    internal static string ParseMonographUrl(string html) =>
        MonographLinkPattern().Match(html) is { Success: true } m ? m.Groups[1].Value.Replace("http://", "https://", StringComparison.Ordinal) : "";

    [GeneratedRegex(@"href=""(https?://pdf\.hres\.ca/dpd_pm/\d+\.PDF)""", RegexOptions.IgnoreCase)]
    private static partial Regex MonographLinkPattern();

    /// <summary>What the monograph says this product looks like, or null if it can't be found or read.</summary>
    public async Task<MonographLook?> LookFromMonographAsync(CanadianDrug drug, CancellationToken cancel = default)
    {
        var url = await MonographUrlAsync(drug.Product, cancel).ConfigureAwait(false);
        if (url.Length == 0) return null;
        var pdf = await http.GetByteArrayAsync(new Uri(url), cancel).ConfigureAwait(false);
        var text = await Task.Run(() => PdfText.Extract(pdf), cancel).ConfigureAwait(false);
        return Monograph.Describe(text, drug.Ingredients, drug.Form) is { } look ? look with { Url = url } : new MonographLook("", PillAppearance.Unknown, url);
    }
}

/// <summary>
/// Finds, in a product monograph's text, the sentence that says what the pill of one strength looks like, and reads
/// its colour, shape, size, score and imprint out of it, in the words labels use (SPL's: "PINK", "ROUND").
/// </summary>
public static partial class Monograph
{
    // Colour words, in the order they're tried (longer first), to SPL's colours.
    private static readonly (string Word, string Color)[] ColorWords =
    [
        ("off-white", "WHITE"), ("off white", "WHITE"), ("cream", "WHITE"), ("ivory", "WHITE"), ("white", "WHITE"),
        ("dusty rose", "PINK"), ("rose", "PINK"), ("salmon", "PINK"), ("pink", "PINK"),
        ("peach", "ORANGE"), ("orange", "ORANGE"), ("maroon", "RED"), ("red", "RED"),
        ("lavender", "PURPLE"), ("lilac", "PURPLE"), ("mauve", "PURPLE"), ("violet", "PURPLE"), ("purple", "PURPLE"),
        ("turquoise", "TURQUOISE"), ("aqua", "TURQUOISE"), ("teal", "TURQUOISE"), ("blue", "BLUE"), ("green", "GREEN"),
        ("yellow", "YELLOW"), ("beige", "BROWN"), ("tan", "BROWN"), ("brown", "BROWN"),
        ("grey", "GRAY"), ("gray", "GRAY"), ("silver", "GRAY"), ("black", "BLACK"),
    ];

    private static readonly (string Pattern, string Shape)[] ShapeWords =
    [
        (@"capsule[- ]shaped|oblong|caplet|elongated", "CAPSULE"), (@"oval|ovaloid|elliptical|ellipsoid", "OVAL"), (@"round|circular|disc", "ROUND"),
        (@"rectangular", "RECTANGLE"), (@"square", "SQUARE"), (@"triangular", "TRIANGLE"), (@"diamond", "DIAMOND"),
        (@"pentagonal|pentagon", "PENTAGON (5 SIDED)"), (@"hexagonal|hexagon", "HEXAGON (6 SIDED)"), (@"octagonal|octagon", "OCTAGON (8 SIDED)"),
        (@"heart[- ]shaped", "FREEFORM"),
    ];

    /// <summary>The sentence describing the pill of these ingredients' strength, and what it says; null if none does.</summary>
    public static MonographLook? Describe(string text, IReadOnlyList<Ingredient> ingredients, string form)
    {
        var flat = Regex.Replace(text, @"\s+", " ").Replace(" .", ".", StringComparison.Ordinal).Replace(" ,", ",", StringComparison.Ordinal);
        var sentences = SentenceEnd().Split(flat).Select(s => s.Trim()).Where(s => s.Length is > 15 and < 500).ToList();
        var strengths = ingredients.Select(i => i.Strength).Where(s => s.Length > 0).ToList();
        string? best = null;
        var bestScore = 0;
        foreach (var sentence in sentences)
        {
            var lower = sentence.ToLowerInvariant();
            var unquoted = Quoted().Replace(lower, " ");
            if (!ColorWords.Any(c => Word(unquoted, c.Word))) continue;
            var score = 1;
            if (ShapeWords.Any(s => Regex.IsMatch(unquoted, $@"\b(?:{s.Pattern})\b"))) score += 2;
            if (Regex.IsMatch(lower, @"imprint|engraved|debossed|embossed|printed|inscribed|marked")) score += 2;
            if (Regex.IsMatch(lower, @"\b(tablets?|capsules?|caplets?)\b")) score += 1;
            if (lower.Contains("description", StringComparison.Ordinal)) score += 2;
            var mentioned = StrengthMention().Matches(lower).Select(m => m.Groups[1].Value).ToList();
            if (strengths.Count > 0 && mentioned.Count > 0)
                score += mentioned.Any(m => strengths.Any(s => SameNumber(m, s))) ? 4 : -8;
            if (score > bestScore)
            {
                (best, bestScore) = (sentence, score);
            }
        }
        if (best is null || bestScore < 4) return null;
        return new MonographLook(best, Read(best, form), "");
    }

    /// <summary>The appearance a description gives: "Round, convex, dusty rose ... imprinted with "ADALAT 30"".</summary>
    public static PillAppearance Read(string sentence, string form = "")
    {
        var lower = sentence.ToLowerInvariant();
        var unquoted = Quoted().Replace(lower, " ");
        var colors = ColorWords
            .Select(c => (c.Color, At: WordAt(unquoted, c.Word)))
            .Where(c => c.At >= 0)
            .OrderBy(c => c.At)
            .Select(c => c.Color)
            .Distinct()
            .Take(2)
            .ToList();
        var shape = ShapeWords.FirstOrDefault(s => Regex.IsMatch(unquoted, $@"\b(?:{s.Pattern})\b")).Shape ?? "";
        if (shape.Length == 0 && Regex.IsMatch(form, "capsule", RegexOptions.IgnoreCase)) shape = "CAPSULE";
        var size = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*mm\b") is { Success: true } mm && double.TryParse(mm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 0;
        var score = Regex.IsMatch(lower, @"quadrisect|quarter") ? 4 : Regex.IsMatch(lower, @"trisect") ? 3 : Regex.IsMatch(lower, @"\bscor(ed|e line)|bisect|break[- ]line|breakline") ? 2 : 1;

        // The imprint: the quoted marks after "imprinted", "engraved", "debossed"..., a side each.
        var imprints = new List<string>();
        var marking = Regex.Match(sentence, @"(imprint|engrav|deboss|emboss|print|inscrib|mark)", RegexOptions.IgnoreCase);
        if (marking.Success)
        {
            foreach (Match q in Quoted().Matches(sentence[marking.Index..]))
            {
                var mark = q.Groups[1].Value.Trim();
                if (mark.Length is > 0 and <= 24 && !imprints.Contains(mark)) imprints.Add(mark);
            }
            if (imprints.Count == 0 && Regex.Match(sentence[marking.Index..], @"\bwith\s+(?:the\s+)?(?:letters?\s+|numbers?\s+|logo\s+)?([A-Z0-9][A-Z0-9 /.-]{0,15}?)\s+on\b") is { Success: true } plain)
                imprints.Add(plain.Groups[1].Value.Trim());
        }
        return new PillAppearance(colors, shape, size, string.Join(';', imprints), score);
    }

    private static bool Word(string text, string word) => WordAt(text, word) >= 0;

    private static int WordAt(string text, string word) =>
        Regex.Match(text, $@"(?<![a-z-]){Regex.Escape(word)}(?![a-z])") is { Success: true } m ? m.Index : -1;

    private static bool SameNumber(string a, string b) =>
        double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && Math.Abs(x - y) < 1e-9;

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z0-9“""])")]
    private static partial Regex SentenceEnd();

    // Not plain single quotes, which are apostrophes as often.
    [GeneratedRegex(@"[“""‘]([^”""’]{1,40})[”""’]")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(?:mg|mcg|µg|g)\b")]
    private static partial Regex StrengthMention();
}
