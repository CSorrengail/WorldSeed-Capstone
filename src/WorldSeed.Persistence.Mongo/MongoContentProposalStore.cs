using MongoDB.Driver;
using WorldSeed.Content;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoContentProposalStore : IContentProposalStore
{
    private readonly IMongoCollection<ContentProposal> _proposals;
    private readonly IMongoCollection<ContentProposalDecision> _decisions;

    public MongoContentProposalStore(IMongoDatabase database)
    {
        _proposals = database.GetCollection<ContentProposal>("contentProposals");
        _decisions = database.GetCollection<ContentProposalDecision>("contentProposalDecisions");
    }
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _proposals.Indexes.CreateOneAsync(new CreateIndexModel<ContentProposal>(Builders<ContentProposal>.IndexKeys.Ascending(x => x.ProjectId).Ascending(x => x.CreatedAt), new CreateIndexOptions { Name = "content-proposals-by-project" }), cancellationToken: cancellationToken);
        await _decisions.Indexes.CreateOneAsync(new CreateIndexModel<ContentProposalDecision>(Builders<ContentProposalDecision>.IndexKeys.Ascending(x => x.ProjectId).Ascending(x => x.CreatedAt), new CreateIndexOptions { Name = "content-decisions-by-project" }), cancellationToken: cancellationToken);
    }
    public Task SaveProposalAsync(ContentProposal proposal, CancellationToken cancellationToken = default) => _proposals.ReplaceOneAsync(x => x.Id == proposal.Id, proposal, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    public async Task<ContentProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default) =>
        await _proposals.Find(x => x.ProjectId == projectId && x.Id == proposalId).FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<ContentProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default) => await _proposals.Find(x => x.ProjectId == projectId).SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    public Task SaveDecisionAsync(ContentProposalDecision decision, CancellationToken cancellationToken = default) => _decisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<ContentProposalDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default) => await _decisions.Find(x => x.ProjectId == projectId).SortBy(x => x.CreatedAt).ToListAsync(cancellationToken);
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) { await _proposals.DeleteManyAsync(x => x.ProjectId == projectId, cancellationToken); await _decisions.DeleteManyAsync(x => x.ProjectId == projectId, cancellationToken); }
}
