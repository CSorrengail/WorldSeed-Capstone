using MongoDB.Driver;
using WorldSeed.Publication;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoPublicationEditionStore : IPublicationEditionStore
{
    private readonly IMongoCollection<PublicationEdition> _editions;

    public MongoPublicationEditionStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _editions = database.GetCollection<PublicationEdition>("publicationEditions");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _editions.Indexes.CreateManyAsync([
            new CreateIndexModel<PublicationEdition>(Builders<PublicationEdition>.IndexKeys.Ascending(edition => edition.ProjectId).Descending(edition => edition.CreatedAt), new CreateIndexOptions { Name = "publication-editions-by-project" }),
            new CreateIndexModel<PublicationEdition>(Builders<PublicationEdition>.IndexKeys.Ascending(edition => edition.SeriesId).Descending(edition => edition.Revision), new CreateIndexOptions { Name = "publication-editions-by-series-revision", Unique = true })
        ], cancellationToken);
    }

    public Task SaveAsync(PublicationEdition edition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edition);
        return _editions.InsertOneAsync(edition, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PublicationEdition>> ListAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _editions.Find(edition => edition.ProjectId == projectId).SortByDescending(edition => edition.CreatedAt).ToListAsync(cancellationToken);

    public Task<PublicationEdition?> LoadLatestAsync(string projectId, string seriesId, CancellationToken cancellationToken = default) =>
        _editions.Find(edition => edition.ProjectId == projectId && edition.SeriesId == seriesId).SortByDescending(edition => edition.Revision).FirstOrDefaultAsync(cancellationToken)!;

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _editions.DeleteManyAsync(edition => edition.ProjectId == projectId, cancellationToken);
}
