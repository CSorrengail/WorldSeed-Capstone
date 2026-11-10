using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;

namespace WorldSeed.Content.Tests;

public sealed class ContentApplicationServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");

    [Fact]
    public void Prepares_validated_canonical_entry_and_completes_auditable_event()
    {
        var (proposal, approval, schema, data) = Fixture();

        var prepared = ContentApplicationService.Prepare(proposal, approval, schema, data, Now);
        var applied = ContentApplicationService.Complete(prepared.Application, prepared.UpdatedGameData, Now.AddSeconds(1));

        Assert.Equal(ContentApplicationStatus.Pending, prepared.Application.Status);
        Assert.Equal(proposal.Id, prepared.Application.ProposalId);
        Assert.Equal(approval.Id, prepared.Application.ApprovalDecisionId);
        Assert.Equal("glass-fox", prepared.Application.EntityInstanceId);
        Assert.Equal(64, prepared.Application.CandidateFingerprint.Length);
        Assert.Contains("glass-fox", prepared.UpdatedGameData.DataJson);
        Assert.True(ContentApplicationService.CanonicalEntryMatches(prepared.Application, prepared.UpdatedGameData));
        Assert.Equal(ContentApplicationStatus.Applied, applied.Status);
        Assert.Equal(prepared.UpdatedGameData.UpdatedAt, applied.ResultingGameDataUpdatedAt);
    }

    [Fact]
    public void Rejects_unapproved_stale_invalid_or_duplicate_content()
    {
        var (proposal, approval, schema, data) = Fixture();
        var rejected = approval with { Kind = ContentProposalDecisionKind.Rejected };
        Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(proposal, rejected, schema, data, Now));
        Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(proposal with { ActiveGameSchemaRevision = 0 }, approval, schema, data, Now));

        var duplicateRoot = JsonNode.Parse(data.DataJson)!.AsObject();
        duplicateRoot["entityInstances"]!.AsArray().Add(proposal.Candidate.DeepClone());
        var duplicateData = data with { DataJson = duplicateRoot.ToJsonString() };
        Assert.Contains("will not overwrite", Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(proposal, approval, schema, duplicateData, Now)).Message);

        var invalid = proposal with { Candidate = JsonNode.Parse("""{"id":"glass-fox","entityTypeId":"creature","values":{}}""")!.AsObject() };
        Assert.Contains("mechanically valid", Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(invalid, approval, schema, data, Now)).Message);
    }

    private static (ContentProposal Proposal, ContentProposalDecision Approval, ActiveGameSchema Schema, ProjectGameData Data) Fixture()
    {
        var schemaJson = """{"id":"test-schema","name":"Test","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""";
        var schema = new ActiveGameSchema("active", "project", "0.1", "Test", schemaJson, "schema-proposal", "rules", 1, 1, Now, Now);
        var data = GameDataService.CreateEmpty(schema, Now);
        var candidate = JsonNode.Parse("""{"id":"glass-fox","entityTypeId":"creature","values":{"name":"Glass Fox"}}""")!.AsObject();
        var proposal = ContentProposalLifecycleService.Create("project", "creature", candidate, schema.Id, schema.Revision, "rules", 1, Now, designerRequest: "Create a glass fox.");
        var approval = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, null, Now);
        return (proposal, approval, schema, data);
    }
}
