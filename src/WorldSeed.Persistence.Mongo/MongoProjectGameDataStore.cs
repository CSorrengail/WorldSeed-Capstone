using MongoDB.Driver;
using WorldSeed.GameData;

namespace WorldSeed.Persistence.Mongo;

/// <summary>Persists one canonical game-data document per project.</summary>
public sealed class MongoProjectGameDataStore : IProjectGameDataStore
{
    private readonly IMongoCollection<ProjectGameData> _gameData;

    public MongoProjectGameDataStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _gameData = database.GetCollection<ProjectGameData>("projectGameData");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<ProjectGameData>(Builders<ProjectGameData>.IndexKeys.Ascending(data => data.ProjectId), new CreateIndexOptions { Unique = true, Name = "one-game-data-document-per-project" });
        await _gameData.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(ProjectGameData gameData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        await _gameData.ReplaceOneAsync(stored => stored.ProjectId == gameData.ProjectId, gameData, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<ProjectGameData?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _gameData.Find(stored => stored.ProjectId == projectId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await _gameData.DeleteOneAsync(stored => stored.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
