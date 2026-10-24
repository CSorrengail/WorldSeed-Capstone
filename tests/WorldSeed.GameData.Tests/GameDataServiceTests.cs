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
