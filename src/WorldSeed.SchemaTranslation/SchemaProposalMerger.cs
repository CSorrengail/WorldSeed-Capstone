using System.Text.Json.Nodes;

namespace WorldSeed.SchemaTranslation;

/// <summary>Combines independently validated schema additions into one mutable, non-canonical project proposal.</summary>
public static class SchemaProposalMerger
{
    private static readonly string[] DefinitionCollections = ["schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations"];

    /// <summary>
    /// Combines a normal addition, or replaces only definitions explicitly returned by a
    /// designer-approved schema-change request. Normal rule translation remains append-only.
    /// </summary>
    public static JsonObject MergeSchemas(JsonObject? existingSchema, JsonObject addition, bool allowExistingDefinitionUpdates = false)
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
                    if (!allowExistingDefinitionUpdates)
                        throw new SchemaTranslationFormatException($"Proposed definition '{id}' conflicts with the saved schema. Return a new id, or reference the saved definition instead of redefining it.");

                    MergeChangedDefinition(existing, definitionObject);
                    continue;
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

    /// <summary>Finds definitions that merely repeat an immutable saved definition without changing it.</summary>
    public static IReadOnlySet<string> ExactSavedDefinitionIds(JsonObject? existingSchema, JsonObject addition)
    {
        if (existingSchema is null) return new HashSet<string>(StringComparer.Ordinal);
        var existingById = DefinitionsById(existingSchema);
        return DefinitionsById(addition)
            .Where(pair => existingById.TryGetValue(pair.Key, out var existing) && JsonNode.DeepEquals(existing, pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, JsonObject> DefinitionsById(JsonObject schema) => DefinitionCollections
        .SelectMany(collection => (schema[collection] as JsonArray)?.OfType<JsonObject>() ?? [])
        .Select(definition => new { Id = DefinitionId(definition), Definition = definition })
        .Where(entry => entry.Id is not null)
        .ToDictionary(entry => entry.Id!, entry => entry.Definition, StringComparer.Ordinal);

    private static string? DefinitionId(JsonObject definition) =>
        (definition["metadata"] as JsonObject)?["id"]?.GetValue<string>();

    /// <summary>
    /// Applies an additive schema patch without requiring a local model to echo a large existing
    /// definition. Unmentioned members remain untouched; listed members are merged by their id.
    /// This keeps a small request such as "add reach to Weapon" bounded and non-destructive.
    /// </summary>
    private static void MergeChangedDefinition(JsonObject existing, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Key is "properties" or "entries" && property.Value is JsonArray patchMembers)
            {
                var members = existing[property.Key] as JsonArray ?? new JsonArray();
                existing[property.Key] = members;
                MergeMembers(members, patchMembers);
                continue;
            }
            if (property.Value is JsonObject patchObject && existing[property.Key] is JsonObject existingObject)
            {
                MergeJsonObjects(existingObject, patchObject);
                continue;
            }
            existing[property.Key] = property.Value?.DeepClone();
        }
    }

    private static void MergeMembers(JsonArray existing, JsonArray patch)
    {
        var byId = existing.OfType<JsonObject>()
            .Select(member => new { Id = DefinitionId(member) ?? member["id"]?.GetValue<string>(), Member = member })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id!, item => item.Member, StringComparer.Ordinal);
        foreach (var patchMember in patch)
        {
            if (patchMember is not JsonObject patchObject)
            {
                existing.Add(patchMember?.DeepClone());
                continue;
            }
            var id = DefinitionId(patchObject) ?? patchObject["id"]?.GetValue<string>();
            if (id is not null && byId.TryGetValue(id, out var existingMember)) MergeJsonObjects(existingMember, patchObject);
            else existing.Add(patchObject.DeepClone());
        }
    }

    private static void MergeJsonObjects(JsonObject existing, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is JsonObject patchObject && existing[property.Key] is JsonObject existingObject)
                MergeJsonObjects(existingObject, patchObject);
            else existing[property.Key] = property.Value?.DeepClone();
        }
    }
}
