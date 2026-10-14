namespace WorldSeed.WorkingRules;

public enum WorkingRuleKind { Definition, Rule, Procedure, Constraint }
public enum WorkingRuleAutomationStatus { None, Proposed, Partial, Reviewed, Executable }
public enum SchemaHintKind { EntityType, ValueType, SchemaFragment, Property, Relationship, Rule, Procedure, Validation }

/// <summary>Identifies a rule statement from a completed source-interpretation session. Session scope avoids collisions between model-generated rule IDs.</summary>
public sealed record DraftRuleReference(string SessionId, string RuleId);
/// <summary>Literal evidence preserved from a project source note. A passage ID is preferred when available.</summary>
public sealed record WorkingRuleEvidence(string SourceNoteId, string? PassageId, string Excerpt);
public sealed record WorkingRuleConcept(string Id, string Name, string Description, IReadOnlyList<WorkingRuleEvidence> Evidence);
/// <summary>A non-authoritative suggestion for later schema translation; it never creates a schema definition on its own.</summary>
public sealed record SchemaHint(SchemaHintKind Kind, string SuggestedId, string SuggestedName, string? ParentSuggestedId, string Rationale, IReadOnlyList<string> SourceWorkingRuleIds);
/// <summary>Optional machine-oriented support. PayloadJson is opaque to v0.1 and cannot be executed by WorldSeed yet.</summary>
public sealed record WorkingRuleStructuredSupport(WorkingRuleAutomationStatus AutomationStatus, string? RepresentationKind, string? RepresentationVersion, string? PayloadJson, IReadOnlyList<string> Limitations);

public sealed record WorkingRule(
    string Id,
    string Name,
    WorkingRuleKind Kind,
    string AuthoritativeText,
    IReadOnlyList<WorkingRuleEvidence> Evidence,
    IReadOnlyList<DraftRuleReference> SourceDraftRules,
    IReadOnlyList<string> ConceptIds,
    IReadOnlyList<SchemaHint> SchemaHints,
    WorkingRuleStructuredSupport? StructuredSupport,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Exclusions);

/// <summary>The single mutable, non-canonical project proposal used by schema translation in the MVP.</summary>
public sealed record WorkingRuleSet(
    string Id,
    string ProjectId,
    string FormatVersion,
    string Name,
    string? Description,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkingRule> Rules,
    IReadOnlyList<WorkingRuleConcept> Concepts,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> Conflicts);

public interface IWorkingRuleSetStore
{
    Task SaveAsync(WorkingRuleSet ruleSet, CancellationToken cancellationToken = default);
    Task<WorkingRuleSet?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}
