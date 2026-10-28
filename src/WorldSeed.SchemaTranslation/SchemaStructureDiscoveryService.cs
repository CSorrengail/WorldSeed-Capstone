using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

/// <summary>
/// Finds small, related schema-building jobs from a Working Rule Set. It deliberately produces a
/// compact plan rather than a schema, so the later translation of each plan remains bounded and traceable.
/// </summary>
public sealed class SchemaStructureDiscoveryService
{
    public const int MaxRulesPerDiscoveryBatch = 12;
    public const int MaxPlansPerBatch = 4;
    public const int MaxRulesPerPlan = SchemaTranslationService.MaxWorkingRulesPerProposal;
    private const int MaxRuleTextLength = 700;
    private const int MaxOutputTokens = 1400;
    private const string SystemPrompt = """
        You organize Working Rules into small game-schema building jobs. Return JSON only.
        A content type is a kind of game content with individual entries, such as a weapon, skill, location, faction, or any game-specific equivalent. A category/value type is not a content type by itself.
        Return exactly {"plans":[...]}. Each plan has id, name, kind, workingRuleIds, and rationale. kind is either "contentType" or "relationship".
        A contentType plan must use one to three supplied workingRuleIds that collectively describe a reusable kind of content. A relationship plan must use one to three supplied workingRuleIds and include fromContentTypeId and toContentTypeId. Only propose a relationship when the supplied rules directly support it; do not infer vague associations.
        Return at most four plans. Do not create plans for categories alone, do not repeat content types already present in existingSchema, and do not invent rule ids. Keep rationales under twenty words.
        """;

    private readonly ILanguageModelClient _client;

    public SchemaStructureDiscoveryService(ILanguageModelClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<IReadOnlyList<SchemaStructurePlan>> DiscoverAsync(WorkingRuleSet workingRuleSet, JsonObject? existingSchema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingRuleSet);
        if (workingRuleSet.Rules.Count == 0) return [];

        var plans = new List<SchemaStructurePlan>();
        foreach (var batch in workingRuleSet.Rules.Chunk(MaxRulesPerDiscoveryBatch))
        {
            var input = new
            {
                existingSchema = SummarizeSchema(existingSchema),
                workingRules = batch.Select(rule => new
                {
                    rule.Id,
                    rule.Name,
                    kind = rule.Kind.ToString(),
                    text = Truncate(rule.AuthoritativeText, MaxRuleTextLength),
                    conceptIds = rule.ConceptIds
                })
            };
            var messages = new[]
            {
                new LlmMessage(LlmMessageRole.System, SystemPrompt),
                new LlmMessage(LlmMessageRole.User, JsonSerializer.Serialize(input))
            };
            var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0, MaxOutputTokens: MaxOutputTokens, RequireJsonObject: true), cancellationToken);
            plans.AddRange(Parse(response.Content, batch.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal)));
        }
        return MergePlans(plans);
    }

    internal static IReadOnlyList<SchemaStructurePlan> Parse(string content, IReadOnlySet<string> allowedRuleIds)
    {
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new SchemaTranslationFormatException("Structure discovery did not return a JSON object."); }
        catch (JsonException) { throw new SchemaTranslationFormatException("Structure discovery did not return complete valid JSON."); }
        catch (InvalidOperationException) { throw new SchemaTranslationFormatException("Structure discovery did not return a JSON object."); }

        if (root["plans"] is not JsonArray planNodes) throw new SchemaTranslationFormatException("Structure discovery requires a plans array.");
        if (planNodes.Count > MaxPlansPerBatch) throw new SchemaTranslationFormatException($"Structure discovery returned more than {MaxPlansPerBatch} plans for one batch.");
        var plans = new List<SchemaStructurePlan>();
        foreach (var node in planNodes.OfType<JsonObject>())
        {
            var id = Optional(node, "id");
            var name = Optional(node, "name");
            SchemaStructurePlanKind? kind = Optional(node, "kind") switch
            {
                "contentType" => SchemaStructurePlanKind.ContentType,
                "relationship" => SchemaStructurePlanKind.Relationship,
                _ => null
            };
            if (id is null || name is null || kind is null) continue;
            // A local model may accidentally cite a nearby batch. Retain only rules that were actually
            // shown in this request; plans with no local support are discarded instead of aborting a
            // whole automatic pass that may contain other valid structures.
            var ruleIds = Strings(node["workingRuleIds"]).Where(allowedRuleIds.Contains).Take(MaxRulesPerPlan).ToArray();
            if (ruleIds.Length == 0) continue;
            var from = Optional(node, "fromContentTypeId");
            var to = Optional(node, "toContentTypeId");
            if (kind == SchemaStructurePlanKind.Relationship && (from is null || to is null)) continue;
            plans.Add(new SchemaStructurePlan(id, name, kind.Value, ruleIds, Optional(node, "rationale") ?? string.Empty, from, to));
        }
        return plans;
    }

    private static IReadOnlyList<SchemaStructurePlan> MergePlans(IEnumerable<SchemaStructurePlan> plans)
    {
        var merged = new Dictionary<(SchemaStructurePlanKind Kind, string Name), SchemaStructurePlan>();
        foreach (var plan in plans)
        {
            var key = (plan.Kind, ComparableName(plan.Name));
            if (!merged.TryGetValue(key, out var previous))
            {
                merged[key] = plan;
                continue;
            }
            var ruleIds = previous.WorkingRuleIds.Concat(plan.WorkingRuleIds).Distinct(StringComparer.Ordinal).Take(MaxRulesPerPlan).ToArray();
            merged[key] = previous with { WorkingRuleIds = ruleIds };
        }
        return merged.Values
            .OrderBy(plan => plan.Kind)
            .ThenBy(plan => plan.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static object? SummarizeSchema(JsonObject? schema) => schema is null ? null : new
    {
        contentTypes = (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>().Select(DefinitionSummary).ToArray() ?? [],
        valueTypes = (schema["valueTypes"] as JsonArray)?.OfType<JsonObject>().Select(DefinitionSummary).ToArray() ?? []
    };

    private static object DefinitionSummary(JsonObject definition) => new
    {
        id = Optional(definition["metadata"] as JsonObject ?? definition, "id"),
        name = Optional(definition["metadata"] as JsonObject ?? definition, "name")
    };

    private static string Truncate(string text, int maximum) => text.Length <= maximum ? text : text[..maximum] + "…";
    private static string ComparableName(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static string? Optional(JsonObject node, string name) => node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static IReadOnlyList<string> Strings(JsonNode? node) => node is JsonArray values
        ? values.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text : null).Where(text => !string.IsNullOrWhiteSpace(text)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray()
        : [];
}

public enum SchemaStructurePlanKind { ContentType, Relationship }

/// <summary>A small, rule-cited job that the normal schema translator can execute without a manual grouping step.</summary>
public sealed record SchemaStructurePlan(
    string Id,
    string Name,
    SchemaStructurePlanKind Kind,
    IReadOnlyList<string> WorkingRuleIds,
    string Rationale,
    string? FromContentTypeId = null,
    string? ToContentTypeId = null)
{
    public string TranslationGoal => Kind == SchemaStructurePlanKind.ContentType
        ? $"Create one content type named '{Name}' from these rules."
        : $"Create one directly supported relationship named '{Name}' from content type '{FromContentTypeId}' to content type '{ToContentTypeId}' using these rules.";
}
