using MongoDB.Driver;
using WorldSeed.Content;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoContentBalanceProfileStore(IMongoDatabase database) : IContentBalanceProfileStore
{
    private readonly IMongoCollection<ProjectContentBalanceProfile> _profiles = database.GetCollection<ProjectContentBalanceProfile>("contentBalanceProfiles");

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) =>
        _profiles.Indexes.CreateOneAsync(new CreateIndexModel<ProjectContentBalanceProfile>(Builders<ProjectContentBalanceProfile>.IndexKeys.Ascending(x => x.ProjectId).Ascending(x => x.ActiveGameSchemaRevision).Ascending(x => x.Profile.EntityTypeId), new CreateIndexOptions { Name = "content-balance-profiles-by-project-schema-type" }), cancellationToken: cancellationToken);
    public Task SaveAsync(ProjectContentBalanceProfile profile, CancellationToken cancellationToken = default) => _profiles.ReplaceOneAsync(item => item.Id == profile.Id, profile, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    public async Task<IReadOnlyList<ProjectContentBalanceProfile>> ListAsync(string projectId, CancellationToken cancellationToken = default) => await _profiles.Find(item => item.ProjectId == projectId).SortByDescending(item => item.UpdatedAt).ToListAsync(cancellationToken);
    public Task DeleteAsync(string projectId, CancellationToken cancellationToken = default) => _profiles.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
}
