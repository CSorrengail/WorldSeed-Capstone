using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.LlmIntegration;

/// <summary>Minimal OpenAI-compatible Chat Completions adapter for hosted or local models.</summary>
public sealed class OpenAiCompatibleChatClient : ILanguageModelClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmModelProfile _profile;
    private readonly string? _apiKey;

    public OpenAiCompatibleChatClient(HttpClient httpClient, LlmModelProfile profile, string? apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _apiKey = apiKey;
        ValidateProfile(profile, apiKey);
    }

    public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0) throw new ArgumentException("At least one message is required.", nameof(request));
        if (request.Messages.Any(message => string.IsNullOrWhiteSpace(message.Content))) throw new ArgumentException("Messages require content.", nameof(request));

        var body = new JsonObject
        {
            ["model"] = _profile.Model,
            ["messages"] = new JsonArray(request.Messages.Select(message => (JsonNode)new JsonObject
            {
                ["role"] = RoleName(message.Role),
                ["content"] = message.Content
            }).ToArray())
        };
        if (request.Temperature is { } temperature) body["temperature"] = temperature;
        if (request.MaxOutputTokens is { } maximum) body["max_tokens"] = maximum;

        using var message = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUri())
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        if (_profile.UseBearerAuthentication && !string.IsNullOrWhiteSpace(_apiKey)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new LlmClientException("The language-model provider rejected the request.", (int)response.StatusCode);

        JsonNode? root;
        try { root = JsonNode.Parse(responseBody); }
        catch (JsonException) { throw new LlmClientException("The language-model provider returned invalid JSON.", (int)response.StatusCode); }
        var content = ExtractContent(root);
        if (content is null) throw new LlmClientException("The language-model provider returned no assistant content.", (int)response.StatusCode);
        var usage = root?["usage"] as JsonObject;
        var usageDetails = usage is null ? null : new LlmUsage(IntValue(usage["prompt_tokens"]), IntValue(usage["completion_tokens"]));
        return new LlmChatResponse(content, StringValue(root?["model"]) ?? _profile.Model, usageDetails);
    }

    private static string? ExtractContent(JsonNode? root)
    {
        var content = root?["choices"]?[0]?["message"]?["content"];
        if (StringValue(content) is { } text) return text;
        if (content is not JsonArray parts) return null;
        return string.Concat(parts.OfType<JsonObject>().Select(part => StringValue(part["text"]) ?? StringValue(part["content"]) ?? string.Empty));
    }

    private static void ValidateProfile(LlmModelProfile profile, string? apiKey)
    {
        if (profile.Provider != LlmProviderKind.OpenAiCompatible) throw new ArgumentException("This client only supports OpenAI-compatible profiles.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Model)) throw new ArgumentException("Profiles require an id and model.", nameof(profile));
        if (!profile.BaseUri.IsAbsoluteUri) throw new ArgumentException("Profiles require an absolute base URI.", nameof(profile));
        if (profile.UseBearerAuthentication && string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("Bearer-authenticated profiles require an API key.", nameof(apiKey));
    }

    private Uri ChatCompletionsUri() => new(_profile.BaseUri.ToString().TrimEnd('/') + "/chat/completions", UriKind.Absolute);
    private static string RoleName(LlmMessageRole role) => role switch { LlmMessageRole.System => "system", LlmMessageRole.User => "user", LlmMessageRole.Assistant => "assistant", _ => throw new ArgumentOutOfRangeException(nameof(role)) };
    private static string? StringValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static int? IntValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
}
