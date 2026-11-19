using System.Text.Json.Nodes;

namespace WorldSeed.RuleDrafting;

/// <summary>Native structured-output contract for local providers that support JSON Schema.</summary>
public static class RuleDraftResponseSchema
{
    public static JsonObject Create(IReadOnlySet<string> allowedSourceNoteIds, IReadOnlyList<SourcePassage>? sourcePassages = null, bool allowDesignOptions = false, bool requireDesignChoices = false)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceNoteIds);
        if (requireDesignChoices && !allowDesignOptions) throw new ArgumentException("Design choices require the trusted expansion workflow.");
        if (requireDesignChoices)
            return new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["required"] = new JsonArray("action", "designQuestions"),
                ["properties"] = new JsonObject
                {
                    ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("askClarifyingQuestion") },
                    ["designQuestions"] = DesignQuestionsSchema()
                }
            };
        var sourceIds = allowedSourceNoteIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var passageIds = (sourcePassages ?? []).Select(passage => passage.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var schema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["action"],
          "properties": {
            "action": { "type": "string", "enum": ["askClarifyingQuestion", "presentDraft"] },
            "clarifyingQuestions": { "type": "array", "minItems": 1, "maxItems": 5, "uniqueItems": true, "items": { "type": "string", "minLength": 1 } },
            "draft": {
              "type": "object",
              "additionalProperties": false,
              "required": ["title", "intent", "rules", "concepts", "assumptions", "openQuestions", "exclusions"],
              "properties": {
                "title": { "type": "string", "minLength": 1 },
                "intent": { "type": "string", "minLength": 1 },
                "rules": {
                  "type": "array", "minItems": 1,
                  "items": {
                    "type": "object", "additionalProperties": false,
                    "required": ["id", "name", "kind", "text", "sourceNoteIds", "sourceSupport"],
                    "properties": {
                      "id": { "type": "string", "minLength": 1 },
                      "name": { "type": "string", "minLength": 1 },
                      "kind": { "type": "string", "enum": ["definition", "rule", "procedure", "constraint"] },
                      "text": { "type": "string", "minLength": 1 },
                      "sourceNoteIds": { "type": "array", "minItems": 1, "items": { "type": "string", "enum": [] } },
                      "sourceSupport": { "type": "array", "minItems": 1, "items": { "type": "object", "additionalProperties": false, "required": ["sourceNoteId", "passageIds"], "properties": { "sourceNoteId": { "type": "string", "enum": [] }, "passageIds": { "type": "array", "minItems": 1, "items": { "type": "string", "enum": [] } } } } }
                    }
                  }
                },
                "concepts": {
                  "type": "array",
                  "items": {
                    "type": "object", "additionalProperties": false,
                    "required": ["id", "name", "description"],
                    "properties": {
                      "id": { "type": "string", "minLength": 1 },
                      "name": { "type": "string", "minLength": 1 },
                      "description": { "type": "string", "minLength": 1 }
                    }
                  }
                },
                "assumptions": { "type": "array", "items": { "type": "string", "minLength": 1 } },
                "openQuestions": { "type": "array", "items": { "type": "string", "minLength": 1 } },
                "exclusions": { "type": "array", "items": { "type": "string", "minLength": 1 } }
              }
            }
          },
          "allOf": [
            { "if": { "properties": { "action": { "const": "askClarifyingQuestion" } } }, "then": { "required": ["clarifyingQuestions"] } },
            { "if": { "properties": { "action": { "const": "presentDraft" } } }, "then": { "required": ["draft"] } }
          ]
        }
        """)!.AsObject();
        var sourceIdEnum = schema["properties"]!["draft"]!["properties"]!["rules"]!["items"]!["properties"]!["sourceNoteIds"]!["items"]!.AsObject();
        sourceIdEnum["enum"] = new JsonArray(sourceIds.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        var supportSourceIdEnum = schema["properties"]!["draft"]!["properties"]!["rules"]!["items"]!["properties"]!["sourceSupport"]!["items"]!["properties"]!["sourceNoteId"]!.AsObject();
        supportSourceIdEnum["enum"] = new JsonArray(sourceIds.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        var passageIdEnum = schema["properties"]!["draft"]!["properties"]!["rules"]!["items"]!["properties"]!["sourceSupport"]!["items"]!["properties"]!["passageIds"]!["items"]!.AsObject();
        passageIdEnum["enum"] = new JsonArray(passageIds.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        if (allowDesignOptions)
        {
            schema["properties"]!["designQuestions"] = DesignQuestionsSchema();
            schema["allOf"]![0]!["then"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["required"] = new JsonArray("clarifyingQuestions") },
                    new JsonObject { ["required"] = new JsonArray("designQuestions") })
            };
        }
        return schema;
    }

    private static JsonObject DesignQuestionsSchema() => JsonNode.Parse("""
        {
          "type":"array", "minItems":1, "maxItems":2,
          "items":{
            "type":"object", "additionalProperties":false, "required":["question","options"],
            "properties":{
              "question":{"type":"string","minLength":1,"maxLength":240},
              "options":{
                "type":"array","minItems":2,"maxItems":2,
                "items":{
                  "type":"object","additionalProperties":false,"required":["mechanic","tradeoff"],
                  "properties":{
                    "mechanic":{"type":"string","minLength":1,"maxLength":360},
                    "tradeoff":{"type":"string","minLength":1,"maxLength":180}
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
}
