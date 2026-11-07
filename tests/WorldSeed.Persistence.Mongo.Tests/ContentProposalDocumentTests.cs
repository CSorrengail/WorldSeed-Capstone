using System.Text.Json.Nodes;
using MongoDB.Bson;
using WorldSeed.Content;
using WorldSeed.Persistence.Mongo;

namespace WorldSeed.Persistence.Mongo.Tests;

public sealed class ContentProposalDocumentTests
{
    [Fact]
    public void Candidate_RoundTripsThroughBsonSafeDocument()
    {
        var createdAt = new DateTimeOffset(2026, 11, 7, 16, 30, 0, TimeSpan.Zero);
        var candidate = JsonNode.Parse("""
            {
              "id": "ember-wolf",
              "name": "Ember Wolf",
              "stats": { "health": 18, "armor": 2 },
              "traits": ["pack hunter", "fire resistant"]
            }
            """)!.AsObject();
        var proposal = new ContentProposal(
            "proposal-1",
            "project-1",
            "monster",
            candidate,
            "schema-1",
            3,
            "rules-1",
            7,
            1,
            createdAt);

        var document = ContentProposalDocument.FromDomain(proposal);

        _ = document.ToBson();
        var restored = document.ToDomain();

        Assert.Equal(proposal.Id, restored.Id);
        Assert.Equal(proposal.ProjectId, restored.ProjectId);
        Assert.Equal(proposal.EntityTypeId, restored.EntityTypeId);
        Assert.Equal(proposal.ActiveGameSchemaRevision, restored.ActiveGameSchemaRevision);
        Assert.Equal(proposal.WorkingRuleSetRevision, restored.WorkingRuleSetRevision);
        Assert.Equal(proposal.CreatedAt, restored.CreatedAt);
        Assert.True(JsonNode.DeepEquals(proposal.Candidate, restored.Candidate));
    }
}
