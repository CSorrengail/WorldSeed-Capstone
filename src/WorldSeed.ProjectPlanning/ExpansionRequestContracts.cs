using WorldSeed.DesignSessions;
using WorldSeed.RuleDrafting;
using WorldSeed.WorkingRules;

namespace WorldSeed.ProjectPlanning;

public sealed record ExpansionRequestEvidence(
    string ObservationId,
    InventoryObservationKind Kind,
    string SubjectId,
    string Description,
    IReadOnlyList<string> EvidenceIds);

/// <summary>
/// Immutable handoff from one explicitly accepted planning recommendation into
/// source-grounded clarification and rule drafting. It is not a rule, schema
/// change, content proposal, or authorization to alter canonical game data.
/// </summary>
public sealed record ExpansionRequest(
    string Id,
    string ProjectId,
    string InventoryId,
    int InventoryRevision,
    string PlanId,
    int PlanVersion,
    string RecommendationId,
    string AcceptanceDecisionId,
    string SourceNoteId,
    string Title,
    string Intent,
    IReadOnlyList<ExpansionRequestEvidence> Evidence,
    DateTimeOffset CreatedAt);

public enum ExpansionProgressStage
{
    ReadyForClarification,
    ClarificationInProgress,
    DraftReady,
    WorkingRulesIncorporated
}

public sealed record ExpansionRequestProgress(
    string RequestId,
    ExpansionProgressStage Stage,
    string? SessionId,
    int DraftRuleCount,
    int? WorkingRuleSetRevision,
    IReadOnlyList<string> WorkingRuleIds);

public interface IExpansionRequestStore
{
    Task SaveAsync(ExpansionRequest request, CancellationToken cancellationToken = default);
    Task<ExpansionRequest?> LoadAsync(string projectId, string requestId, CancellationToken cancellationToken = default);
    Task<ExpansionRequest?> LoadByAcceptanceDecisionAsync(string projectId, string acceptanceDecisionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpansionRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class ExpansionRequestService
{
    public static ExpansionRequestProgress EvaluateProgress(
        ExpansionRequest request,
        DesignProject project,
        WorkingRuleSet? workingRules)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(project);
        if (request.ProjectId != project.Id || workingRules is not null && workingRules.ProjectId != project.Id)
            throw new ArgumentException("Expansion progress inputs must belong to the same project.");
        var session = project.Sessions
            .Where(item => item.SourceNotes.Any(source => source.Id == request.SourceNoteId))
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefault();
        var workingRuleIds = workingRules?.Rules
            .Where(rule => rule.Evidence.Any(item => item.SourceNoteId == request.SourceNoteId) ||
                           session is not null && rule.SourceDraftRules.Any(item => item.SessionId == session.Id))
            .Select(rule => rule.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        var draftRuleCount = session?.LatestTurn?.Draft?.Rules.Count ?? 0;
        var stage = workingRuleIds.Length > 0
            ? ExpansionProgressStage.WorkingRulesIncorporated
            : session?.LatestTurn?.Action == RuleDraftAction.PresentDraft
                ? ExpansionProgressStage.DraftReady
                : session is not null
                    ? ExpansionProgressStage.ClarificationInProgress
                    : ExpansionProgressStage.ReadyForClarification;
        return new ExpansionRequestProgress(
            request.Id, stage, session?.Id, draftRuleCount,
            workingRuleIds.Length > 0 ? workingRules?.Revision : null,
            workingRuleIds);
    }

    public static (ExpansionRequest Request, DesignSourceNote SourceNote) Create(
        DesignProject project,
        ProjectInventory inventory,
        DevelopmentPlanProposal plan,
        DevelopmentRecommendation recommendation,
        DevelopmentRecommendationDecision acceptance,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(acceptance);
        if (inventory.ProjectId != project.Id || plan.ProjectId != project.Id || acceptance.ProjectId != project.Id)
            throw new ArgumentException("The project, inventory, plan, and decision must belong to the same project.");
        if (plan.InventoryId != inventory.Id || plan.InventoryRevision != inventory.Revision)
            throw new ArgumentException("The development plan must use the supplied inventory revision.");
        if (!plan.Recommendations.Any(item => item.Id == recommendation.Id))
            throw new ArgumentException("The recommendation does not belong to the development plan.");
        if (acceptance.PlanId != plan.Id || acceptance.PlanVersion != plan.Version || acceptance.RecommendationId != recommendation.Id)
            throw new ArgumentException("The acceptance decision does not match the selected recommendation.");
        if (acceptance.Kind != DevelopmentRecommendationDecisionKind.Accepted)
            throw new ArgumentException("Only an explicitly accepted recommendation can start an expansion request.");

        var observations = inventory.Observations.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (recommendation.EvidenceObservationIds.Count == 0 || recommendation.EvidenceObservationIds.Any(id => !observations.ContainsKey(id)))
            throw new ArgumentException("The recommendation must cite observations from its bound inventory.");
        var evidence = recommendation.EvidenceObservationIds.Select(id => observations[id])
            .Select(item => new ExpansionRequestEvidence(item.Id, item.Kind, item.SubjectId, item.Description, item.EvidenceIds.ToArray()))
            .ToArray();
        var requestId = $"expansion-{acceptance.Id}";
        var sourceNoteId = $"source-{requestId}";
        var request = new ExpansionRequest(
            requestId, project.Id, inventory.Id, inventory.Revision, plan.Id, plan.Version,
            recommendation.Id, acceptance.Id, sourceNoteId, recommendation.Title, recommendation.Goal,
            evidence, now);
        var sourceText = BuildSourceText(plan, recommendation, acceptance, evidence);
        return (request, new DesignSourceNote(sourceNoteId, sourceText, now, new SourceNoteOrigin($"Expansion Request - {recommendation.Title}")));
    }

    public static string ClarificationInstruction =>
        "This source is an accepted development goal, not approved game mechanics. On the first turn, ask focused clarification questions about the rules, boundaries, dependencies, validation needs, and intended player-facing result. Do not present a rule draft until the designer has answered those questions. Later drafts may use only the expansion request and designer answers as evidence.";

    private static string BuildSourceText(
        DevelopmentPlanProposal plan,
        DevelopmentRecommendation recommendation,
        DevelopmentRecommendationDecision acceptance,
        IReadOnlyList<ExpansionRequestEvidence> evidence)
    {
        var lines = new List<string>
        {
            "DESIGNER-ACCEPTED DEVELOPMENT GOAL",
            "This records intent to explore and clarify a possible game-system expansion. It does not approve any new mechanic, schema, or content.",
            "",
            $"Title: {recommendation.Title}",
            $"Goal: {recommendation.Goal}",
            $"Rationale: {recommendation.Rationale}",
            $"Expected outcome: {recommendation.ExpectedOutcome}",
            $"Plan summary: {plan.Summary}",
            $"Designer acceptance note: {acceptance.Note ?? "No additional note."}",
            "",
            "Inventory observations that motivated this request:"
        };
        lines.AddRange(evidence.Select(item => $"- [{item.Kind}] {item.Description}"));
        if (!string.IsNullOrWhiteSpace(recommendation.DesignerDecisionNeeded))
            lines.Add($"Decision still needed: {recommendation.DesignerDecisionNeeded}");
        lines.Add("");
        lines.Add("Clarify the intended mechanics and boundaries with the designer before proposing rules. Treat inventory observations as planning evidence, not as permission to invent missing rules.");
        return string.Join("\n", lines);
    }
}
