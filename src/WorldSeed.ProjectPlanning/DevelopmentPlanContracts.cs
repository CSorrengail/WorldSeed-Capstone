namespace WorldSeed.ProjectPlanning;

public enum DevelopmentRecommendationCategory
{
    ResolveSourceOrRule,
    ExpandRuleSystem,
    ExpandContent,
    AddEvaluationCoverage,
    ImprovePublication
}

public enum DevelopmentRecommendationPriority { Now, Next, Later }

public sealed record DevelopmentRecommendation(
    string Id,
    string Title,
    DevelopmentRecommendationCategory Category,
    DevelopmentRecommendationPriority Priority,
    string Goal,
    string Rationale,
    string ExpectedOutcome,
    IReadOnlyList<string> EvidenceObservationIds,
    IReadOnlyList<string> DependsOnRecommendationIds,
    IReadOnlyList<string> Tradeoffs,
    string? AlternativeApproach,
    string? DifferenceOfOpinion,
    string? DesignerDecisionNeeded);

public sealed record DevelopmentPlanProposal(
    string Id,
    string ProjectId,
    string FormatVersion,
    string SeriesId,
    int Version,
    string InventoryId,
    int InventoryRevision,
    string? ReplacesPlanId,
    string Summary,
    IReadOnlyList<string> OverallTradeoffs,
    IReadOnlyList<DevelopmentRecommendation> Recommendations,
    string? DesignerDirection,
    DateTimeOffset CreatedAt);

public enum DevelopmentPlanDecisionKind { Approved, Rejected, RevisionRequested }
public enum DevelopmentRecommendationDecisionKind { Accepted, Rejected, RevisionRequested, Deferred }

public sealed record DevelopmentPlanDecision(
    string Id,
    string ProjectId,
    string PlanId,
    int PlanVersion,
    DevelopmentPlanDecisionKind Kind,
    string? Note,
    string? ReplacementPlanId,
    DateTimeOffset CreatedAt);

public sealed record DevelopmentRecommendationDecision(
    string Id,
    string ProjectId,
    string PlanId,
    int PlanVersion,
    string RecommendationId,
    DevelopmentRecommendationDecisionKind Kind,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record DevelopmentPlanDecisionRequest(DevelopmentPlanDecisionKind Kind, string? Note);
public sealed record DevelopmentRecommendationDecisionRequest(DevelopmentRecommendationDecisionKind Kind, string? Note);

public interface IDevelopmentPlanStore
{
    Task SavePlanAsync(DevelopmentPlanProposal plan, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DevelopmentPlanProposal>> ListPlansAsync(string projectId, CancellationToken cancellationToken = default);
    Task<DevelopmentPlanProposal?> LoadPlanAsync(string projectId, string planId, CancellationToken cancellationToken = default);
    Task SavePlanDecisionAsync(DevelopmentPlanDecision decision, CancellationToken cancellationToken = default);
    Task SaveRecommendationDecisionAsync(DevelopmentRecommendationDecision decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DevelopmentPlanDecision>> ListPlanDecisionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DevelopmentRecommendationDecision>> ListRecommendationDecisionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}
