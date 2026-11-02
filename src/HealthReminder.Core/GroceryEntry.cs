using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>
/// One line typed the way the package says it, read: "Bick's Gourmet Tangy Dill Relish 370 ml" is Gourmet Tangy Dill
/// Relish, by Bick's, 370 ml. An amount with a unit, at either end, is an amount; a brand seen before, at either end,
/// is that brand. Not <paramref name="Sure"/> when it's a guess: a bare number (eggs 12, but Heinz 57), a first word
/// like Bick's or Shepherd's that's never been a brand, or an amount both in the line and in the box for it. Those are
/// asked about. A name there's been already, or that the catalog knows, is left whole, whatever it looks like (7 Up).
/// </summary>
public sealed record GroceryEntry(string Name, string? Note, string Quantity, bool Sure)
{
    private const string Number = @"(?:\d+(?:[.,]\d+)?(?:\s+\d+/\d+|\s*[½¼¾⅓⅔])?|\d+/\d+|[½¼¾⅓⅔])";
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Longest first: "fl oz" before "oz", "kg" before "g".
    private static readonly string Unit = "(?:" + string.Join("|", Quantities.UnitWords.OrderByDescending(w => w.Length).Select(Regex.Escape)) + @")(?:e?s)?\b\.?";
    private static readonly string Measure = $@"(?:\d+\s*[x×*]\s*)?{Number}[\s-]*{Unit}";

    private static readonly Regex MeasureAtEnd = new($@"(?:^|[\s,(–-])(?<amount>{Measure})\)?$", Options);
    private static readonly Regex MeasureAtStart = new($@"^(?<amount>{Measure})(?:\s+of)?\s+(?<rest>.+)$", Options);
    private static readonly Regex WordsAtStart = new($@"^(?<amount>an?\s+{Unit})\s+of\s+(?<rest>.+)$|^(?<amount>a\s+dozen)\s+(?<rest>.+)$", Options);
    private static readonly Regex DozenAtEnd = new(@"\s(?<amount>(?:a\s+|1\s+)?dozen)$", Options);
    private static readonly Regex TimesAtEnd = new(@"\s[x×*]\s*(?<amount>\d+)$", Options);
    private static readonly Regex TimesAtStart = new(@"^(?<amount>\d+)\s*[x×*]\s+(?<rest>.+)$", Options);
    private static readonly Regex CountAtEnd = new(@"\s(?<amount>\d+(?:[.,]\d+)?)$", Options);
    private static readonly Regex CountAtStart = new(@"^(?<amount>\d+(?:[.,]\d+)?)\s+(?<rest>\D.*)$", Options);
    private static readonly Regex Possessive = new(@"^(?<brand>[\p{L}&.-]+'s)\s+(?<rest>.+)$", Options);

    private static readonly char[] Edges = [' ', ',', '-', '–', '(', ')'];

    /// <summary>What's typed as the name, and in the box for how much (""), read with the names and brands there have
    /// been, each as it's spelled.</summary>
    public static GroceryEntry Read(string typed, string quantity, IReadOnlyCollection<string> names, IReadOnlyCollection<string> brands)
    {
        var same = StringComparer.OrdinalIgnoreCase;
        var text = string.Join(' ', typed.Replace('’', '\'').Split(' ', StringSplitOptions.RemoveEmptyEntries));
        quantity = quantity.Trim();
        string? Known(string name) => names.FirstOrDefault(n => same.Equals(n, name)) ?? GroceryCatalog.Get(name)?.Name;
        if (Known(text) is { } whole) return new(whole, null, quantity, true);

        var (name, amount, sure) = Amount(text, Known);
        var note = default(string);
        if (Known(name) is { } known) name = known;
        else
        {
            foreach (var brand in brands.Where(b => b.Length > 0).OrderByDescending(b => b.Length))
            {
                if (name.StartsWith(brand + " ", StringComparison.OrdinalIgnoreCase)) (note, name) = (brand, name[(brand.Length + 1)..].Trim());
                else if (name.EndsWith(" " + brand, StringComparison.OrdinalIgnoreCase)) (note, name) = (brand, name[..^(brand.Length + 1)].Trim());
                else continue;
                break;
            }
            if (note is null && Possessive.Match(name) is { Success: true } match)
            {
                (note, name) = (match.Groups["brand"].Value, match.Groups["rest"].Value);
                sure = false;
            }
            name = Known(name) ?? name;
        }
        // Both said: what's in the box is what counts, but that's worth a look.
        if (quantity.Length > 0)
        {
            sure &= amount.Length == 0;
            amount = quantity;
        }
        return new(name, note, amount, sure);
    }

    /// <summary>The name with its amount taken out, the amount, and whether that's more than a guess.</summary>
    private static (string Name, string Amount, bool Sure) Amount(string text, Func<string, string?> known)
    {
        // A name has a letter in it: a line that's all amount stays as it is, for what's asked to say so.
        (string, string, bool) Taken(string rest, string amount, bool sure)
        {
            rest = rest.Trim(Edges);
            return rest.Any(char.IsLetter) ? (rest, amount.Trim(), sure) : (text, "", sure);
        }
        if (MeasureAtEnd.Match(text) is { Success: true } end)
        {
            var amount = end.Groups["amount"].Value.Replace('-', ' ');
            var rest = text[..end.Index];
            // "2 x Coke 355 mL" is two of them.
            if (TimesAtStart.Match(rest) is { Success: true } times) (rest, amount) = (times.Groups["rest"].Value, $"{times.Groups["amount"].Value} x {amount}");
            return Taken(rest, amount, true);
        }
        if (MeasureAtStart.Match(text) is { Success: true } start) return Taken(start.Groups["rest"].Value, start.Groups["amount"].Value.Replace('-', ' '), true);
        if (WordsAtStart.Match(text) is { Success: true } words) return Taken(words.Groups["rest"].Value, words.Groups["amount"].Value, true);
        if (DozenAtEnd.Match(text) is { Success: true } dozen)
            return Taken(text[..dozen.Index], dozen.Groups["amount"].Value.Equals("dozen", StringComparison.OrdinalIgnoreCase) ? "a dozen" : dozen.Groups["amount"].Value, true);
        if (TimesAtEnd.Match(text) is { Success: true } after) return Taken(text[..after.Index], after.Groups["amount"].Value, true);
        if (TimesAtStart.Match(text) is { Success: true } before) return Taken(before.Groups["rest"].Value, before.Groups["amount"].Value, true);
        if (CountAtEnd.Match(text) is { Success: true } last) return Taken(text[..last.Index], last.Groups["amount"].Value, false);
        // "12 eggs" is twelve; "7 Up" is asked about, unless what's after the number is known food.
        if (CountAtStart.Match(text) is { Success: true } first)
            return Taken(first.Groups["rest"].Value, first.Groups["amount"].Value, known(first.Groups["rest"].Value) is not null);
        return (text, "", true);
    }
}
