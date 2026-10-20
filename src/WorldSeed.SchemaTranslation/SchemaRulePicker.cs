using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

/// <summary>
/// Small, deterministic presentation helper for choosing a bounded, related set of Working Rules.
/// It deliberately contains no UI framework dependency so its grouping and selection limits are testable.
/// </summary>
public static class SchemaRulePicker
{
    private static readonly WorkingRuleKind[] DisplayOrder =
    [
        WorkingRuleKind.Definition,
        WorkingRuleKind.Rule,
        WorkingRuleKind.Procedure,
        WorkingRuleKind.Constraint
    ];

    public static IReadOnlyList<SchemaRuleGroup> Group(IReadOnlyList<WorkingRule> rules) =>
        DisplayOrder
            .Select(kind => new SchemaRuleGroup(kind, rules
                .Where(rule => rule.Kind == kind)
                .OrderBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()))
            .Where(group => group.Rules.Count > 0)
            .ToArray();

    /// <summary>Removes only a redundant leading kind label because the surrounding group already supplies it.</summary>
    public static string DisplayName(WorkingRule rule)
    {
        var prefix = rule.Kind + " - ";
        return rule.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? rule.Name[prefix.Length..].Trim()
            : rule.Name;
    }

    /// <summary>Changes a selection only when it remains within the caller-supplied bound.</summary>
    public static bool TrySetSelected(ISet<string> selectedIds, string ruleId, bool selected, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        if (!selected)
        {
            selectedIds.Remove(ruleId);
            return true;
        }
        if (selectedIds.Contains(ruleId)) return true;
        if (selectedIds.Count >= maximum) return false;
        selectedIds.Add(ruleId);
        return true;
    }
}

public sealed record SchemaRuleGroup(WorkingRuleKind Kind, IReadOnlyList<WorkingRule> Rules);
