using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;

namespace WorldSeed.Content.Tests;

public sealed class ContentExpansionRunServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_accepts_arbitrary_schema_types_and_ordered_dependencies()
    {
        var run = CreateRun();

        Assert.Equal(["factions", "creatures"], run.Stages.Select(stage => stage.Id));
        Assert.Equal(["factions"], run.Stages[1].DependsOnStageIds);
        Assert.DoesNotContain("monster", run.Stages.Select(stage => stage.EntityTypeId));
    }

    [Fact]
    public void Create_rejects_unknown_types_and_forward_dependencies()
    {
        var schema = Schema();
        Assert.Throws<ArgumentException>(() => ContentExpansionRunService.Create("project", "Run", schema, "rules", 2,
            [new("unknown", "spell", 1, "Create one.", [])], Now));
        Assert.Throws<ArgumentException>(() => ContentExpansionRunService.Create("project", "Run", schema, "rules", 2,
            [new("creatures", "creature", 1, "Create one.", ["factions"]), new("factions", "faction", 1, "Create one.", [])], Now));
    }

    [Fact]
    public void Downstream_stage_stays_blocked_until_every_dependency_entry_is_applied()
    {
        var run = CreateRun();
        var factionOne = Proposal("faction-one", "faction");
        var factionTwo = Proposal("faction-two", "faction");
        var links = new[]
        {
            ContentExpansionRunService.LinkProposal(run, "factions", factionOne, [], [factionOne], [], Now),
            ContentExpansionRunService.LinkProposal(run, "factions", factionTwo,
                [new("content-expansion-link-x", "project", run.Id, "factions", 1, factionOne.Id, 1, Now)], [factionOne, factionTwo], [], Now)
        };
        var firstApplication = Applied(factionOne);

        var partial = ContentExpansionRunService.Evaluate(run, links, [factionOne, factionTwo], [firstApplication]);
        Assert.Equal(ContentExpansionStageState.AwaitingDesignerReview, partial.Stages[0].State);
        Assert.Equal(ContentExpansionStageState.BlockedByDependencies, partial.Stages[1].State);
        Assert.Null(partial.NextStageId);

        var complete = ContentExpansionRunService.Evaluate(run, links, [factionOne, factionTwo], [firstApplication, Applied(factionTwo)]);
        Assert.Equal(ContentExpansionStageState.Complete, complete.Stages[0].State);
        Assert.Equal(ContentExpansionStageState.ReadyToGenerate, complete.Stages[1].State);
        Assert.Equal("creatures", complete.NextStageId);
    }

    [Fact]
    public void Linking_never_approves_applies_or_overfills_a_stage()
    {
        var run = CreateRun();
        var proposals = new[] { Proposal("one", "faction"), Proposal("two", "faction"), Proposal("three", "faction") };
        var first = ContentExpansionRunService.LinkProposal(run, "factions", proposals[0], [], proposals, [], Now);
        var second = ContentExpansionRunService.LinkProposal(run, "factions", proposals[1], [first], proposals, [], Now);

        Assert.Equal(1, first.Ordinal);
        Assert.Equal(2, second.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ContentExpansionRunService.LinkProposal(run, "factions", proposals[2], [first, second], proposals, [], Now));
        Assert.Throws<InvalidOperationException>(() => ContentExpansionRunService.LinkProposal(run, "factions", proposals[0], [first], proposals, [], Now));
    }

    private static ContentExpansionRun CreateRun() => ContentExpansionRunService.Create("project", "World Expansion", Schema(), "rules", 2,
        [
            new("factions", "faction", 2, "Create two rival factions.", []),
            new("creatures", "creature", 1, "Create a creature tied to an applied faction.", ["factions"])
        ], Now);

    private static ActiveGameSchema Schema() => new("active", "project", "0.1", "Schema", """
        {"id":"schema","name":"Schema","version":"1","entityTypes":[{"id":"faction","name":"Faction","properties":[]},{"id":"creature","name":"Creature","properties":[]}]}
        """, "proposal", "rules", 2, 3, Now, Now);

    private static ContentProposal Proposal(string id, string type) => ContentProposalLifecycleService.Create("project", type,
        new JsonObject { ["id"] = id, ["entityTypeId"] = type, ["values"] = new JsonObject() }, "active", 3, "rules", 2, Now);

    private static ContentApplication Applied(ContentProposal proposal) => new(
        "application-" + proposal.Id, "project", proposal.Id, proposal.Version, "approval-" + proposal.Id,
        proposal.Candidate["id"]!.GetValue<string>(), proposal.EntityTypeId, proposal.Candidate.ToJsonString(), "fingerprint",
        "active", 3, "game-data", ContentApplicationStatus.Applied, Now, Now, Now);
}
