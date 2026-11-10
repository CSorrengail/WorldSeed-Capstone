using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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
            createdAt,
            SchemaGaps: [new ContentSchemaGap("reach", "No reach property exists.", "Add reach to the schema.")],
            DesignerRequest: "Create an ember wolf with reach.",
            GenerationContext: new ContentGenerationContext(["seed-1"], ["decision-1"]),
            SimilarityWarnings: [new ContentSimilarityWarning("seed-1", "Similar values.")]);

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
        Assert.Equal("reach", Assert.Single(restored.SchemaGaps!).RequestedConcept);
        Assert.Equal(proposal.DesignerRequest, restored.DesignerRequest);
        Assert.Equal("seed-1", Assert.Single(restored.GenerationContext!.CanonicalEntityIds));
        Assert.Equal("decision-1", Assert.Single(restored.GenerationContext.DesignerDecisionIds));
        Assert.Equal("seed-1", Assert.Single(restored.SimilarityWarnings!).CanonicalEntityId);
    }

    [Fact]
    public void Legacy_document_without_schema_gaps_remains_loadable()
    {
        var legacy = new ContentProposalDocument("proposal-legacy", "project-1", "monster", BsonDocument.Parse("{ id: 'legacy', entityTypeId: 'monster', values: {} }"), "schema-1", 1, "rules-1", 1, 1, DateTimeOffset.UtcNow, null);
        var bson = legacy.ToBsonDocument();
        bson.Remove(nameof(ContentProposalDocument.SchemaGaps));
        bson.Remove(nameof(ContentProposalDocument.DesignerRequest));
        bson.Remove(nameof(ContentProposalDocument.GenerationContext));
        bson.Remove(nameof(ContentProposalDocument.SimilarityWarnings));

        var restored = BsonSerializer.Deserialize<ContentProposalDocument>(bson).ToDomain();

        Assert.Null(restored.SchemaGaps);
        Assert.Null(restored.DesignerRequest);
        Assert.Null(restored.GenerationContext);
        Assert.Null(restored.SimilarityWarnings);
    }
}
