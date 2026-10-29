using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strongthium.HealthReminder;

/// <summary>A food seen in a photo: what, how much, where it's kept, and how many days it's good for (0: months).</summary>
public sealed record SeenFood(string Name, string Quantity, string Where, int UseWithinDays, string Confidence);

/// <summary>A meal idea from what's in the kitchen, as a recipe for one.</summary>
public sealed record KitchenIdea(string Name, string Why, int Kcal, int PrepMinutes, int CookMinutes, string[] Uses, string[] Missing,
    IdeaIngredient[] Ingredients, IdeaStep[] Steps)
{
    /// <summary>The idea as a recipe, for the steps at the side of the screen, or picked for a meal (kept by
    /// <paramref name="id"/>).</summary>
    public Recipe ToRecipe(MealSlot slot, string id = "idea") => new(id, Name, [slot], Kcal, PrepMinutes, CookMinutes, 0, [],
        Ingredients.Select(i => new FoodItem(i.Amount, i.Name, "")).ToArray(), Steps.Select(s => new CookStep(s.Text, s.Minutes)).ToArray());
}

public sealed record IdeaIngredient(string Amount, string Name);

public sealed record IdeaStep(string Text, int Minutes);

/// <summary>What's in the kitchen, for ideas: a food, how much, and when it's best used by, if it doesn't keep.</summary>
public sealed record KitchenFood(string Name, string Quantity, DateTime? UseBy);

/// <summary>Why the kitchen assistant couldn't help, in words for the user.</summary>
public sealed class KitchenAssistantException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Claude, through the Anthropic API (its HTTP Messages API, called directly) with the user's own API key, for two
/// things the app can't do itself: reading photos of groceries (a fridge, a cupboard, a haul on the counter, a receipt)
/// into a list, and suggesting meals for one from what's in the kitchen, using first what won't keep. Only the photos,
/// the list of food and the meal's calories are sent; nothing about medication or health. Answers come back as JSON in a
/// set shape (structured outputs).
/// </summary>
public sealed class KitchenAssistant(string apiKey, HttpClient? http = null)
{
    public const string Model = "claude-opus-5-5";
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2026-10-21";
    // Fallbacks: "default" lets the API choose a model to answer if a safety classifier declines.
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    /// <summary>A reading of several photos can take a minute or two.</summary>
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(5) };

    private readonly HttpClient http = http ?? SharedHttp;

    private const string PhotoInstructions =
        "You read photos of someone's groceries: the inside of a fridge or freezer, a cupboard or pantry shelf, groceries " +
        "laid out on a counter, or a grocery receipt. List each food or drink you can identify, once, with how much there " +
        "looks to be in the way it's sold or measured (\"12\", \"2 L\", \"about half a 454 g pack\", \"1 loaf\"), where it's " +
        "kept (fridge, freezer, cupboard or counter), and how many days it's good for from now, judging from what it is and " +
        "how it looks (0 when it keeps for months, as dry goods, canned and frozen food do). On a receipt, read the items " +
        "bought, not prices, and expand abbreviations to plain names. Skip what isn't food. Say \"likely\" when you're not " +
        "sure what something is. When an item is one of the names given, use exactly that name.";

    private const string IdeaInstructions =
        "You suggest meals for one person cooking for themself at home, from what's in their kitchen. Give 3 ideas that " +
        "use what they have, the food that won't keep first; each may need at most two or three things they don't have. " +
        "Keep them plain and everyday, one serving, near the calories given. Measure ingredients the way they're sold and " +
        "used: dry pasta and rice by weight, meat by the piece or by weight, eggs by count; spoons and cups only for small " +
        "or liquid things. Give the steps in order, each short, with the minutes for any that take time (simmer, bake, " +
        "rest). For meat and fish, say the safe inside temperature: poultry 74 °C (165 °F), ground meat 71 °C (160 °F), " +
        "whole cuts of beef and pork and fish 63 °C (145 °F) with a rest for beef and pork. Don't give health or medical " +
        "advice. In \"why\", one sentence on what it uses up.";

    /// <summary>Reads what food is in the photos (JPEG or PNG), naming what's in the catalog by its name there.</summary>
    public async Task<List<SeenFood>> ReadPhotosAsync(IReadOnlyList<(byte[] Data, string MediaType)> photos, IEnumerable<string> knownNames, CancellationToken cancel = default)
    {
        var content = photos.Select(p => (Block)new ImageBlock(p.MediaType, Convert.ToBase64String(p.Data))).ToList();
        content.Add(new TextBlock("Names to use when an item is one of them: " + string.Join(", ", knownNames) + "."));
        var answer = await AskAsync(Request(PhotoInstructions, content, PhotoSchema), cancel);
        return JsonSerializer.Deserialize(answer, AnswerJson.Default.PhotoAnswer)?.Items ?? [];
    }

    /// <summary>Three meal ideas for a meal of about <paramref name="kcal"/>, from what's in the kitchen. With
    /// <paramref name="instead"/>, it's time to eat: only what's there, and other than that meal, when it's said.</summary>
    public async Task<List<KitchenIdea>> IdeasAsync(IReadOnlyList<KitchenFood> kitchen, MealSlot slot, double kcal, string? feel, DateTime now,
        string? instead = null, CancellationToken cancel = default)
    {
        var answer = await AskAsync(Request(IdeaInstructions, [new TextBlock(IdeasAsk(kitchen, slot, kcal, feel, now, instead))], IdeasSchema), cancel);
        return JsonSerializer.Deserialize(answer, AnswerJson.Default.IdeasAnswer)?.Ideas ?? [];
    }

    internal static string IdeasAsk(IReadOnlyList<KitchenFood> kitchen, MealSlot slot, double kcal, string? feel, DateTime now, string? instead)
    {
        var list = string.Join("\n", kitchen.Select(f => $"- {f.Name}" + (f.Quantity.Length > 0 ? $" ({f.Quantity})" : "")
            + (f.UseBy is { } by ? $", best used by {by:dddd MMMM d}" : "")));
        return $"Today is {now:dddd MMMM d}. The meal is {slot.ToString().ToLowerInvariant()}, about {kcal:0} kcal."
            + (feel is { Length: > 0 } ? $" They feel like: {feel}." : "")
            + (instead is null ? "" : " It's time to eat now, so use only what they have" + (instead.Length > 0 ? $", and something other than: {instead}." : "."))
            + $"\n\nIn their kitchen:\n{list}\n\nStaples like oil, salt, pepper and common spices can be assumed.";
    }

    // The shapes of the answers (structured outputs): every object closed, every field required.
    private const string PhotoSchema = """
        {"type": "object", "additionalProperties": false, "required": ["items"], "properties": {
          "items": {"type": "array", "items": {"type": "object", "additionalProperties": false,
            "required": ["name", "quantity", "where", "use_within_days", "confidence"], "properties": {
              "name": {"type": "string"}, "quantity": {"type": "string"},
              "where": {"type": "string", "enum": ["fridge", "freezer", "cupboard", "counter"]},
              "use_within_days": {"type": "integer"},
              "confidence": {"type": "string", "enum": ["sure", "likely"]}}}}}}
        """;

    private const string IdeasSchema = """
        {"type": "object", "additionalProperties": false, "required": ["ideas"], "properties": {
          "ideas": {"type": "array", "items": {"type": "object", "additionalProperties": false,
            "required": ["name", "why", "kcal", "prep_minutes", "cook_minutes", "uses", "missing", "ingredients", "steps"], "properties": {
              "name": {"type": "string"}, "why": {"type": "string"}, "kcal": {"type": "integer"},
              "prep_minutes": {"type": "integer"}, "cook_minutes": {"type": "integer"},
              "uses": {"type": "array", "items": {"type": "string"}},
              "missing": {"type": "array", "items": {"type": "string"}},
              "ingredients": {"type": "array", "items": {"type": "object", "additionalProperties": false, "required": ["amount", "name"],
                "properties": {"amount": {"type": "string"}, "name": {"type": "string"}}}},
              "steps": {"type": "array", "items": {"type": "object", "additionalProperties": false, "required": ["text", "minutes"],
                "properties": {"text": {"type": "string"}, "minutes": {"type": "integer"}}}}}}}}}
        """;

    internal const string PhotoRequestSchema = PhotoSchema;

    internal const string IdeasRequestSchema = IdeasSchema;

    internal abstract record Block;

    internal sealed record TextBlock(string Text) : Block;

    internal sealed record ImageBlock(string MediaType, string Base64) : Block;

    /// <summary>The body of a request: Claude Opus 5.5 at medium effort, one user message, the answer in the schema's
    /// shape, and the server's own fallback models if a safety classifier declines.</summary>
    internal static string Request(string system, IEnumerable<Block> content, string schema)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("model", Model);
            json.WriteNumber("max_tokens", 16000);
            json.WriteString("fallbacks", "default");
            json.WriteString("system", system);
            json.WriteStartObject("output_config");
            json.WriteString("effort", "medium");
            json.WriteStartObject("format");
            json.WriteString("type", "json_schema");
            json.WritePropertyName("schema");
            using (var parsed = JsonDocument.Parse(schema)) parsed.RootElement.WriteTo(json);
            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteStartArray("messages");
            json.WriteStartObject();
            json.WriteString("role", "user");
            json.WriteStartArray("content");
            foreach (var block in content)
            {
                json.WriteStartObject();
                switch (block)
                {
                    case TextBlock text:
                        json.WriteString("type", "text");
                        json.WriteString("text", text.Text);
                        break;
                    case ImageBlock image:
                        json.WriteString("type", "image");
                        json.WriteStartObject("source");
                        json.WriteString("type", "base64");
                        json.WriteString("media_type", image.MediaType);
                        json.WriteString("data", image.Base64);
                        json.WriteEndObject();
                        break;
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Sends the request; the JSON text of the answer, or why there isn't one, in plain words.</summary>
    private async Task<string> AskAsync(string body, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Add("anthropic-beta", FallbackBeta);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage response;
        string text;
        try
        {
            response = await http.SendAsync(request, cancel);
            text = await response.Content.ReadAsStringAsync(cancel);
        }
        catch (HttpRequestException e)
        {
            throw new KitchenAssistantException("Couldn't reach Anthropic: check the internet connection.", e);
        }
        catch (TaskCanceledException e) when (!cancel.IsCancellationRequested)
        {
            throw new KitchenAssistantException("Anthropic took too long to answer; try again.", e);
        }
        using (response)
            return Answer(response.StatusCode, text);
    }

    /// <summary>The JSON text of an answer, from the response's status and body.</summary>
    internal static string Answer(HttpStatusCode status, string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException e)
        {
            throw new KitchenAssistantException($"Anthropic answered with something unreadable (HTTP {(int)status}).", e);
        }
        using (document)
        {
            var root = document.RootElement;
            if (status != HttpStatusCode.OK)
            {
                var message = root.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m) ? m.GetString() : null;
                throw (int)status switch
                {
                    401 or 403 => new KitchenAssistantException("The Anthropic API key wasn't accepted: check it in Settings."),
                    429 or 529 => new KitchenAssistantException("Anthropic is busy, or the key's limit is reached: try again in a minute."),
                    >= 500 => new KitchenAssistantException("Anthropic had a problem answering; try again in a minute."),
                    _ => new KitchenAssistantException("Anthropic couldn't answer: " + (message ?? $"HTTP {(int)status}")),
                };
            }
            if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
                throw new KitchenAssistantException("Claude declined to answer this one.");
            var answer = new StringBuilder();
            if (root.TryGetProperty("content", out var content))
                foreach (var block in content.EnumerateArray())
                    if (block.TryGetProperty("type", out var type) && type.GetString() == "text" && block.TryGetProperty("text", out var t))
                        answer.Append(t.GetString());
            if (answer.Length == 0) throw new KitchenAssistantException("Claude didn't answer; try again.");
            return answer.ToString();
        }
    }

    internal sealed record PhotoAnswer(List<SeenFood> Items);

    internal sealed record IdeasAnswer(List<KitchenIdea> Ideas);
}

/// <summary>Reads the answers without reflection (the library is trimmable).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(KitchenAssistant.PhotoAnswer))]
[JsonSerializable(typeof(KitchenAssistant.IdeasAnswer))]
[JsonSerializable(typeof(KitchenIdea))]
internal sealed partial class AnswerJson : JsonSerializerContext;
