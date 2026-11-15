using MongoDB.Driver;
using WorldSeed.Content;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoContentExpansionRunStore : IContentExpansionRunStore
{
    private readonly IMongoCollection<ContentExpansionRun> _runs;
    private readonly IMongoCollection<ContentExpansionProposalLink> _links;

    public MongoContentExpansionRunStore(IMongoDatabase database)
    {
        _runs = database.GetCollection<ContentExpansionRun>("contentExpansionRuns");
        _links = database.GetCollection<ContentExpansionProposalLink>("contentExpansionProposalLinks");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _runs.Indexes.CreateOneAsync(new CreateIndexModel<ContentExpansionRun>(
            Builders<ContentExpansionRun>.IndexKeys.Ascending(item => item.ProjectId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "content-expansion-runs-by-project" }), cancellationToken: cancellationToken);
        await _links.Indexes.CreateOneAsync(new CreateIndexModel<ContentExpansionProposalLink>(
            Builders<ContentExpansionProposalLink>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.RunId).Ascending(item => item.StageId).Ascending(item => item.Ordinal),
            new CreateIndexOptions { Name = "content-expansion-links-by-slot", Unique = true }), cancellationToken: cancellationToken);
        await _links.Indexes.CreateOneAsync(new CreateIndexModel<ContentExpansionProposalLink>(
            Builders<ContentExpansionProposalLink>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.RunId).Ascending(item => item.ProposalId).Ascending(item => item.ProposalVersion),
            new CreateIndexOptions { Name = "content-expansion-links-by-proposal", Unique = true }), cancellationToken: cancellationToken);
    }

    public Task SaveRunAsync(ContentExpansionRun run, CancellationToken cancellationToken = default) =>
        _runs.ReplaceOneAsync(item => item.ProjectId == run.ProjectId && item.Id == run.Id, run, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public Task<ContentExpansionRun?> LoadRunAsync(string projectId, string runId, CancellationToken cancellationToken = default) =>
        _runs.Find(item => item.ProjectId == projectId && item.Id == runId).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<ContentExpansionRun>> ListRunsAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _runs.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);

    public Task SaveProposalLinkAsync(ContentExpansionProposalLink link, CancellationToken cancellationToken = default) =>
        _links.InsertOneAsync(link, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<ContentExpansionProposalLink>> ListProposalLinksAsync(string projectId, string runId, CancellationToken cancellationToken = default) =>
        await _links.Find(item => item.ProjectId == projectId && item.RunId == runId).SortBy(item => item.CreatedAt).ToListAsync(cancellationToken);

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await _links.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
        await _runs.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
    }
}
