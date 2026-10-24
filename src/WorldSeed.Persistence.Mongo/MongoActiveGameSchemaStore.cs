using MongoDB.Driver;
using WorldSeed.GameData;

namespace WorldSeed.Persistence.Mongo;

/// <summary>Persists one active schema snapshot per project.</summary>
public sealed class MongoActiveGameSchemaStore : IActiveGameSchemaStore
{
    private readonly IMongoCollection<ActiveGameSchema> _schemas;

    public MongoActiveGameSchemaStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _schemas = database.GetCollection<ActiveGameSchema>("activeGameSchemas");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<ActiveGameSchema>(Builders<ActiveGameSchema>.IndexKeys.Ascending(schema => schema.ProjectId), new CreateIndexOptions { Unique = true, Name = "one-active-schema-per-project" });
        await _schemas.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(ActiveGameSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        await _schemas.ReplaceOneAsync(stored => stored.ProjectId == schema.ProjectId, schema, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<ActiveGameSchema?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _schemas.Find(stored => stored.ProjectId == projectId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await _schemas.DeleteOneAsync(stored => stored.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
