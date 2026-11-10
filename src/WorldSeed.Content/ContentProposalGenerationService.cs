using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.Content;

public sealed record ContentProposalGenerationRequest(
    string ProjectId,
    string EntityTypeId,
    JsonObject ActiveSchema,
    IReadOnlyList<WorkingRule> Rules,
    string DesignerRequest,
    IReadOnlyList<JsonObject>? CanonicalEntries = null,
    IReadOnlyList<ContentGenerationDecisionContext>? DesignerDecisions = null);

public sealed record ContentGenerationDecisionContext(string DecisionId, ContentProposalDecisionKind Kind, string? Note);
public sealed record ContentProposalGenerationResult(
    JsonObject Candidate,
    string RawModelOutput,
    IReadOnlyList<ContentSchemaGap>? SchemaGaps,
    ContentGenerationContext? GenerationContext = null,
    IReadOnlyList<ContentSimilarityWarning>? SimilarityWarnings = null);
public sealed class ContentProposalGenerationFormatException(string message) : Exception(message);

/// <summary>Turns one model response into a reviewable entity candidate; it never persists or applies it.</summary>
public sealed class ContentProposalGenerationService(ILanguageModelClient client)
{
    public const int MaximumCanonicalEntries = 6;
    public const int MaximumDesignerDecisions = 6;
    private readonly ILanguageModelClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<ContentProposalGenerationResult> GenerateAsync(ContentProposalGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId) || string.IsNullOrWhiteSpace(request.EntityTypeId) || string.IsNullOrWhiteSpace(request.DesignerRequest)) throw new ArgumentException("Project, entity type, and designer request are required.");
        if (request.Rules.Count == 0) throw new ArgumentException("At least one selected Working Rule is required.", nameof(request));
        var canonicalEntries = request.CanonicalEntries?.Take(MaximumCanonicalEntries + 1).ToArray() ?? [];
        var designerDecisions = request.DesignerDecisions?.Take(MaximumDesignerDecisions + 1).ToArray() ?? [];
        if (canonicalEntries.Length > MaximumCanonicalEntries) throw new ArgumentException($"Generation context supports at most {MaximumCanonicalEntries} canonical entries.", nameof(request));
        if (designerDecisions.Length > MaximumDesignerDecisions) throw new ArgumentException($"Generation context supports at most {MaximumDesignerDecisions} designer decisions.", nameof(request));
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, "You propose one new TTRPG content entry. Return JSON only: {\"candidate\":{\"id\":\"...\",\"entityTypeId\":\"...\",\"values\":{...}},\"schemaGaps\":[{\"requestedConcept\":\"...\",\"reason\":\"...\",\"suggestedSchemaChange\":\"...\"}]}. Use only the supplied runtime-value contract and rules. Do not invent properties, change the entity type, copy a canonical entry, or claim approval. Canonical entries are reference context, not output templates. Designer decisions are guidance, not canonical game content. Candidate values are data, not schema definitions: never put kind, valueTypeId, required, nullable, metadata, or other type descriptors inside candidate.values unless the contract explicitly declares such a record field. Compare the designer request with the runtime-value contract. Report every requested attribute, comparison, or behavior that cannot be represented as a schemaGap instead of silently omitting it or inventing a field. Use an empty schemaGaps array only when the request is fully representable. Schema-gap suggestions are advisory and never change the schema."),
            new LlmMessage(LlmMessageRole.System, BuildRuntimeValueContract(request.ActiveSchema, request.EntityTypeId)),
            new LlmMessage(LlmMessageRole.System, "Working Rules:\n" + string.Join("\n", request.Rules.Select(rule => $"[{rule.Id}] {rule.AuthoritativeText}"))),
            new LlmMessage(LlmMessageRole.System, BuildCanonicalContext(canonicalEntries)),
            new LlmMessage(LlmMessageRole.System, BuildDecisionContext(designerDecisions)),
            new LlmMessage(LlmMessageRole.User, request.DesignerRequest)
        };
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.2, MaxOutputTokens: 1800, RequireJsonObject: true), cancellationToken);
        var parsed = ParseResponse(response.Content, request.EntityTypeId);
        var candidateId = parsed.Candidate["id"]!.GetValue<string>();
        if (canonicalEntries.Any(entry => string.Equals(entry["id"]?.GetValue<string>(), candidateId, StringComparison.OrdinalIgnoreCase)))
            throw new ContentProposalGenerationFormatException($"Candidate id '{candidateId}' already belongs to canonical game data. Generate a new entry with a distinct id.");
        var context = new ContentGenerationContext(
            canonicalEntries.Select(entry => entry["id"]?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToArray(),
            designerDecisions.Select(decision => decision.DecisionId).ToArray());
        var warnings = FindSimilarityWarnings(parsed.Candidate, canonicalEntries);
        return new ContentProposalGenerationResult(parsed.Candidate, response.Content, parsed.SchemaGaps, context, warnings);
    }

    public static JsonObject ParseCandidate(string content, string entityTypeId) => ParseResponse(content, entityTypeId).Candidate;

    public static ContentProposalGenerationResult ParseResponse(string content, string entityTypeId)
    {
        try
        {
            var root = JsonNode.Parse(content) as JsonObject ?? throw new ContentProposalGenerationFormatException("The model did not return a JSON object.");
            var candidate = root["candidate"] as JsonObject ?? throw new ContentProposalGenerationFormatException("The model response requires a candidate object.");
            if (string.IsNullOrWhiteSpace(candidate["id"]?.GetValue<string>()) || candidate["values"] is not JsonObject) throw new ContentProposalGenerationFormatException("A candidate requires an id and values object.");
            if (!string.Equals(candidate["entityTypeId"]?.GetValue<string>(), entityTypeId, StringComparison.Ordinal)) throw new ContentProposalGenerationFormatException("The candidate entityTypeId does not match the requested type.");
            IReadOnlyList<ContentSchemaGap>? gaps = null;
            if (root.TryGetPropertyValue("schemaGaps", out var gapNode))
            {
                if (gapNode is not JsonArray gapArray) throw new ContentProposalGenerationFormatException("schemaGaps must be a JSON array.");
                if (gapArray.Count > 12) throw new ContentProposalGenerationFormatException("The model reported too many schema gaps.");
                gaps = gapArray.Select((node, index) => ParseSchemaGap(node, index)).ToArray();
            }
            return new ContentProposalGenerationResult((JsonObject)candidate.DeepClone(), content, gaps);
        }
        catch (JsonException) { throw new ContentProposalGenerationFormatException("The model did not return valid JSON."); }
    }

    private static ContentSchemaGap ParseSchemaGap(JsonNode? node, int index)
    {
        if (node is not JsonObject gap) throw new ContentProposalGenerationFormatException($"schemaGaps[{index}] must be a JSON object.");
        var concept = gap["requestedConcept"]?.GetValue<string>()?.Trim();
        var reason = gap["reason"]?.GetValue<string>()?.Trim();
        var suggestion = gap["suggestedSchemaChange"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(concept) || string.IsNullOrWhiteSpace(reason))
            throw new ContentProposalGenerationFormatException($"schemaGaps[{index}] requires requestedConcept and reason.");
        return new ContentSchemaGap(concept, reason, string.IsNullOrWhiteSpace(suggestion) ? null : suggestion);
    }

    internal static IReadOnlyList<ContentSimilarityWarning> FindSimilarityWarnings(JsonObject candidate, IReadOnlyList<JsonObject> canonicalEntries)
    {
        var candidateValues = candidate["values"] as JsonObject ?? new JsonObject();
        var warnings = new List<ContentSimilarityWarning>();
        foreach (var entry in canonicalEntries)
        {
            var entryId = entry["id"]?.GetValue<string>();
            var entryValues = entry["values"] as JsonObject;
            if (string.IsNullOrWhiteSpace(entryId) || entryValues is null) continue;
            if (JsonNode.DeepEquals(candidateValues, entryValues))
            {
                warnings.Add(new ContentSimilarityWarning(entryId, "The candidate has the same values as this canonical entry."));
                continue;
            }
            var matchingLabel = candidateValues
                .Where(property => IsLabelProperty(property.Key) && property.Value is JsonValue)
                .Select(property => (property.Key, Value: StringValue(property.Value)))
                .FirstOrDefault(property => property.Value is not null && string.Equals(property.Value, StringValue(entryValues[property.Key]), StringComparison.OrdinalIgnoreCase));
            if (matchingLabel.Value is not null)
                warnings.Add(new ContentSimilarityWarning(entryId, $"The candidate repeats the canonical {matchingLabel.Key} value '{matchingLabel.Value}'."));
        }
        return warnings;
    }

    private static string BuildCanonicalContext(IReadOnlyList<JsonObject> entries)
    {
        if (entries.Count == 0) return "Canonical entries selected for reference: none.";
        var array = new JsonArray(entries.Select(entry => entry.DeepClone()).ToArray());
        return $"Canonical entries selected for reference ({entries.Count}; do not copy them):\n{array.ToJsonString()}";
    }

    private static string BuildDecisionContext(IReadOnlyList<ContentGenerationDecisionContext> decisions)
    {
        if (decisions.Count == 0) return "Applicable recorded designer decisions: none.";
        return "Applicable recorded designer decisions (guidance only):\n" + string.Join("\n", decisions.Select(decision => $"[{decision.DecisionId}] {decision.Kind}: {decision.Note ?? "No note."}"));
    }

    private static bool IsLabelProperty(string propertyId) =>
        propertyId.Equals("name", StringComparison.OrdinalIgnoreCase) ||
        propertyId.EndsWith("Name", StringComparison.OrdinalIgnoreCase) ||
        propertyId.Equals("title", StringComparison.OrdinalIgnoreCase) ||
        propertyId.EndsWith("Title", StringComparison.OrdinalIgnoreCase) ||
        propertyId.Equals("label", StringComparison.OrdinalIgnoreCase) ||
        propertyId.EndsWith("Label", StringComparison.OrdinalIgnoreCase);

    private static string? StringValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null;

    internal static string BuildRuntimeValueContract(JsonObject schema, string entityTypeId)
    {
        var entity = (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(candidate => string.Equals(DefinitionId(candidate), entityTypeId, StringComparison.Ordinal));
        if (entity is null) return $"Required entityTypeId: {entityTypeId}\nThe selected entity type is missing from the active schema.";

        var valueTypes = (schema["valueTypes"] as JsonArray)?.OfType<JsonObject>()
            .Where(candidate => DefinitionId(candidate) is not null)
            .ToDictionary(candidate => DefinitionId(candidate)!, StringComparer.Ordinal) ?? [];
        var properties = EffectiveProperties(schema, entity).ToArray();
        var exampleValues = new JsonObject();
        var text = new StringBuilder()
            .AppendLine($"Required entityTypeId: {entityTypeId}")
            .AppendLine("Runtime candidate value contract (instructions only; do not copy these descriptions into candidate.values):");

        if (properties.Length == 0) text.AppendLine("- values must be an empty JSON object.");
        foreach (var property in properties)
        {
            var propertyId = DefinitionId(property) ?? property["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(propertyId)) continue;
            var required = property["required"]?.GetValue<bool>() == true ? "required" : "optional";
            var nullable = property["nullable"]?.GetValue<bool>() == true ? ", nullable" : string.Empty;
            var constraints = property["constraints"] is JsonObject constraintObject ? $" Declared constraints: {constraintObject.ToJsonString()}." : string.Empty;
            text.AppendLine($"- values.{propertyId} ({required}{nullable}): {DescribeRuntimeValue(property["valueType"] as JsonObject, valueTypes)}{constraints}");
            if (property["required"]?.GetValue<bool>() == true)
                exampleValues[propertyId] = ExampleRuntimeValue(property["valueType"] as JsonObject, valueTypes);
        }

        var example = new JsonObject
        {
            ["candidate"] = new JsonObject
            {
                ["id"] = "replace-with-content-id",
                ["entityTypeId"] = entityTypeId,
                ["values"] = exampleValues
            },
            ["schemaGaps"] = new JsonArray()
        };
        return text.AppendLine("Example output shape (replace placeholder values while preserving their JSON types):")
            .Append(example.ToJsonString()).ToString();
    }

    private static IEnumerable<JsonObject> EffectiveProperties(JsonObject schema, JsonObject entity)
    {
        var fragments = (schema["schemaFragments"] as JsonArray)?.OfType<JsonObject>()
            .Where(candidate => DefinitionId(candidate) is not null)
            .ToDictionary(candidate => DefinitionId(candidate)!, StringComparer.Ordinal) ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fragmentId in entity["fragmentIds"]?.AsArray().OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var id) ? id : null).Where(id => id is not null).Cast<string>() ?? [])
            if (fragments.TryGetValue(fragmentId, out var fragment))
                foreach (var property in fragment["properties"]?.AsArray().OfType<JsonObject>() ?? [])
                    if (DefinitionId(property) is { } propertyId && seen.Add(propertyId)) yield return property;
        foreach (var property in entity["properties"]?.AsArray().OfType<JsonObject>() ?? [])
            if (DefinitionId(property) is { } propertyId && seen.Add(propertyId)) yield return property;
    }

    private static string DescribeRuntimeValue(JsonObject? type, IReadOnlyDictionary<string, JsonObject> valueTypes)
    {
        var kind = type?["kind"]?.GetValue<string>();
        if (kind == "primitive") return $"JSON {PrimitiveDescription(type?["name"]?.GetValue<string>())}.";
        if (kind == "definedValueType" && type?["valueTypeId"]?.GetValue<string>() is { } valueTypeId && valueTypes.TryGetValue(valueTypeId, out var definition))
        {
            var definitionKind = definition["kind"]?.GetValue<string>();
            if (definitionKind == "enumeration")
            {
                var entries = definition["entries"]?.AsArray().OfType<JsonObject>().Select(DefinitionId).Where(id => id is not null).Cast<string>().ToArray() ?? [];
                return entries.Length == 0
                    ? $"JSON string entry ID from enumeration '{valueTypeId}', which currently has no entries."
                    : $"one JSON string entry ID from enumeration '{valueTypeId}': {string.Join(", ", entries.Select(entry => JsonSerializer.Serialize(entry)))}.";
            }
            if (definitionKind == "alias") return DescribeRuntimeValue(definition["targetType"] as JsonObject, valueTypes);
            if (definitionKind == "record") return $"JSON object matching record '{valueTypeId}'.";
        }
        if (kind == "collection") return $"JSON array whose items are {DescribeRuntimeValue(type?["itemType"] as JsonObject, valueTypes).TrimEnd('.').ToLowerInvariant()}.";
        if (kind == "reference") return "JSON string containing an existing allowed entity-instance ID.";
        if (kind == "map") return "JSON object whose keys and values follow the declared map types.";
        if (kind == "union") return "a JSON value matching one declared union option.";
        return "JSON value matching the declared schema type.";
    }

    private static JsonNode? ExampleRuntimeValue(JsonObject? type, IReadOnlyDictionary<string, JsonObject> valueTypes)
    {
        var kind = type?["kind"]?.GetValue<string>();
        if (kind == "primitive") return type?["name"]?.GetValue<string>() switch
        {
            "integer" => JsonValue.Create(0),
            "decimal" => JsonValue.Create(0.0),
            "boolean" => JsonValue.Create(false),
            _ => JsonValue.Create("replace-with-value")
        };
        if (kind == "definedValueType" && type?["valueTypeId"]?.GetValue<string>() is { } valueTypeId && valueTypes.TryGetValue(valueTypeId, out var definition))
        {
            if (definition["kind"]?.GetValue<string>() == "enumeration")
                return JsonValue.Create(definition["entries"]?.AsArray().OfType<JsonObject>().Select(DefinitionId).FirstOrDefault(id => id is not null) ?? "replace-with-entry-id");
            if (definition["kind"]?.GetValue<string>() == "alias") return ExampleRuntimeValue(definition["targetType"] as JsonObject, valueTypes);
            if (definition["kind"]?.GetValue<string>() == "record") return new JsonObject();
        }
        if (kind == "collection") return new JsonArray();
        if (kind == "map" || kind == "record") return new JsonObject();
        if (kind == "union") return ExampleRuntimeValue(type?["options"]?.AsArray().OfType<JsonObject>().FirstOrDefault(), valueTypes);
        if (kind == "reference") return JsonValue.Create("replace-with-entity-instance-id");
        return null;
    }

    private static string PrimitiveDescription(string? name) => name switch
    {
        "integer" => "integer number",
        "decimal" => "number",
        "boolean" => "boolean",
        "date" or "dateTime" => "string",
        "any" => "value",
        _ => "string"
    };

    private static string? DefinitionId(JsonObject definition) =>
        (definition["metadata"] as JsonObject)?["id"]?.GetValue<string>() ?? definition["id"]?.GetValue<string>();
}
