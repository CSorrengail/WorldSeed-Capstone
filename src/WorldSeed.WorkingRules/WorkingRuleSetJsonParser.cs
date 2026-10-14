using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.DesignSessions;

namespace WorldSeed.WorkingRules;

/// <summary>Maps compact LLM JSON into evidence-bearing domain records, then validates it against the project.</summary>
public sealed class WorkingRuleSetJsonParser
{
    private readonly WorkingRuleSetValidator _validator = new();

    public WorkingRuleSet Parse(string content, DesignProject project, IReadOnlyList<DraftCitation>? citations = null, WorkingRuleSet? existingRuleSet = null, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var catalog = (citations ?? DraftCitationCatalog.Create(CompletedDraftCatalog.Create(project))).ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (catalog.Count == 0) throw new WorkingRuleSetFormatException("No completed source drafts are available for working-rule provenance.");
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new WorkingRuleSetFormatException("The model did not return a JSON object."); }
        catch (JsonException)
        {
            var trimmed = content.Trim();
            var detail = trimmed.StartsWith('{') && !trimmed.EndsWith('}') ? " The response appears incomplete, usually because the model reached its output limit." : string.Empty;
            throw new WorkingRuleSetFormatException("The model did not return valid JSON." + detail);
        }
        catch (InvalidOperationException) { throw new WorkingRuleSetFormatException("The model did not return a JSON object."); }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        var rules = Objects(root, "rules").Select(rule => ParseRule(rule, catalog)).ToArray();
        var concepts = Objects(root, "concepts").Select(concept => ParseConcept(concept, catalog)).ToArray();
        var revision = existingRuleSet is null ? 1 : existingRuleSet.Revision + 1;
        var createdAt = existingRuleSet?.CreatedAt ?? timestamp;
        var ruleSet = new WorkingRuleSet(
            existingRuleSet?.Id ?? "working-rules-" + project.Id,
            project.Id,
            "0.1",
            Required(root, "name"),
            Optional(root, "description"),
            revision,
            createdAt,
            timestamp,
            rules,
            concepts,
            Strings(root, "openQuestions"),
            Strings(root, "exclusions"),
            Strings(root, "conflicts"));
        var validation = _validator.Validate(ruleSet, project);
        if (!validation.IsValid) throw new WorkingRuleSetFormatException(string.Join(" ", validation.Issues.Select(issue => issue.Message)));
        return ruleSet;
    }

    private static WorkingRule ParseRule(JsonObject item, IReadOnlyDictionary<string, DraftCitation> catalog)
    {
        var references = References(item, catalog);
        return new WorkingRule(
            Required(item, "id"), Required(item, "name"), ParseKind(Required(item, "kind")), Required(item, "authoritativeText"),
            EvidenceFor(references, catalog), references, Strings(item, "conceptIds"), Objects(item, "schemaHints").Select(ParseSchemaHint).ToArray(),
            null, Strings(item, "openQuestions"), Strings(item, "exclusions"));
    }

    private static WorkingRuleConcept ParseConcept(JsonObject item, IReadOnlyDictionary<string, DraftCitation> catalog)
    {
        var references = References(item, catalog);
        return new WorkingRuleConcept(Required(item, "id"), Required(item, "name"), Required(item, "description"), EvidenceFor(references, catalog));
    }

    private static SchemaHint ParseSchemaHint(JsonObject item) => new(
        ParseHintKind(Required(item, "kind")), Required(item, "suggestedId"), Required(item, "suggestedName"), Optional(item, "parentSuggestedId"), Required(item, "rationale"), Strings(item, "sourceWorkingRuleIds"));

    private static IReadOnlyList<DraftRuleReference> References(JsonObject item, IReadOnlyDictionary<string, DraftCitation> catalog)
    {
        var citationIds = Strings(item, "sourceDraftIds");
        if (citationIds.Length == 0) throw new WorkingRuleSetFormatException("Every working rule and concept requires at least one source-draft citation ID.");
        foreach (var citationId in citationIds.Where(citationId => !catalog.ContainsKey(citationId))) throw new WorkingRuleSetFormatException($"Source-draft citation '{citationId}' is not available for this project.");
        var references = citationIds.Select(citationId => catalog[citationId].Draft.Reference).ToArray();
        return references.Distinct().ToArray();
    }

    private static IReadOnlyList<WorkingRuleEvidence> EvidenceFor(IEnumerable<DraftRuleReference> references, IReadOnlyDictionary<string, DraftCitation> catalog) => references
        .SelectMany(reference => catalog.Values.Single(citation => citation.Draft.Reference == reference).Draft.Rule.SourceSupport)
        .Select(support => new WorkingRuleEvidence(support.SourceNoteId, support.PassageId, support.Excerpt))
        .Distinct()
        .ToArray();

    private static WorkingRuleKind ParseKind(string value) => value switch
    {
        "definition" => WorkingRuleKind.Definition, "rule" => WorkingRuleKind.Rule,
        "procedure" => WorkingRuleKind.Procedure, "constraint" => WorkingRuleKind.Constraint,
        _ => throw new WorkingRuleSetFormatException("A working rule has an unknown kind.")
    };

    private static SchemaHintKind ParseHintKind(string value) => value switch
    {
        "entityType" => SchemaHintKind.EntityType, "valueType" => SchemaHintKind.ValueType,
        "schemaFragment" => SchemaHintKind.SchemaFragment, "property" => SchemaHintKind.Property,
        "relationship" => SchemaHintKind.Relationship, "rule" => SchemaHintKind.Rule,
        "procedure" => SchemaHintKind.Procedure, "validation" => SchemaHintKind.Validation,
        _ => throw new WorkingRuleSetFormatException("A schema hint has an unknown kind.")
    };

    private static string Required(JsonObject owner, string name) => Optional(owner, name) ?? throw new WorkingRuleSetFormatException($"'{name}' is required.");
    private static string? Optional(JsonObject owner, string name) => owner[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static string[] Strings(JsonObject owner, string name) => owner[name] is JsonArray values ? values.Select(value => value is JsonValue json && json.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null).Where(text => text is not null).Cast<string>().ToArray() : [];
    private static JsonObject[] Objects(JsonObject owner, string name) => owner[name] is JsonArray values ? values.OfType<JsonObject>().ToArray() : [];
}
