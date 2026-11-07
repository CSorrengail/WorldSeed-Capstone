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
    DateTimeOffset CreatedAt);

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

public sealed record MechanicalViolation(string Path, string Message);
public sealed record BalanceConcern(string SignalId, string EntityInstanceId, string PropertyId, decimal ObservedValue, string Message, IReadOnlyList<BalanceReference> References);
public sealed record ContentEvaluationResult(IReadOnlyList<MechanicalViolation> MechanicalViolations, IReadOnlyList<BalanceConcern> BalanceConcerns)
{
    public bool IsMechanicallyValid => MechanicalViolations.Count == 0;
}

public sealed record ContentEvaluationRequest(ActiveGameSchema ActiveSchema, ProjectGameData GameData, ContentBalanceProfile BalanceProfile);
