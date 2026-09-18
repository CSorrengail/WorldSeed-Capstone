using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.RuleDrafting;
using WorldSeed.SchemaValidation;

namespace WorldSeed.SchemaTranslation;

public sealed class SchemaTranslationJsonParser
{
    private readonly GameSchemaValidator _schemaValidator = new();

    public SchemaTranslationTurn Parse(string content, StructuredRuleDraft draft)
    {
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new SchemaTranslationFormatException("The model did not return a JSON object."); }
        catch (JsonException) { throw new SchemaTranslationFormatException("The model did not return valid JSON."); }
        catch (InvalidOperationException) { throw new SchemaTranslationFormatException("The model did not return a JSON object."); }
        var action = String(root["action"]) switch { "askClarifyingQuestion" => SchemaTranslationAction.AskClarifyingQuestion, "presentProposal" => SchemaTranslationAction.PresentProposal, _ => throw new SchemaTranslationFormatException("The model response has an unknown action.") };
        var question = String(root["clarifyingQuestion"]);
        if (action == SchemaTranslationAction.AskClarifyingQuestion && question is null) throw new SchemaTranslationFormatException("A clarification response requires a question.");
        if (action == SchemaTranslationAction.AskClarifyingQuestion) return new SchemaTranslationTurn(action, question, null);
        var proposal = root["proposal"] as JsonObject ?? throw new SchemaTranslationFormatException("A proposal response requires a proposal object.");
        var schema = proposal["schema"] as JsonObject ?? throw new SchemaTranslationFormatException("A schema proposal requires a schema object.");
        var sources = (proposal["definitionSources"] as JsonArray)?.OfType<JsonObject>().Select(ParseSource).ToArray() ?? throw new SchemaTranslationFormatException("A schema proposal requires definition sources.");
        var issues = _schemaValidator.Validate(schema).Issues.Select(issue => issue.Message).ToList();
        var allowedRules = draft.Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var definitionIds = Definitions(schema).ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!definitionIds.Contains(source.DefinitionId)) issues.Add($"Definition source '{source.DefinitionId}' does not exist in the proposed schema.");
            if (source.RuleIds.Count == 0 || source.RuleIds.Any(id => !allowedRules.Contains(id))) issues.Add($"Definition source '{source.DefinitionId}' must cite only rule ids in the draft.");
        }
        var mapped = sources.Select(source => source.DefinitionId).ToHashSet(StringComparer.Ordinal);
        foreach (var id in definitionIds.Where(id => !mapped.Contains(id))) issues.Add($"Proposed definition '{id}' has no source-rule mapping.");
        if (issues.Count > 0) throw new SchemaTranslationFormatException(string.Join(" ", issues));
        return new SchemaTranslationTurn(action, null, new SchemaTranslationProposal(Required(proposal, "summary"), schema, sources));
    }

    private static DefinitionSource ParseSource(JsonObject source) => new(Required(source, "definitionId"), Strings(source, "ruleIds"));
    private static IEnumerable<string> Definitions(JsonObject schema) => new[] { "schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations" }.SelectMany(name => schema[name] is JsonArray definitions ? definitions.OfType<JsonObject>().Select(definition => String((definition["metadata"] as JsonObject)?["id"])).Where(id => id is not null).Cast<string>() : []);
    private static string Required(JsonObject node, string name) => String(node[name]) ?? throw new SchemaTranslationFormatException($"'{name}' is required.");
    private static string? String(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static string[] Strings(JsonObject node, string name) => node[name] is JsonArray values ? values.Select(String).Where(value => value is not null).Cast<string>().ToArray() : [];
}
