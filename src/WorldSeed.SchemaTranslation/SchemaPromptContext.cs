using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WorldSeed.SchemaTranslation;

/// <summary>Prompt-only projection. Validation and merging always use the full schema.</summary>
internal static class SchemaPromptContext
{
    internal const int MaximumCharacters = 6000;
    private static readonly string[] Collections = ["entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations", "schemaFragments"];
    private static readonly HashSet<string> StopWords = new("a an the and or of to in on is are be by for with from this that has have as at it if".Split(' '), StringComparer.OrdinalIgnoreCase);

    public static (JsonObject? Schema, JsonObject Index) Create(JsonObject? schema, string selectedText)
    {
        var index = new JsonObject();
        if (schema is null) return (null, index);
        var definitions = Collections.SelectMany(collection => (schema[collection] as JsonArray)?.OfType<JsonObject>()
            .Select(definition => (Collection: collection, Definition: definition, Id: Id(definition))) ?? [])
            .Where(item => item.Id.Length > 0).ToArray();
        foreach (var collection in Collections)
            index[collection] = new JsonArray(definitions.Where(item => item.Collection == collection).Select(item => (JsonNode?)JsonValue.Create(item.Id)).ToArray());
        if (schema.ToJsonString().Length + index.ToJsonString().Length <= MaximumCharacters) return ((JsonObject)schema.DeepClone(), index);

        var result = new JsonObject();
        foreach (var property in new[] { "id", "name", "version" }) result[property] = schema[property]?.DeepClone();
        foreach (var collection in Collections) result[collection] = new JsonArray();
        if (result.ToJsonString().Length + index.ToJsonString().Length > MaximumCharacters)
            throw new SchemaTranslationFormatException("The Schema Reference Index Exceeds The Bounded Prompt Budget. Existing Schema Was Kept; This Request Needs A More Focused Context Strategy.");
        var terms = Tokens(selectedText);
        var byId = definitions.GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var included = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seed in definitions.Select(item => (Item: item, Score: Tokens(item.Id + " " + (item.Definition["metadata"]?["name"] ?? item.Definition["name"])?.ToString()).Count(terms.Contains)))
                     .Where(item => item.Score > 0).OrderByDescending(item => item.Score))
        {
            var closure = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string id)
            {
                if (included.Contains(id) || !closure.Add(id) || !byId.TryGetValue(id, out var item)) return;
                foreach (var dependency in References(item.Definition).Where(byId.ContainsKey)) Visit(dependency);
            }
            Visit(seed.Item.Id);
            var candidate = (JsonObject)result.DeepClone();
            foreach (var id in closure)
                if (byId.TryGetValue(id, out var item)) candidate[item.Collection]!.AsArray().Add(item.Definition.DeepClone());
            if (candidate.ToJsonString().Length + index.ToJsonString().Length > MaximumCharacters) continue;
            result = candidate; included.UnionWith(closure);
        }
        return (result, index);
    }

    private static string Id(JsonObject definition) => (definition["metadata"]?["id"] ?? definition["id"])?.GetValue<string>() ?? "";
    private static HashSet<string> Tokens(string text) => Regex.Matches(Regex.Replace(text, "(?<=[a-z0-9])(?=[A-Z])", " "), "[A-Za-z0-9]+")
        .Select(match => match.Value).Where(token => token.Length >= 2 && !StopWords.Contains(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    internal static IEnumerable<string> References(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var pair in obj)
            {
                if (pair.Key is "entityTypeId" or "valueTypeId" or "fragmentId")
                { if (pair.Value is JsonValue value && value.TryGetValue<string>(out var id)) yield return id; }
                else if (pair.Key is "allowedEntityTypeIds" or "fragmentIds" && pair.Value is JsonArray ids)
                { foreach (var id in ids.OfType<JsonValue>()) if (id.TryGetValue<string>(out var text)) yield return text; }
                else foreach (var reference in References(pair.Value)) yield return reference;
            }
        else if (node is JsonArray array)
            foreach (var item in array) foreach (var reference in References(item)) yield return reference;
    }
}
