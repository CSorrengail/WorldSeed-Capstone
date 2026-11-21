using System.Text.Json.Nodes;

namespace WorldSeed.Content;

/// <summary>Bounded game-owned reference targets, including nested types and declared relationships.</summary>
public static class ContentReferenceContext
{
    public const int MaximumEntries = 12;
    public static IReadOnlyList<JsonObject> Select(JsonObject schema, string entityTypeId, JsonObject data)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var definitions = (schema["valueTypes"] as JsonArray)?.OfType<JsonObject>().ToDictionary(Id, StringComparer.Ordinal) ?? [];
        void Visit(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                if (obj["kind"]?.GetValue<string>() == "reference")
                    foreach (var id in (obj["allowedEntityTypeIds"] as JsonArray)?.Select(value => value!.GetValue<string>()) ?? []) allowed.Add(id);
                if (obj["kind"]?.GetValue<string>() == "definedValueType" && obj["valueTypeId"]?.GetValue<string>() is { } referencedTypeId &&
                    visited.Add(referencedTypeId) && definitions.TryGetValue(referencedTypeId, out var definition)) Visit(definition);
                foreach (var value in obj.Select(item => item.Value)) Visit(value);
            }
            else if (node is JsonArray array) foreach (var child in array) Visit(child);
        }
        var entity = (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item => Id(item) == entityTypeId);
        Visit(entity);
        var fragments = (schema["schemaFragments"] as JsonArray)?.OfType<JsonObject>().ToDictionary(Id, StringComparer.Ordinal) ?? [];
        foreach (var id in (entity?["fragmentIds"] as JsonArray)?.Select(value => value!.GetValue<string>()) ?? [])
            if (fragments.TryGetValue(id, out var fragment)) Visit(fragment);
        foreach (var relation in Relationships(schema, entityTypeId))
        {
            var from = relation["from"]?["entityTypeId"]?.GetValue<string>();
            var to = relation["to"]?["entityTypeId"]?.GetValue<string>();
            if (from == entityTypeId && to is not null) allowed.Add(to);
            if (to == entityTypeId && from is not null) allowed.Add(from);
        }
        return (data["entityInstances"] as JsonArray)?.OfType<JsonObject>()
            .Where(item => allowed.Contains(item["entityTypeId"]?.GetValue<string>() ?? ""))
            .GroupBy(item => item["entityTypeId"]!.GetValue<string>())
            .SelectMany(group => group.OrderBy(item => item["id"]!.GetValue<string>(), StringComparer.Ordinal).Select((item, index) => new { Item = item, Index = index }))
            .OrderBy(item => item.Index).ThenBy(item => item.Item["entityTypeId"]!.GetValue<string>(), StringComparer.Ordinal)
            .Take(MaximumEntries).Select(item => (JsonObject)item.Item.DeepClone()).ToArray() ?? [];
    }

    public static IEnumerable<JsonObject> Relationships(JsonObject schema, string entityTypeId) =>
        (schema["relationships"] as JsonArray)?.OfType<JsonObject>().Where(item =>
            item["from"]?["entityTypeId"]?.GetValue<string>() == entityTypeId || item["to"]?["entityTypeId"]?.GetValue<string>() == entityTypeId) ?? [];
    private static string Id(JsonObject node) => node["metadata"]?["id"]?.GetValue<string>() ?? node["id"]?.GetValue<string>() ?? "";
}
