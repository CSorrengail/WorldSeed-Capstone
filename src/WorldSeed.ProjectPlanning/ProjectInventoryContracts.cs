using WorldSeed.Content;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.Publication;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.ProjectPlanning;

public sealed record ProjectInventoryBinding(
    DateTimeOffset ProjectUpdatedAt,
    string? WorkingRuleSetId,
    int? WorkingRuleSetRevision,
    string? SchemaProposalId,
    DateTimeOffset? SchemaProposalUpdatedAt,
    string? ActiveSchemaId,
    int? ActiveSchemaRevision,
    string? GameDataId,
    DateTimeOffset? GameDataUpdatedAt,
    string? LatestPublicationSnapshotId,
    string? LatestPublicationFingerprint);

public sealed record InventorySourceCoverage(
    int SourceCount,
    int SourceCharacterCount,
    int CompletedDraftCount,
    IReadOnlyList<string> UnreferencedSourceIds);

public sealed record InventoryRuleCoverage(
    int RuleCount,
    IReadOnlyDictionary<string, int> CountsByKind,
    int EvidenceBackedRuleCount,
    IReadOnlyList<string> RulesWithoutEvidence,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Exclusions);

public sealed record InventoryContentTypeCoverage(
    string Id,
    string Name,
    int PropertyCount,
    int RequiredPropertyCount,
    int CanonicalEntryCount,
    int ProposalCount,
    int ApprovedProposalCount,
    int AppliedProposalCount,
    bool HasCurrentBalanceProfile);

public sealed record InventorySchemaCoverage(
    IReadOnlyDictionary<string, int> DefinitionCounts,
    IReadOnlyList<InventoryContentTypeCoverage> ContentTypes,
    int ValidationCount,
    bool ProposalDiffersFromActive);

public sealed record InventoryPublicationCoverage(
    int SnapshotCount,
    string? LatestSnapshotId,
    DateTimeOffset? LatestSnapshotAt,
    int PublishedCanonicalEntryCount);

public enum InventoryObservationKind
{
    MissingFoundation,
    UnresolvedQuestion,
    Conflict,
    MissingSourceTrace,
    EmptyContentType,
    MissingEvaluationCoverage,
    UnpublishedChanges,
    UncitedSourcePassage,
    UnincorporatedDraftRule,
    ReportedSchemaGap
}

public sealed record InventoryObservation(
    string Id,
    InventoryObservationKind Kind,
    string SubjectId,
    string Description,
    IReadOnlyList<string> EvidenceIds);

public sealed record ProjectInventory(
    string Id,
    string ProjectId,
    string FormatVersion,
    int Revision,
    DateTimeOffset CapturedAt,
    ProjectInventoryBinding Binding,
    InventorySourceCoverage Sources,
    InventoryRuleCoverage Rules,
    InventorySchemaCoverage Schema,
    InventoryPublicationCoverage Publication,
    IReadOnlyList<InventoryObservation> Observations);

public sealed record ProjectInventoryInput(
    DesignProject Project,
    WorkingRuleSet? WorkingRules,
    ProjectSchemaProposal? SchemaProposal,
    ActiveGameSchema? ActiveSchema,
    ProjectGameData? GameData,
    IReadOnlyList<ContentProposal> ContentProposals,
    IReadOnlyList<ContentProposalDecision> ContentDecisions,
    IReadOnlyList<ContentApplication> ContentApplications,
    IReadOnlyList<ProjectContentBalanceProfile> BalanceProfiles,
    IReadOnlyList<PublicationSnapshot> PublicationSnapshots);

public interface IProjectInventoryStore
{
    Task SaveAsync(ProjectInventory inventory, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectInventory>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectInventory?> LoadLatestAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}
