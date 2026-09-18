namespace WorldSeed.RuleDrafting;

public sealed class StructuredRuleDraftValidator
{
    public IReadOnlyList<string> Validate(StructuredRuleDraft draft, bool requireRules, IReadOnlySet<string> allowedSourceNoteIds, IReadOnlyDictionary<string, string>? sourceTextById = null)
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
            if (sourceTextById is null || sourceTextById.Count == 0) continue;
            if (rule.SourceSupport.Count == 0) issues.Add($"Rule statement '{rule.Id}' requires at least one supporting excerpt.");
            foreach (var support in rule.SourceSupport)
            {
                if (!rule.SourceNoteIds.Contains(support.SourceNoteId, StringComparer.Ordinal)) issues.Add($"Supporting excerpt for rule statement '{rule.Id}' must use one of its source note ids.");
                if (!sourceTextById.TryGetValue(support.SourceNoteId, out var sourceText)) issues.Add($"Supporting excerpt for rule statement '{rule.Id}' cites an unavailable source note.");
                else if (string.IsNullOrWhiteSpace(support.Excerpt) || !Normalize(sourceText).Contains(Normalize(support.Excerpt), StringComparison.Ordinal)) issues.Add($"Supporting excerpt for rule statement '{rule.Id}' does not occur in its cited source note.");
            }
        }
        foreach (var concept in draft.Concepts) if (string.IsNullOrWhiteSpace(concept.Id) || string.IsNullOrWhiteSpace(concept.Name) || string.IsNullOrWhiteSpace(concept.Description)) issues.Add("Each concept requires an id, name, and description.");
        return issues;
    }

    private static void AddDuplicateIssues(IEnumerable<string> ids, string kind, ICollection<string> issues)
    {
        foreach (var id in ids.Where(string.IsNullOrWhiteSpace).Concat(ids.Where(id => !string.IsNullOrWhiteSpace(id)).GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key))) issues.Add($"Every {kind} requires a unique non-empty id.");
    }

    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
