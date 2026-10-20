using MongoDB.Driver;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.Persistence.Mongo;

/// <summary>Persists one current non-canonical schema proposal per project.</summary>
public sealed class MongoProjectSchemaProposalStore : IProjectSchemaProposalStore
{
    private readonly IMongoCollection<ProjectSchemaProposal> _proposals;

    public MongoProjectSchemaProposalStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _proposals = database.GetCollection<ProjectSchemaProposal>("schemaProposals");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<ProjectSchemaProposal>(Builders<ProjectSchemaProposal>.IndexKeys.Ascending(proposal => proposal.ProjectId), new CreateIndexOptions { Unique = true, Name = "one-schema-proposal-per-project" });
        await _proposals.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(ProjectSchemaProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        await _proposals.ReplaceOneAsync(stored => stored.ProjectId == proposal.ProjectId, proposal, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<ProjectSchemaProposal?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _proposals.Find(stored => stored.ProjectId == projectId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await _proposals.DeleteOneAsync(stored => stored.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
