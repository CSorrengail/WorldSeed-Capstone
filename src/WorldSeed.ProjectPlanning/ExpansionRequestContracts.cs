using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.RuleDrafting;
using WorldSeed.SchemaTranslation;
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
    WorkingRulesIncorporated,
    SchemaWorkRequested,
    SchemaProposalReady,
    SchemaActivated,
    NoSchemaChangeRequired
}

public sealed record ExpansionRequestProgress(
    string RequestId,
    ExpansionProgressStage Stage,
    string? SessionId,
    int DraftRuleCount,
    int? WorkingRuleSetRevision,
    IReadOnlyList<string> WorkingRuleIds,
    string? SchemaDecisionId = null);

public enum ExpansionSchemaDecisionKind { SchemaWorkRequested, NoSchemaChangeRequired }

public sealed record ExpansionSchemaDecision(
    string Id,
    string ProjectId,
    string RequestId,
    ExpansionSchemaDecisionKind Kind,
    int WorkingRuleSetRevision,
    IReadOnlyList<string> WorkingRuleIds,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ExpansionSchemaDecisionRequest(ExpansionSchemaDecisionKind Kind, string? Note);

public interface IExpansionRequestStore
{
    Task SaveAsync(ExpansionRequest request, CancellationToken cancellationToken = default);
    Task<ExpansionRequest?> LoadAsync(string projectId, string requestId, CancellationToken cancellationToken = default);
    Task<ExpansionRequest?> LoadByAcceptanceDecisionAsync(string projectId, string acceptanceDecisionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpansionRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveSchemaDecisionAsync(ExpansionSchemaDecision decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpansionSchemaDecision>> ListSchemaDecisionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class ExpansionRequestService
{
    public static ExpansionRequestProgress EvaluateProgress(
        ExpansionRequest request,
        DesignProject project,
        WorkingRuleSet? workingRules,
        ProjectSchemaProposal? schemaProposal = null,
        ActiveGameSchema? activeSchema = null,
        IReadOnlyList<ExpansionSchemaDecision>? schemaDecisions = null)
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
        var latestSchemaDecision = (schemaDecisions ?? [])
            .Where(item => item.RequestId == request.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var proposalIncludesRules = schemaProposal is not null && workingRuleIds.Length > 0 &&
            workingRuleIds.All(id => schemaProposal.SelectedWorkingRuleIds.Contains(id, StringComparer.Ordinal));
        var activeMatchesProposal = proposalIncludesRules && activeSchema is not null && schemaProposal is not null &&
            activeSchema.SourceSchemaProposalId == schemaProposal.Id &&
            activeSchema.WorkingRuleSetRevision == schemaProposal.WorkingRuleSetRevision &&
            SemanticallySameJson(activeSchema.SchemaJson, schemaProposal.SchemaJson);
        var stage = activeMatchesProposal
            ? ExpansionProgressStage.SchemaActivated
            : proposalIncludesRules
                ? ExpansionProgressStage.SchemaProposalReady
                : latestSchemaDecision?.Kind == ExpansionSchemaDecisionKind.NoSchemaChangeRequired
                    ? ExpansionProgressStage.NoSchemaChangeRequired
                    : latestSchemaDecision?.Kind == ExpansionSchemaDecisionKind.SchemaWorkRequested && workingRuleIds.Length > 0
                        ? ExpansionProgressStage.SchemaWorkRequested
                        : workingRuleIds.Length > 0
            ? ExpansionProgressStage.WorkingRulesIncorporated
            : session?.LatestTurn?.Action == RuleDraftAction.PresentDraft
                ? ExpansionProgressStage.DraftReady
                : session is not null
                    ? ExpansionProgressStage.ClarificationInProgress
                    : ExpansionProgressStage.ReadyForClarification;
        return new ExpansionRequestProgress(
            request.Id, stage, session?.Id, draftRuleCount,
            workingRuleIds.Length > 0 ? workingRules?.Revision : null,
            workingRuleIds,
            latestSchemaDecision?.Id);
    }

    public static ExpansionSchemaDecision DecideSchema(
        ExpansionRequest request,
        ExpansionRequestProgress progress,
        ExpansionSchemaDecisionKind kind,
        string? note,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.RequestId != request.Id || progress.WorkingRuleSetRevision is null || progress.WorkingRuleIds.Count == 0)
            throw new ArgumentException("The expansion must be incorporated into Working Rules before deciding schema work.");
        if (kind == ExpansionSchemaDecisionKind.NoSchemaChangeRequired && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Explain why these rules require no schema change.", nameof(note));
        return new ExpansionSchemaDecision(
            $"expansion-schema-decision-{Guid.NewGuid():N}", request.ProjectId, request.Id, kind,
            progress.WorkingRuleSetRevision.Value, progress.WorkingRuleIds.ToArray(),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(), now);
    }

    /// <summary>Prepares only a proposal; normal transition review and explicit activation remain required.</summary>
    public static ProjectSchemaProposal PrepareRuleContextUpdate(
        ExpansionSchemaDecision decision, WorkingRuleSet rules, ActiveGameSchema active,
        ProjectSchemaProposal savedProposal, DateTimeOffset now,
        IReadOnlyList<SchemaChangeRequest>? schemaChangeRequests = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(savedProposal);
        if (decision.Kind != ExpansionSchemaDecisionKind.NoSchemaChangeRequired ||
            decision.ProjectId != rules.ProjectId || active.ProjectId != rules.ProjectId || savedProposal.ProjectId != rules.ProjectId ||
            decision.WorkingRuleSetRevision != rules.Revision || rules.Id != active.WorkingRuleSetId || rules.Id != savedProposal.WorkingRuleSetId ||
            rules.Revision <= active.WorkingRuleSetRevision)
            throw new ArgumentException("A current no-schema-change decision and a newer revision of this project's Working Rules are required.");
        if (savedProposal.Id != active.SourceSchemaProposalId || !SemanticallySameJson(savedProposal.SchemaJson, active.SchemaJson))
            throw new ArgumentException("The saved proposal has pending structural changes or differs from the active proposal. Review those changes before preparing a rules-only update.");
        var ruleIds = rules.Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        // Schema evolution retains designer-change IDs as provenance. Resolve those
        // against persisted requests, never by accepting an ID prefix alone.
        var citationIds = ruleIds.Concat((schemaChangeRequests ?? [])
            .Where(request => request.ProjectId == rules.ProjectId &&
                request.Status == SchemaChangeRequestStatus.Applied && request.AppliedAt.HasValue)
            .Select(request => request.Id)).ToHashSet(StringComparer.Ordinal);
        if (decision.WorkingRuleIds.Count == 0 || decision.WorkingRuleIds.Any(id => !ruleIds.Contains(id)) ||
            savedProposal.SelectedWorkingRuleIds.Any(id => !citationIds.Contains(id)) ||
            savedProposal.DefinitionSources.SelectMany(source => source.WorkingRuleIds).Any(id => !citationIds.Contains(id)))
            throw new ArgumentException("The reviewed expansion must cite current Working Rules. Existing proposal citations must resolve to current Working Rules or saved, applied schema changes from this project.");
        return savedProposal with
        {
            WorkingRuleSetRevision = rules.Revision,
            SelectedWorkingRuleIds = SchemaProposalMerger.MergeWorkingRuleIds(savedProposal.SelectedWorkingRuleIds, decision.WorkingRuleIds),
            UpdatedAt = now
        };
    }

    private static bool SemanticallySameJson(string left, string right)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right)); }
        catch (JsonException) { return false; }
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
        "This source is an accepted development goal, not approved game mechanics. On the first turn, ask focused clarification questions about the rules, boundaries, dependencies, validation needs, and intended player-facing result. For a missing design choice, offer at most two clearly labeled Proposed Options with concise tradeoffs for the designer to select, revise, or reject. Goal acceptance is not mechanic approval. Do not present a rule draft until the designer has explicitly supplied or selected the mechanics. Later drafts may use only the expansion request's established facts and the saved designer question-and-answer choices as evidence; never adopt rejected or unselected options.";

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
