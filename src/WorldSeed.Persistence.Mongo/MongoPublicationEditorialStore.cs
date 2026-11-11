using MongoDB.Driver;
using WorldSeed.Publication;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoPublicationEditorialStore : IPublicationEditorialStore
{
    private readonly IMongoCollection<PublicationEditorialProposal> _proposals;
    private readonly IMongoCollection<PublicationEditorialDecision> _decisions;

    public MongoPublicationEditorialStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _proposals = database.GetCollection<PublicationEditorialProposal>("publicationEditorialProposals");
        _decisions = database.GetCollection<PublicationEditorialDecision>("publicationEditorialDecisions");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _proposals.Indexes.CreateManyAsync([
            new CreateIndexModel<PublicationEditorialProposal>(Builders<PublicationEditorialProposal>.IndexKeys.Ascending(value => value.ProjectId).Descending(value => value.CreatedAt), new CreateIndexOptions { Name = "publication-editorial-proposals-by-project" }),
            new CreateIndexModel<PublicationEditorialProposal>(Builders<PublicationEditorialProposal>.IndexKeys.Ascending(value => value.SeriesId).Descending(value => value.Revision), new CreateIndexOptions { Name = "publication-editorial-proposals-by-series-revision", Unique = true })
        ], cancellationToken);
        await _decisions.Indexes.CreateOneAsync(new CreateIndexModel<PublicationEditorialDecision>(Builders<PublicationEditorialDecision>.IndexKeys.Ascending(value => value.ProjectId).Ascending(value => value.CreatedAt), new CreateIndexOptions { Name = "publication-editorial-decisions-by-project" }), cancellationToken: cancellationToken);
    }

    public Task SaveProposalAsync(PublicationEditorialProposal proposal, CancellationToken cancellationToken = default) => _proposals.InsertOneAsync(proposal, cancellationToken: cancellationToken);
    public Task<PublicationEditorialProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default) => _proposals.Find(value => value.ProjectId == projectId && value.Id == proposalId).FirstOrDefaultAsync(cancellationToken)!;
    public async Task<IReadOnlyList<PublicationEditorialProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default) => await _proposals.Find(value => value.ProjectId == projectId).SortByDescending(value => value.CreatedAt).ToListAsync(cancellationToken);
    public Task SaveDecisionAsync(PublicationEditorialDecision decision, CancellationToken cancellationToken = default) => _decisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<PublicationEditorialDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default) => await _decisions.Find(value => value.ProjectId == projectId).SortBy(value => value.CreatedAt).ToListAsync(cancellationToken);
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await _proposals.DeleteManyAsync(value => value.ProjectId == projectId, cancellationToken);
        await _decisions.DeleteManyAsync(value => value.ProjectId == projectId, cancellationToken);
    }
}
