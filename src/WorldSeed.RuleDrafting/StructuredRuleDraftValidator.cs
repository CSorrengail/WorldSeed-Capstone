namespace WorldSeed.RuleDrafting;

public sealed class StructuredRuleDraftValidator
{
    public IReadOnlyList<string> Validate(StructuredRuleDraft draft, bool requireRules, IReadOnlySet<string> allowedSourceNoteIds)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Title)) issues.Add("Draft title is required.");
        if (string.IsNullOrWhiteSpace(draft.Intent)) issues.Add("Draft intent is required.");
        if (requireRules && draft.Rules.Count == 0) issues.Add("A presented draft requires at least one rule statement.");
        AddDuplicateIssues(draft.Rules.Select(rule => rule.Id), "rule statement", issues);
        AddDuplicateIssues(draft.Concepts.Select(concept => concept.Id), "concept", issues);
        foreach (var rule in draft.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.Text)) issues.Add("Each rule statement requires an id, name, and text.");
            if (rule.SourceNoteIds.Count == 0) issues.Add($"Rule statement '{rule.Id}' requires at least one source note id.");
            foreach (var sourceNoteId in rule.SourceNoteIds.Where(sourceNoteId => !allowedSourceNoteIds.Contains(sourceNoteId))) issues.Add($"Rule statement '{rule.Id}' cites source note '{sourceNoteId}', which is not available in this conversation.");
        }
        foreach (var concept in draft.Concepts) if (string.IsNullOrWhiteSpace(concept.Id) || string.IsNullOrWhiteSpace(concept.Name) || string.IsNullOrWhiteSpace(concept.Description)) issues.Add("Each concept requires an id, name, and description.");
        return issues;
    }

    private static void AddDuplicateIssues(IEnumerable<string> ids, string kind, ICollection<string> issues)
    {
        foreach (var id in ids.Where(string.IsNullOrWhiteSpace).Concat(ids.Where(id => !string.IsNullOrWhiteSpace(id)).GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key))) issues.Add($"Every {kind} requires a unique non-empty id.");
    }
}
