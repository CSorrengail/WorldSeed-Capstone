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

    [Theory]
    [InlineData("Armor sets have a protection value.", "A full set grants its listed protection.")]
    [InlineData("Promises connect two participants.", "A broken promise changes their relationship.")]
    public void Reports_uncited_details_even_when_another_passage_in_the_source_is_cited(string first, string second)
    {
        var input = CreateInput();
        var source = new DesignSourceNote("source-used", first + " " + second, Now);
        var rule = input.WorkingRules!.Rules[0] with { Evidence = [new("source-used", "source-used:p001", first)] };
        input = input with { Project = input.Project with { SourceNotes = [source] }, WorkingRules = input.WorkingRules with { Rules = [rule] } };

        var inventory = ProjectInventoryService.Create(input, null, Now);

        Assert.Empty(inventory.Sources.UnreferencedSourceIds);
        var gap = Assert.Single(inventory.Observations, item => item.Kind == InventoryObservationKind.UncitedSourcePassage);
        Assert.Equal("source-used:p002", gap.SubjectId);
        Assert.Contains(second, gap.Description);
        Assert.Contains("intentional exclusion", gap.Description);
    }

    [Fact]
    public void Fully_cited_roleplay_prose_requires_no_content_type_or_schema_definition()
    {
        var input = CreateInput();
        const string text = "The facilitator interprets a promise's consequence in the story.";
        var rule = input.WorkingRules!.Rules[0] with { AuthoritativeText = text, Evidence = [new("promise", null, text)], SchemaHints = [] };
        input = input with
        {
            Project = input.Project with { SourceNotes = [new("promise", text, Now)], Sessions = [] },
            WorkingRules = input.WorkingRules with { Rules = [rule], OpenQuestions = [] },
            ActiveSchema = input.ActiveSchema! with { SchemaJson = "{\"entityTypes\":[]}" },
            SchemaProposal = null, BalanceProfiles = []
        };

        var inventory = ProjectInventoryService.Create(input, null, Now);

        Assert.Empty(inventory.Schema.ContentTypes);
        Assert.DoesNotContain(inventory.Observations, item => item.Kind is InventoryObservationKind.UncitedSourcePassage
            or InventoryObservationKind.ReportedSchemaGap or InventoryObservationKind.MissingEvaluationCoverage);
    }

    [Fact]
    public void Reports_current_content_schema_gaps_but_ignores_rejected_and_stale_candidates()
    {
        var input = CreateInput();
        var candidate = JsonNode.Parse("{\"id\":\"sample\",\"entityTypeId\":\"weapon\",\"values\":{}}")!.AsObject();
        var proposal = ContentProposalLifecycleService.Create(input.Project.Id, "weapon", candidate,
            input.ActiveSchema!.Id, input.ActiveSchema.Revision, input.WorkingRules!.Id, input.WorkingRules.Revision, Now,
            schemaGaps: [new("relationship", "The requested relationship has no representation.", null)]);
        input = input with { ContentProposals = [proposal, proposal with { Id = "stale", ActiveGameSchemaRevision = 1 }] };
        var inventory = ProjectInventoryService.Create(input, null, Now);
        Assert.Single(inventory.Observations, item => item.Kind == InventoryObservationKind.ReportedSchemaGap);

        var rejected = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Rejected, null, Now);
        inventory = ProjectInventoryService.Create(input with { ContentDecisions = [rejected] }, null, Now);
        Assert.DoesNotContain(inventory.Observations, item => item.Kind == InventoryObservationKind.ReportedSchemaGap);
    }

    [Fact]
    public void Reports_unincorporated_draft_rules_using_session_scoped_ids()
    {
        var input = CreateInput();
        var source = new DesignSourceNote("promise", "Promises connect participants.", Now);
        var draft = new WorldSeed.RuleDrafting.RuleStatement("rule-1", "Promise", WorldSeed.RuleDrafting.RuleStatementKind.Rule,
            source.OriginalText, [source.Id], [new(source.Id, source.OriginalText)]);
        var turn = new WorldSeed.RuleDrafting.RuleDraftTurn(WorldSeed.RuleDrafting.RuleDraftAction.PresentDraft, null, [],
            new("Promises", "Describe relationships.", [draft], [], [], [], []));
        var session = new DesignSession("current", input.Project.Id, "local", Now, Now, [source], [], turn);
        var rule = input.WorkingRules!.Rules[0] with { SourceDraftRules = [new("old", "rule-1")] };
        input = input with { Project = input.Project with { SourceNotes = [source], Sessions = [session] }, WorkingRules = input.WorkingRules with { Rules = [rule] } };

        var inventory = ProjectInventoryService.Create(input, null, Now);
        Assert.Equal("current/rule-1", Assert.Single(inventory.Observations, item => item.Kind == InventoryObservationKind.UnincorporatedDraftRule).SubjectId);

        input = input with { WorkingRules = input.WorkingRules with { Rules = [rule with { SourceDraftRules = [new("current", "rule-1")] }] } };
        inventory = ProjectInventoryService.Create(input, null, Now);
        Assert.DoesNotContain(inventory.Observations, item => item.Kind == InventoryObservationKind.UnincorporatedDraftRule);
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
