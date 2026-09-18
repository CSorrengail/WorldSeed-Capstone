namespace WorldSeed.LlmIntegration;

/// <summary>GUI or service code supplies a profile and resolves its secret at call time.</summary>
public sealed class LlmClientFactory
{
    private readonly HttpClient _httpClient;
    private readonly ILlmCredentialProvider _credentialProvider;

    public LlmClientFactory(HttpClient httpClient, ILlmCredentialProvider credentialProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
    }

    public async Task<ILanguageModelClient> CreateAsync(LlmModelProfile profile, CancellationToken cancellationToken = default)
    {
        return profile.Provider switch
        {
            LlmProviderKind.Ollama => new OllamaChatClient(_httpClient, profile),
            LlmProviderKind.OpenAiCompatible => new OpenAiCompatibleChatClient(
                _httpClient,
                profile,
                await _credentialProvider.GetApiKeyAsync(profile.Id, cancellationToken)),
            _ => throw new NotSupportedException($"Unsupported model provider '{profile.Provider}'.")
        };
    }
}
