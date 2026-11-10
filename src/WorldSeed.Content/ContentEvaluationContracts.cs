using System.Text.Json.Nodes;
using WorldSeed.GameData;

namespace WorldSeed.Content;

/// <summary>
/// A proposal remains outside canonical game data until a later designer decision
/// accepts it. Its entity type is game-owned; WorldSeed does not name it Creature.
/// </summary>
public sealed record ContentProposal(
    string Id,
    string ProjectId,
    string EntityTypeId,
    JsonObject Candidate,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    int Version,
    DateTimeOffset CreatedAt,
    string? ReplacesProposalId = null,
    IReadOnlyList<ContentSchemaGap>? SchemaGaps = null,
    string? DesignerRequest = null);

/// <summary>An advisory model observation that a designer request exceeds the active game-owned schema.</summary>
public sealed record ContentSchemaGap(string RequestedConcept, string Reason, string? SuggestedSchemaChange = null);

public enum ContentProposalDecisionKind { Approved, Rejected, RevisionRequested }

/// <summary>Append-only designer feedback kept outside canonical schema and game data.</summary>
public sealed record ContentProposalDecision(
    string Id,
    string ProjectId,
    string ProposalId,
    int ProposalVersion,
    ContentProposalDecisionKind Kind,
    string? Note,
    string? ReplacementProposalId,
    DateTimeOffset CreatedAt);

public interface IContentProposalStore
{
    Task SaveProposalAsync(ContentProposal proposal, CancellationToken cancellationToken = default);
    Task<ContentProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveDecisionAsync(ContentProposalDecision decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentProposalDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public enum BalanceMeasure { NumericValue, CollectionCount }

/// <summary>A named reference is explanatory evidence, never a cross-game formula.</summary>
public sealed record BalanceReference(string Id, string Label, decimal Value, string? Rationale = null);

/// <summary>
/// A game-owned advisory threshold. Hard requirements belong in the active schema
/// and are reported separately by structural validation.
/// </summary>
public sealed record BalanceSignal(
    string Id,
    string PropertyId,
    BalanceMeasure Measure,
    decimal? MinimumReferenceValue,
    decimal? MaximumReferenceValue,
    string Message,
    IReadOnlyList<BalanceReference> References);

public sealed record ContentBalanceProfile(
    string Id,
    string EntityTypeId,
    IReadOnlyList<string> SourceWorkingRuleIds,
    IReadOnlyList<BalanceSignal> Signals);

/// <summary>A versioned, project-owned profile tied to the exact schema revision it can evaluate.</summary>
public sealed record ProjectContentBalanceProfile(
    string Id,
    string ProjectId,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    ContentBalanceProfile Profile,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IContentBalanceProfileStore
{
    Task SaveAsync(ProjectContentBalanceProfile profile, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectContentBalanceProfile>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public sealed record MechanicalViolation(string Path, string Message);
public sealed record BalanceConcern(string SignalId, string EntityInstanceId, string PropertyId, decimal ObservedValue, string Message, IReadOnlyList<BalanceReference> References);
public sealed record ContentEvaluationResult(IReadOnlyList<MechanicalViolation> MechanicalViolations, IReadOnlyList<BalanceConcern> BalanceConcerns)
{
    public bool IsMechanicallyValid => MechanicalViolations.Count == 0;
}

public sealed record ContentEvaluationRequest(ActiveGameSchema ActiveSchema, ProjectGameData GameData, ContentBalanceProfile BalanceProfile);
