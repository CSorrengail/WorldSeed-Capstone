using System.Text.Json.Nodes;

namespace WorldSeed.RuleDrafting;

/// <summary>Native structured-output contract for local providers that support JSON Schema.</summary>
public static class RuleDraftResponseSchema
{
    public static JsonObject Create(IReadOnlySet<string> allowedSourceNoteIds)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceNoteIds);
        var sourceIds = allowedSourceNoteIds.OrderBy(id => id, StringComparer.Ordinal).Select(id => (JsonNode)id).ToArray();
        var schema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["action"],
          "properties": {
            "action": { "type": "string", "enum": ["askClarifyingQuestion", "presentDraft"] },
            "clarifyingQuestion": { "type": "string", "minLength": 1 },
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
                    "required": ["id", "name", "kind", "text", "sourceNoteIds"],
                    "properties": {
                      "id": { "type": "string", "minLength": 1 },
                      "name": { "type": "string", "minLength": 1 },
                      "kind": { "type": "string", "enum": ["definition", "rule", "procedure", "constraint"] },
                      "text": { "type": "string", "minLength": 1 },
                      "sourceNoteIds": { "type": "array", "minItems": 1, "items": { "type": "string", "enum": [] } }
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
            { "if": { "properties": { "action": { "const": "askClarifyingQuestion" } } }, "then": { "required": ["clarifyingQuestion"] } },
            { "if": { "properties": { "action": { "const": "presentDraft" } } }, "then": { "required": ["draft"] } }
          ]
        }
        """)!.AsObject();
        var sourceIdEnum = schema["properties"]!["draft"]!["properties"]!["rules"]!["items"]!["properties"]!["sourceNoteIds"]!["items"]!.AsObject();
        sourceIdEnum["enum"] = new JsonArray(sourceIds);
        return schema;
    }
}
