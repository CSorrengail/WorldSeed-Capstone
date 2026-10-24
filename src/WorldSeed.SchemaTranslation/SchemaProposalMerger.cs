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

        var existingIds = DefinitionIds(existingSchema).ToHashSet(StringComparer.Ordinal);
        foreach (var id in DefinitionIds(addition))
            if (!existingIds.Add(id)) throw new SchemaTranslationFormatException($"Proposed definition '{id}' already exists in the saved schema. Return only genuinely new definitions.");

        var merged = (JsonObject)existingSchema.DeepClone();
        foreach (var collection in DefinitionCollections)
        {
            if (addition[collection] is not JsonArray additions || additions.Count == 0) continue;
            if (merged[collection] is not JsonArray definitions)
            {
                definitions = [];
                merged[collection] = definitions;
            }
            foreach (var definition in additions) definitions.Add(definition?.DeepClone());
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

    private static IEnumerable<string> DefinitionIds(JsonObject schema) => DefinitionCollections
        .SelectMany(collection => (schema[collection] as JsonArray)?.OfType<JsonObject>() ?? [])
        .Select(definition => (definition["metadata"] as JsonObject)?["id"]?.GetValue<string>())
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Cast<string>();
}
