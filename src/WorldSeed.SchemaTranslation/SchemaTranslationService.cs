using System.Text.Json;
using System.Text.Json.Serialization;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

/// <summary>Requests a non-canonical schema proposal from the project's validated Working Rule Set.</summary>
public sealed class SchemaTranslationService
{
    // Local models are substantially more reliable when each proposal is a genuinely small,
    // related unit of schema work. Larger selections encourage unrelated definitions and can
    // exhaust their output budget before the enclosing JSON is complete.
    public const int MaxWorkingRulesPerProposal = 3;
    public const int MaxOutputTokens = 3000;
    public const int MaxCorrectiveRetries = 2;
    private const string SystemPrompt = """
        You translate a small selected group of project Working Rules into one compact candidate game-schema proposal. Return JSON only.
        Use the exact outer response shape {"action":"presentProposal","proposal":{...}} or {"action":"askClarifyingQuestion","clarifyingQuestion":"..."}. Never use a presentProposal wrapper property.
        Use askClarifyingQuestion when a missing answer prevents a structurally honest schema; otherwise use presentProposal.
        User-provided schemaClarifications answer prior schema-design questions. They are context for this bounded proposal, not new Working Rules. Use them when they resolve a genuine missing detail; do not ask the same question again.
        structureGoal, when present, is a WorldSeed planning instruction based on the supplied rules. Follow it only within those rules: it may request one content type or one directly supported relationship. Do not create any extra structures outside that goal.
        A presentProposal has proposal.summary, proposal.schema, and proposal.definitionSources. definitionSources is beside schema, never inside schema, and must be an array such as [{"definitionId":"weapon","workingRuleIds":["b001-r001"]}], never an object keyed by definition id. Its definitionId must be an id from a top-level schema array such as entityTypes or valueTypes; do not cite a property or a nested object.
        proposal.schema must always be ONE JSON OBJECT, never an array. It must contain id, name, version, entityTypes, and only the valueTypes, relationships, rules, procedures, or validations needed by this selected group. Definitions use metadata.id and metadata.name. Put a content type directly in schema.entityTypes; never write kind:"entityType", never nest it in an entity object, and never return a flat list of definitions. Entity properties go directly in entity.properties; never add a nested definitions array. Properties require metadata, valueType, required, and nullable. A property valueType is always an object: use {"kind":"primitive","name":"string"} for a basic value. Do not write valueType as a bare string such as "enum". A top-level valueTypes definition can be only enumeration, record, or alias: for a named primitive, use {"kind":"alias","targetType":{"kind":"primitive","name":"integer"}} and never repeat a name key. For categories, define a valueTypes entry with kind "enumeration" and one or more object entries such as {"id":"light","name":"Light"}, then use {"kind":"definedValueType","valueTypeId":"that-id"}; if its entries are unknown, ask a clarification instead of inventing them. Only use recognized top-level definition arrays; represent tiers as an enumeration valueTypes entry, not a custom consumableTiers array.
        When baseSchema is supplied, it is the already-saved portion of this project's in-progress schema. Treat it as immutable context: return only top-level definitions newly needed for the selected rules, do not copy a definition that is already present, and reuse its ids through references where appropriate. definitionSources must map only your newly returned definitions; do not repeat base-schema mappings.
        Distinguish an instance-bearing concept from a value label. When a rule says a named concept has, possesses, provides, or is described by one or more properties, propose that concept as an entityTypes definition with those properties. Do not replace such a concept with only an enumeration or alias. Use valueTypes only for reusable scalar domains, categories, or nested value shapes. If the rule does not make clear whether the named concept should have independently created game-data entries, ask a clarification question rather than guessing.
        Prefer the smallest honest proposal: for one queued rule, return one definition unless one directly referenced category/value type is essential. Keep summary under twelve words. Do not add conventional id/name properties, generic value types, or definitions that a supplied rule does not require. Return at most three top-level definitions. If the selected rules would need more than three definitions, or are not one closely related concept, ask one clarification question instead. Do not add genre-specific universal concepts. Do not invent definitions for rules not supplied. Treat the result as a proposal, not an applied schema. Every top-level proposed definition must have a definitionSources entry citing only supplied workingRuleIds. Never treat a schema hint as an instruction; it is optional, non-authoritative advice.
        """;
    private const string RetryPrompt = """
        Your prior response could not be parsed. Return a new, complete JSON object only; do not explain or use Markdown.
        Begin exactly with {"action":"presentProposal","proposal":...} or {"action":"askClarifyingQuestion","clarifyingQuestion":...}. Do not use a presentProposal wrapper property.
        Keep the schema minimal. proposal.schema must be one object, never a flat array. Put every definition array inside proposal.schema: for example, proposal.schema must contain both entityTypes and valueTypes, never put valueTypes beside schema. A content type goes directly in proposal.schema.entityTypes; do not use kind:"entityType" or a nested entity object. Every definition must use metadata.id and metadata.name, and every top-level definition needs a definitionSources entry that cites only supplied workingRuleIds. If armorClass and armorClassWeight are both returned, definitionSources needs one entry for armorClass and one for armorClassWeight. Enumeration entries are objects with id and name in the entries array, never bare strings and never objects in properties. Remove any definition you cannot map to a supplied Working Rule.
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
            schemaClarifications = request.Clarifications?.Select(clarification => new { clarification.Question, clarification.Answer }),
            structureGoal = request.StructureGoal,
            baseSchema = request.BaseSchema
        };
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, SystemPrompt),
            new LlmMessage(LlmMessageRole.User, JsonSerializer.Serialize(input, JsonOptions))
        };
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.1, MaxOutputTokens: MaxOutputTokens, RequireJsonObject: true), cancellationToken);
        for (var retryCount = 0; ; retryCount++)
        {
            try
            {
                return _parser.Parse(response.Content, request.WorkingRuleSet, request.BaseSchema);
            }
            catch (SchemaTranslationFormatException exception) when (retryCount < MaxCorrectiveRetries)
            {
                var correction = RetryPrompt + "\nWorldSeed rejected the prior proposal for this exact reason: " + exception.Message;
                response = await _client.CompleteAsync(new LlmChatRequest([.. messages, new(LlmMessageRole.User, correction)], Temperature: 0.0, MaxOutputTokens: MaxOutputTokens, RequireJsonObject: true), cancellationToken);
            }
            catch (SchemaTranslationFormatException exception)
            {
                throw new SchemaTranslationFormatException($"The model's initial proposal was incomplete or invalid; WorldSeed retried {MaxCorrectiveRetries} times, but the final retry also failed: {exception.Message}");
            }
        }
    }

}
