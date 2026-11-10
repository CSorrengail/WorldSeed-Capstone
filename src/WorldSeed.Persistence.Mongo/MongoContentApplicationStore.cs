using MongoDB.Driver;
using WorldSeed.Content;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoContentApplicationStore : IContentApplicationStore
{
    private readonly IMongoCollection<ContentApplication> _applications;

    public MongoContentApplicationStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _applications = database.GetCollection<ContentApplication>("contentApplications");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var keys = Builders<ContentApplication>.IndexKeys
            .Ascending(application => application.ProjectId)
            .Ascending(application => application.ProposalId)
            .Ascending(application => application.ProposalVersion);
        await _applications.Indexes.CreateOneAsync(new CreateIndexModel<ContentApplication>(keys, new CreateIndexOptions { Unique = true, Name = "one-application-per-proposal-version" }), cancellationToken: cancellationToken);
    }

    public Task SaveAsync(ContentApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        return _applications.ReplaceOneAsync(
            stored => stored.ProjectId == application.ProjectId && stored.ProposalId == application.ProposalId && stored.ProposalVersion == application.ProposalVersion,
            application,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<ContentApplication?> LoadAsync(string projectId, string proposalId, int proposalVersion, CancellationToken cancellationToken = default) =>
        await _applications.Find(application => application.ProjectId == projectId && application.ProposalId == proposalId && application.ProposalVersion == proposalVersion).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentApplication>> ListAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _applications.Find(application => application.ProjectId == projectId).SortByDescending(application => application.CreatedAt).ToListAsync(cancellationToken);

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _applications.DeleteManyAsync(application => application.ProjectId == projectId, cancellationToken);
}
