using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WorldSeed.SchemaValidation;

/// <summary>Validates game-data instances against one loaded canonical game schema.</summary>
public sealed class GameDataValidator
{
    public SchemaValidationResult Validate(JsonNode? schemaNode, JsonNode? dataNode)
    {
        var result = new SchemaValidationResult();
        if (schemaNode is not JsonObject schema || dataNode is not JsonObject data) { result.Add("$", "Schema and game data must both be JSON objects."); return result; }
        if (data["gameSchemaId"]?.GetValue<string>() != schema["id"]?.GetValue<string>()) result.Add("$.gameSchemaId", "Game data references a different schema.");
        if (data["gameSchemaVersion"]?.GetValue<string>() != schema["version"]?.GetValue<string>()) result.Add("$.gameSchemaVersion", "Game data references a different schema version.");

        var entities = ById(schema["entityTypes"] as JsonArray);
        var fragments = ById(schema["schemaFragments"] as JsonArray);
        var valueTypes = ById(schema["valueTypes"] as JsonArray);
        var instances = data["entityInstances"] as JsonArray;
        if (instances is null) { result.Add("$.entityInstances", "Game data requires an entityInstances array."); return result; }

        // Build the complete index before validating values so references may point either forward or backward.
        var instanceIds = new HashSet<string>();
        var instanceTypes = new Dictionary<string, string>();
        foreach (var instance in instances.OfType<JsonObject>())
        {
            var id = StringValue(instance["id"]); var typeId = StringValue(instance["entityTypeId"]);
            if (string.IsNullOrWhiteSpace(id) || !instanceIds.Add(id)) result.Add("$.entityInstances", "Every entity instance requires a unique id.");
            if (string.IsNullOrWhiteSpace(typeId) || !entities.ContainsKey(typeId)) result.Add("$.entityInstances", $"Unknown entity type '{typeId}'.");
            else if (!string.IsNullOrWhiteSpace(id) && !instanceTypes.ContainsKey(id)) instanceTypes[id] = typeId;
        }
        foreach (var instance in instances.OfType<JsonObject>())
        {
            var id = StringValue(instance["id"]); var typeId = StringValue(instance["entityTypeId"]);
            if (string.IsNullOrWhiteSpace(typeId) || !entities.TryGetValue(typeId, out var entity)) continue;
            ValidateValues(instance["values"] as JsonObject, EffectiveProperties(entity, fragments), valueTypes, instanceTypes, $"$.entityInstances[{id}]", result);
        }
        ValidateRelationships(schema["relationships"] as JsonArray, data["relationshipInstances"] as JsonArray, instanceTypes, valueTypes, result);
        return result;
    }

    private static void ValidateValues(JsonObject? values, IReadOnlyDictionary<string, JsonObject> properties, IReadOnlyDictionary<string, JsonObject> valueTypes, IReadOnlyDictionary<string, string> instances, string path, SchemaValidationResult result)
    {
        if (values is null) { result.Add(path, "Instance requires a values object."); return; }
        foreach (var (propertyId, property) in properties)
        {
            if (!values.TryGetPropertyValue(propertyId, out var value)) { if (property["defaultValue"] is not null) continue; if (property["required"]?.GetValue<bool>() == true) result.Add(path, $"Missing required property '{propertyId}'."); continue; }
            if (value is null) { if (property["nullable"]?.GetValue<bool>() != true) result.Add(path, $"Property '{propertyId}' cannot be null."); continue; }
            var valuePath = path + "." + propertyId;
            ValidateValue(value, property["valueType"] as JsonObject, valueTypes, instances, valuePath, result);
            ValidateConstraints(value, property["constraints"] as JsonObject, valuePath, result);
        }
        foreach (var key in values.Select(pair => pair.Key)) if (!properties.ContainsKey(key)) result.Add(path, $"Unknown property '{key}'.");
    }

    private static void ValidateValue(JsonNode value, JsonObject? type, IReadOnlyDictionary<string, JsonObject> valueTypes, IReadOnlyDictionary<string, string> instances, string path, SchemaValidationResult result)
    {
        var kind = StringValue(type?["kind"]);
        if (kind == "primitive") { var name = StringValue(type?["name"]); if (!MatchesPrimitive(value, name)) result.Add(path, $"Expected {name} value."); }
        else if (kind == "collection")
        {
            if (value is not JsonArray array) { result.Add(path, "Expected collection value."); return; }
            CheckCollectionBounds(array, type, path, result);
            for (var index = 0; index < array.Count; index++) if (array[index] is not null) ValidateValue(array[index]!, type?["itemType"] as JsonObject, valueTypes, instances, $"{path}[{index}]", result);
        }
        else if (kind == "map")
        {
            if (value is not JsonObject map) { result.Add(path, "Expected map value."); return; }
            foreach (var (key, mapValue) in map)
            {
                var keyValue = MapKeyValue(key, type?["keyType"] as JsonObject);
                if (keyValue is null) result.Add(path, $"Map key '{key}' cannot be represented by its declared key type.");
                else ValidateValue(keyValue, type?["keyType"] as JsonObject, valueTypes, instances, path + ".<key>", result);
                if (mapValue is null) result.Add(path + "." + key, "Map values cannot be null.");
                else ValidateValue(mapValue, type?["valueType"] as JsonObject, valueTypes, instances, path + "." + key, result);
            }
        }
        else if (kind == "union")
        {
            var options = type?["options"] as JsonArray;
            var matches = options?.OfType<JsonObject>().Any(option => MatchesType(value, option, valueTypes, instances, path)) == true;
            if (!matches) result.Add(path, "Value does not match any union option.");
        }
        else if (kind == "reference")
        {
            var id = StringValue(value);
            if (id is null || !instances.TryGetValue(id, out var instanceType) || !Strings(type!, "allowedEntityTypeIds").Contains(instanceType)) result.Add(path, "Reference does not resolve to an allowed entity instance.");
        }
        else if (kind == "definedValueType")
        {
            var id = StringValue(type?["valueTypeId"]);
            if (!valueTypes.TryGetValue(id ?? "", out var definition)) { result.Add(path, "Referenced value type does not exist."); return; }
            switch (StringValue(definition["kind"]))
            {
                case "record": ValidateValues(value as JsonObject, EffectiveProperties(definition, new Dictionary<string, JsonObject>()), valueTypes, instances, path, result); break;
                case "enumeration":
                    var entry = StringValue(value);
                    if (entry is null || !Strings(definition, "entries", "id").Contains(entry)) result.Add(path, $"Expected an entry from enumeration '{id}'.");
                    break;
                case "alias": ValidateValue(value, definition["targetType"] as JsonObject, valueTypes, instances, path, result); break;
            }
        }
    }

    private static bool MatchesType(JsonNode value, JsonObject option, IReadOnlyDictionary<string, JsonObject> valueTypes, IReadOnlyDictionary<string, string> instances, string path)
    {
        var trial = new SchemaValidationResult();
        ValidateValue(value, option, valueTypes, instances, path, trial);
        return trial.IsValid;
    }

    private static void ValidateConstraints(JsonNode value, JsonObject? constraints, string path, SchemaValidationResult result)
    {
        if (constraints is null) return;
        if (TryNumber(value, out var numeric))
        {
            if (TryNumber(constraints["minimum"], out var minimum) && numeric < minimum) result.Add(path, "Value is below its minimum.");
            if (TryNumber(constraints["maximum"], out var maximum) && numeric > maximum) result.Add(path, "Value is above its maximum.");
        }
        if (StringValue(value) is { } text)
        {
            if (TryNumber(constraints["minimumLength"], out var minimumLength) && text.Length < minimumLength) result.Add(path, "Value is shorter than its minimum length.");
            if (TryNumber(constraints["maximumLength"], out var maximumLength) && text.Length > maximumLength) result.Add(path, "Value is longer than its maximum length.");
            var pattern = StringValue(constraints["pattern"]);
            if (pattern is not null && !Regex.IsMatch(text, pattern)) result.Add(path, "Value does not match its required pattern.");
        }
        if (constraints["distinctItems"]?.GetValue<bool>() == true && value is JsonArray array)
        {
            var duplicates = array.Where(item => item is not null).GroupBy(item => item!.ToJsonString()).Any(group => group.Count() > 1);
            if (duplicates) result.Add(path, "Collection items must be distinct.");
        }
    }

    private static void CheckCollectionBounds(JsonArray array, JsonObject? type, string path, SchemaValidationResult result)
    {
        if (TryNumber(type?["minimumItems"], out var minimum) && array.Count < minimum) result.Add(path, "Collection has fewer than its minimum items.");
        if (TryNumber(type?["maximumItems"], out var maximum) && array.Count > maximum) result.Add(path, "Collection has more than its maximum items.");
    }

    private static void ValidateRelationships(JsonArray? definitions, JsonArray? instances, IReadOnlyDictionary<string, string> entityTypes, IReadOnlyDictionary<string, JsonObject> valueTypes, SchemaValidationResult result)
    {
        var relationships = ById(definitions);
        var validInstances = new List<(string DefinitionId, string From, string To)>();
        var relationshipIds = new HashSet<string>();
        foreach (var instance in instances?.OfType<JsonObject>() ?? [])
        {
            var instanceId = StringValue(instance["id"]);
            if (string.IsNullOrWhiteSpace(instanceId) || !relationshipIds.Add(instanceId)) result.Add("$.relationshipInstances", "Every relationship instance requires a unique id.");
            var definitionId = StringValue(instance["relationshipDefinitionId"]);
            if (!relationships.TryGetValue(definitionId ?? "", out var definition)) { result.Add("$.relationshipInstances", $"Unknown relationship '{definitionId}'."); continue; }
            var from = StringValue(instance["fromEntityInstanceId"]); var to = StringValue(instance["toEntityInstanceId"]);
            var validFrom = CheckEnd(from, definition["from"] as JsonObject, entityTypes, result);
            var validTo = CheckEnd(to, definition["to"] as JsonObject, entityTypes, result);
            ValidateValues(instance["attributeValues"] as JsonObject ?? new JsonObject(), ById(definition["attributes"] as JsonArray), valueTypes, entityTypes, "$.relationshipInstances[" + instanceId + "].attributeValues", result);
            if (validFrom && validTo) validInstances.Add((definitionId!, from!, to!));
        }
        foreach (var (definitionId, definition) in relationships)
        {
            var fromType = StringValue((definition["from"] as JsonObject)?["entityTypeId"]);
            var toType = StringValue((definition["to"] as JsonObject)?["entityTypeId"]);
            foreach (var entityId in entityTypes.Where(pair => pair.Value == fromType).Select(pair => pair.Key))
                CheckCardinality(validInstances.Count(edge => edge.DefinitionId == definitionId && edge.From == entityId), definition["to"] as JsonObject, "$.relationshipInstances", result);
            foreach (var entityId in entityTypes.Where(pair => pair.Value == toType).Select(pair => pair.Key))
                CheckCardinality(validInstances.Count(edge => edge.DefinitionId == definitionId && edge.To == entityId), definition["from"] as JsonObject, "$.relationshipInstances", result);
        }
    }

    private static bool CheckEnd(string? instanceId, JsonObject? end, IReadOnlyDictionary<string, string> entityTypes, SchemaValidationResult result)
    {
        if (instanceId is not null && end is not null && entityTypes.TryGetValue(instanceId, out var type) && type == StringValue(end["entityTypeId"])) return true;
        result.Add("$.relationshipInstances", "Relationship instance endpoint has the wrong entity type."); return false;
    }

    private static void CheckCardinality(int count, JsonObject? end, string path, SchemaValidationResult result)
    {
        if (TryNumber(end?["minimumPerOppositeInstance"], out var minimum) && count < minimum) result.Add(path, "Relationship count is below its minimum cardinality.");
        if (TryNumber(end?["maximumPerOppositeInstance"], out var maximum) && count > maximum) result.Add(path, "Relationship count is above its maximum cardinality.");
    }

    private static JsonNode? MapKeyValue(string key, JsonObject? type) => StringValue(type?["kind"]) switch
    {
        "primitive" when StringValue(type?["name"]) == "integer" && int.TryParse(key, out var integer) => JsonValue.Create(integer),
        "primitive" when StringValue(type?["name"]) == "decimal" && double.TryParse(key, out var decimalValue) => JsonValue.Create(decimalValue),
        "primitive" when StringValue(type?["name"]) == "boolean" && bool.TryParse(key, out var boolean) => JsonValue.Create(boolean),
        "primitive" => JsonValue.Create(key),
        "definedValueType" or "union" => JsonValue.Create(key),
        _ => null
    };
    private static bool MatchesPrimitive(JsonNode value, string? name) => name switch { "string" or "date" or "dateTime" => StringValue(value) is not null, "integer" => value is JsonValue i && i.TryGetValue<int>(out _), "decimal" => value is JsonValue d && d.TryGetValue<double>(out _), "boolean" => value is JsonValue b && b.TryGetValue<bool>(out _), "any" => true, _ => false };
    private static bool TryNumber(JsonNode? value, out double number) { number = 0; return value is JsonValue node && (node.TryGetValue<double>(out number) || (node.TryGetValue<int>(out var integer) && (number = integer) == integer)); }
    private static string? StringValue(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;
    private static Dictionary<string, JsonObject> ById(JsonArray? definitions) => definitions?.OfType<JsonObject>().Where(node => Id(node) is not null).ToDictionary(node => Id(node)!) ?? [];
    private static Dictionary<string, JsonObject> EffectiveProperties(JsonObject owner, IReadOnlyDictionary<string, JsonObject> fragments) { var result = new Dictionary<string, JsonObject>(); foreach (var fragment in Strings(owner, "fragmentIds").Where(fragments.ContainsKey)) foreach (var property in fragments[fragment]["properties"]?.AsArray().OfType<JsonObject>() ?? []) if (Id(property) is string id) result[id] = property; foreach (var property in owner["properties"]?.AsArray().OfType<JsonObject>() ?? []) if (Id(property) is string id) result[id] = property; return result; }
    private static string? Id(JsonObject node) => StringValue((node["metadata"] as JsonObject)?["id"]);
    private static IEnumerable<string> Strings(JsonObject node, string name) => node[name]?.AsArray().Select(StringValue).Where(value => value is not null).Cast<string>() ?? [];
    private static IEnumerable<string> Strings(JsonObject node, string arrayName, string propertyName) => node[arrayName]?.AsArray().OfType<JsonObject>().Select(entry => StringValue(entry[propertyName])).Where(value => value is not null).Cast<string>() ?? [];
}
