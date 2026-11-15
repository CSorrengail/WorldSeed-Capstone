using MongoDB.Driver;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoExpansionRequestStore : IExpansionRequestStore
{
    private readonly IMongoCollection<ExpansionRequest> _requests;
    private readonly IMongoCollection<ExpansionSchemaDecision> _schemaDecisions;

    public MongoExpansionRequestStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _requests = database.GetCollection<ExpansionRequest>("expansionRequests");
        _schemaDecisions = database.GetCollection<ExpansionSchemaDecision>("expansionSchemaDecisions");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _requests.Indexes.CreateOneAsync(new CreateIndexModel<ExpansionRequest>(
            Builders<ExpansionRequest>.IndexKeys.Ascending(item => item.ProjectId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "expansion-requests-by-project" }), cancellationToken: cancellationToken);
        await _requests.Indexes.CreateOneAsync(new CreateIndexModel<ExpansionRequest>(
            Builders<ExpansionRequest>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.AcceptanceDecisionId),
            new CreateIndexOptions { Name = "expansion-request-acceptance", Unique = true }), cancellationToken: cancellationToken);
        await _schemaDecisions.Indexes.CreateOneAsync(new CreateIndexModel<ExpansionSchemaDecision>(
            Builders<ExpansionSchemaDecision>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.RequestId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "expansion-schema-decisions" }), cancellationToken: cancellationToken);
    }

    public Task SaveAsync(ExpansionRequest request, CancellationToken cancellationToken = default) =>
        _requests.InsertOneAsync(request, cancellationToken: cancellationToken);
    public async Task<ExpansionRequest?> LoadAsync(string projectId, string requestId, CancellationToken cancellationToken = default) =>
        await _requests.Find(item => item.ProjectId == projectId && item.Id == requestId).FirstOrDefaultAsync(cancellationToken);
    public async Task<ExpansionRequest?> LoadByAcceptanceDecisionAsync(string projectId, string acceptanceDecisionId, CancellationToken cancellationToken = default) =>
        await _requests.Find(item => item.ProjectId == projectId && item.AcceptanceDecisionId == acceptanceDecisionId).FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<ExpansionRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _requests.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public Task SaveSchemaDecisionAsync(ExpansionSchemaDecision decision, CancellationToken cancellationToken = default) =>
        _schemaDecisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<ExpansionSchemaDecision>> ListSchemaDecisionsAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _schemaDecisions.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await _schemaDecisions.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
        await _requests.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
    }
}
