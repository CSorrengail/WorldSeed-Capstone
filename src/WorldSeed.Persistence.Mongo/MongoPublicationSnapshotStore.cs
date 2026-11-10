using MongoDB.Driver;
using WorldSeed.Publication;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoPublicationSnapshotStore : IPublicationSnapshotStore
{
    private readonly IMongoCollection<PublicationSnapshot> _snapshots;

    public MongoPublicationSnapshotStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _snapshots = database.GetCollection<PublicationSnapshot>("publicationSnapshots");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var keys = Builders<PublicationSnapshot>.IndexKeys.Ascending(snapshot => snapshot.ProjectId).Descending(snapshot => snapshot.CreatedAt);
        await _snapshots.Indexes.CreateOneAsync(new CreateIndexModel<PublicationSnapshot>(keys, new CreateIndexOptions { Name = "publication-snapshots-by-project" }), cancellationToken: cancellationToken);
    }

    public Task SaveAsync(PublicationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return _snapshots.InsertOneAsync(snapshot, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PublicationSnapshot>> ListAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _snapshots.Find(snapshot => snapshot.ProjectId == projectId).SortByDescending(snapshot => snapshot.CreatedAt).ToListAsync(cancellationToken);

    public Task<PublicationSnapshot?> LoadAsync(string projectId, string snapshotId, CancellationToken cancellationToken = default) =>
        _snapshots.Find(snapshot => snapshot.ProjectId == projectId && snapshot.Id == snapshotId).FirstOrDefaultAsync(cancellationToken)!;

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _snapshots.DeleteManyAsync(snapshot => snapshot.ProjectId == projectId, cancellationToken);
}
