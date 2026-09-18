namespace WorldSeed.LlmIntegration;

/// <summary>A profile safe to store in application configuration. Credentials are kept separately.</summary>
public sealed record LlmModelProfile(
    string Id,
    string DisplayName,
    Uri BaseUri,
    string Model,
    LlmProviderKind Provider = LlmProviderKind.Ollama,
    bool UseBearerAuthentication = false);

public enum LlmProviderKind
{
    /// <summary>Ollama's local HTTP API. This is WorldSeed's first supported runtime provider.</summary>
    Ollama,

    /// <summary>Reserved technical debt: retained for a future hosted-provider configuration experience.</summary>
    OpenAiCompatible
}
public enum LlmMessageRole { System, User, Assistant }

public sealed record LlmMessage(LlmMessageRole Role, string Content);
public sealed record LlmChatRequest(IReadOnlyList<LlmMessage> Messages, double? Temperature = null, int? MaxOutputTokens = null);
public sealed record LlmUsage(int? InputTokens, int? OutputTokens);
public sealed record LlmChatResponse(string Content, string Model, LlmUsage? Usage);

public interface ILanguageModelClient
{
    Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Resolves a secret only when a profile is used. Implementations must not write secrets to a ledger or log.</summary>
public interface ILlmCredentialProvider
{
    ValueTask<string?> GetApiKeyAsync(string profileId, CancellationToken cancellationToken = default);
}

public sealed class LlmClientException : Exception
{
    public LlmClientException(string message, int? statusCode = null) : base(message) => StatusCode = statusCode;
    public int? StatusCode { get; }
}

/// <summary>Well-known local defaults. No credentials are required for a normal Ollama installation.</summary>
public static class OllamaDefaults
{
    public static readonly Uri BaseUri = new("http://127.0.0.1:11434/");

    public static LlmModelProfile CreateProfile(string id, string displayName, string model) =>
        new(id, displayName, BaseUri, model, LlmProviderKind.Ollama, UseBearerAuthentication: false);
}

/// <summary>Metadata for a model already installed in a local Ollama library.</summary>
public sealed record OllamaModelInfo(string Name, long? SizeBytes, DateTimeOffset? ModifiedAt);

/// <summary>Reads the local Ollama model library without downloading, importing, or changing models.</summary>
public interface IOllamaModelCatalog
{
    Task<IReadOnlyList<OllamaModelInfo>> GetInstalledModelsAsync(CancellationToken cancellationToken = default);
}
