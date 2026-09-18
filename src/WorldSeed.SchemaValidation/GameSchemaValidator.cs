using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WorldSeed.SchemaValidation;

/// <summary>Performs cross-reference and composition checks beyond JSON document-shape validation.</summary>
public sealed class GameSchemaValidator
{
    public SchemaValidationResult Validate(JsonNode? schema)
    {
        var result = new SchemaValidationResult();
        if (schema is not JsonObject root) { result.Add("$", "Game schema must be a JSON object."); return result; }

        RequireString(root, "id", "$", result);
        RequireString(root, "name", "$", result);
        RequireString(root, "version", "$", result);
        var entityTypes = RequireArray(root, "entityTypes", "$", result);

        var entityIds = CollectDefinitionIds(entityTypes, "$.entityTypes", result);
        var valueTypeIds = CollectDefinitionIds(GetArray(root, "valueTypes"), "$.valueTypes", result);
        var fragmentsArray = GetArray(root, "schemaFragments");
        var fragmentIds = CollectDefinitionIds(fragmentsArray, "$.schemaFragments", result);
        var fragments = fragmentsArray?.OfType<JsonObject>().Where(fragment => GetId(fragment) is not null)
            .ToDictionary(fragment => GetId(fragment)!) ?? new Dictionary<string, JsonObject>();
        var allDefinitionIds = entityIds.Concat(valueTypeIds).Concat(fragmentIds)
            .Concat(CollectDefinitionIds(GetArray(root, "relationships"), "$.relationships", result))
            .Concat(CollectDefinitionIds(GetArray(root, "rules"), "$.rules", result))
            .Concat(CollectDefinitionIds(GetArray(root, "procedures"), "$.procedures", result))
            .Concat(CollectDefinitionIds(GetArray(root, "validations"), "$.validations", result)).ToHashSet();

        var propertyIds = new HashSet<string>();
        foreach (var group in new[] { entityTypes, GetArray(root, "valueTypes"), GetArray(root, "schemaFragments") })
            ValidateOwners(group, entityIds, valueTypeIds, fragmentIds, fragments, propertyIds, result);

        ValidateValueTypeDefinitions(GetArray(root, "valueTypes"), entityIds, valueTypeIds, result);
        ValidateRelationships(GetArray(root, "relationships"), entityIds, result);
        ValidateRelationshipAttributes(GetArray(root, "relationships"), entityIds, valueTypeIds, propertyIds, result);
        ValidateProcedureReferences(GetArray(root, "procedures"), entityIds, GetIds(GetArray(root, "rules")), result);
        ValidateTargets(GetArray(root, "rules"), allDefinitionIds, propertyIds, result);
        ValidateTargets(GetArray(root, "validations"), allDefinitionIds, propertyIds, result);
        return result;
    }

    private static void ValidateOwners(JsonArray? owners, HashSet<string> entityIds, HashSet<string> valueTypeIds, HashSet<string> fragmentIds, IReadOnlyDictionary<string, JsonObject> fragments, HashSet<string> propertyIds, SchemaValidationResult result)
    {
        if (owners is null) return;
        foreach (var owner in owners.OfType<JsonObject>())
        {
            var ownerId = GetId(owner);
            if (ownerId is null) continue;
            var effective = new HashSet<string>();
            foreach (var fragmentId in GetStringArray(owner, "fragmentIds"))
            {
                if (!fragmentIds.Contains(fragmentId)) result.Add($"{ownerId}.fragmentIds", $"Unknown schema fragment '{fragmentId}'.");
                else foreach (var property in GetArray(fragments[fragmentId], "properties")?.OfType<JsonObject>() ?? [])
                {
                    var propertyId = GetId(property);
                    if (propertyId is not null && !effective.Add(propertyId)) result.Add($"{ownerId}.fragmentIds", $"Property ID '{propertyId}' is supplied more than once through composition.");
                }
            }
            foreach (var property in GetArray(owner, "properties")?.OfType<JsonObject>() ?? [])
            {
                var propertyId = GetId(property);
                if (propertyId is null) { result.Add($"{ownerId}.properties", "Every property requires metadata.id."); continue; }
                if (!effective.Add(propertyId)) result.Add($"{ownerId}.properties", $"Duplicate property ID '{propertyId}'.");
                propertyIds.Add(propertyId);
                ValidateProperty(property, entityIds, valueTypeIds, $"{ownerId}.{propertyId}", result);
            }
        }
    }

    private static void ValidateValueTypeDefinitions(JsonArray? definitions, HashSet<string> entityIds, HashSet<string> valueTypeIds, SchemaValidationResult result)
    {
        if (definitions is null) return;
        foreach (var definition in definitions.OfType<JsonObject>())
        {
            var id = GetId(definition) ?? "$.valueTypes";
            switch (definition["kind"]?.GetValue<string>())
            {
                case "enumeration":
                    var entries = new HashSet<string>();
                    foreach (var entry in GetArray(definition, "entries")?.OfType<JsonObject>() ?? [])
                    {
                        var entryId = entry["id"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(entryId) || !entries.Add(entryId)) result.Add(id, "Enumeration entries require unique non-empty ids.");
                    }
                    if (entries.Count == 0) result.Add(id, "Enumeration value types require at least one entry.");
                    break;
                case "record": break;
                case "alias": ValidateType(definition["targetType"] as JsonObject, entityIds, valueTypeIds, id + ".targetType", result); break;
                default: result.Add(id, "Value type kind must be enumeration, record, or alias."); break;
            }
        }
    }

    private static void ValidateType(JsonObject? type, HashSet<string> entityIds, HashSet<string> valueTypeIds, string path, SchemaValidationResult result)
    {
        var kind = type?["kind"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(kind)) { result.Add(path, "Property valueType requires a kind."); return; }
        if (!new[] { "primitive", "definedValueType", "reference", "collection", "map", "union" }.Contains(kind)) { result.Add(path, $"Unknown value type kind '{kind}'."); return; }
        if (kind == "primitive" && !new[] { "string", "integer", "decimal", "boolean", "date", "dateTime", "any" }.Contains(type?["name"]?.GetValue<string>())) result.Add(path, "Primitive value types require a supported name.");
        if (kind == "definedValueType" && !valueTypeIds.Contains(type!["valueTypeId"]?.GetValue<string>() ?? "")) result.Add(path, "Referenced value type does not exist.");
        if (kind == "reference") { var ids = GetStringArray(type!, "allowedEntityTypeIds").ToArray(); if (ids.Length == 0) result.Add(path, "Reference value types require allowed entity types."); foreach (var id in ids) if (!entityIds.Contains(id)) result.Add(path, $"Referenced entity type '{id}' does not exist."); }
        if (kind == "collection") { ValidateType(type?["itemType"] as JsonObject, entityIds, valueTypeIds, path + ".itemType", result); ValidateBounds(type, "minimumItems", "maximumItems", path, result); }
        if (kind == "map") { ValidateType(type?["keyType"] as JsonObject, entityIds, valueTypeIds, path + ".keyType", result); ValidateType(type?["valueType"] as JsonObject, entityIds, valueTypeIds, path + ".valueType", result); }
        if (kind == "union") { var options = GetArray(type!, "options"); if (options is not { Count: > 0 }) result.Add(path, "Union value types require at least one option."); else foreach (var option in options.OfType<JsonObject>()) ValidateType(option, entityIds, valueTypeIds, path + ".options", result); }
    }

    private static void ValidateRelationships(JsonArray? relationships, HashSet<string> entityIds, SchemaValidationResult result)
    {
        if (relationships is null) return;
        foreach (var relationship in relationships.OfType<JsonObject>())
            foreach (var end in new[] { relationship["from"] as JsonObject, relationship["to"] as JsonObject })
                if (end is null || !entityIds.Contains(end["entityTypeId"]?.GetValue<string>() ?? "")) result.Add("$.relationships", "Relationship ends must reference an existing entity type.");
                else ValidateBounds(end, "minimumPerOppositeInstance", "maximumPerOppositeInstance", "$.relationships", result);
    }

    private static void ValidateRelationshipAttributes(JsonArray? relationships, HashSet<string> entityIds, HashSet<string> valueTypeIds, HashSet<string> propertyIds, SchemaValidationResult result)
    {
        if (relationships is null) return;
        foreach (var relationship in relationships.OfType<JsonObject>())
        {
            var relationshipId = GetId(relationship) ?? "$.relationships"; var attributes = new HashSet<string>();
            foreach (var attribute in GetArray(relationship, "attributes")?.OfType<JsonObject>() ?? [])
            {
                var attributeId = GetId(attribute);
                if (attributeId is null || !attributes.Add(attributeId)) result.Add(relationshipId, "Relationship attributes require unique metadata ids.");
                else propertyIds.Add(attributeId);
                ValidateProperty(attribute, entityIds, valueTypeIds, relationshipId + ".attributes", result);
            }
        }
    }

    private static void ValidateProcedureReferences(JsonArray? procedures, HashSet<string> entityIds, HashSet<string> ruleIds, SchemaValidationResult result)
    {
        if (procedures is null) return;
        foreach (var procedure in procedures.OfType<JsonObject>())
        {
            foreach (var id in GetStringArray(procedure, "participantEntityTypeIds")) if (!entityIds.Contains(id)) result.Add("$.procedures", $"Unknown procedure participant entity type '{id}'.");
            foreach (var id in GetStringArray(procedure, "ruleIds")) if (!ruleIds.Contains(id)) result.Add("$.procedures", $"Unknown procedure rule '{id}'.");
        }
    }

    private static void ValidateProperty(JsonObject property, HashSet<string> entityIds, HashSet<string> valueTypeIds, string path, SchemaValidationResult result)
    {
        ValidateType(property["valueType"] as JsonObject, entityIds, valueTypeIds, path, result);
        var constraints = property["constraints"] as JsonObject;
        if (constraints is not null)
        {
            ValidateBounds(constraints, "minimum", "maximum", path, result);
            ValidateBounds(constraints, "minimumLength", "maximumLength", path, result);
            if (constraints["pattern"]?.GetValue<string>() is { } pattern) try { _ = new Regex(pattern); } catch (ArgumentException) { result.Add(path, "Property constraint pattern must be a valid regular expression."); }
        }
    }

    private static void ValidateBounds(JsonObject? owner, string minimumName, string maximumName, string path, SchemaValidationResult result)
    {
        var minimum = Number(owner?[minimumName]); var maximum = Number(owner?[maximumName]);
        if (minimum is { } min && min < 0 && minimumName != "minimum") result.Add(path, $"{minimumName} cannot be negative.");
        if (maximum is { } max && max < 0) result.Add(path, $"{maximumName} cannot be negative.");
        if (minimum is { } lower && maximum is { } upper && lower > upper) result.Add(path, $"{minimumName} cannot exceed {maximumName}.");
    }

    private static void ValidateTargets(JsonArray? definitions, HashSet<string> definitionIds, HashSet<string> propertyIds, SchemaValidationResult result)
    {
        if (definitions is null) return;
        foreach (var definition in definitions.OfType<JsonObject>())
            foreach (var target in GetArray(definition, "targets")?.OfType<JsonObject>() ?? [])
            {
                foreach (var id in GetStringArray(target, "definitionIds")) if (!definitionIds.Contains(id)) result.Add("$.targets", $"Unknown definition target '{id}'.");
                foreach (var id in GetStringArray(target, "propertyIds")) if (!propertyIds.Contains(id)) result.Add("$.targets", $"Unknown property target '{id}'.");
            }
    }

    private static HashSet<string> CollectDefinitionIds(JsonArray? definitions, string path, SchemaValidationResult result)
    {
        var ids = new HashSet<string>(); if (definitions is null) return ids;
        foreach (var definition in definitions.OfType<JsonObject>()) { var id = GetId(definition); if (id is null) result.Add(path, "Every definition requires metadata.id."); else if (!ids.Add(id)) result.Add(path, $"Duplicate definition ID '{id}'."); }
        return ids;
    }
    private static JsonArray? RequireArray(JsonObject root, string name, string path, SchemaValidationResult result) => root[name] as JsonArray ?? AddMissing(path, name, result);
    private static JsonArray? AddMissing(string path, string name, SchemaValidationResult result) { result.Add(path, $"'{name}' must be an array."); return null; }
    private static JsonArray? GetArray(JsonObject root, string name) => root[name] as JsonArray;
    private static HashSet<string> GetIds(JsonArray? definitions) => definitions?.OfType<JsonObject>().Select(GetId).Where(id => id is not null).Cast<string>().ToHashSet() ?? [];
    private static double? Number(JsonNode? value) => value is JsonValue node && (node.TryGetValue<double>(out var number) || (node.TryGetValue<int>(out var integer) && (number = integer) == integer)) ? number : null;
    private static string? GetId(JsonObject node) => (node["metadata"] as JsonObject)?["id"]?.GetValue<string>();
    private static IEnumerable<string> GetStringArray(JsonObject node, string name) => (node[name] as JsonArray)?.Select(x => x?.GetValue<string>()).Where(x => x is not null).Cast<string>() ?? [];
    private static void RequireString(JsonObject node, string name, string path, SchemaValidationResult result) { if (node[name]?.GetValue<string>() is not { Length: > 0 }) result.Add(path, $"'{name}' must be a non-empty string."); }
}
