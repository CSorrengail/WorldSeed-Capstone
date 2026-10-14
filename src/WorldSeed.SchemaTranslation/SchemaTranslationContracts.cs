using System.Text.Json.Nodes;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

public enum SchemaTranslationAction { AskClarifyingQuestion, PresentProposal }
public sealed record DefinitionSource(string DefinitionId, IReadOnlyList<string> WorkingRuleIds);
/// <summary>A non-canonical candidate schema accompanied by links back to the precise Working Rule Set revision that informed it.</summary>
public sealed record SchemaTranslationProposal(string Summary, JsonObject Schema, IReadOnlyList<DefinitionSource> DefinitionSources, string WorkingRuleSetId, int WorkingRuleSetRevision);
public sealed record SchemaTranslationTurn(SchemaTranslationAction Action, string? ClarifyingQuestion, SchemaTranslationProposal? Proposal);
public sealed record SchemaTranslationRequest(WorkingRuleSet WorkingRuleSet, JsonObject? BaseSchema = null);
public sealed class SchemaTranslationFormatException(string message) : Exception(message);
