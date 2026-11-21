using System.Text.Json.Nodes;

namespace WorldSeed.Content;

/// <summary>One bounded entry plus explicitly visible gaps and proposed links.</summary>
public static class ContentGenerationResponseSchema
{
    public static JsonObject Create(string entityTypeId, IReadOnlyList<string> relationshipIds)
    {
        var schema = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["candidate","schemaGaps","relationshipInstances"],"properties":{
          "candidate":{"type":"object","additionalProperties":false,"required":["id","entityTypeId","values"],"properties":{"id":{"type":"string","minLength":1},"entityTypeId":{"type":"string"},"values":{"type":"object"}}},
          "schemaGaps":{"type":"array","maxItems":12,"items":{"type":"object","required":["requestedConcept","reason"],"properties":{"requestedConcept":{"type":"string","minLength":1},"reason":{"type":"string","minLength":1},"suggestedSchemaChange":{"type":["string","null"]}}}},
          "relationshipInstances":{"type":"array","maxItems":6,"items":{"type":"object","additionalProperties":false,"required":["id","relationshipDefinitionId","fromEntityInstanceId","toEntityInstanceId","attributeValues"],"properties":{"id":{"type":"string","minLength":1},"relationshipDefinitionId":{"type":"string"},"fromEntityInstanceId":{"type":"string","minLength":1},"toEntityInstanceId":{"type":"string","minLength":1},"attributeValues":{"type":"object"}}}}
        }}
        """)!.AsObject();
        schema["properties"]!["candidate"]!["properties"]!["entityTypeId"]!["const"] = entityTypeId;
        if (relationshipIds.Count == 0) schema["properties"]!["relationshipInstances"]!["maxItems"] = 0;
        else schema["properties"]!["relationshipInstances"]!["items"]!["properties"]!["relationshipDefinitionId"]!["enum"] =
            new JsonArray(relationshipIds.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        return schema;
    }
}
