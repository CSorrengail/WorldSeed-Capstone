using System.Text.Json.Nodes;

namespace WorldSeed.GameData;

/// <summary>Creates an editable starter entry from one game-owned entity definition.</summary>
public static class GameDataEntryTemplateService
{
    public static JsonObject Create(JsonObject schema, string entityTypeId, string instanceId)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (string.IsNullOrWhiteSpace(entityTypeId)) throw new ArgumentException("An entity type is required.", nameof(entityTypeId));
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("An instance id is required.", nameof(instanceId));

        var entityTypes = ById(schema["entityTypes"] as JsonArray);
        if (!entityTypes.TryGetValue(entityTypeId, out var entityType))
            throw new GameDataFormatException($"Entity type '{entityTypeId}' does not exist in the active schema.");
        var fragments = ById(schema["schemaFragments"] as JsonArray);
        var valueTypes = ById(schema["valueTypes"] as JsonArray);
        var values = CreateValues(EffectiveProperties(entityType, fragments), valueTypes);

        return new JsonObject
        {
            ["id"] = instanceId,
            ["entityTypeId"] = entityTypeId,
            ["values"] = values
        };
    }

    private static JsonObject CreateValues(IReadOnlyDictionary<string, JsonObject> properties, IReadOnlyDictionary<string, JsonObject> valueTypes)
    {
        var values = new JsonObject();
        foreach (var (propertyId, property) in properties)
        {
            if (property["defaultValue"] is { } defaultValue)
            {
                values[propertyId] = defaultValue.DeepClone();
                continue;
            }
            if (property["required"]?.GetValue<bool>() != true) continue;
            values[propertyId] = CreateValue(property["valueType"] as JsonObject, property, valueTypes, propertyId);
        }
        return values;
    }

    private static JsonNode? CreateValue(JsonObject? type, JsonObject property, IReadOnlyDictionary<string, JsonObject> valueTypes, string propertyId)
    {
        var kind = StringValue(type?["kind"]);
        return kind switch
        {
            "primitive" => CreatePrimitive(StringValue(type?["name"]), property, propertyId),
            "collection" => CreateCollection(type!, property, valueTypes, propertyId),
            "map" => new JsonObject(),
            "union" => (type?["options"] as JsonArray)?.OfType<JsonObject>().Select(option => CreateValue(option, property, valueTypes, propertyId)).FirstOrDefault(),
            "reference" => string.Empty,
            "definedValueType" => CreateDefinedValue(StringValue(type?["valueTypeId"]), property, valueTypes, propertyId),
            _ when property["nullable"]?.GetValue<bool>() == true => null,
            _ => throw new GameDataFormatException($"Required property '{propertyId}' has an unsupported value type.")
        };
    }

    private static JsonNode? CreateDefinedValue(string? valueTypeId, JsonObject property, IReadOnlyDictionary<string, JsonObject> valueTypes, string propertyId)
    {
        if (valueTypeId is null || !valueTypes.TryGetValue(valueTypeId, out var definition))
            throw new GameDataFormatException($"Required property '{propertyId}' references a value type that does not exist.");
        return StringValue(definition["kind"]) switch
        {
            "enumeration" => StringValue((definition["entries"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault()?["id"])
                ?? throw new GameDataFormatException($"Enumeration '{valueTypeId}' has no starter value."),
            "record" => CreateValues(ById(definition["properties"] as JsonArray), valueTypes),
            "alias" => CreateValue(definition["targetType"] as JsonObject, property, valueTypes, propertyId),
            _ => throw new GameDataFormatException($"Required property '{propertyId}' uses unsupported value type '{valueTypeId}'.")
        };
    }

    private static JsonArray CreateCollection(JsonObject type, JsonObject property, IReadOnlyDictionary<string, JsonObject> valueTypes, string propertyId)
    {
        var result = new JsonArray();
        var minimumItems = IntegerValue(type["minimumItems"]);
        for (var index = 0; index < minimumItems; index++)
            result.Add(CreateValue(type["itemType"] as JsonObject, property, valueTypes, propertyId)?.DeepClone());
        return result;
    }

    private static JsonNode? CreatePrimitive(string? name, JsonObject property, string propertyId)
    {
        var constraints = property["constraints"] as JsonObject;
        return name switch
        {
            "string" => StarterText(property, constraints, propertyId),
            "integer" => (int)Math.Round(BoundedNumber(constraints, integer: true)),
            "decimal" => BoundedNumber(constraints, integer: false),
            "boolean" => false,
            "date" => "2000-01-01",
            "dateTime" => "2000-01-01T00:00:00Z",
            "any" when property["nullable"]?.GetValue<bool>() == true => null,
            "any" => new JsonObject(),
            _ => throw new GameDataFormatException($"Required property '{propertyId}' uses unsupported primitive '{name}'.")
        };
    }

    private static string StarterText(JsonObject property, JsonObject? constraints, string propertyId)
    {
        var text = StringValue((property["metadata"] as JsonObject)?["name"]) ?? propertyId;
        var minimum = IntegerValue(constraints?["minimumLength"]);
        var maximum = IntegerValue(constraints?["maximumLength"]);
        if (minimum > 0 && text.Length < minimum) text = text.PadRight(minimum, 'x');
        if (maximum > 0 && text.Length > maximum) text = text[..maximum];
        return text;
    }

    private static double BoundedNumber(JsonObject? constraints, bool integer)
    {
        var minimum = NumberValue(constraints?["minimum"]);
        var maximum = NumberValue(constraints?["maximum"]);
        var value = minimum ?? 0;
        if (maximum is { } upper && value > upper) value = upper;
        return integer ? Math.Ceiling(value) : value;
    }

    private static IReadOnlyDictionary<string, JsonObject> EffectiveProperties(JsonObject owner, IReadOnlyDictionary<string, JsonObject> fragments)
    {
        var result = new Dictionary<string, JsonObject>();
        foreach (var fragmentId in Strings(owner, "fragmentIds").Where(fragments.ContainsKey))
            foreach (var (id, property) in ById(fragments[fragmentId]["properties"] as JsonArray)) result[id] = property;
        foreach (var (id, property) in ById(owner["properties"] as JsonArray)) result[id] = property;
        return result;
    }

    private static Dictionary<string, JsonObject> ById(JsonArray? definitions) => definitions?.OfType<JsonObject>()
        .Select(item => (Id: StringValue((item["metadata"] as JsonObject)?["id"]), Item: item))
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .ToDictionary(item => item.Id!, item => item.Item, StringComparer.Ordinal) ?? [];
    private static IEnumerable<string> Strings(JsonObject node, string name) => node[name]?.AsArray().Select(StringValue).Where(value => value is not null).Cast<string>() ?? [];
    private static string? StringValue(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;
    private static int IntegerValue(JsonNode? value) => value is JsonValue node && node.TryGetValue<int>(out var number) ? Math.Max(0, number) : 0;
    private static double? NumberValue(JsonNode? value) => value is JsonValue node && (node.TryGetValue<double>(out var number) || (node.TryGetValue<int>(out var integer) && (number = integer) == integer)) ? number : null;
}
