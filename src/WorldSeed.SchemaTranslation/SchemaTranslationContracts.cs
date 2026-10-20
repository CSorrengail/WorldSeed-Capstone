using System.Text.Json.Nodes;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

public enum SchemaTranslationAction { AskClarifyingQuestion, PresentProposal }
public sealed record DefinitionSource(string DefinitionId, IReadOnlyList<string> WorkingRuleIds);
/// <summary>A designer answer used only to resolve a bounded schema proposal; it is retained with the resulting proposal.</summary>
public sealed record SchemaClarification(string Question, string Answer);
/// <summary>A non-canonical candidate schema accompanied by links back to the precise Working Rule Set revision that informed it.</summary>
public sealed record SchemaTranslationProposal(string Summary, JsonObject Schema, IReadOnlyList<DefinitionSource> DefinitionSources, string WorkingRuleSetId, int WorkingRuleSetRevision);
public sealed record SchemaTranslationTurn(SchemaTranslationAction Action, string? ClarifyingQuestion, SchemaTranslationProposal? Proposal);
public sealed record SchemaTranslationRequest(WorkingRuleSet WorkingRuleSet, JsonObject? BaseSchema = null, IReadOnlyList<SchemaClarification>? Clarifications = null);
public sealed class SchemaTranslationFormatException(string message) : Exception(message);

/// <summary>One mutable, non-canonical schema proposal for a project, stored separately from its Working Rule Set.</summary>
public sealed record ProjectSchemaProposal(
    string Id,
    string ProjectId,
    string FormatVersion,
    string Summary,
    string SchemaJson,
    IReadOnlyList<DefinitionSource> DefinitionSources,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    IReadOnlyList<string> SelectedWorkingRuleIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SchemaClarification>? Clarifications = null);

public interface IProjectSchemaProposalStore
{
    Task SaveAsync(ProjectSchemaProposal proposal, CancellationToken cancellationToken = default);
    Task<ProjectSchemaProposal?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}
