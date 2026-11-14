using MongoDB.Driver;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoDevelopmentPlanStore : IDevelopmentPlanStore
{
    private readonly IMongoCollection<DevelopmentPlanProposal> _plans;
    private readonly IMongoCollection<DevelopmentPlanDecision> _planDecisions;
    private readonly IMongoCollection<DevelopmentRecommendationDecision> _recommendationDecisions;

    public MongoDevelopmentPlanStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _plans = database.GetCollection<DevelopmentPlanProposal>("developmentPlans");
        _planDecisions = database.GetCollection<DevelopmentPlanDecision>("developmentPlanDecisions");
        _recommendationDecisions = database.GetCollection<DevelopmentRecommendationDecision>("developmentRecommendationDecisions");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _plans.Indexes.CreateOneAsync(new CreateIndexModel<DevelopmentPlanProposal>(
            Builders<DevelopmentPlanProposal>.IndexKeys.Ascending(item => item.ProjectId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "development-plans-by-project" }), cancellationToken: cancellationToken);
        await _planDecisions.Indexes.CreateOneAsync(new CreateIndexModel<DevelopmentPlanDecision>(
            Builders<DevelopmentPlanDecision>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.PlanId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "development-plan-decisions" }), cancellationToken: cancellationToken);
        await _recommendationDecisions.Indexes.CreateOneAsync(new CreateIndexModel<DevelopmentRecommendationDecision>(
            Builders<DevelopmentRecommendationDecision>.IndexKeys.Ascending(item => item.ProjectId).Ascending(item => item.PlanId).Ascending(item => item.RecommendationId).Descending(item => item.CreatedAt),
            new CreateIndexOptions { Name = "development-recommendation-decisions" }), cancellationToken: cancellationToken);
    }

    public Task SavePlanAsync(DevelopmentPlanProposal plan, CancellationToken cancellationToken = default) => _plans.InsertOneAsync(plan, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<DevelopmentPlanProposal>> ListPlansAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _plans.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public async Task<DevelopmentPlanProposal?> LoadPlanAsync(string projectId, string planId, CancellationToken cancellationToken = default) =>
        await _plans.Find(item => item.ProjectId == projectId && item.Id == planId).FirstOrDefaultAsync(cancellationToken);
    public Task SavePlanDecisionAsync(DevelopmentPlanDecision decision, CancellationToken cancellationToken = default) => _planDecisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public Task SaveRecommendationDecisionAsync(DevelopmentRecommendationDecision decision, CancellationToken cancellationToken = default) => _recommendationDecisions.InsertOneAsync(decision, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<DevelopmentPlanDecision>> ListPlanDecisionsAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _planDecisions.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<DevelopmentRecommendationDecision>> ListRecommendationDecisionsAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _recommendationDecisions.Find(item => item.ProjectId == projectId).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await _recommendationDecisions.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
        await _planDecisions.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
        await _plans.DeleteManyAsync(item => item.ProjectId == projectId, cancellationToken);
    }
}
