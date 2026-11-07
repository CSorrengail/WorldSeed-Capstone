using System.Text.Json.Nodes;
using WorldSeed.Content;

namespace WorldSeed.Content.Tests;

public class ContentProposalLifecycleServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T00:00:00Z");

    [Fact]
    public void Creates_traceable_proposal_and_revision_linkage()
    {
        var original = Create();
        var replacement = ContentProposalLifecycleService.Create("project", "creature", Candidate("replacement"), "active", 1, "rules", 2, Now, original);
        var decision = ContentProposalLifecycleService.Decide(original, ContentProposalDecisionKind.RevisionRequested, "Make it quieter.", Now, replacement);

        Assert.Equal(1, original.Version);
        Assert.Equal(2, replacement.Version);
        Assert.Equal(original.Id, replacement.ReplacesProposalId);
        Assert.Equal(replacement.Id, decision.ReplacementProposalId);
        Assert.Equal(original.Version, decision.ProposalVersion);
    }

    [Fact]
    public void Requires_guidance_for_a_revision_request()
    {
        Assert.Throws<ArgumentException>(() => ContentProposalLifecycleService.Decide(Create(), ContentProposalDecisionKind.RevisionRequested, " ", Now));
    }

    [Fact]
    public void Refuses_candidate_for_the_wrong_entity_type()
    {
        Assert.Throws<ArgumentException>(() => ContentProposalLifecycleService.Create("project", "creature", new JsonObject { ["entityTypeId"] = "item", ["values"] = new JsonObject() }, "active", 1, "rules", 1, Now));
    }

    private static ContentProposal Create() => ContentProposalLifecycleService.Create("project", "creature", Candidate("original"), "active", 1, "rules", 1, Now);
    private static JsonObject Candidate(string id) => new() { ["id"] = id, ["entityTypeId"] = "creature", ["values"] = new JsonObject { ["name"] = id } };
}
