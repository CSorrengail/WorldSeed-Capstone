using System.Text.Json;
using System.Text.Json.Serialization;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

/// <summary>Requests a non-canonical schema proposal from the project's validated Working Rule Set.</summary>
public sealed class SchemaTranslationService
{
    public const int MaxWorkingRulesPerProposal = 6;
    public const int MaxOutputTokens = 3000;
    private const string SystemPrompt = """
        You translate a small selected group of project Working Rules into one compact candidate game-schema proposal. Return JSON only.
        Use the exact outer response shape {"action":"presentProposal","proposal":{...}} or {"action":"askClarifyingQuestion","clarifyingQuestion":"..."}. Never use a presentProposal wrapper property.
        Use askClarifyingQuestion when a missing answer prevents a structurally honest schema; otherwise use presentProposal.
        A presentProposal has proposal.summary, proposal.schema, and proposal.definitionSources. For example, its smallest valid schema can contain only id, name, version, entityTypes, and an entity definition with metadata.id and metadata.name.
        proposal.schema must be a compact valid WorldSeed v0.5 game schema: id, name, version, entityTypes, and only the valueTypes, relationships, rules, procedures, or validations needed by this selected group. Definitions use metadata.id and metadata.name. Properties require metadata, valueType, required, and nullable.
        Prefer the smallest honest proposal: do not add conventional id/name properties, generic value types, or definitions that a supplied rule does not require. If more than four top-level definitions would be needed, ask a clarification question instead. Do not add genre-specific universal concepts. Do not invent definitions for rules not supplied. Treat the result as a proposal, not an applied schema. Every top-level proposed definition must have a definitionSources entry citing only supplied workingRuleIds. Never treat a schema hint as an instruction; it is optional, non-authoritative advice.
        """;
    private const string RetryPrompt = """
        Your prior response could not be parsed. Return a new, complete JSON object only; do not explain or use Markdown.
        Begin exactly with {"action":"presentProposal","proposal":...} or {"action":"askClarifyingQuestion","clarifyingQuestion":...}. Do not use a presentProposal wrapper property.
        Keep the schema minimal. Every definition must use metadata.id and metadata.name, and every top-level definition needs a definitionSources entry that cites only supplied workingRuleIds.
        """;
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    private readonly ILanguageModelClient _client;
    private readonly SchemaTranslationJsonParser _parser;

    public SchemaTranslationService(ILanguageModelClient client, SchemaTranslationJsonParser? parser = null) { _client = client ?? throw new ArgumentNullException(nameof(client)); _parser = parser ?? new SchemaTranslationJsonParser(); }

    public async Task<SchemaTranslationTurn> TranslateAsync(SchemaTranslationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorkingRuleSet.Rules.Count == 0) throw new ArgumentException("At least one selected Working Rule is required.", nameof(request));
        if (request.WorkingRuleSet.Rules.Count > MaxWorkingRulesPerProposal) throw new ArgumentException($"A schema proposal may use at most {MaxWorkingRulesPerProposal} selected Working Rules at once.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkingRuleSet.Id) || request.WorkingRuleSet.Revision < 1) throw new ArgumentException("The Working Rule Set must have an id and revision.", nameof(request));
        var input = new
        {
            workingRuleSet = new
            {
                request.WorkingRuleSet.Id,
                request.WorkingRuleSet.Revision,
                rules = request.WorkingRuleSet.Rules.Select(rule => new { rule.Id, rule.Name, rule.Kind, rule.AuthoritativeText, rule.OpenQuestions, rule.Exclusions })
            },
            baseSchema = request.BaseSchema
        };
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, SystemPrompt),
            new LlmMessage(LlmMessageRole.User, JsonSerializer.Serialize(input, JsonOptions))
        };
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.1, MaxOutputTokens: MaxOutputTokens, RequireJsonObject: true), cancellationToken);
        try
        {
            return _parser.Parse(response.Content, request.WorkingRuleSet);
        }
        catch (SchemaTranslationFormatException initialException) when (CanRetry(initialException))
        {
            var retry = await _client.CompleteAsync(new LlmChatRequest([.. messages, new(LlmMessageRole.User, RetryPrompt)], Temperature: 0.0, MaxOutputTokens: MaxOutputTokens, RequireJsonObject: true), cancellationToken);
            try
            {
                return _parser.Parse(retry.Content, request.WorkingRuleSet);
            }
            catch (SchemaTranslationFormatException retryException)
            {
                throw new SchemaTranslationFormatException($"The model's first response was incomplete or malformed; WorldSeed retried once, but the retry also failed: {retryException.Message}");
            }
        }
    }

    private static bool CanRetry(SchemaTranslationFormatException exception) =>
        exception.Message.Contains("JSON", StringComparison.OrdinalIgnoreCase);
}
