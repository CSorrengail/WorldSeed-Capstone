using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.LlmIntegration;

public sealed class OllamaModelCatalog : IOllamaModelCatalog
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;

    public OllamaModelCatalog(HttpClient httpClient, Uri? baseUri = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = baseUri ?? OllamaDefaults.BaseUri;
        if (!_baseUri.IsAbsoluteUri) throw new ArgumentException("Ollama requires an absolute base URI.", nameof(baseUri));
    }

    public async Task<IReadOnlyList<OllamaModelInfo>> GetInstalledModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(new Uri(_baseUri.ToString().TrimEnd('/') + "/api/tags"), cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new LlmClientException("Could not read the local Ollama model library.", (int)response.StatusCode);
        JsonNode? root;
        try { root = JsonNode.Parse(responseBody); }
        catch (JsonException) { throw new LlmClientException("Ollama returned invalid JSON.", (int)response.StatusCode); }
        if (root?["models"] is not JsonArray models) throw new LlmClientException("Ollama returned an invalid model library response.", (int)response.StatusCode);
        return models.OfType<JsonObject>()
            .Select(model => new OllamaModelInfo(StringValue(model["name"]) ?? string.Empty, LongValue(model["size"]), DateValue(model["modified_at"])))
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? StringValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static long? LongValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<long>(out var number) ? number : null;
    private static DateTimeOffset? DateValue(JsonNode? node) => node is JsonValue jsonValue && jsonValue.TryGetValue<DateTimeOffset>(out var date) ? date : null;
}
