using System.Text.Json.Nodes;

namespace WorldSeed.SchemaTranslation;

/// <summary>Combines independently validated schema additions into one mutable, non-canonical project proposal.</summary>
public static class SchemaProposalMerger
{
    private static readonly string[] DefinitionCollections = ["schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations"];

    public static JsonObject MergeSchemas(JsonObject? existingSchema, JsonObject addition)
    {
        ArgumentNullException.ThrowIfNull(addition);
        if (existingSchema is null) return (JsonObject)addition.DeepClone();

        var merged = (JsonObject)existingSchema.DeepClone();
        var definitionsById = DefinitionsById(merged);
        foreach (var collection in DefinitionCollections)
        {
            if (addition[collection] is not JsonArray additions || additions.Count == 0) continue;
            if (merged[collection] is not JsonArray definitions)
            {
                definitions = [];
                merged[collection] = definitions;
            }
            foreach (var definition in additions)
            {
                if (definition is not JsonObject definitionObject)
                {
                    definitions.Add(definition?.DeepClone());
                    continue;
                }

                var id = DefinitionId(definitionObject);
                if (id is not null && definitionsById.TryGetValue(id, out var existing))
                {
                    // Local models sometimes echo immutable base-schema context verbatim. An exact echo is
                    // safe to ignore: source mappings are merged separately, and references still resolve
                    // against the saved definition. Anything different remains a conflict, not a guessable
                    // field-level merge.
                    if (JsonNode.DeepEquals(existing, definitionObject)) continue;
                    throw new SchemaTranslationFormatException($"Proposed definition '{id}' conflicts with the saved schema. Return a new id, or reference the saved definition instead of redefining it.");
                }

                definitions.Add(definitionObject.DeepClone());
                if (id is not null) definitionsById[id] = definitionObject;
            }
        }
        return merged;
    }

    public static IReadOnlyList<DefinitionSource> MergeDefinitionSources(IEnumerable<DefinitionSource> existing, IEnumerable<DefinitionSource> addition)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(addition);
        var byDefinition = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var source in existing.Concat(addition))
        {
            if (!byDefinition.TryGetValue(source.DefinitionId, out var ruleIds))
            {
                ruleIds = [];
                byDefinition[source.DefinitionId] = ruleIds;
                order.Add(source.DefinitionId);
            }
            foreach (var ruleId in source.WorkingRuleIds)
                if (!ruleIds.Contains(ruleId, StringComparer.Ordinal)) ruleIds.Add(ruleId);
        }
        return order.Select(id => new DefinitionSource(id, byDefinition[id])).ToArray();
    }

    public static IReadOnlyList<string> MergeWorkingRuleIds(IEnumerable<string> existing, IEnumerable<string> addition) =>
        existing.Concat(addition).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static Dictionary<string, JsonObject> DefinitionsById(JsonObject schema) => DefinitionCollections
        .SelectMany(collection => (schema[collection] as JsonArray)?.OfType<JsonObject>() ?? [])
        .Select(definition => new { Id = DefinitionId(definition), Definition = definition })
        .Where(entry => entry.Id is not null)
        .ToDictionary(entry => entry.Id!, entry => entry.Definition, StringComparer.Ordinal);

    private static string? DefinitionId(JsonObject definition) =>
        (definition["metadata"] as JsonObject)?["id"]?.GetValue<string>();
}
