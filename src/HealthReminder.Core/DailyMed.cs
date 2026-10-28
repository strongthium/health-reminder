using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Strongthium.HealthReminder;

/// <summary>A drug label on DailyMed: one manufacturer's (or repackager's) label, which can cover several strengths.</summary>
public sealed record LabelSummary(string SetId, string Title, string Published);

/// <summary>One product of a label, as sold under one NDC product code: its strength and what the pill looks like.</summary>
/// <param name="PackageNdcs">The NDCs of its packages ("72789-101-01"): what a pharmacy label or bottle shows.</param>
/// <param name="SourceNdc">For a repackaged product, the NDC of the manufacturer's product it repackages.</param>
/// <param name="Image">The file name of the label's photo of this product (its SPLIMAGE), if the label has one.</param>
public sealed record PillProduct(string Ndc, string Name, string Form, string Strength, PillAppearance Appearance,
    IReadOnlyList<string> PackageNdcs, string SourceNdc, string Image)
{
    public string Display => $"{Name} {Strength}".Trim() + (Form.Length > 0 ? $" ({Form.ToLowerInvariant()})" : "");
}

/// <summary>One pill of a drug and strength, as one or more labels describe it: a maker's version of it.</summary>
/// <param name="Count">How many labels have this same pill.</param>
public sealed record LookAlike(PillProduct Product, LabelSummary Label, int Count)
{
    /// <summary>The labels' products, one of each different pill (colour, shape and imprint), the most common first,
    /// with a photo where any of its labels has one.</summary>
    internal static List<LookAlike> Group(IEnumerable<(PillProduct Product, LabelSummary Label)> found) =>
        found
            .GroupBy(f => string.Join('|', string.Join(';', f.Product.Appearance.Colors.Order(StringComparer.Ordinal)), f.Product.Appearance.Shape,
                string.Join(';', f.Product.Appearance.ImprintSides.Select(s => s.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant()).Order(StringComparer.Ordinal))))
            .Select(g => g.OrderBy(f => f.Product.Image.Length == 0).First() is var first ? new LookAlike(first.Product, first.Label, g.Select(f => f.Label.SetId).Distinct().Count()) : null!)
            .OrderByDescending(l => l.Count).ThenBy(l => l.Product.Image.Length == 0)
            .ToList();
}

/// <summary>A picture from a label: the pill itself, or its packaging.</summary>
public sealed record LabelImage(string Name, string Url);

/// <summary>A photo of one product found by <see cref="DailyMed.FindPhotoAsync"/>, and how it was found.</summary>
/// <param name="Name">A file name for it: the label's set id and the image's name.</param>
/// <param name="Source">Where it came from, in words: "the photo on this label", "the manufacturer's label ...".</param>
public sealed record PillPhoto(string Name, string Url, string Source);

/// <summary>
/// Looks up what a pill looks like on DailyMed, the U.S. National Library of Medicine's copy of the labels that
/// manufacturers file with the FDA. Every label of a solid medicine describes each product's colour, shape, size,
/// score and imprint (its SPL "characteristics"), and many carry a photo of each product. RxNorm, also the NLM's,
/// says which labels are of the same drug and strength. Neither needs a key.
/// https://dailymed.nlm.nih.gov/dailymed/app-support-web-services.cfm, https://lhncbc.nlm.nih.gov/RxNav/APIs/
/// </summary>
public sealed partial class DailyMed(HttpClient http)
{
    public const string BaseUrl = "https://dailymed.nlm.nih.gov/dailymed/services/v2/";
    private const string ImageUrl = "https://dailymed.nlm.nih.gov/dailymed/image.cfm";
    private const string RxNavUrl = "https://rxnav.nlm.nih.gov/REST/";
    /// <summary>How many labels of the same drug and strength are read, at most, looking for a photo.</summary>
    private const int MaxLabelsToSearch = 60;
    private static readonly XNamespace V3 = "urn:hl7-org:v3";

    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        var version = typeof(DailyMed).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("health-reminder", version));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/strongthium/health-reminder)"));
        return http;
    }

    /// <summary>Labels for a drug name ("lisinopril") or an NDC from the bottle ("72789-101-01"), newest first.</summary>
    public Task<List<LabelSummary>> SearchAsync(string query, CancellationToken cancel = default)
    {
        query = query.Trim();
        if (query.Length == 0) return Task.FromResult(new List<LabelSummary>());
        return LabelsAsync(IsNdc(query) ? "ndc" : "drug_name", query, cancel);
    }

    private async Task<List<LabelSummary>> LabelsAsync(string parameter, string value, CancellationToken cancel)
    {
        var url = $"spls.json?{parameter}={Uri.EscapeDataString(value)}&pagesize=100";
        using var stream = await http.GetStreamAsync(url, cancel).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
        return ParseSearch(json);
    }

    internal static List<LabelSummary> ParseSearch(JsonDocument json) =>
        json.RootElement.GetProperty("data").EnumerateArray()
            .Select(e => new LabelSummary(e.GetProperty("setid").GetString() ?? "", e.GetProperty("title").GetString() ?? "", e.GetProperty("published_date").GetString() ?? ""))
            .Where(l => l.SetId.Length > 0)
            .ToList();

    /// <summary>An NDC as printed on a bottle: 10 or 11 digits, in two or three groups separated by dashes.</summary>
    public static bool IsNdc(string text) => NdcPattern().IsMatch(text.Trim());

    [GeneratedRegex(@"^(\d{4,5}-\d{3,4}(-\d{1,2})?|\d{10,11})$")]
    private static partial Regex NdcPattern();

    /// <summary>Whether a product's NDC (labeler-product, "72789-101") is the one of a package NDC from a bottle
    /// ("72789-101-01"); dashes are optional in the package NDC.</summary>
    public static bool NdcMatches(PillProduct product, string searched)
    {
        if (product.Ndc.Length == 0) return false;
        if (searched.StartsWith(product.Ndc + "-", StringComparison.Ordinal) || searched == product.Ndc) return true;
        var digits = searched.Replace("-", "", StringComparison.Ordinal);
        return product.PackageNdcs.Any(p => p.Replace("-", "", StringComparison.Ordinal) == digits)
            || digits.StartsWith(product.Ndc.Replace("-", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>The products of a label, each with what it looks like.</summary>
    public async Task<List<PillProduct>> ProductsAsync(string setId, CancellationToken cancel = default) =>
        ParseProducts(await LabelXmlAsync(setId, cancel).ConfigureAwait(false));

    private async Task<XDocument> LabelXmlAsync(string setId, CancellationToken cancel)
    {
        using var stream = await http.GetStreamAsync($"spls/{Uri.EscapeDataString(setId)}.xml", cancel).ConfigureAwait(false);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, Async = true };
        using var reader = XmlReader.Create(stream, settings);
        return await XDocument.LoadAsync(reader, LoadOptions.None, cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the products out of an SPL document. Each is a manufacturedProduct inside another: the inner one has the
    /// NDC, name, form, ingredients, packages and (for a repackaged product) the NDC it is the same as; the outer one
    /// the characteristics (SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLSCORE, SPLIMAGE).
    /// </summary>
    internal static List<PillProduct> ParseProducts(XDocument document)
    {
        var products = new List<PillProduct>();
        foreach (var outer in document.Descendants(V3 + "manufacturedProduct"))
        {
            var inner = outer.Element(V3 + "manufacturedProduct");
            if (inner is null) continue;

            var ndc = inner.Element(V3 + "code")?.Attribute("code")?.Value ?? "";
            var name = Title(inner.Element(V3 + "name")?.Value ?? "");
            var form = inner.Element(V3 + "formCode")?.Attribute("displayName")?.Value ?? "";
            var strength = string.Join(", ", inner.Elements(V3 + "ingredient")
                .Where(i => i.Attribute("classCode")?.Value is "ACTIB" or "ACTIM" or "ACTIR")
                .Select(Strength)
                .Where(s => s.Length > 0));
            var packages = inner.Elements(V3 + "asContent")
                .Select(c => c.Element(V3 + "containerPackagedProduct")?.Element(V3 + "code")?.Attribute("code")?.Value)
                .OfType<string>()
                .ToList();
            var source = inner.Elements(V3 + "asEquivalentEntity")
                .Select(e => e.Element(V3 + "definingMaterialKind")?.Element(V3 + "code")?.Attribute("code")?.Value)
                .FirstOrDefault(c => c is not null) ?? "";

            var colors = new List<string>();
            string shape = "", imprint = "", image = "";
            double size = 0;
            var score = 1;
            foreach (var characteristic in outer.Elements(V3 + "subjectOf").Elements(V3 + "characteristic"))
            {
                var value = characteristic.Element(V3 + "value");
                if (value is null) continue;
                switch (characteristic.Element(V3 + "code")?.Attribute("code")?.Value)
                {
                    case "SPLCOLOR":
                        var colour = value.Attribute("displayName")?.Value.Trim().ToUpperInvariant();
                        if (!string.IsNullOrEmpty(colour) && !colors.Contains(colour)) colors.Add(colour);
                        break;
                    case "SPLSHAPE":
                        shape = value.Attribute("displayName")?.Value.Trim().ToUpperInvariant() ?? "";
                        break;
                    case "SPLSIZE":
                        double.TryParse(value.Attribute("value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out size);
                        break;
                    case "SPLIMPRINT":
                        imprint = value.Value.Trim();
                        break;
                    case "SPLSCORE":
                        if (int.TryParse(value.Attribute("value")?.Value, CultureInfo.InvariantCulture, out var s) && s > 0) score = s;
                        break;
                    case "SPLIMAGE":
                        image = value.Element(V3 + "reference")?.Attribute("value")?.Value ?? "";
                        break;
                }
            }

            var product = new PillProduct(ndc, name, form, strength, new PillAppearance(colors, shape, size, imprint, score), packages, source, image);
            if (!products.Any(p => p.Ndc == product.Ndc && p.Ndc.Length > 0)) products.Add(product);
        }
        return products;
    }

    private static string Strength(XElement ingredient)
    {
        var numerator = ingredient.Element(V3 + "quantity")?.Element(V3 + "numerator");
        var value = numerator?.Attribute("value")?.Value;
        if (value is null) return "";
        var unit = numerator!.Attribute("unit")?.Value is { } u && u != "1" ? " " + u : "";
        // A liquid's strength is a concentration: "1 mg/mL", "250 mg/5 mL".
        var denominator = ingredient.Element(V3 + "quantity")?.Element(V3 + "denominator");
        if (denominator?.Attribute("unit")?.Value is { } per && per != "1")
            unit += "/" + (denominator.Attribute("value")?.Value is { } count && count != "1" ? count + " " : "") + per;
        var substance = ingredient.Element(V3 + "ingredientSubstance")?.Element(V3 + "name")?.Value;
        return ingredient.Parent?.Elements(V3 + "ingredient").Count(i => i.Attribute("classCode")?.Value is "ACTIB" or "ACTIM" or "ACTIR") > 1 && substance is not null
            ? $"{Title(substance)} {value}{unit}"
            : $"{value}{unit}";
    }

    /// <summary>"LISINOPRIL AND HYDROCHLOROTHIAZIDE" → "Lisinopril and Hydrochlorothiazide": labels shout.</summary>
    private static string Title(string text) =>
        text.Any(char.IsLower) ? text.Trim() : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.Trim().ToLowerInvariant()).Replace(" And ", " and ", StringComparison.Ordinal);

    /// <summary>The pictures of a label, for choosing one by hand when no photo of the product was found.</summary>
    public async Task<List<LabelImage>> ImagesAsync(string setId, CancellationToken cancel = default)
    {
        using var stream = await http.GetStreamAsync($"spls/{Uri.EscapeDataString(setId)}/media.json", cancel).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
        return ParseImages(json);
    }

    internal static List<LabelImage> ParseImages(JsonDocument json) =>
        json.RootElement.GetProperty("data").GetProperty("media").EnumerateArray()
            .Where(e => e.GetProperty("mime_type").GetString()?.StartsWith("image/", StringComparison.Ordinal) == true)
            .Select(e => new LabelImage(e.GetProperty("name").GetString() ?? "", e.GetProperty("url").GetString() ?? ""))
            .Where(i => i.Url.StartsWith("https://", StringComparison.Ordinal))
            .ToList();

    public async Task<byte[]> DownloadAsync(string url, CancellationToken cancel = default) =>
        await http.GetByteArrayAsync(new Uri(url), cancel).ConfigureAwait(false);

    /// <summary>The RxNorm concept (RxCUI) of a package NDC: the drug, strength and form, whoever makes it.</summary>
    public async Task<string> RxCuiAsync(string packageNdc, CancellationToken cancel = default)
    {
        using var stream = await http.GetStreamAsync(new Uri($"{RxNavUrl}ndcstatus.json?ndc={Uri.EscapeDataString(packageNdc)}"), cancel).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
        return ParseRxCui(json);
    }

    internal static string ParseRxCui(JsonDocument json) =>
        json.RootElement.TryGetProperty("ndcStatus", out var status) && status.TryGetProperty("rxcui", out var rxcui)
            ? rxcui.GetString() ?? "" : "";

    /// <summary>
    /// The RxNorm clinical drugs (RxCUIs) of these ingredients at these strengths, in this form: what a product of
    /// another country, or a strength missing from its own maker's label, is the same drug as. The form is said as
    /// Health Canada or a label says it ("Tablet (Extended-Release)"); an extended-release tablet only matches
    /// extended-release tablets.
    /// </summary>
    public async Task<List<string>> ClinicalDrugsAsync(IReadOnlyList<Ingredient> ingredients, string form, CancellationToken cancel = default)
    {
        if (ingredients.Count == 0) return [];
        var name = ingredients[0].Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        using var stream = await http.GetStreamAsync(new Uri($"{RxNavUrl}drugs.json?name={Uri.EscapeDataString(name)}"), cancel).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
        return MatchClinicalDrugs(ParseClinicalDrugs(json), ingredients, form);
    }

    /// <summary>The clinical drugs (SCD) of a drugs.json answer: RxCUI and name.</summary>
    internal static List<(string RxCui, string Name)> ParseClinicalDrugs(JsonDocument json)
    {
        var list = new List<(string, string)>();
        if (!json.RootElement.TryGetProperty("drugGroup", out var group) || !group.TryGetProperty("conceptGroup", out var concepts)) return list;
        foreach (var concept in concepts.EnumerateArray())
        {
            if (concept.TryGetProperty("tty", out var tty) && tty.GetString() == "SCD" && concept.TryGetProperty("conceptProperties", out var properties))
                list.AddRange(properties.EnumerateArray().Select(p => (p.GetProperty("rxcui").GetString() ?? "", p.GetProperty("name").GetString() ?? "")));
        }
        return list;
    }

    /// <summary>Those of the clinical drugs with each ingredient at its strength, in the same kind of form.</summary>
    internal static List<string> MatchClinicalDrugs(IEnumerable<(string RxCui, string Name)> drugs, IReadOnlyList<Ingredient> ingredients, string form)
    {
        var f = form.ToUpperInvariant();
        var extended = Regex.IsMatch(f, "EXTENDED|SUSTAINED|CONTROLLED|PROLONGED|MODIFIED|SLOW");
        var delayed = Regex.IsMatch(f, "DELAYED|ENTERIC");
        var required = new[] { "TABLET", "CAPSULE", "CHEWABLE", "DISINTEGRATING" }.Where(word => f.Contains(word, StringComparison.Ordinal)).ToList();
        return drugs
            .Where(d =>
            {
                var name = d.Name.ToUpperInvariant();
                if (ingredients.Count != name.Split(" / ").Length) return false;
                if (!ingredients.All(i => Regex.IsMatch(name, $@"\b{Regex.Escape(i.Name.Split(' ')[0].ToUpperInvariant())}\b.*?\b{Regex.Escape(i.Strength)} {Regex.Escape(i.Unit.ToUpperInvariant())}\b"))) return false;
                if (!required.All(r => name.Contains(r, StringComparison.Ordinal))) return false;
                return name.Contains("EXTENDED RELEASE", StringComparison.Ordinal) == extended && name.Contains("DELAYED RELEASE", StringComparison.Ordinal) == delayed;
            })
            .Select(d => d.RxCui)
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The different pills of these clinical drugs (RxCUIs) on their labels, each once, the most common first: what
    /// the drug looks like from each maker. Only the products of <paramref name="strength"/> ("30 mg") are kept. Up to
    /// <paramref name="maxLabels"/> labels are read.
    /// </summary>
    public async Task<List<LookAlike>> LookAlikesAsync(IReadOnlyList<string> rxCuis, string strength, IProgress<string>? progress = null,
        int maxLabels = 80, CancellationToken cancel = default)
    {
        var labels = new List<LabelSummary>();
        foreach (var rxCui in rxCuis)
            labels.AddRange((await LabelsAsync("rxcui", rxCui, cancel).ConfigureAwait(false)).Where(l => labels.All(x => x.SetId != l.SetId)));
        labels = labels.Take(maxLabels).ToList();
        var found = new List<(PillProduct Product, LabelSummary Label)>();
        using var limit = new SemaphoreSlim(6);
        var done = 0;
        var wanted = Regex.Replace(strength.ToLowerInvariant(), @"\s+", "");
        await Task.WhenAll(labels.Select(async label =>
        {
            await limit.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                var products = await ProductsAsync(label.SetId, cancel).ConfigureAwait(false);
                lock (found)
                    found.AddRange(products
                        .Where(p => p.Appearance.IsKnown && (wanted.Length == 0 || Regex.Replace(p.Strength.ToLowerInvariant(), @"\s+", "") == wanted))
                        .Select(p => (p, label)));
            }
            catch (Exception exception) when (exception is HttpRequestException or XmlException)
            {
                // A label that can't be read is passed over.
            }
            finally
            {
                limit.Release();
                progress?.Report($"Reading the labels of the same drug ({Interlocked.Increment(ref done)} of {labels.Count})…");
            }
        })).ConfigureAwait(false);
        return LookAlike.Group(found);
    }

    /// <summary>The photo a label gives a product (its <see cref="PillProduct.Image"/>).</summary>
    public static PillPhoto PhotoOf(PillProduct product, LabelSummary label, string source) => Photo(label.SetId, product.Image, source);

    private static PillPhoto Photo(string setId, string image, string source) =>
        new($"{setId}-{image}", $"{ImageUrl}?setid={Uri.EscapeDataString(setId)}&name={Uri.EscapeDataString(image)}", source);

    /// <summary>
    /// Finds a photo of this very pill. In order: the photo the product's own label gives it; for a repackaged pill,
    /// the photo on the label of the product it repackages (its source NDC); and last, among the labels of the same
    /// RxNorm concept (drug, strength and form), a product with a photo and the same imprint, shape and colour, which
    /// is the same pill from the same maker. Null when none of them has one.
    /// </summary>
    /// <param name="rxCui">The product's RxCUI if already known; it is looked up from its NDC otherwise.</param>
    /// <param name="progress">Told what is being looked at, for showing while it searches.</param>
    public async Task<(PillPhoto? Photo, string RxCui)> FindPhotoAsync(PillProduct product, LabelSummary label, string rxCui,
        IProgress<string>? progress = null, CancellationToken cancel = default)
    {
        if (product.Image.Length > 0) return (Photo(label.SetId, product.Image, "the photo on this label"), rxCui);

        var checkedLabels = new HashSet<string> { label.SetId };
        if (product.SourceNdc.Length > 0)
        {
            progress?.Report("Looking at the manufacturer's label…");
            foreach (var source in (await LabelsAsync("ndc", product.SourceNdc, cancel).ConfigureAwait(false)).Take(5))
            {
                checkedLabels.Add(source.SetId);
                var original = (await ProductsAsync(source.SetId, cancel).ConfigureAwait(false)).FirstOrDefault(p => p.Ndc == product.SourceNdc);
                if (original?.Image.Length > 0) return (Photo(source.SetId, original.Image, "the manufacturer's label, " + source.Title), rxCui);
            }
        }

        if (!product.Appearance.IsKnown || product.Appearance.Imprint.Length == 0) return (null, rxCui);
        if (rxCui.Length == 0)
        {
            var ndc = product.PackageNdcs.FirstOrDefault() ?? product.SourceNdc;
            if (ndc.Length == 0) return (null, rxCui);
            progress?.Report("Looking up the drug in RxNorm…");
            rxCui = await RxCuiAsync(ndc, cancel).ConfigureAwait(false);
            if (rxCui.Length == 0) return (null, rxCui);
        }

        var others = (await LabelsAsync("rxcui", rxCui, cancel).ConfigureAwait(false))
            .Where(l => !checkedLabels.Contains(l.SetId))
            .Take(MaxLabelsToSearch)
            .ToList();
        using var found = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        using var limit = new SemaphoreSlim(6);
        PillPhoto? match = null;
        var done = 0;
        await Task.WhenAll(others.Select(async other =>
        {
            try
            {
                await limit.WaitAsync(found.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                var same = (await ProductsAsync(other.SetId, found.Token).ConfigureAwait(false))
                    .FirstOrDefault(p => p.Image.Length > 0 && p.Appearance.SamePillAs(product.Appearance));
                if (same is not null && Interlocked.CompareExchange(ref match, Photo(other.SetId, same.Image, "another label of the same pill, " + other.Title), null) is null)
                    await found.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or XmlException or OperationCanceledException)
            {
                // A label that can't be read is passed over.
            }
            finally
            {
                limit.Release();
                progress?.Report($"Looking for a photo of this pill in other labels ({Interlocked.Increment(ref done)} of {others.Count})…");
            }
        })).ConfigureAwait(false);
        cancel.ThrowIfCancellationRequested();
        return (match, rxCui);
    }
}
