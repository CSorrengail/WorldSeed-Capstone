using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;

namespace WorldSeed.ProjectPlanning;

public sealed class DevelopmentPlanFormatException(string message) : Exception(message);

public sealed class DevelopmentPlanGenerationService(ILanguageModelClient client)
{
    private const int MaxOutputTokens = 3600;
    private const int MaxCorrectiveRetries = 2;
    private const string SystemPrompt = """
        You are a TTRPG project planning assistant. Produce a development plan from the supplied immutable WorldSeed inventory. Return JSON only and follow the supplied response schema.
        The inventory describes facts; it does not authorize you to invent canonical rules or content. Recommend between one and ten bounded next steps. Every recommendation must cite one or more supplied observation ids. Do not cite ids that were not supplied.
        Distinguish resolving uncertain rules, expanding a rule system, expanding game-specific content, adding game-specific evaluation coverage, and improving publication. Do not assume this game has monsters, items, skills, dice, levels, or any other common concept unless the inventory explicitly names it.
        Explain tradeoffs, an alternative approach where meaningful, uncertainty, and any reasonable difference of opinion with the designer direction. A difference of opinion is advice, not authority. State any designer choice needed before execution.
        Dependencies may reference only recommendation ids in this response and must not contain cycles. A plan never executes work, changes schema, generates content, or approves itself.
        Match every recommendation category to the facts it cites: ResolveSourceOrRule may cite MissingFoundation, UnresolvedQuestion, Conflict, or MissingSourceTrace; ExpandRuleSystem may cite MissingFoundation, UnresolvedQuestion, or Conflict; ExpandContent may cite EmptyContentType or MissingFoundation; AddEvaluationCoverage may cite MissingEvaluationCoverage; ImprovePublication may cite UnpublishedChanges. Do not recommend work for a category when the inventory has no compatible observation.
        ResolveSourceOrRule may also cite UncitedSourcePassage and UnincorporatedDraftRule. These indicate missing trace coverage, which may be intentional or narrative; ask for review rather than claiming a rule is missing. ExpandRuleSystem may also cite ReportedSchemaGap. Such gaps are model reports needing rule-backed review, not approved mechanics. Narrative rules can remain authoritative prose without structured fields. Never assume every rule requires a schema definition.
        """;

    public async Task<DevelopmentPlanProposal> CreateAsync(ProjectInventory inventory, string? designerDirection, DevelopmentPlanProposal? replaces = null, string? revisionGuidance = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (replaces is not null && string.IsNullOrWhiteSpace(revisionGuidance)) throw new ArgumentException("Revising a development plan requires designer guidance.", nameof(revisionGuidance));
        var input = new
        {
            inventory = Summarize(inventory),
            designerDirection = string.IsNullOrWhiteSpace(designerDirection) ? null : designerDirection.Trim(),
            priorPlan = replaces is null ? null : new { replaces.Id, replaces.Version, replaces.Summary, recommendations = replaces.Recommendations.Select(item => new { item.Id, item.Title, category = item.Category.ToString(), item.Goal }) },
            revisionGuidance = string.IsNullOrWhiteSpace(revisionGuidance) ? null : revisionGuidance.Trim()
        };
        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, SystemPrompt),
            new(LlmMessageRole.User, JsonSerializer.Serialize(input))
        };
        var responseSchema = ResponseSchema(inventory.Observations.Select(item => item.Id).ToArray());
        for (var retryCount = 0; ; retryCount++)
        {
            var request = new LlmChatRequest(messages.ToArray(), Temperature: 0.2, MaxOutputTokens: MaxOutputTokens, RequireJsonObject: true, ResponseSchema: responseSchema);
            var response = await client.CompleteAsync(request, cancellationToken);
            try { return Parse(inventory, response.Content, designerDirection, replaces); }
            catch (DevelopmentPlanFormatException exception) when (retryCount < MaxCorrectiveRetries)
            {
                messages.Add(new(LlmMessageRole.Assistant, response.Content));
                messages.Add(new(LlmMessageRole.System,
                    $"WorldSeed rejected that response: {exception.Message} Return the complete corrected JSON plan. Do not preserve an unsupported recommendation merely by changing its wording; remove it or cite only compatible factual observations using the required category mapping."));
            }
            catch (DevelopmentPlanFormatException exception)
            {
                throw new DevelopmentPlanFormatException($"The model's initial plan was invalid; WorldSeed retried {MaxCorrectiveRetries} times, but the final retry also failed: {exception.Message}");
            }
        }
    }

    internal static DevelopmentPlanProposal Parse(ProjectInventory inventory, string content, string? designerDirection, DevelopmentPlanProposal? replaces, DateTimeOffset? now = null)
    {
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new DevelopmentPlanFormatException("The model did not return a JSON object."); }
        catch (JsonException) { throw new DevelopmentPlanFormatException("The model did not return complete valid JSON."); }
        catch (InvalidOperationException) { throw new DevelopmentPlanFormatException("The model did not return a JSON object."); }
        var summary = Required(root, "summary");
        var tradeoffs = Strings(root["overallTradeoffs"]);
        if (root["recommendations"] is not JsonArray nodes) throw new DevelopmentPlanFormatException("A development plan requires recommendations.");
        var recommendations = nodes.OfType<JsonObject>().Select(ParseRecommendation).ToArray();
        try { return DevelopmentPlanService.Create(inventory, summary, tradeoffs, recommendations, designerDirection, now ?? DateTimeOffset.UtcNow, replaces); }
        catch (ArgumentException exception) { throw new DevelopmentPlanFormatException(exception.Message); }
    }

    private static DevelopmentRecommendation ParseRecommendation(JsonObject node) => new(
        Required(node, "id"), Required(node, "title"),
        EnumValue<DevelopmentRecommendationCategory>(node, "category"),
        EnumValue<DevelopmentRecommendationPriority>(node, "priority"),
        Required(node, "goal"), Required(node, "rationale"), Required(node, "expectedOutcome"),
        Strings(node["evidenceObservationIds"]), Strings(node["dependsOnRecommendationIds"]), Strings(node["tradeoffs"]),
        Optional(node, "alternativeApproach"), Optional(node, "differenceOfOpinion"), Optional(node, "designerDecisionNeeded"));

    private static object Summarize(ProjectInventory inventory) => new
    {
        inventory.Id,
        inventory.Revision,
        inventory.Sources,
        inventory.Rules,
        inventory.Schema,
        inventory.Publication,
        observations = inventory.Observations.Select(item => new { item.Id, kind = item.Kind.ToString(), item.SubjectId, item.Description, item.EvidenceIds })
    };

    private static JsonObject ResponseSchema(string[] observationIds) => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("summary", "overallTradeoffs", "recommendations"),
        ["properties"] = new JsonObject
        {
            ["summary"] = StringSchema(),
            ["overallTradeoffs"] = StringArray(),
            ["recommendations"] = new JsonObject
            {
                ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 10,
                ["items"] = new JsonObject
                {
                    ["type"] = "object", ["additionalProperties"] = false,
                    ["required"] = new JsonArray("id", "title", "category", "priority", "goal", "rationale", "expectedOutcome", "evidenceObservationIds", "dependsOnRecommendationIds", "tradeoffs", "alternativeApproach", "differenceOfOpinion", "designerDecisionNeeded"),
                    ["properties"] = new JsonObject
                    {
                        ["id"] = StringSchema(), ["title"] = StringSchema(),
                        ["category"] = EnumSchema(Enum.GetNames<DevelopmentRecommendationCategory>()),
                        ["priority"] = EnumSchema(Enum.GetNames<DevelopmentRecommendationPriority>()),
                        ["goal"] = StringSchema(), ["rationale"] = StringSchema(), ["expectedOutcome"] = StringSchema(),
                        ["evidenceObservationIds"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["items"] = EnumSchema(observationIds) },
                        ["dependsOnRecommendationIds"] = StringArray(), ["tradeoffs"] = StringArray(),
                        ["alternativeApproach"] = NullableString(), ["differenceOfOpinion"] = NullableString(), ["designerDecisionNeeded"] = NullableString()
                    }
                }
            }
        }
    };

    private static JsonObject StringSchema() => new() { ["type"] = "string", ["minLength"] = 1 };
    private static JsonObject NullableString() => new() { ["type"] = new JsonArray("string", "null") };
    private static JsonObject StringArray() => new() { ["type"] = "array", ["items"] = StringSchema() };
    private static JsonObject EnumSchema(IEnumerable<string> values) => new() { ["type"] = "string", ["enum"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) };
    private static string Required(JsonObject node, string name) => Optional(node, name) ?? throw new DevelopmentPlanFormatException($"'{name}' is required.");
    private static string? Optional(JsonObject node, string name) => node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
    private static IReadOnlyList<string> Strings(JsonNode? node) => node is JsonArray array ? array.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text?.Trim() : null).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray() : [];
    private static T EnumValue<T>(JsonObject node, string name) where T : struct, Enum => Enum.TryParse<T>(Required(node, name), out var value) ? value : throw new DevelopmentPlanFormatException($"'{name}' is not supported.");
}
