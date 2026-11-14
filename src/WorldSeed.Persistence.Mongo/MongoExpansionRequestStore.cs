using MongoDB.Driver;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoExpansionRequestStore : IExpansionRequestStore
{
    private readonly IMongoCollection<ExpansionRequest> _requests;

    public MongoExpansionRequestStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _requests = database.GetCollection<ExpansionRequest>("expansionRequests");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _requests.Indexes.CreateOneAsync(new CreateIndexModel<ExpansionRequest>(
            Builders<ExpansionRequest>.IndexKeys.Ascending(item => item.ProjectId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "expansion-requests-by-project" }), cancellationToken: cancellationToken);
        await _requests.Indexes.CreateOneAsync(new CreateIndexModel<ExpansionRequest>(
            Builders<ExpansionRequest>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.AcceptanceDecisionId),
            new CreateIndexOptions { Name = "expansion-request-acceptance", Unique = true }), cancellationToken: cancellationToken);
    }

    public Task SaveAsync(ExpansionRequest request, CancellationToken cancellationToken = default) =>
        _requests.InsertOneAsync(request, cancellationToken: cancellationToken);
    public async Task<ExpansionRequest?> LoadAsync(string projectId, string requestId, CancellationToken cancellationToken = default) =>
        await _requests.Find(item => item.ProjectId == projectId && item.Id == requestId).FirstOrDefaultAsync(cancellationToken);
    public async Task<ExpansionRequest?> LoadByAcceptanceDecisionAsync(string projectId, string acceptanceDecisionId, CancellationToken cancellationToken = default) =>
        await _requests.Find(item => item.ProjectId == projectId && item.AcceptanceDecisionId == acceptanceDecisionId).FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<ExpansionRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _requests.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) =>
        _requests.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
}
