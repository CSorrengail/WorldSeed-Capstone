using MongoDB.Driver;
using WorldSeed.Content;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoEvaluationAssistanceStore(IMongoDatabase database) : IEvaluationAssistanceStore
{
    private readonly IMongoCollection<EvaluationAssistanceProposal> _proposals = database.GetCollection<EvaluationAssistanceProposal>("evaluationAssistanceProposals");
    private readonly IMongoCollection<EvaluationProposalDecision> _decisions = database.GetCollection<EvaluationProposalDecision>("evaluationAssistanceDecisions");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _proposals.Indexes.CreateOneAsync(new CreateIndexModel<EvaluationAssistanceProposal>(Builders<EvaluationAssistanceProposal>.IndexKeys.Ascending(item => item.ProjectId).Descending(item => item.CreatedAt), new CreateIndexOptions { Name = "evaluation-proposals-by-project" }), cancellationToken: cancellationToken);
        await _decisions.Indexes.CreateOneAsync(new CreateIndexModel<EvaluationProposalDecision>(Builders<EvaluationProposalDecision>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.ProposalId).Descending(item => item.CreatedAt), new CreateIndexOptions { Name = "evaluation-decisions-by-proposal" }), cancellationToken: cancellationToken);
    }

    public Task SaveProposalAsync(EvaluationAssistanceProposal proposal, CancellationToken cancellationToken = default) =>
        _proposals.ReplaceOneAsync(item => item.ProjectId == proposal.ProjectId && item.Id == proposal.Id, proposal, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    public async Task<EvaluationAssistanceProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default) =>
        await _proposals.Find(item => item.ProjectId == projectId && item.Id == proposalId).FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<EvaluationAssistanceProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _proposals.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public Task SaveDecisionAsync(EvaluationProposalDecision decision, CancellationToken cancellationToken = default) =>
        _decisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<EvaluationProposalDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _decisions.Find(item => item.ProjectId == projectId).SortBy(item => item.CreatedAt).ToListAsync(cancellationToken);
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await _decisions.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
        await _proposals.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
    }
}
