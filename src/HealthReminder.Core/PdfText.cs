using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Strongthium.HealthReminder;

/// <summary>
/// The text of a PDF, roughly: enough to find a sentence in a product monograph, not to lay it out. It reads the
/// page content streams (deflated or not, and those packed in object streams), and the text-showing operators in them
/// (Tj, TJ, ' and "), decoding each string through its font's ToUnicode map when it has one, or else as Windows-1252,
/// which is what word processors write for Latin text. Lines are broken where the text moves down the page.
/// </summary>
public static partial class PdfText
{
    public static string Extract(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var objects = new Dictionary<int, (string Dict, byte[]? Stream)>();

        // Every "n 0 obj ... endobj", with its stream inflated.
        foreach (Match match in ObjectPattern().Matches(raw))
        {
            var number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var body = match.Groups[2].Value;
            var streamAt = StreamPattern().Match(body);
            if (!streamAt.Success)
            {
                objects[number] = (body, null);
                continue;
            }
            var dict = body[..streamAt.Index];
            var start = match.Groups[2].Index + streamAt.Index + streamAt.Length;
            var end = raw.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0) continue;
            objects[number] = (dict, Decode(dict, pdf, start, end - start));
        }

        // Objects packed in object streams: "n offset n offset ..." then the objects.
        foreach (var (dict, stream) in objects.Values.ToList())
        {
            if (stream is null || !dict.Contains("/ObjStm", StringComparison.Ordinal)) continue;
            var first = Number(dict, "First");
            var count = Number(dict, "N");
            var text = Encoding.Latin1.GetString(stream);
            if (first <= 0 || first > text.Length) continue;
            var header = text[..first].Split((char[])[' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 1 < header.Length && i / 2 < count; i += 2)
            {
                if (!int.TryParse(header[i], CultureInfo.InvariantCulture, out var number) || !int.TryParse(header[i + 1], CultureInfo.InvariantCulture, out var offset)) continue;
                var from = first + offset;
                var to = i + 3 < header.Length && int.TryParse(header[i + 3], CultureInfo.InvariantCulture, out var next) ? first + next : text.Length;
                if (from < text.Length && to <= text.Length && to > from) objects.TryAdd(number, (text[from..to], null));
            }
        }

        // Each font's ToUnicode map, by its object number.
        var fontMaps = new Dictionary<int, CMap>();
        foreach (var (number, (dict, _)) in objects)
        {
            var toUnicode = ToUnicodePattern().Match(dict);
            if (!toUnicode.Success || !objects.TryGetValue(Int(toUnicode.Groups[1].Value), out var cmap) || cmap.Stream is null) continue;
            fontMaps[number] = ParseCMap(Encoding.Latin1.GetString(cmap.Stream));
        }

        // The pages in order, each with its fonts by the names its content uses for them (/F1): the same name is a
        // different font on another page.
        string Dict(int number) => objects.TryGetValue(number, out var o) ? o.Dict : "";
        Dictionary<string, CMap> Fonts(string page)
        {
            var fonts = new Dictionary<string, CMap>();
            var resources = Regex.Match(page, @"/Resources\s+(\d+)\s+\d+\s+R") is { Success: true } r ? Dict(Int(r.Groups[1].Value)) : page;
            var fontDict = Regex.Match(resources, @"/Font\s+(\d+)\s+\d+\s+R") is { Success: true } f ? Dict(Int(f.Groups[1].Value))
                : Regex.Match(resources, @"/Font\s*<<(.*?)>>", RegexOptions.Singleline) is { Success: true } inline ? inline.Groups[1].Value : "";
            foreach (Match font in FontEntryPattern().Matches(fontDict))
                if (fontMaps.TryGetValue(Int(font.Groups[2].Value), out var map)) fonts[font.Groups[1].Value] = map;
            return fonts;
        }
        var pages = new List<int>();
        void Walk(int number, int depth)
        {
            var dict = Dict(number);
            if (depth > 32 || dict.Length == 0) return;
            if (PageTypePattern().IsMatch(dict)) pages.Add(number);
            else if (Regex.Match(dict, @"/Kids\s*\[(.*?)\]", RegexOptions.Singleline) is { Success: true } kids)
                foreach (Match kid in Regex.Matches(kids.Groups[1].Value, @"(\d+)\s+\d+\s+R")) Walk(Int(kid.Groups[1].Value), depth + 1);
        }
        var root = objects.Values.Select(o => Regex.Match(o.Dict, @"/Type\s*/Catalog.*?/Pages\s+(\d+)|/Pages\s+(\d+)\s+\d+\s+R.*?/Type\s*/Catalog", RegexOptions.Singleline))
            .FirstOrDefault(m => m.Success);
        if (root is not null) Walk(Int(root.Groups[1].Success ? root.Groups[1].Value : root.Groups[2].Value), 0);
        if (pages.Count == 0) pages = objects.Where(o => PageTypePattern().IsMatch(o.Value.Dict)).Select(o => o.Key).Order().ToList();

        var output = new StringBuilder();
        foreach (var page in pages)
        {
            var dict = Dict(page);
            var fonts = Fonts(dict);
            var contents = Regex.Match(dict, @"/Contents\s*\[(.*?)\]", RegexOptions.Singleline) is { Success: true } list
                ? Regex.Matches(list.Groups[1].Value, @"(\d+)\s+\d+\s+R").Select(m => Int(m.Groups[1].Value))
                : Regex.Match(dict, @"/Contents\s+(\d+)\s+\d+\s+R") is { Success: true } one ? [Int(one.Groups[1].Value)] : [];
            foreach (var content in contents)
            {
                if (objects.TryGetValue(content, out var o) && o.Stream is not null) ReadContent(Encoding.Latin1.GetString(o.Stream), fonts, output);
            }
            output.Append('\n');
        }
        return output.ToString();
    }

    private static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);

    private static int Number(string dict, string key) =>
        Regex.Match(dict, $@"/{key}\s+(\d+)") is { Success: true } m ? Int(m.Groups[1].Value) : 0;

    /// <summary>A font's ToUnicode map, and whether its codes are two bytes (as in an Identity-H font) or one.</summary>
    internal sealed record CMap(Dictionary<int, string> Map, bool Wide);

    private static byte[]? Decode(string dict, byte[] pdf, int start, int length)
    {
        if (length <= 0) return null;
        if (!dict.Contains("/Filter", StringComparison.Ordinal)) return pdf.AsSpan(start, length).ToArray();
        if (!dict.Contains("/FlateDecode", StringComparison.Ordinal)) return null;
        try
        {
            using var inflater = new ZLibStream(new MemoryStream(pdf, start, length), CompressionMode.Decompress);
            using var output = new MemoryStream();
            inflater.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>A ToUnicode CMap's bfchar and bfrange entries: code to text.</summary>
    internal static CMap ParseCMap(string cmap)
    {
        var wide = Regex.Match(cmap, @"begincodespacerange\s*<([0-9A-Fa-f]+)>") is { Success: true } space ? space.Groups[1].Value.Length >= 4 : false;
        var map = new Dictionary<int, string>();
        static string Text(string hex)
        {
            var builder = new StringBuilder();
            for (var i = 0; i + 4 <= hex.Length; i += 4) builder.Append((char)Convert.ToInt32(hex.Substring(i, 4), 16));
            return builder.ToString();
        }
        foreach (Match block in Regex.Matches(cmap, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline))
            foreach (Match pair in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                map[Convert.ToInt32(pair.Groups[1].Value, 16)] = Text(pair.Groups[2].Value);
        foreach (Match block in Regex.Matches(cmap, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline))
        {
            // <low> <high> <first>: consecutive codes to consecutive characters.
            foreach (Match range in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
            {
                int low = Convert.ToInt32(range.Groups[1].Value, 16), high = Convert.ToInt32(range.Groups[2].Value, 16);
                var target = Convert.ToInt32(range.Groups[3].Value, 16);
                for (var code = low; code <= high && code - low < 65536; code++) map[code] = ((char)(target + code - low)).ToString();
            }
            // <low> <high> [<a> <b> ...]: each code its own text.
            foreach (Match range in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<[0-9A-Fa-f]+>\s*\[([^\]]*)\]"))
            {
                var code = Convert.ToInt32(range.Groups[1].Value, 16);
                foreach (Match target in Regex.Matches(range.Groups[2].Value, @"<([0-9A-Fa-f]+)>")) map[code++] = Text(target.Groups[1].Value);
            }
        }
        return new CMap(map, wide || map.Keys.DefaultIfEmpty().Max() > 0xFF);
    }

    /// <summary>Reads the text out of a content stream, a token at a time.</summary>
    private static void ReadContent(string content, Dictionary<string, CMap> maps, StringBuilder output)
    {
        CMap? map = null;
        var operands = new List<object>();
        double lastY = double.NaN;
        var i = 0;

        void NewLine()
        {
            if (output.Length > 0 && output[^1] != '\n') output.Append('\n');
        }
        void Show(object operand)
        {
            if (operand is byte[] bytes) output.Append(DecodeString(bytes, map));
            else if (operand is List<object> array)
            {
                foreach (var part in array)
                {
                    if (part is byte[] b) output.Append(DecodeString(b, map));
                    else if (part is double gap && gap < -200 && output.Length > 0 && output[^1] != ' ') output.Append(' ');
                }
            }
        }
        void MoveTo(double y)
        {
            if (!double.IsNaN(lastY) && Math.Abs(y - lastY) > 1) NewLine();
            else if (output.Length > 0 && output[^1] is not (' ' or '\n')) output.Append(' ');
            lastY = y;
        }

        while (i < content.Length)
        {
            var c = content[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '%') { while (i < content.Length && content[i] is not ('\n' or '\r')) i++; continue; }
            if (c == '(') { operands.Add(ReadLiteral(content, ref i)); continue; }
            if (c == '<' && i + 1 < content.Length && content[i + 1] == '<') { i = SkipDictionary(content, i); continue; }
            if (c == '<') { operands.Add(ReadHex(content, ref i)); continue; }
            if (c == '[')
            {
                i++;
                var array = new List<object>();
                while (i < content.Length && content[i] != ']')
                {
                    var d = content[i];
                    if (char.IsWhiteSpace(d)) i++;
                    else if (d == '(') array.Add(ReadLiteral(content, ref i));
                    else if (d == '<') array.Add(ReadHex(content, ref i));
                    else
                    {
                        var start = i;
                        while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] is not ('(' or '<' or ']')) i++;
                        if (double.TryParse(content.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) array.Add(n);
                        if (i == start) i++;
                    }
                }
                i++;
                operands.Add(array);
                continue;
            }
            if (c == '/')
            {
                var start = i++;
                while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] is not ('/' or '[' or '(' or '<')) i++;
                operands.Add(content[start..i]);
                continue;
            }

            var tokenStart = i;
            while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] is not ('/' or '[' or '(' or '<')) i++;
            if (i == tokenStart) { i++; continue; }
            var token = content[tokenStart..i];
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                operands.Add(number);
                continue;
            }
            switch (token)
            {
                case "Tf":
                    map = operands.Count >= 2 && operands[^2] is string name && maps.TryGetValue(name[1..], out var m) ? m : null;
                    break;
                case "Tj":
                    if (operands.Count > 0) Show(operands[^1]);
                    break;
                case "TJ":
                    if (operands.Count > 0) Show(operands[^1]);
                    break;
                case "'":
                case "\"":
                    NewLine();
                    if (operands.Count > 0) Show(operands[^1]);
                    break;
                case "Td":
                case "TD":
                    if (operands.Count >= 2 && operands[^1] is double dy)
                    {
                        if (Math.Abs(dy) > 1) NewLine();
                        else if (output.Length > 0 && output[^1] is not (' ' or '\n')) output.Append(' ');
                        if (!double.IsNaN(lastY)) lastY += dy;
                    }
                    break;
                case "Tm":
                    if (operands.Count >= 6 && operands[^1] is double y) MoveTo(y);
                    break;
                case "T*":
                    NewLine();
                    break;
                case "ET":
                    if (output.Length > 0 && output[^1] is not (' ' or '\n')) output.Append(' ');
                    break;
                case "BI":
                    // An inline image: skip its data.
                    var end = content.IndexOf("EI", i, StringComparison.Ordinal);
                    i = end < 0 ? content.Length : end + 2;
                    break;
            }
            operands.Clear();
        }
    }

    private static int SkipDictionary(string content, int i)
    {
        var depth = 0;
        while (i < content.Length - 1)
        {
            if (content[i] == '<' && content[i + 1] == '<') { depth++; i += 2; }
            else if (content[i] == '>' && content[i + 1] == '>') { depth--; i += 2; if (depth == 0) return i; }
            else i++;
        }
        return content.Length;
    }

    private static byte[] ReadLiteral(string content, ref int i)
    {
        var bytes = new List<byte>();
        var depth = 0;
        i++;
        while (i < content.Length)
        {
            var c = content[i++];
            if (c == '\\' && i < content.Length)
            {
                var e = content[i++];
                switch (e)
                {
                    case 'n': bytes.Add((byte)'\n'); break;
                    case 'r': bytes.Add((byte)'\r'); break;
                    case 't': bytes.Add((byte)'\t'); break;
                    case 'b': bytes.Add(8); break;
                    case 'f': bytes.Add(12); break;
                    case '\r': if (i < content.Length && content[i] == '\n') i++; break;
                    case '\n': break;
                    case >= '0' and <= '7':
                        var octal = e - '0';
                        for (var k = 0; k < 2 && i < content.Length && content[i] is >= '0' and <= '7'; k++) octal = octal * 8 + (content[i++] - '0');
                        bytes.Add((byte)octal);
                        break;
                    default: bytes.Add((byte)e); break;
                }
            }
            else if (c == '(') { depth++; bytes.Add((byte)c); }
            else if (c == ')') { if (depth-- == 0) break; bytes.Add((byte)c); }
            else bytes.Add((byte)c);
        }
        return [.. bytes];
    }

    private static byte[] ReadHex(string content, ref int i)
    {
        var end = content.IndexOf('>', i);
        if (end < 0) end = content.Length;
        var hex = string.Concat(content.AsSpan(i + 1, end - i - 1).ToString().Where(Uri.IsHexDigit));
        i = end + 1;
        if (hex.Length % 2 == 1) hex += "0";
        return Convert.FromHexString(hex);
    }

    // Windows-1252's 0x80 to 0x9F, which Latin-1 leaves as control characters.
    private const string Cp1252High = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ";

    private static string DecodeString(byte[] bytes, CMap? cmap)
    {
        var text = new StringBuilder();
        if (cmap is { Map.Count: > 0 })
        {
            var (map, wide) = (cmap.Map, cmap.Wide);
            for (var k = 0; k < bytes.Length; k += wide ? 2 : 1)
            {
                var code = wide && k + 1 < bytes.Length ? bytes[k] << 8 | bytes[k + 1] : bytes[k];
                text.Append(map.TryGetValue(code, out var s) ? s : wide ? "" : ((char)code).ToString());
            }
            return text.ToString();
        }
        foreach (var b in bytes) text.Append(b is >= 0x80 and <= 0x9F ? Cp1252High[b - 0x80] : (char)b);
        return text.ToString();
    }

    [GeneratedRegex(@"(\d+)\s+\d+\s+obj\b(.*?)\bendobj", RegexOptions.Singleline)]
    private static partial Regex ObjectPattern();

    [GeneratedRegex(@"stream\r?\n")]
    private static partial Regex StreamPattern();

    [GeneratedRegex(@"/ToUnicode\s+(\d+)\s+\d+\s+R")]
    private static partial Regex ToUnicodePattern();

    [GeneratedRegex(@"/Type\s*/Page(?![a-zA-Z])")]
    private static partial Regex PageTypePattern();

    [GeneratedRegex(@"/([A-Za-z0-9_.+-]+)\s+(\d+)\s+\d+\s+R")]
    private static partial Regex FontEntryPattern();
}
