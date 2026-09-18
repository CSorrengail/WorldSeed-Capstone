using System.Text.Json;
using System.Text.Json.Serialization;
using WorldSeed.LlmIntegration;

namespace WorldSeed.SchemaTranslation;

/// <summary>Requests a non-canonical schema proposal from a validated structured rule draft.</summary>
public sealed class SchemaTranslationService
{
    private const string SystemPrompt = """
        You translate a structured TTRPG rule draft into a candidate canonical game schema. Return JSON only.
        Use action askClarifyingQuestion when a missing answer prevents a structurally honest schema; otherwise use presentProposal.
        A presentProposal has proposal.summary, proposal.schema, and proposal.definitionSources.
        proposal.schema must be a full WorldSeed v0.5 game schema: id, name, version, entityTypes, and any needed valueTypes, relationships, rules, procedures, or validations. Definitions use metadata.id and metadata.name. Properties require metadata, valueType, required, and nullable.
        Do not add genre-specific universal concepts. Treat the result as a proposal, not an applied schema. Every top-level proposed definition must have a definitionSources entry citing only rule ids supplied in the draft.
        """;
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    private readonly ILanguageModelClient _client;
    private readonly SchemaTranslationJsonParser _parser;

    public SchemaTranslationService(ILanguageModelClient client, SchemaTranslationJsonParser? parser = null) { _client = client ?? throw new ArgumentNullException(nameof(client)); _parser = parser ?? new SchemaTranslationJsonParser(); }

    public async Task<SchemaTranslationTurn> TranslateAsync(SchemaTranslationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); if (request.Draft.Rules.Count == 0) throw new ArgumentException("A rule draft requires at least one rule.", nameof(request));
        var input = new { draft = request.Draft, baseSchema = request.BaseSchema };
        var response = await _client.CompleteAsync(new LlmChatRequest([new(LlmMessageRole.System, SystemPrompt), new(LlmMessageRole.User, JsonSerializer.Serialize(input, JsonOptions))], Temperature: 0.1, RequireJsonObject: true), cancellationToken);
        return _parser.Parse(response.Content, request.Draft);
    }
}
