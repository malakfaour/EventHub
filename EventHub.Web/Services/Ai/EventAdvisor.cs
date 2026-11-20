using System.Net.Http.Json;
using System.Text.Json;
using EventHub.Contracts;
using EventHub.Web.Models;
using Microsoft.Extensions.Options;

namespace EventHub.Web.Services.Ai;

public sealed class AiOptions
{
    public string? ApiKey { get; set; }

    /// <summary>"Gemini" (free key from Google AI Studio) or "Anthropic" (Claude API, paid).</summary>
    public string Provider { get; set; } = "Gemini";
    public string? Model { get; set; }
}

public sealed record NeedSuggestion(string Category, string Notes, string Why);

/// <summary>Source is "AI" when the model answered, "Rules" for the built-in fallback.</summary>
public sealed record AdviceResult(List<NeedSuggestion> Needs, string Source, string? Warning);

public interface IEventAdvisor
{
    Task<AdviceResult> SuggestAsync(Event ev, CancellationToken ct = default);
}

/// <summary>
/// Suggests which supply categories an event needs and in what amounts. It asks the AI model when an API key is
/// configured, and falls back to simple rules when there is no key or the call fails, so the feature always works.
/// The suggestions are advice only: nothing is added until the organizer clicks.
/// </summary>
public sealed class EventAdvisor : IEventAdvisor
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<EventAdvisor> _logger;

    public EventAdvisor(HttpClient http, IOptions<AiOptions> options, ILogger<EventAdvisor> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdviceResult> SuggestAsync(Event ev, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return new AdviceResult(RuleBased(ev), "Rules", "No AI key is configured, so these come from built-in rules.");

        // Never let the AI make the page wait more than ~25 seconds in total (retries included).
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            var needs = await AskModelAsync(ev, budget.Token);
            if (needs.Count > 0) return new AdviceResult(needs, "AI", null);
            _logger.LogWarning("The AI model returned no usable suggestions for event {EventId}", ev.Id);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Includes timeouts: only the user leaving the page (ct) is allowed to stop the request.
            _logger.LogWarning(ex, "AI suggestion failed for event {EventId}; using rules", ev.Id);
        }
        return new AdviceResult(RuleBased(ev), "Rules", "The AI service did not answer, so these come from built-in rules.");
    }

    // ───────────── AI model ─────────────

    private async Task<List<NeedSuggestion>> AskModelAsync(Event ev, CancellationToken ct)
    {
        var system = "You help event organizers decide which supplies to order. Answer with JSON only, no other text.";
        var prompt = $$"""
            Event: {{ev.Name}}
            Type: {{ev.EventType}}
            Venue: {{ev.Venue}}
            Expected guests: {{ev.ExpectedGuests}}
            Starts: {{ev.StartsAtUtc:yyyy-MM-dd HH:mm}} UTC
            Description: {{ev.Description ?? "none"}}

            Suppliers can only provide these categories: catering, decoration, sound (sound and lighting).
            Suggest which of them this event needs. For each, give practical quantities in "notes"
            (for example portions per guest, number of speakers) and one short sentence in "why".
            Only suggest a category when it makes sense for this event.

            Reply exactly as: {"needs":[{"category":"catering","notes":"...","why":"..."}]}
            """;

        var text = string.Equals(_options.Provider, "Anthropic", StringComparison.OrdinalIgnoreCase)
            ? await AskAnthropicAsync(system, prompt, ct)
            : await AskGeminiAsync(system, prompt, ct);

        // The model is asked for JSON only, but tolerate surrounding text.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return [];
        using var json = JsonDocument.Parse(text[start..(end + 1)]);

        var result = new List<NeedSuggestion>();
        foreach (var item in json.RootElement.GetProperty("needs").EnumerateArray())
        {
            var category = item.GetProperty("category").GetString();
            if (!SupplyCategories.IsValid(category) || result.Any(r => r.Category == category)) continue;   // never trust model output blindly
            result.Add(new NeedSuggestion(category!,
                Clip(item.TryGetProperty("notes", out var n) ? n.GetString() : null, 500),
                Clip(item.TryGetProperty("why", out var w) ? w.GetString() : null, 300)));
        }
        return result;
    }

    private async Task<string> AskAnthropicAsync(string system, string prompt, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = _options.Model ?? "claude-sonnet-5-5",
            max_tokens = 800,
            system,
            messages = new[] { new { role = "user", content = prompt } }
        });

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> AskGeminiAsync(string system, string prompt, CancellationToken ct)
    {
        var model = _options.Model ?? "gemini-flash-latest";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", maxOutputTokens = 800 }
        };

        // The free tier is sometimes overloaded (503) or rate limited (429). Those pass quickly, so wait and try again.
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "";
            }

            var transient = (int)response.StatusCode is 429 or 503;
            if (transient && attempt < attempts)
            {
                _logger.LogInformation("Gemini returned {Status}; retrying ({Attempt}/{Max})", (int)response.StatusCode, attempt, attempts);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Gemini returned {(int)response.StatusCode} for model '{model}': {(body.Length > 300 ? body[..300] : body)}");
        }
    }

    private static string Clip(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? "" : text.Trim().Length <= max ? text.Trim() : text.Trim()[..max];

    // ───────────── Fallback rules ─────────────

    private static List<NeedSuggestion> RuleBased(Event ev)
    {
        var guests = Math.Max(ev.ExpectedGuests, 1);
        var type = ev.EventType;
        var list = new List<NeedSuggestion>();

        // Everyone eats, except in a sports event where concessions are usually separate.
        if (type != "Sports")
        {
            var perGuest = type is "Wedding" or "Food & Drink" ? 1.2 : type == "Conference" ? 1.0 : 0.8;
            list.Add(new NeedSuggestion(SupplyCategories.Catering,
                $"About {Math.Ceiling(guests * perGuest)} portions for {guests} guests.",
                type is "Wedding" or "Food & Drink" ? "A meal is central to this kind of event, so plan for more than one portion each."
                                                    : "Guests need food and drinks; this leaves a small margin."));
        }

        if (type is "Wedding" or "Party" or "Food & Drink" or "Music" or "Other")
        {
            list.Add(new NeedSuggestion(SupplyCategories.Decoration,
                guests >= 100 ? "Entrance and stage decoration plus about one centerpiece per table of 8."
                              : "Entrance decoration plus a centerpiece per table.",
                "Decoration sets the mood and makes the venue feel prepared."));
        }

        if (type is "Music" or "Conference" or "Party" or "Wedding" or "Sports")
        {
            list.Add(new NeedSuggestion(SupplyCategories.Sound,
                guests >= 300 ? "Full PA system with a mixing desk, plus stage lighting."
                : guests >= 80 ? "PA system with 2 speakers, 2 microphones and basic lighting."
                               : "Small PA with 1 microphone.",
                type == "Conference" ? "Speakers must be heard clearly from the back of the room."
                                     : "Music and announcements need proper sound for this crowd size."));
        }

        return list;
    }
}
