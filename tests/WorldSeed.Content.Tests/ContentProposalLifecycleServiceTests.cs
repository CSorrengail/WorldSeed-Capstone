using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;

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

    [Fact]
    public void Exports_only_the_matching_approved_version_with_required_provenance()
    {
        var proposal = ContentProposalLifecycleService.Create("project", "creature", Candidate("original"), "active", 1, "rules", 1, Now, schemaGaps: [new ContentSchemaGap("reach", "No reach property exists.", "Add an optional reach property.")]);
        var approved = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, null, Now);
        var schema = new ActiveGameSchema("active", "project", "0.1", "Test", """{"id":"test-schema","version":"1"}""", "schema", "rules", 1, 1, Now, Now);

        var export = JsonNode.Parse(ContentExportService.ExportApproved(proposal, approved, schema))!.AsObject();

        Assert.Equal("original", export["content"]!["id"]!.GetValue<string>());
        Assert.Equal("test-schema", export["gameSchemaId"]!.GetValue<string>());
        Assert.Equal("rules", export["workingRuleSetId"]!.GetValue<string>());
        Assert.Equal(proposal.Version, export["proposalVersion"]!.GetValue<int>());
        Assert.True(export["schemaGapAnalysis"]!["reported"]!.GetValue<bool>());
        Assert.Equal("reach", export["schemaGapAnalysis"]!["gaps"]![0]!["requestedConcept"]!.GetValue<string>());
        Assert.Equal("Approved", export["designerDecision"]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void Refuses_unapproved_or_mismatched_exports()
    {
        var proposal = Create();
        var rejected = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Rejected, null, Now);
        var schema = new ActiveGameSchema("active", "project", "0.1", "Test", "{}", "schema", "rules", 1, 1, Now, Now);
        Assert.Throws<ArgumentException>(() => ContentExportService.ExportApproved(proposal, rejected, schema));
    }

    private static ContentProposal Create() => ContentProposalLifecycleService.Create("project", "creature", Candidate("original"), "active", 1, "rules", 1, Now);
    private static JsonObject Candidate(string id) => new() { ["id"] = id, ["entityTypeId"] = "creature", ["values"] = new JsonObject { ["name"] = id } };
}
