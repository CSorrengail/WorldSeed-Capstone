using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using WorldSeed.Content;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoContentProposalStore : IContentProposalStore
{
    private readonly IMongoCollection<ContentProposalDocument> _proposals;
    private readonly IMongoCollection<ContentProposalDecision> _decisions;

    public MongoContentProposalStore(IMongoDatabase database)
    {
        _proposals = database.GetCollection<ContentProposalDocument>("contentProposals");
        _decisions = database.GetCollection<ContentProposalDecision>("contentProposalDecisions");
    }
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _proposals.Indexes.CreateOneAsync(new CreateIndexModel<ContentProposalDocument>(Builders<ContentProposalDocument>.IndexKeys.Ascending(x => x.ProjectId).Ascending(x => x.CreatedAt), new CreateIndexOptions { Name = "content-proposals-by-project" }), cancellationToken: cancellationToken);
        await _decisions.Indexes.CreateOneAsync(new CreateIndexModel<ContentProposalDecision>(Builders<ContentProposalDecision>.IndexKeys.Ascending(x => x.ProjectId).Ascending(x => x.CreatedAt), new CreateIndexOptions { Name = "content-decisions-by-project" }), cancellationToken: cancellationToken);
    }
    public Task SaveProposalAsync(ContentProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var document = ContentProposalDocument.FromDomain(proposal);
        return _proposals.ReplaceOneAsync(x => x.Id == proposal.Id, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }
    public async Task<ContentProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default)
    {
        var document = await _proposals.Find(x => x.ProjectId == projectId && x.Id == proposalId).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }
    public async Task<IReadOnlyList<ContentProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var documents = await _proposals.Find(x => x.ProjectId == projectId).SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return documents.Select(document => document.ToDomain()).ToArray();
    }
    public Task SaveDecisionAsync(ContentProposalDecision decision, CancellationToken cancellationToken = default) => _decisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<ContentProposalDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default) => await _decisions.Find(x => x.ProjectId == projectId).SortBy(x => x.CreatedAt).ToListAsync(cancellationToken);
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) { await _proposals.DeleteManyAsync(x => x.ProjectId == projectId, cancellationToken); await _decisions.DeleteManyAsync(x => x.ProjectId == projectId, cancellationToken); }
}

/// <summary>
/// MongoDB cannot serialize <see cref="JsonObject"/> directly because JsonNode.Parent
/// forms a circular object graph. Persisting an ordinary BSON document preserves the
/// candidate's JSON shape without leaking a database-specific type into the domain.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed record ContentProposalDocument(
    string Id,
    string ProjectId,
    string EntityTypeId,
    BsonDocument Candidate,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    int Version,
    DateTimeOffset CreatedAt,
    string? ReplacesProposalId,
    ContentSchemaGap[]? SchemaGaps = null,
    string? DesignerRequest = null,
    ContentGenerationContext? GenerationContext = null,
    ContentSimilarityWarning[]? SimilarityWarnings = null)
{
    public static ContentProposalDocument FromDomain(ContentProposal proposal) => new(
        proposal.Id,
        proposal.ProjectId,
        proposal.EntityTypeId,
        BsonDocument.Parse(proposal.Candidate.ToJsonString()),
        proposal.ActiveGameSchemaId,
        proposal.ActiveGameSchemaRevision,
        proposal.WorkingRuleSetId,
        proposal.WorkingRuleSetRevision,
        proposal.Version,
        proposal.CreatedAt,
        proposal.ReplacesProposalId,
        proposal.SchemaGaps?.ToArray(),
        proposal.DesignerRequest,
        proposal.GenerationContext,
        proposal.SimilarityWarnings?.ToArray());

    public ContentProposal ToDomain()
    {
        var candidateJson = Candidate.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
        var candidate = JsonNode.Parse(candidateJson) as JsonObject
            ?? throw new InvalidDataException($"Stored content proposal '{Id}' does not contain a JSON object candidate.");

        return new ContentProposal(
            Id,
            ProjectId,
            EntityTypeId,
            candidate,
            ActiveGameSchemaId,
            ActiveGameSchemaRevision,
            WorkingRuleSetId,
            WorkingRuleSetRevision,
            Version,
            CreatedAt,
            ReplacesProposalId,
            SchemaGaps,
            DesignerRequest,
            GenerationContext,
            SimilarityWarnings);
    }
}
