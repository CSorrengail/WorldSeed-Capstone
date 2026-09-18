using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.LlmIntegration;

/// <summary>Adapter for Ollama's native local <c>/api/chat</c> endpoint.</summary>
public sealed class OllamaChatClient : ILanguageModelClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmModelProfile _profile;

    public OllamaChatClient(HttpClient httpClient, LlmModelProfile profile)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (profile.Provider != LlmProviderKind.Ollama) throw new ArgumentException("This client only supports Ollama profiles.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Model)) throw new ArgumentException("Profiles require an id and model.", nameof(profile));
        if (!profile.BaseUri.IsAbsoluteUri) throw new ArgumentException("Profiles require an absolute base URI.", nameof(profile));
    }

    public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0) throw new ArgumentException("At least one message is required.", nameof(request));
        if (request.Messages.Any(message => string.IsNullOrWhiteSpace(message.Content))) throw new ArgumentException("Messages require content.", nameof(request));

        var body = new JsonObject
        {
            ["model"] = _profile.Model,
            ["stream"] = false,
            ["messages"] = new JsonArray(request.Messages.Select(message => (JsonNode)new JsonObject
            {
                ["role"] = RoleName(message.Role),
                ["content"] = message.Content
            }).ToArray())
        };
        if (request.Temperature is { } temperature || request.MaxOutputTokens is { })
        {
            var options = new JsonObject();
            if (request.Temperature is { } temperatureValue) options["temperature"] = temperatureValue;
            if (request.MaxOutputTokens is { } maximum) options["num_predict"] = maximum;
            body["options"] = options;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, ApiUri())
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new LlmClientException("Ollama rejected the request.", (int)response.StatusCode);

        JsonNode? root;
        try { root = JsonNode.Parse(responseBody); }
        catch (JsonException) { throw new LlmClientException("Ollama returned invalid JSON.", (int)response.StatusCode); }
        var content = StringValue(root?["message"]?["content"]);
        if (content is null) throw new LlmClientException("Ollama returned no assistant content.", (int)response.StatusCode);
        return new LlmChatResponse(content, StringValue(root?["model"]) ?? _profile.Model,
            new LlmUsage(IntValue(root?["prompt_eval_count"]), IntValue(root?["eval_count"])));
    }

    private Uri ApiUri() => new(_profile.BaseUri.ToString().TrimEnd('/') + "/api/chat", UriKind.Absolute);
    private static string RoleName(LlmMessageRole role) => role switch { LlmMessageRole.System => "system", LlmMessageRole.User => "user", LlmMessageRole.Assistant => "assistant", _ => throw new ArgumentOutOfRangeException(nameof(role)) };
    private static string? StringValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static int? IntValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
}
