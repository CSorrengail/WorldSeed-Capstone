namespace WorldSeed.LlmIntegration;

/// <summary>A profile safe to store in application configuration. Credentials are kept separately.</summary>
public sealed record LlmModelProfile(
    string Id,
    string DisplayName,
    Uri BaseUri,
    string Model,
    LlmProviderKind Provider = LlmProviderKind.OpenAiCompatible,
    bool UseBearerAuthentication = true);

public enum LlmProviderKind { OpenAiCompatible }
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
