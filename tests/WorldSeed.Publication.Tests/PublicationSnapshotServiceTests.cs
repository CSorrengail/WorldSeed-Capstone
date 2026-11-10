using System.Text.Json.Nodes;
using WorldSeed.GameData;
using WorldSeed.Publication;
using WorldSeed.WorkingRules;

namespace WorldSeed.Publication.Tests;

public sealed class PublicationSnapshotServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T14:00:00Z");

    [Fact]
    public void Renders_generic_rules_categories_content_and_provenance_deterministically()
    {
        var (rules, schema, data) = Fixture();

        var snapshot = PublicationSnapshotService.Create("Test Game", rules, schema, data, Now);

        Assert.Contains("# Test Game", snapshot.Markdown);
        Assert.Contains("## Rules", snapshot.Markdown);
        Assert.Contains("### Creature Rule", snapshot.Markdown);
        Assert.Contains("## Categories And Value Types", snapshot.Markdown);
        Assert.Contains("Scout", snapshot.Markdown);
        Assert.Contains("## Game Content", snapshot.Markdown);
        Assert.Contains("#### Glass Fox", snapshot.Markdown);
        Assert.Contains("**Role:** scout", snapshot.Markdown);
        Assert.DoesNotContain("proposal", snapshot.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["glass-fox"], snapshot.IncludedEntityIds);
        Assert.Equal(64, snapshot.GameDataFingerprint.Length);
        var manifest = JsonNode.Parse(snapshot.ManifestJson)!.AsObject();
        Assert.Equal(rules.Revision, manifest["workingRuleSetRevision"]!.GetValue<int>());
        Assert.Equal(schema.Revision, manifest["activeGameSchemaRevision"]!.GetValue<int>());
        Assert.Equal("glass-fox", manifest["includedEntityIds"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Refuses_mismatched_rule_revision_or_invalid_game_data()
    {
        var (rules, schema, data) = Fixture();
        Assert.Contains("exact Working Rule Set", Assert.Throws<InvalidOperationException>(() => PublicationSnapshotService.Create("Test", rules with { Revision = 2 }, schema, data, Now)).Message);
        var invalidRoot = JsonNode.Parse(data.DataJson)!.AsObject();
        invalidRoot["entityInstances"]![0]!["values"]!.AsObject().Remove("role");
        Assert.Contains("valid canonical Game Data", Assert.Throws<InvalidOperationException>(() => PublicationSnapshotService.Create("Test", rules, schema, data with { DataJson = invalidRoot.ToJsonString() }, Now)).Message);
    }

    private static (WorkingRuleSet Rules, ActiveGameSchema Schema, ProjectGameData Data) Fixture()
    {
        var rule = new WorkingRule("rule-1", "Creature Rule", WorkingRuleKind.Rule, "Creatures have a role.", [], [], [], [], null, [], []);
        var rules = new WorkingRuleSet("rules-1", "project", "0.1", "Rules", null, 1, Now, Now, [rule], [], [], [], []);
        var schemaJson = """{"id":"schema","name":"Schema","version":"1","valueTypes":[{"metadata":{"id":"roleType","name":"Roles"},"kind":"enumeration","entries":[{"id":"scout","name":"Scout"}]}],"entityTypes":[{"metadata":{"id":"creature","name":"Creatures"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false},{"metadata":{"id":"role","name":"Role"},"valueType":{"kind":"definedValueType","valueTypeId":"roleType"},"required":true,"nullable":false}]}]}""";
        var schema = new ActiveGameSchema("active-1", "project", "0.1", "Schema", schemaJson, "proposal", rules.Id, rules.Revision, 1, Now, Now);
        var root = JsonNode.Parse(GameDataService.CreateEmpty(schema, Now).DataJson)!.AsObject();
        root["entityInstances"]!.AsArray().Add(JsonNode.Parse("""{"id":"glass-fox","entityTypeId":"creature","values":{"name":"Glass Fox","role":"scout"}}""")!.AsObject());
        var data = GameDataService.CreateEmpty(schema, Now) with { DataJson = root.ToJsonString() };
        return (rules, schema, data);
    }
}
