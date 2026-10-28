using System.Net;
using System.Text.Json;

namespace Strongthium.HealthReminder.Tests;

/// <summary>What's sent to Claude and how its answers are read; no request goes out.</summary>
public sealed class KitchenAssistantTests
{
    [Fact]
    public void AsksOpusWithTheServersFallbacksForAnAnswerInShape()
    {
        var body = KitchenAssistant.Request("system", [new KitchenAssistant.TextBlock("hello"), new KitchenAssistant.ImageBlock("image/jpeg", "AAAA")],
            KitchenAssistant.IdeasRequestSchema);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("claude-opus-5-5", root.GetProperty("model").GetString());
        Assert.Equal("default", root.GetProperty("fallbacks").GetString());
        Assert.Equal("medium", root.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("object", root.GetProperty("output_config").GetProperty("format").GetProperty("schema").GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("temperature", out _));
        var content = root.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal("hello", content[0].GetProperty("text").GetString());
        Assert.Equal("base64", content[1].GetProperty("source").GetProperty("type").GetString());
        Assert.Equal("image/jpeg", content[1].GetProperty("source").GetProperty("media_type").GetString());
    }

    [Fact]
    public void TheAnswerIsTheTextOfTheResponse()
    {
        var answer = KitchenAssistant.Answer(HttpStatusCode.OK,
            """{"id": "msg_1", "type": "message", "stop_reason": "end_turn", "content": [{"type": "thinking", "thinking": ""}, {"type": "text", "text": "{\"items\": []}"}]}""");
        Assert.Equal("""{"items": []}""", answer);
    }

    [Theory]
    [InlineData(401, "API key wasn't accepted")]
    [InlineData(429, "busy")]
    [InlineData(529, "busy")]
    [InlineData(500, "had a problem")]
    [InlineData(400, "Anthropic couldn't answer: messages: bad")]
    public void AFailureSaysWhyInPlainWords(int status, string says)
    {
        var e = Assert.Throws<KitchenAssistantException>(() =>
            KitchenAssistant.Answer((HttpStatusCode)status, """{"type": "error", "error": {"type": "x", "message": "messages: bad"}}"""));
        Assert.Contains(says, e.Message);
    }

    [Fact]
    public void ADeclinedRequestSaysSo()
    {
        var e = Assert.Throws<KitchenAssistantException>(() =>
            KitchenAssistant.Answer(HttpStatusCode.OK, """{"stop_reason": "refusal", "content": []}"""));
        Assert.Contains("declined", e.Message);
    }

    [Fact]
    public void ReadsWhatThePhotosShow()
    {
        var answer = """
            {"items": [
              {"name": "Eggs", "quantity": "12", "where": "fridge", "use_within_days": 30, "confidence": "sure"},
              {"name": "Spinach", "quantity": "about half a 142 g box", "where": "fridge", "use_within_days": 2, "confidence": "likely"}]}
            """;
        var items = JsonSerializer.Deserialize(answer, AnswerJson.Default.PhotoAnswer)!.Items;
        Assert.Equal(2, items.Count);
        Assert.Equal(new SeenFood("Spinach", "about half a 142 g box", "fridge", 2, "likely"), items[1]);
    }

    [Fact]
    public void ReadsTheIdeasAsRecipes()
    {
        var answer = """
            {"ideas": [{"name": "Spinach omelette", "why": "Uses the spinach before it wilts.", "kcal": 420, "prep_minutes": 5, "cook_minutes": 6,
              "uses": ["Eggs", "Spinach"], "missing": [],
              "ingredients": [{"amount": "3", "name": "eggs"}, {"amount": "a handful of", "name": "spinach"}],
              "steps": [{"text": "Beat the eggs.", "minutes": 0}, {"text": "Cook until set.", "minutes": 4}]}]}
            """;
        var idea = Assert.Single(JsonSerializer.Deserialize(answer, AnswerJson.Default.IdeasAnswer)!.Ideas);
        var recipe = idea.ToRecipe(MealSlot.Breakfast);
        Assert.Equal(("Spinach omelette", 420, 11), (recipe.Name, recipe.Kcal, recipe.PrepMinutes + recipe.CookMinutes));
        Assert.Equal(4, recipe.Steps[1].Minutes);
        Assert.Equal("3 eggs", recipe.Ingredients[0].ToString());
    }

    [Fact]
    public void WhatsInTheKitchenComesWithItsUseByDay()
    {
        var bought = new DateTime(2026, 10, 25, 10, 0, 0);
        var kitchen = GroceryCatalog.Kitchen([
            new GroceryItem { Name = "Rice", Quantity = "2 kg", Bought = bought },
            new GroceryItem { Name = "Salad greens", Quantity = "142 g", Bought = bought },
            new GroceryItem { Name = "Leftover curry", Quantity = "1 bowl", Bought = bought, UseBy = new DateTime(2026, 10, 27) },
        ]);
        Assert.Equal(["Leftover curry", "Salad greens", "Rice"], kitchen.Select(k => k.Name));
        Assert.Equal(new DateTime(2026, 10, 29), kitchen[1].UseBy);
        Assert.Null(kitchen[2].UseBy);
    }
}
