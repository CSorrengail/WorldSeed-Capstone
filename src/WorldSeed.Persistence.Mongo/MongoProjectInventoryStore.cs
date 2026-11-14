using MongoDB.Driver;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoProjectInventoryStore(IMongoDatabase database) : IProjectInventoryStore
{
    private readonly IMongoCollection<ProjectInventory> _inventories = database.GetCollection<ProjectInventory>("projectInventories");

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var keys = Builders<ProjectInventory>.IndexKeys.Ascending(item => item.ProjectId).Descending(item => item.Revision);
        return _inventories.Indexes.CreateOneAsync(new CreateIndexModel<ProjectInventory>(keys,
            new CreateIndexOptions { Unique = true, Name = "project-inventories-by-project-revision" }), cancellationToken: cancellationToken);
    }

    public Task SaveAsync(ProjectInventory inventory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return _inventories.InsertOneAsync(inventory, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectInventory>> ListAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _inventories.Find(item => item.ProjectId == projectId).SortByDescending(item => item.Revision).ToListAsync(cancellationToken);
    }

    public async Task<ProjectInventory?> LoadLatestAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _inventories.Find(item => item.ProjectId == projectId).SortByDescending(item => item.Revision).FirstOrDefaultAsync(cancellationToken);
    }

    public Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return _inventories.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
