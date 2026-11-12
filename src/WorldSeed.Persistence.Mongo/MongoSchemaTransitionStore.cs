using MongoDB.Driver;
using WorldSeed.GameData;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoSchemaTransitionStore : ISchemaTransitionStore
{
    private readonly IMongoCollection<SchemaTransitionRecord> _transitions;

    public MongoSchemaTransitionStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _transitions = database.GetCollection<SchemaTransitionRecord>("schemaTransitions");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<SchemaTransitionRecord>(
            Builders<SchemaTransitionRecord>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.ToRevision),
            new CreateIndexOptions { Unique = true, Name = "one-schema-transition-per-project-revision" });
        await _transitions.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(SchemaTransitionRecord transition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);
        await _transitions.InsertOneAsync(transition, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SchemaTransitionRecord>> ListAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _transitions.Find(item => item.ProjectId == projectId)
            .SortBy(item => item.ToRevision).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await _transitions.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
