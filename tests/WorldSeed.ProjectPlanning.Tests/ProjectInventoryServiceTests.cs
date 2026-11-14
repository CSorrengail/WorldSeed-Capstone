using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.ProjectPlanning;
using WorldSeed.Publication;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.ProjectPlanning.Tests;

public sealed class ProjectInventoryServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-13T12:00:00Z");

    [Fact]
    public void Creates_a_revision_bound_factual_inventory()
    {
        var input = CreateInput();

        var inventory = ProjectInventoryService.Create(input, null, Now);

        Assert.Equal(1, inventory.Revision);
        Assert.Equal(2, inventory.Sources.SourceCount);
        Assert.Equal(new[] { "source-unused" }, inventory.Sources.UnreferencedSourceIds);
        Assert.Equal(2, inventory.Rules.RuleCount);
        Assert.Equal(1, inventory.Rules.CountsByKind["Definition"]);
        var weapon = Assert.Single(inventory.Schema.ContentTypes);
        Assert.Equal(1, weapon.CanonicalEntryCount);
        Assert.True(weapon.HasCurrentBalanceProfile);
        Assert.Equal("active", inventory.Binding.ActiveSchemaId);
        Assert.Contains(inventory.Observations, item => item.Kind == InventoryObservationKind.UnresolvedQuestion);
        Assert.DoesNotContain(inventory.Observations, item => item.Kind == InventoryObservationKind.EmptyContentType);
    }

    [Fact]
    public void Records_missing_foundations_without_inventing_a_game_structure()
    {
        var project = DesignProjectService.Create("empty-project", "Empty", timeProvider: new FixedTimeProvider(Now));
        var input = new ProjectInventoryInput(project, null, null, null, null, [], [], [], [], []);

        var inventory = ProjectInventoryService.Create(input, null, Now);

        Assert.Empty(inventory.Schema.ContentTypes);
        Assert.Equal(3, inventory.Observations.Count(item => item.Kind == InventoryObservationKind.MissingFoundation));
    }

    [Fact]
    public void Increments_inventory_revision_without_changing_prior_snapshot()
    {
        var input = CreateInput();
        var first = ProjectInventoryService.Create(input, null, Now);

        var second = ProjectInventoryService.Create(input, first, Now.AddMinutes(1));

        Assert.Equal(1, first.Revision);
        Assert.Equal(2, second.Revision);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Binding, second.Binding);
    }

    private static ProjectInventoryInput CreateInput()
    {
        var project = DesignProjectService.Create("project", "Project", timeProvider: new FixedTimeProvider(Now)) with
        {
            SourceNotes =
            [
                new("source-used", "Weapons have names.", Now),
                new("source-unused", "Unprocessed note.", Now)
            ]
        };
        var evidence = new WorkingRuleEvidence("source-used", null, "Weapons have names.");
        var ruleSet = new WorkingRuleSet("rules", project.Id, "0.1", "Rules", null, 3, Now, Now,
        [
            new("weapon", "Weapon", WorkingRuleKind.Definition, "A weapon is an item.", [evidence], [], [], [], null, [], []),
            new("weapon-name", "Weapon Name", WorkingRuleKind.Rule, "Each weapon has a name.", [evidence], [], [], [], null, [], [])
        ], [], ["How much damage does a weapon cause?"], [], []);
        var schemaJson = """{"id":"schema","name":"Schema","version":"1","entityTypes":[{"metadata":{"id":"weapon","name":"Weapon"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"validations":[]}""";
        var proposal = new ProjectSchemaProposal("proposal", project.Id, "0.1", "Schema", schemaJson, [], ruleSet.Id, ruleSet.Revision, [], Now, Now);
        var active = new ActiveGameSchema("active", project.Id, "0.1", "Schema", schemaJson, proposal.Id, ruleSet.Id, ruleSet.Revision, 2, Now, Now);
        var dataJson = """{"id":"data","gameSchemaId":"schema","gameSchemaVersion":"1","entityInstances":[{"id":"sword","entityTypeId":"weapon","values":{"name":"Sword"}}],"relationshipInstances":[]}""";
        var data = new ProjectGameData("data", project.Id, "0.1", active.Id, active.Revision, dataJson, Now, Now);
        var profile = new ProjectContentBalanceProfile("profile", project.Id, active.Id, active.Revision,
            new ContentBalanceProfile("weapon-balance", "weapon", [], []), 1, Now, Now);
        return new ProjectInventoryInput(project, ruleSet, proposal, active, data, [], [], [], [profile], []);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
