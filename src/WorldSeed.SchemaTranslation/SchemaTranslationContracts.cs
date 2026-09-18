using System.Text.Json.Nodes;
using WorldSeed.RuleDrafting;

namespace WorldSeed.SchemaTranslation;

public enum SchemaTranslationAction { AskClarifyingQuestion, PresentProposal }
public sealed record DefinitionSource(string DefinitionId, IReadOnlyList<string> RuleIds);
/// <summary>A non-canonical candidate schema accompanied by links back to structured rule statements.</summary>
public sealed record SchemaTranslationProposal(string Summary, JsonObject Schema, IReadOnlyList<DefinitionSource> DefinitionSources);
public sealed record SchemaTranslationTurn(SchemaTranslationAction Action, string? ClarifyingQuestion, SchemaTranslationProposal? Proposal);
public sealed record SchemaTranslationRequest(StructuredRuleDraft Draft, JsonObject? BaseSchema = null);
public sealed class SchemaTranslationFormatException(string message) : Exception(message);
