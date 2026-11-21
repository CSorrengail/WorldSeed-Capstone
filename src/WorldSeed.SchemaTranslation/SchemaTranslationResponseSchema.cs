using System.Text.Json.Nodes;

namespace WorldSeed.SchemaTranslation;

/// <summary>Native structured-output contract that keeps local schema proposals small and syntactically complete.</summary>
public static class SchemaTranslationResponseSchema
{
    public static JsonObject Create(IReadOnlySet<string> allowedWorkingRuleIds, bool singleContentType = false)
    {
        ArgumentNullException.ThrowIfNull(allowedWorkingRuleIds);
        if (allowedWorkingRuleIds.Count == 0) throw new ArgumentException("At least one Working Rule id is required.", nameof(allowedWorkingRuleIds));
        var schema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["action"],
          "properties": {
            "action": { "type": "string", "enum": ["askClarifyingQuestion", "presentProposal"] },
            "clarifyingQuestion": { "type": "string", "minLength": 1 },
            "proposal": {
              "type": "object",
              "additionalProperties": false,
              "required": ["summary", "schema", "definitionSources"],
              "properties": {
                "summary": { "type": "string", "minLength": 1 },
                "schema": {
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["id", "name", "version", "entityTypes"],
                  "properties": {
                    "id": { "type": "string", "minLength": 1 },
                    "name": { "type": "string", "minLength": 1 },
                    "version": { "type": "string", "minLength": 1 },
                    "entityTypes": { "type": "array", "maxItems": 3, "items": { "$ref": "#/$defs/entityType" } },
                    "valueTypes": { "type": "array", "maxItems": 3, "items": { "$ref": "#/$defs/valueType" } },
                    "relationships": { "type": "array", "maxItems": 3, "items": { "$ref": "#/$defs/relationship" } },
                    "rules": { "type": "array", "maxItems": 3, "items": { "type": "object" } },
                    "procedures": { "type": "array", "maxItems": 3, "items": { "type": "object" } },
                    "validations": { "type": "array", "maxItems": 3, "items": { "type": "object" } }
                  }
                },
                "definitionSources": {
                  "type": "array", "minItems": 1, "maxItems": 3,
                  "items": {
                    "type": "object", "additionalProperties": false,
                    "required": ["definitionId", "workingRuleIds"],
                    "properties": {
                      "definitionId": { "type": "string", "minLength": 1 },
                      "workingRuleIds": { "type": "array", "minItems": 1, "uniqueItems": true, "items": { "type": "string", "enum": [] } }
                    }
                  }
                }
              }
            }
          },
          "allOf": [
            { "if": { "properties": { "action": { "const": "askClarifyingQuestion" } } }, "then": { "required": ["clarifyingQuestion"] } },
            { "if": { "properties": { "action": { "const": "presentProposal" } } }, "then": { "required": ["proposal"] } }
          ],
          "$defs": {
            "metadata": {
              "type": "object", "additionalProperties": false,
              "required": ["id", "name"],
              "properties": { "id": { "type": "string", "minLength": 1 }, "name": { "type": "string", "minLength": 1 }, "description": { "type": "string" } }
            },
            "valueReference": {
              "type": "object", "additionalProperties": true,
              "required": ["kind"],
              "properties": {
                "kind": { "type": "string", "enum": ["primitive", "definedValueType", "collection", "map", "union", "reference"] },
                "name": { "type": "string" },
                "valueTypeId": { "type": "string" },
                "allowedEntityTypeIds": { "type": "array", "minItems": 1, "items": { "type": "string", "minLength": 1 } }
              },
              "allOf": [{ "if": { "properties": { "kind": { "const": "reference" } } }, "then": { "required": ["allowedEntityTypeIds"] } }]
            },
            "relationshipEnd": {
              "type": "object", "required": ["entityTypeId"],
              "properties": {
                "entityTypeId": { "type": "string", "minLength": 1 },
                "minimumPerOppositeInstance": { "type": "integer", "minimum": 0 },
                "maximumPerOppositeInstance": { "type": ["integer", "null"], "minimum": 0 }
              }
            },
            "relationship": {
              "type": "object", "required": ["metadata", "from", "to"],
              "properties": {
                "metadata": { "$ref": "#/$defs/metadata" },
                "from": { "$ref": "#/$defs/relationshipEnd" },
                "to": { "$ref": "#/$defs/relationshipEnd" },
                "attributes": { "type": "array", "items": { "$ref": "#/$defs/property" } }
              }
            },
            "property": {
              "type": "object", "additionalProperties": false,
              "required": ["metadata", "valueType", "required", "nullable"],
              "properties": {
                "metadata": { "$ref": "#/$defs/metadata" },
                "name": { "type": "string" },
                "valueType": { "$ref": "#/$defs/valueReference" },
                "required": { "type": "boolean" },
                "nullable": { "type": "boolean" },
                "defaultValue": {},
                "constraints": { "type": "object" }
              }
            },
            "entityType": {
              "type": "object", "additionalProperties": false,
              "required": ["metadata", "properties"],
              "properties": {
                "metadata": { "$ref": "#/$defs/metadata" },
                "properties": { "type": "array", "maxItems": 12, "items": { "$ref": "#/$defs/property" } }
              }
            },
            "valueType": {
              "type": "object", "additionalProperties": true,
              "required": ["metadata", "kind"],
              "properties": {
                "metadata": { "$ref": "#/$defs/metadata" },
                "kind": { "type": "string", "enum": ["enumeration", "record", "alias"] },
                "entries": { "type": "array", "items": { "$ref": "#/$defs/metadata" } },
                "properties": { "type": "array", "items": { "$ref": "#/$defs/property" } },
                "targetType": { "$ref": "#/$defs/valueReference" }
              }
            }
          }
        }
        """)!.AsObject();
        var allowed = allowedWorkingRuleIds.OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => (JsonNode)JsonValue.Create(id)!).ToArray();
        schema["properties"]!["proposal"]!["properties"]!["definitionSources"]!["items"]!["properties"]!["workingRuleIds"]!["items"]!["enum"] = new JsonArray(allowed);
        if (singleContentType)
        {
            var entityTypes = schema["properties"]!["proposal"]!["properties"]!["schema"]!["properties"]!["entityTypes"]!;
            entityTypes["minItems"] = 1;
            entityTypes["maxItems"] = 1;
        }
        return schema;
    }
}
