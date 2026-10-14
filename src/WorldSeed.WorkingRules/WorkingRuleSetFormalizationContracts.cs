using WorldSeed.DesignSessions;

namespace WorldSeed.WorkingRules;

/// <summary>Returned when an LLM has consolidated completed source drafts into the project's current working proposal.</summary>
public sealed record WorkingRuleSetFormalizationResult(WorkingRuleSet RuleSet, string RawModelResponse);

/// <summary>Indicates that a model response could not be safely converted into a traceable working rule set.</summary>
public sealed class WorkingRuleSetFormatException(string message) : Exception(message);

/// <summary>A completed source-draft statement together with its session-scoped identity.</summary>
public sealed record CompletedDraftRule(DraftRuleReference Reference, RuleDrafting.RuleStatement Rule);

internal static class CompletedDraftCatalog
{
    public static IReadOnlyList<CompletedDraftRule> Create(DesignProject project) => project.Sessions
        .Where(session => session.LatestTurn is { Action: RuleDrafting.RuleDraftAction.PresentDraft, Draft: not null })
        .SelectMany(session => session.LatestTurn!.Draft!.Rules.Select(rule => new CompletedDraftRule(new DraftRuleReference(session.Id, rule.Id), rule)))
        .ToArray();
}
