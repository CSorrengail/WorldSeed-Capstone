using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.SchemaValidation;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

public sealed class SchemaTranslationJsonParser
{
    private readonly GameSchemaValidator _schemaValidator = new();

    public SchemaTranslationTurn Parse(string content, WorkingRuleSet workingRuleSet)
    {
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new SchemaTranslationFormatException("The model did not return a JSON object."); }
        catch (JsonException) when (LooksIncomplete(content)) { throw new SchemaTranslationFormatException("The model response ended before its JSON object was complete, usually because it reached its output limit."); }
        catch (JsonException) { throw new SchemaTranslationFormatException("The model did not return valid JSON."); }
        catch (InvalidOperationException) { throw new SchemaTranslationFormatException("The model did not return a JSON object."); }
        if (root["action"] is null && root["presentProposal"] is JsonObject alternateProposal)
        {
            root = (JsonObject)alternateProposal.DeepClone();
            root["action"] = "presentProposal";
        }
        else if (root["action"] is null && root["askClarifyingQuestion"] is JsonObject alternateQuestion)
        {
            root = (JsonObject)alternateQuestion.DeepClone();
            root["action"] = "askClarifyingQuestion";
        }
        var action = String(root["action"]) switch { "askClarifyingQuestion" => SchemaTranslationAction.AskClarifyingQuestion, "presentProposal" => SchemaTranslationAction.PresentProposal, _ => throw new SchemaTranslationFormatException("The model response has an unknown action.") };
        var question = String(root["clarifyingQuestion"]);
        if (action == SchemaTranslationAction.AskClarifyingQuestion && question is null) throw new SchemaTranslationFormatException("A clarification response requires a question.");
        if (action == SchemaTranslationAction.AskClarifyingQuestion) return new SchemaTranslationTurn(action, question, null);
        var proposal = root["proposal"] as JsonObject ?? throw new SchemaTranslationFormatException("A proposal response requires a proposal object.");
        var schema = proposal["schema"] as JsonObject ?? throw new SchemaTranslationFormatException("A schema proposal requires a schema object.");
        // Some local models place this proposal-level traceability field inside schema. Moving it is lossless;
        // the later checks still reject links to definitions that the schema did not actually define.
        if (proposal["definitionSources"] is null && schema["definitionSources"] is JsonNode misplacedSources)
            proposal["definitionSources"] = misplacedSources.DeepClone();
        NormalizeSchema(schema);
        var sources = ParseSources(proposal["definitionSources"]) ?? throw new SchemaTranslationFormatException("A schema proposal requires definition sources.");
        var issues = _schemaValidator.Validate(schema).Issues.Select(issue => issue.Message).ToList();
        var allowedRules = workingRuleSet.Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var definitionIds = Definitions(schema).ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!definitionIds.Contains(source.DefinitionId)) issues.Add($"Definition source '{source.DefinitionId}' does not exist in the proposed schema.");
            if (source.WorkingRuleIds.Count == 0 || source.WorkingRuleIds.Any(id => !allowedRules.Contains(id))) issues.Add($"Definition source '{source.DefinitionId}' must cite only working rule ids in the Working Rule Set.");
        }
        var mapped = sources.Select(source => source.DefinitionId).ToHashSet(StringComparer.Ordinal);
        foreach (var id in definitionIds.Where(id => !mapped.Contains(id))) issues.Add($"Proposed definition '{id}' has no source-rule mapping.");
        if (issues.Count > 0) throw new SchemaTranslationFormatException(string.Join(" ", issues));
        return new SchemaTranslationTurn(action, null, new SchemaTranslationProposal(Required(proposal, "summary"), schema, sources, workingRuleSet.Id, workingRuleSet.Revision));
    }

    private static DefinitionSource[]? ParseSources(JsonNode? node) => node switch
    {
        JsonArray sources => sources.OfType<JsonObject>().Select(ParseSource).ToArray(),
        JsonObject sourceMap => sourceMap.Select(pair => new DefinitionSource(pair.Key, Strings(pair.Value))).ToArray(),
        _ => null
    };

    private static DefinitionSource ParseSource(JsonObject source) => new(Required(source, "definitionId"), Strings(source, "workingRuleIds"));

    /// <summary>Normalizes lossless model presentation variants before validation; it never creates game-specific definitions.</summary>
    private static void NormalizeSchema(JsonObject schema)
    {
        foreach (var collection in new[] { "schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations" })
            foreach (var definition in (schema[collection] as JsonArray)?.OfType<JsonObject>() ?? [])
                NormalizeDefinition(definition);
    }

    private static void NormalizeDefinition(JsonObject definition)
    {
        NormalizeMetadata(definition, allowNameAsId: false);
        foreach (var property in (definition["properties"] as JsonArray)?.OfType<JsonObject>() ?? []) NormalizeProperty(property);
        foreach (var attribute in (definition["attributes"] as JsonArray)?.OfType<JsonObject>() ?? []) NormalizeProperty(attribute);
        if (String(definition["kind"]) == "enumeration" && definition["entries"] is JsonArray entries)
        {
            for (var index = 0; index < entries.Count; index++)
                if (String(entries[index]) is { } label)
                    entries[index] = new JsonObject { ["id"] = label, ["name"] = label };
        }
    }

    private static void NormalizeProperty(JsonObject property)
    {
        NormalizeMetadata(property, allowNameAsId: true);
        if (String(property["valueType"]) is { } primitive && new[] { "string", "integer", "decimal", "boolean", "date", "dateTime", "any" }.Contains(primitive))
            property["valueType"] = new JsonObject { ["kind"] = "primitive", ["name"] = primitive };
    }

    private static void NormalizeMetadata(JsonObject node, bool allowNameAsId)
    {
        var metadata = node["metadata"] as JsonObject ?? new JsonObject();
        if (node["metadata"] is null) node["metadata"] = metadata;
        var id = String(metadata["id"]) ?? String(node["id"]) ?? (allowNameAsId ? String(node["name"]) : null);
        var name = String(metadata["name"]) ?? String(node["name"]);
        if (id is not null) metadata["id"] = id;
        if (name is not null) metadata["name"] = name;
    }
    private static bool LooksIncomplete(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith('{') && !trimmed.EndsWith('}');
    }
    private static IEnumerable<string> Definitions(JsonObject schema) => new[] { "schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations" }.SelectMany(name => schema[name] is JsonArray definitions ? definitions.OfType<JsonObject>().Select(definition => String((definition["metadata"] as JsonObject)?["id"])).Where(id => id is not null).Cast<string>() : []);
    private static string Required(JsonObject node, string name) => String(node[name]) ?? throw new SchemaTranslationFormatException($"'{name}' is required.");
    private static string? String(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static string[] Strings(JsonObject node, string name) => Strings(node[name]);
    private static string[] Strings(JsonNode? node) => node is JsonArray values ? values.Select(String).Where(value => value is not null).Cast<string>().ToArray() : [];
}
