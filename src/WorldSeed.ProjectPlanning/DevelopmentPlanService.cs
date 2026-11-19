namespace WorldSeed.ProjectPlanning;

public static class DevelopmentPlanService
{
    private const string FormatVersion = "0.1";

    public static DevelopmentPlanProposal Create(
        ProjectInventory inventory,
        string summary,
        IReadOnlyList<string> overallTradeoffs,
        IReadOnlyList<DevelopmentRecommendation> recommendations,
        string? designerDirection,
        DateTimeOffset now,
        DevelopmentPlanProposal? replaces = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("A development plan needs a summary.", nameof(summary));
        if (recommendations is not { Count: > 0 and <= 10 }) throw new ArgumentException("A development plan needs between one and ten recommendations.", nameof(recommendations));
        if (replaces is not null && (replaces.ProjectId != inventory.ProjectId || replaces.InventoryId != inventory.Id))
            throw new ArgumentException("A replacement plan must use the same project and inventory snapshot.", nameof(replaces));

        ValidateRecommendations(inventory, recommendations);
        return new DevelopmentPlanProposal(
            $"development-plan-{inventory.ProjectId}-{Guid.NewGuid():N}", inventory.ProjectId, FormatVersion,
            replaces?.SeriesId ?? $"development-plan-series-{Guid.NewGuid():N}", (replaces?.Version ?? 0) + 1,
            inventory.Id, inventory.Revision, replaces?.Id, summary.Trim(), overallTradeoffs.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToArray(),
            recommendations, string.IsNullOrWhiteSpace(designerDirection) ? null : designerDirection.Trim(), now);
    }

    public static void Validate(DevelopmentPlanProposal plan, ProjectInventory inventory, DevelopmentPlanProposal? replacedPlan = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(inventory);
        if (plan.ProjectId != inventory.ProjectId || plan.InventoryId != inventory.Id || plan.InventoryRevision != inventory.Revision)
            throw new ArgumentException("The development plan must be bound to the exact project inventory snapshot.", nameof(plan));
        if (plan.Version < 1 || string.IsNullOrWhiteSpace(plan.Id) || string.IsNullOrWhiteSpace(plan.SeriesId) || string.IsNullOrWhiteSpace(plan.Summary))
            throw new ArgumentException("The development plan identity, version, and summary are required.", nameof(plan));
        if (plan.Recommendations is not { Count: > 0 and <= 10 }) throw new ArgumentException("A development plan needs between one and ten recommendations.", nameof(plan));
        if (plan.ReplacesPlanId is null && plan.Version != 1) throw new ArgumentException("Only an initial plan may omit a replaced plan.", nameof(plan));
        if (plan.ReplacesPlanId is not null && (replacedPlan is null || replacedPlan.Id != plan.ReplacesPlanId || replacedPlan.SeriesId != plan.SeriesId || plan.Version != replacedPlan.Version + 1))
            throw new ArgumentException("A plan revision must follow the identified prior plan in the same series.", nameof(plan));
        ValidateRecommendations(inventory, plan.Recommendations);
    }

    private static void ValidateRecommendations(ProjectInventory inventory, IReadOnlyList<DevelopmentRecommendation> recommendations)
    {
        var observationIds = inventory.Observations.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var recommendationIds = recommendations.Select(item => item.Id).ToArray();
        if (recommendationIds.Any(string.IsNullOrWhiteSpace) || recommendationIds.Distinct(StringComparer.Ordinal).Count() != recommendationIds.Length)
            throw new ArgumentException("Recommendation ids must be present and unique.", nameof(recommendations));
        foreach (var recommendation in recommendations)
        {
            if (string.IsNullOrWhiteSpace(recommendation.Title) || string.IsNullOrWhiteSpace(recommendation.Goal) || string.IsNullOrWhiteSpace(recommendation.Rationale) || string.IsNullOrWhiteSpace(recommendation.ExpectedOutcome))
                throw new ArgumentException($"Recommendation '{recommendation.Id}' is incomplete.", nameof(recommendations));
            if (recommendation.EvidenceObservationIds.Count == 0 || recommendation.EvidenceObservationIds.Any(id => !observationIds.Contains(id)))
                throw new ArgumentException($"Recommendation '{recommendation.Id}' must cite existing inventory observations.", nameof(recommendations));
            var evidence = inventory.Observations.Where(observation => recommendation.EvidenceObservationIds.Contains(observation.Id, StringComparer.Ordinal)).ToArray();
            var unsupported = evidence.FirstOrDefault(observation => !Supports(recommendation.Category, observation.Kind));
            if (unsupported is not null)
                throw new ArgumentException($"Recommendation '{recommendation.Id}' cites observation '{unsupported.Id}' ({unsupported.Kind}) that does not support its category '{recommendation.Category}'. Allowed categories for this observation: {string.Join(", ", Enum.GetValues<DevelopmentRecommendationCategory>().Where(category => Supports(category, unsupported.Kind)))}.", nameof(recommendations));
            if (recommendation.DependsOnRecommendationIds.Any(id => id == recommendation.Id || !recommendationIds.Contains(id, StringComparer.Ordinal)))
                throw new ArgumentException($"Recommendation '{recommendation.Id}' has an invalid dependency.", nameof(recommendations));
        }
        EnsureAcyclic(recommendations);
    }

    internal static bool Supports(DevelopmentRecommendationCategory category, InventoryObservationKind observation) => category switch
    {
        DevelopmentRecommendationCategory.ResolveSourceOrRule => observation is InventoryObservationKind.MissingFoundation
            or InventoryObservationKind.UnresolvedQuestion or InventoryObservationKind.Conflict or InventoryObservationKind.MissingSourceTrace
            or InventoryObservationKind.UncitedSourcePassage or InventoryObservationKind.UnincorporatedDraftRule,
        DevelopmentRecommendationCategory.ExpandRuleSystem => observation is InventoryObservationKind.MissingFoundation
            or InventoryObservationKind.UnresolvedQuestion or InventoryObservationKind.Conflict or InventoryObservationKind.ReportedSchemaGap,
        DevelopmentRecommendationCategory.ExpandContent => observation is InventoryObservationKind.EmptyContentType
            or InventoryObservationKind.MissingFoundation,
        DevelopmentRecommendationCategory.AddEvaluationCoverage => observation == InventoryObservationKind.MissingEvaluationCoverage,
        DevelopmentRecommendationCategory.ImprovePublication => observation == InventoryObservationKind.UnpublishedChanges,
        _ => false
    };

    public static DevelopmentPlanDecision DecidePlan(DevelopmentPlanProposal plan, DevelopmentPlanDecisionKind kind, string? note, DateTimeOffset now, DevelopmentPlanProposal? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (kind == DevelopmentPlanDecisionKind.RevisionRequested && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A plan revision request needs designer guidance.", nameof(note));
        if (replacement is not null && (replacement.ProjectId != plan.ProjectId || replacement.ReplacesPlanId != plan.Id)) throw new ArgumentException("A replacement plan must identify the reviewed plan.", nameof(replacement));
        return new DevelopmentPlanDecision($"development-plan-decision-{Guid.NewGuid():N}", plan.ProjectId, plan.Id, plan.Version, kind, Normalize(note), replacement?.Id, now);
    }

    public static DevelopmentRecommendationDecision DecideRecommendation(DevelopmentPlanProposal plan, string recommendationId, DevelopmentRecommendationDecisionKind kind, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.Recommendations.Any(item => item.Id == recommendationId)) throw new ArgumentException("The recommendation does not belong to this plan.", nameof(recommendationId));
        if (kind == DevelopmentRecommendationDecisionKind.RevisionRequested && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A recommendation revision needs designer guidance.", nameof(note));
        return new DevelopmentRecommendationDecision($"development-recommendation-decision-{Guid.NewGuid():N}", plan.ProjectId, plan.Id, plan.Version, recommendationId, kind, Normalize(note), now);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureAcyclic(IReadOnlyList<DevelopmentRecommendation> recommendations)
    {
        var byId = recommendations.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recommendation in recommendations) Visit(recommendation.Id);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Recommendation dependencies cannot contain a cycle.", nameof(recommendations));
            foreach (var dependency in byId[id].DependsOnRecommendationIds) Visit(dependency);
            visiting.Remove(id);
            visited.Add(id);
        }
    }
}
