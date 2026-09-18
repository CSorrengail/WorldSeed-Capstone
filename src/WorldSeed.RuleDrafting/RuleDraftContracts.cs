using WorldSeed.LlmIntegration;

namespace WorldSeed.RuleDrafting;

public enum RuleDraftAction { AskClarifyingQuestion, PresentDraft }
public enum RuleStatementKind { Definition, Rule, Procedure, Constraint }

/// <summary>A human-readable intermediate artifact; it is not a canonical game schema.</summary>
public sealed record StructuredRuleDraft(
    string Title,
    string Intent,
    IReadOnlyList<RuleStatement> Rules,
    IReadOnlyList<RuleConcept> Concepts,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Exclusions);

public sealed record RuleStatement(string Id, string Name, RuleStatementKind Kind, string Text, IReadOnlyList<string> SourceNoteIds);
public sealed record RuleConcept(string Id, string Name, string Description);
public sealed record RuleDraftTurn(RuleDraftAction Action, string? ClarifyingQuestion, StructuredRuleDraft? Draft);

/// <summary>Conversation context supplied by GUI or service code. Source IDs connect later to the change ledger.</summary>
public sealed record RuleDraftConversation(
    string ProjectId,
    IReadOnlyList<string> SourceMaterialIds,
    IReadOnlyList<LlmMessage> Messages);

public sealed class RuleDraftFormatException(string message) : Exception(message);
