using System.Text.Json.Nodes;
using WorldSeed.SchemaValidation;

namespace WorldSeed.SchemaValidation.Tests;

public class GameSchemaValidatorTests
{
    [Fact]
    public void Validates_the_v0_5_5e_subset()
    {
        var path = FindFile("schemas", "examples", "dnd5e-2014-srd-subset.v0.5.schema.example.json");
        var result = new GameSchemaValidator().Validate(JsonNode.Parse(File.ReadAllText(path)));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Theory]
    [InlineData("carta-srd-patterns.v0.5.schema.example.json")]
    [InlineData("wretched-and-alone-srd-patterns.v0.5.schema.example.json")]
    public void Validates_non_tactical_srd_stress_test_schemas(string fileName)
    {
        var path = FindFile("schemas", "examples", fileName);
        var result = new GameSchemaValidator().Validate(JsonNode.Parse(File.ReadAllText(path)));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Rejects_an_unknown_fragment_reference()
    {
        var schema = JsonNode.Parse("""{"id":"test","name":"Test","version":"1","entityTypes":[{"metadata":{"id":"thing","name":"Thing"},"fragmentIds":["missing"]}]}""");
        var result = new GameSchemaValidator().Validate(schema);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Unknown schema fragment"));
    }

    [Fact]
    public void Rejects_a_property_supplied_by_a_fragment_and_the_owner()
    {
        var schema = JsonNode.Parse("""{"id":"test","name":"Test","version":"1","schemaFragments":[{"metadata":{"id":"shared","name":"Shared"},"properties":[{"metadata":{"id":"shared.name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"entityTypes":[{"metadata":{"id":"thing","name":"Thing"},"fragmentIds":["shared"],"properties":[{"metadata":{"id":"shared.name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}]}""");
        var result = new GameSchemaValidator().Validate(schema);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Duplicate property ID"));
    }

    [Theory]
    [InlineData("missing-entity-types.json", "'entityTypes' must be an array.")]
    [InlineData("duplicate-definition-id.json", "Duplicate definition ID")]
    [InlineData("unresolved-reference.json", "Referenced entity type 'missing'")]
    [InlineData("unknown-type-expression.json", "Unknown value type kind 'dice'")]
    [InlineData("unresolved-relationship-end.json", "Relationship ends must reference")]
    public void Rejects_invalid_schema_fixtures(string fileName, string expectedMessage)
    {
        var path = FindFile("schemas", "fixtures", "invalid-schema", fileName);
        var result = new GameSchemaValidator().Validate(JsonNode.Parse(File.ReadAllText(path)));
        Assert.Contains(result.Issues, issue => issue.Message.Contains(expectedMessage));
    }

    [Fact]
    public void Rejects_malformed_value_types_and_relationship_attributes()
    {
        var schema = JsonNode.Parse("""
        {"id":"test","name":"Test","version":"1","valueTypes":[{"metadata":{"id":"empty","name":"Empty"},"kind":"enumeration","entries":[]}],"entityTypes":[{"metadata":{"id":"node","name":"Node"},"properties":[{"metadata":{"id":"node.link","name":"Link"},"valueType":{"kind":"reference","allowedEntityTypeIds":[]},"required":true,"nullable":false}]}],"relationships":[{"metadata":{"id":"rel","name":"Rel"},"from":{"entityTypeId":"node","minimumPerOppositeInstance":2,"maximumPerOppositeInstance":1},"to":{"entityTypeId":"node","minimumPerOppositeInstance":0,"maximumPerOppositeInstance":"unbounded"},"attributes":[{"metadata":{"id":"rel.value","name":"Value"},"valueType":{"kind":"primitive","name":"dice"},"required":true,"nullable":false}]}]}
        """);
        var result = new GameSchemaValidator().Validate(schema);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Enumeration value types require at least one entry"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Reference value types require allowed entity types"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("minimumPerOppositeInstance cannot exceed maximumPerOppositeInstance"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Primitive value types require a supported name"));
    }

    [Fact]
    public void Validates_game_data_against_the_5e_subset()
    {
        var schema = ReadJson("schemas", "examples", "dnd5e-2014-srd-subset.v0.5.schema.example.json");
        var data = ReadJson("schemas", "examples", "dnd5e-2014-srd-subset.v0.5.game-data.example.json");
        var result = new GameDataValidator().Validate(schema, data);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Rejects_game_data_that_breaks_a_numeric_constraint()
    {
        var schema = ReadJson("schemas", "examples", "dnd5e-2014-srd-subset.v0.5.schema.example.json");
        var data = ReadJson("schemas", "examples", "dnd5e-2014-srd-subset.v0.5.game-data.example.json");
        var spell = data!.AsObject()["entityInstances"]!.AsArray().OfType<JsonObject>().Single(instance => instance["id"]!.GetValue<string>() == "sample-spell");
        spell["values"]!["spell.level"] = 10;
        var result = new GameDataValidator().Validate(schema, data);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("above its maximum"));
    }

    [Fact]
    public void Validates_wretched_and_alone_data_against_its_stress_test_schema()
    {
        var schema = ReadJson("schemas", "examples", "wretched-and-alone-srd-patterns.v0.5.schema.example.json");
        var data = ReadJson("schemas", "examples", "wretched-and-alone-srd-patterns.v0.5.game-data.example.json");
        var result = new GameDataValidator().Validate(schema, data);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Validates_advanced_value_shapes_relationship_attributes_and_forward_references()
    {
        var schema = JsonNode.Parse("""
        {"id":"test","name":"Test","version":"1","valueTypes":[{"metadata":{"id":"mood","name":"Mood"},"kind":"enumeration","entries":[{"id":"calm","name":"Calm"},{"id":"angry","name":"Angry"}]}],"entityTypes":[{"metadata":{"id":"node","name":"Node"},"properties":[{"metadata":{"id":"node.mood","name":"Mood"},"valueType":{"kind":"definedValueType","valueTypeId":"mood"},"required":true,"nullable":false},{"metadata":{"id":"node.labels","name":"Labels"},"valueType":{"kind":"collection","itemType":{"kind":"primitive","name":"string"},"minimumItems":2,"maximumItems":3},"required":true,"nullable":false,"constraints":{"distinctItems":true}},{"metadata":{"id":"node.scores","name":"Scores"},"valueType":{"kind":"map","keyType":{"kind":"primitive","name":"string"},"valueType":{"kind":"primitive","name":"integer"}},"required":true,"nullable":false},{"metadata":{"id":"node.note","name":"Note"},"valueType":{"kind":"union","options":[{"kind":"primitive","name":"string"},{"kind":"primitive","name":"integer"}]},"required":true,"nullable":false},{"metadata":{"id":"node.next","name":"Next"},"valueType":{"kind":"reference","allowedEntityTypeIds":["node"]},"required":true,"nullable":false}]}],"relationships":[{"metadata":{"id":"node-links-node","name":"Node Links Node"},"from":{"roleName":"source","entityTypeId":"node","minimumPerOppositeInstance":0,"maximumPerOppositeInstance":"unbounded"},"to":{"roleName":"target","entityTypeId":"node","minimumPerOppositeInstance":0,"maximumPerOppositeInstance":1},"directed":true,"attributes":[{"metadata":{"id":"node-links-node.weight","name":"Weight"},"valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false,"constraints":{"minimum":1}}]}]}
        """);
        var data = JsonNode.Parse("""
        {"id":"data","gameSchemaId":"test","gameSchemaVersion":"1","entityInstances":[{"id":"first","entityTypeId":"node","values":{"node.mood":"calm","node.labels":["start","linked"],"node.scores":{"success":2},"node.note":7,"node.next":"second"}},{"id":"second","entityTypeId":"node","values":{"node.mood":"angry","node.labels":["end","linked"],"node.scores":{"failure":0},"node.note":"done","node.next":"first"}}],"relationshipInstances":[{"id":"first-to-second","relationshipDefinitionId":"node-links-node","fromEntityInstanceId":"first","toEntityInstanceId":"second","attributeValues":{"node-links-node.weight":1}}]}
        """);
        var result = new GameDataValidator().Validate(schema, data);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Rejects_invalid_advanced_game_data_values()
    {
        var schema = JsonNode.Parse("""
        {"id":"test","name":"Test","version":"1","valueTypes":[{"metadata":{"id":"mood","name":"Mood"},"kind":"enumeration","entries":[{"id":"calm","name":"Calm"}]}],"entityTypes":[{"metadata":{"id":"node","name":"Node"},"properties":[{"metadata":{"id":"node.mood","name":"Mood"},"valueType":{"kind":"definedValueType","valueTypeId":"mood"},"required":true,"nullable":false},{"metadata":{"id":"node.labels","name":"Labels"},"valueType":{"kind":"collection","itemType":{"kind":"primitive","name":"string"},"minimumItems":2},"required":true,"nullable":false,"constraints":{"distinctItems":true}},{"metadata":{"id":"node.score","name":"Score"},"valueType":{"kind":"map","keyType":{"kind":"primitive","name":"string"},"valueType":{"kind":"primitive","name":"integer"}},"required":true,"nullable":false},{"metadata":{"id":"node.note","name":"Note"},"valueType":{"kind":"union","options":[{"kind":"primitive","name":"string"},{"kind":"primitive","name":"integer"}]},"required":true,"nullable":false}]}]}
        """);
        var data = JsonNode.Parse("""
        {"id":"data","gameSchemaId":"test","gameSchemaVersion":"1","entityInstances":[{"id":"one","entityTypeId":"node","values":{"node.mood":"wrong","node.labels":["repeat","repeat"],"node.score":{"bad":"not-a-number"},"node.note":true}}]}
        """);
        var result = new GameDataValidator().Validate(schema, data);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Expected an entry from enumeration"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Collection items must be distinct"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Expected integer value"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Value does not match any union option"));
    }

    [Fact]
    public void Accepts_omitted_defaults_and_relationships_without_attributes()
    {
        var schema = JsonNode.Parse("""
        {"id":"test","name":"Test","version":"1","entityTypes":[{"metadata":{"id":"node","name":"Node"},"properties":[{"metadata":{"id":"node.label","name":"Label"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false,"defaultValue":"unnamed"}]}],"relationships":[{"metadata":{"id":"node-links-node","name":"Node Links Node"},"from":{"roleName":"source","entityTypeId":"node","minimumPerOppositeInstance":0,"maximumPerOppositeInstance":"unbounded"},"to":{"roleName":"target","entityTypeId":"node","minimumPerOppositeInstance":0,"maximumPerOppositeInstance":"unbounded"},"directed":true}]}
        """);
        var data = JsonNode.Parse("""
        {"id":"data","gameSchemaId":"test","gameSchemaVersion":"1","entityInstances":[{"id":"one","entityTypeId":"node","values":{}},{"id":"two","entityTypeId":"node","values":{}}],"relationshipInstances":[{"id":"one-to-two","relationshipDefinitionId":"node-links-node","fromEntityInstanceId":"one","toEntityInstanceId":"two"}]}
        """);
        var result = new GameDataValidator().Validate(schema, data);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    private static JsonNode? ReadJson(params string[] parts) => JsonNode.Parse(File.ReadAllText(FindFile(parts)));

    private static string FindFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository fixture was not found.");
    }
}
