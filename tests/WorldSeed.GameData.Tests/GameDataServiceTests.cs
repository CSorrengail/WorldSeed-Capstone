using System.Text.Json.Nodes;
using WorldSeed.GameData;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.GameData.Tests;

public class GameDataServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T00:00:00Z");

    [Fact]
    public void Activates_a_valid_schema_and_creates_valid_empty_game_data()
    {
        var active = GameDataService.Activate(CreateProposal(), null, false, Now);
        var data = GameDataService.CreateEmpty(active, Now);

        Assert.Equal("active-schema-project-sot", active.Id);
        Assert.Equal(1, active.Revision);
        Assert.True(GameDataService.Validate(active, data).IsValid);
        Assert.Empty(JsonNode.Parse(data.DataJson)!["entityInstances"]!.AsArray());
    }

    [Fact]
    public void Refuses_to_replace_an_active_schema_with_existing_game_data()
    {
        var active = GameDataService.Activate(CreateProposal(), null, false, Now);
        var replacement = CreateProposal() with { SchemaJson = Schema("Weapon Catalog") };

        var exception = Assert.Throws<GameDataFormatException>(() => GameDataService.Activate(replacement, active, true, Now));

        Assert.Contains("migration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Identifies_data_bound_to_a_different_active_schema_revision()
    {
        var active = GameDataService.Activate(CreateProposal(), null, false, Now);
        var data = GameDataService.CreateEmpty(active, Now) with { ActiveGameSchemaRevision = 99 };

        var validation = GameDataService.Validate(active, data);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Message.Contains("revision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Treats_an_automatically_created_empty_document_as_not_authored()
    {
        var active = GameDataService.Activate(CreateProposal(), null, false, Now);
        var empty = GameDataService.CreateEmpty(active, Now);

        Assert.False(GameDataService.HasAuthoredContent(empty));

        var root = JsonNode.Parse(empty.DataJson)!.AsObject();
        root["entityInstances"]!.AsArray().Add(new JsonObject { ["id"] = "weapon-1", ["entityTypeId"] = "weapon", ["values"] = new JsonObject { ["name"] = "Sword" } });
        Assert.True(GameDataService.HasAuthoredContent(empty with { DataJson = root.ToJsonString() }));
    }

    [Fact]
    public void Creates_a_valid_schema_aware_starter_entry_with_required_fields()
    {
        var schema = JsonNode.Parse("""
            {
              "id": "starter-schema",
              "name": "Starter Schema",
              "version": "1",
              "valueTypes": [
                {
                  "metadata": { "id": "role", "name": "Role" },
                  "kind": "enumeration",
                  "entries": [ { "id": "scout", "name": "Scout" }, { "id": "guard", "name": "Guard" } ]
                }
              ],
              "schemaFragments": [
                {
                  "metadata": { "id": "named", "name": "Named" },
                  "properties": [
                    { "metadata": { "id": "name", "name": "New Creature" }, "valueType": { "kind": "primitive", "name": "string" }, "required": true, "nullable": false, "constraints": { "minimumLength": 3 } }
                  ]
                }
              ],
              "entityTypes": [
                {
                  "metadata": { "id": "creature", "name": "Creature" },
                  "fragmentIds": ["named"],
                  "properties": [
                    { "metadata": { "id": "vitality", "name": "Vitality" }, "valueType": { "kind": "primitive", "name": "integer" }, "required": true, "nullable": false, "constraints": { "minimum": 1, "maximum": 30 } },
                    { "metadata": { "id": "role", "name": "Role" }, "valueType": { "kind": "definedValueType", "valueTypeId": "role" }, "required": true, "nullable": false },
                    { "metadata": { "id": "tags", "name": "Tags" }, "valueType": { "kind": "collection", "itemType": { "kind": "primitive", "name": "string" }, "minimumItems": 1 }, "required": true, "nullable": false },
                    { "metadata": { "id": "notes", "name": "Notes" }, "valueType": { "kind": "primitive", "name": "string" }, "required": false, "nullable": false }
                  ]
                }
              ]
            }
            """)!.AsObject();

        var entry = GameDataEntryTemplateService.Create(schema, "creature", "creature-1");
        var values = entry["values"]!.AsObject();
        var data = new JsonObject
        {
            ["id"] = "data",
            ["gameSchemaId"] = "starter-schema",
            ["gameSchemaVersion"] = "1",
            ["entityInstances"] = new JsonArray(entry),
            ["relationshipInstances"] = new JsonArray()
        };
        var active = new ActiveGameSchema("active", "project", "0.1", "Starter Schema", schema.ToJsonString(), "proposal", "rules", 1, 1, Now, Now);
        var projectData = new ProjectGameData("data", "project", "0.1", "active", 1, data.ToJsonString(), Now, Now);

        Assert.Equal("New Creature", values["name"]!.GetValue<string>());
        Assert.Equal(1, values["vitality"]!.GetValue<int>());
        Assert.Equal("scout", values["role"]!.GetValue<string>());
        Assert.Single(values["tags"]!.AsArray());
        Assert.False(values.ContainsKey("notes"));
        Assert.True(GameDataService.Validate(active, projectData).IsValid);
    }

    private static ProjectSchemaProposal CreateProposal() => new(
        "proposal-sot",
        "project-sot",
        "0.1",
        "Weapons",
        Schema("Weapons"),
        [],
        "working-rules-sot",
        1,
        [],
        Now,
        Now);

    private static string Schema(string name) => $$"""
        {
          "id": "sot-schema",
          "name": "{{name}}",
          "version": "0.1",
          "entityTypes": [
            {
              "metadata": { "id": "weapon", "name": "Weapon" },
              "properties": [
                {
                  "metadata": { "id": "name", "name": "Name" },
                  "valueType": { "kind": "primitive", "name": "string" },
                  "required": true,
                  "nullable": false
                }
              ]
            }
          ]
        }
        """;
}
