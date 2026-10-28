using MongoDB.Driver;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.Persistence.Mongo;

/// <summary>Persists the schema-evolution requests that can originate in the designer UI or a future content-review suggestion.</summary>
public sealed class MongoProjectSchemaChangeRequestStore : IProjectSchemaChangeRequestStore
{
    private readonly IMongoCollection<SchemaChangeRequest> _requests;

    public MongoProjectSchemaChangeRequestStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _requests = database.GetCollection<SchemaChangeRequest>("schemaChangeRequests");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<SchemaChangeRequest>(
            Builders<SchemaChangeRequest>.IndexKeys.Ascending(request => request.ProjectId).Ascending(request => request.CreatedAt),
            new CreateIndexOptions { Name = "schema-change-requests-by-project" });
        await _requests.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(SchemaChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _requests.ReplaceOneAsync(stored => stored.Id == request.Id, request, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<SchemaChangeRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _requests.Find(request => request.ProjectId == projectId).SortByDescending(request => request.UpdatedAt).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await _requests.DeleteManyAsync(request => request.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
