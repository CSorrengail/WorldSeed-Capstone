using System.Text.Json;
using WorldSeed.DesignSessions;
using WorldSeed.RuleDrafting;

namespace WorldSeed.WorkingRules;

public sealed record WorkingRuleSetValidationIssue(string Path, string Message);
public sealed class WorkingRuleSetValidationResult
{
    public List<WorkingRuleSetValidationIssue> Issues { get; } = [];
    public bool IsValid => Issues.Count == 0;
    public void Add(string path, string message) => Issues.Add(new WorkingRuleSetValidationIssue(path, message));
}

/// <summary>Validates working-rule structure and verifies every cited draft rule and literal source passage against a project.</summary>
public sealed class WorkingRuleSetValidator
{
    public WorkingRuleSetValidationResult Validate(WorkingRuleSet? ruleSet, DesignProject? project)
    {
        var result = new WorkingRuleSetValidationResult();
        if (ruleSet is null) { result.Add("$", "Working rule set is required."); return result; }
        if (project is null) { result.Add("$", "A project is required to validate working-rule provenance."); return result; }
        if (!string.Equals(ruleSet.ProjectId, project.Id, StringComparison.Ordinal)) result.Add("$.projectId", "Working rule set must belong to the supplied project.");
        Require(ruleSet.Id, "$.id", "Working rule set id is required.", result);
        Require(ruleSet.Name, "$.name", "Working rule set name is required.", result);
        if (!string.Equals(ruleSet.FormatVersion, "0.1", StringComparison.Ordinal)) result.Add("$.formatVersion", "Working rule set formatVersion must be '0.1'.");
        if (ruleSet.Revision < 1) result.Add("$.revision", "Working rule set revision must be at least 1.");
        if (ruleSet.UpdatedAt < ruleSet.CreatedAt) result.Add("$.updatedAt", "updatedAt cannot be before createdAt.");

        var context = ValidationContext.FromProject(project);
        RequireCollection(ruleSet.Rules, "$.rules", "rules is required.", result);
        RequireCollection(ruleSet.Concepts, "$.concepts", "concepts is required.", result);
        RequireCollection(ruleSet.OpenQuestions, "$.openQuestions", "openQuestions is required.", result);
        RequireCollection(ruleSet.Exclusions, "$.exclusions", "exclusions is required.", result);
        RequireCollection(ruleSet.Conflicts, "$.conflicts", "conflicts is required.", result);
        var rules = ruleSet.Rules ?? [];
        var concepts = ruleSet.Concepts ?? [];
        var ruleIds = rules.Select(rule => rule.Id).ToArray();
        AddDuplicates(ruleIds, "$.rules", "rule id", result);
        var conceptIds = concepts.Select(concept => concept.Id).ToHashSet(StringComparer.Ordinal);
        AddDuplicates(concepts.Select(concept => concept.Id), "$.concepts", "concept id", result);
        foreach (var concept in concepts)
            ValidateConcept(concept, "$.concepts", context, result);

        foreach (var rule in rules)
            ValidateRule(rule, "$.rules", conceptIds, context, ruleIds.ToHashSet(StringComparer.Ordinal), result);
        ValidateStrings(ruleSet.OpenQuestions ?? [], "$.openQuestions", result);
        ValidateStrings(ruleSet.Exclusions ?? [], "$.exclusions", result);
        ValidateStrings(ruleSet.Conflicts ?? [], "$.conflicts", result);
        return result;
    }

    private static void ValidateConcept(WorkingRuleConcept concept, string path, ValidationContext context, WorkingRuleSetValidationResult result)
    {
        Require(concept.Id, path, "Concept id is required.", result);
        Require(concept.Name, path, "Concept name is required.", result);
        Require(concept.Description, path, "Concept description is required.", result);
        RequireCollection(concept.Evidence, path + ".evidence", "Concept evidence is required.", result);
        foreach (var evidence in concept.Evidence ?? []) ValidateEvidence(evidence, path, context, result);
    }

    private static void ValidateRule(WorkingRule rule, string path, IReadOnlySet<string> conceptIds, ValidationContext context, IReadOnlySet<string> workingRuleIds, WorkingRuleSetValidationResult result)
    {
        var rulePath = $"{path}[{rule.Id}]";
        Require(rule.Id, rulePath, "Rule id is required.", result);
        Require(rule.Name, rulePath, "Rule name is required.", result);
        Require(rule.AuthoritativeText, rulePath, "Rule authoritativeText is required.", result);
        RequireCollection(rule.Evidence, rulePath + ".evidence", "Rule evidence is required.", result);
        RequireCollection(rule.SourceDraftRules, rulePath + ".sourceDraftRules", "Rule sourceDraftRules is required.", result);
        RequireCollection(rule.ConceptIds, rulePath + ".conceptIds", "Rule conceptIds is required.", result);
        RequireCollection(rule.SchemaHints, rulePath + ".schemaHints", "Rule schemaHints is required.", result);
        RequireCollection(rule.OpenQuestions, rulePath + ".openQuestions", "Rule openQuestions is required.", result);
        RequireCollection(rule.Exclusions, rulePath + ".exclusions", "Rule exclusions is required.", result);
        var evidence = rule.Evidence ?? [];
        var sourceDraftRules = rule.SourceDraftRules ?? [];
        var conceptReferences = rule.ConceptIds ?? [];
        var schemaHints = rule.SchemaHints ?? [];
        if (evidence.Count == 0) result.Add(rulePath + ".evidence", "Every working rule requires at least one exact source evidence item.");
        foreach (var item in evidence) ValidateEvidence(item, rulePath + ".evidence", context, result);
        if (sourceDraftRules.Count == 0) result.Add(rulePath + ".sourceDraftRules", "Every working rule requires at least one completed source-draft rule reference.");
        foreach (var reference in sourceDraftRules)
        {
            if (!context.DraftRuleReferences.Contains(reference)) result.Add(rulePath + ".sourceDraftRules", $"Draft rule '{reference.SessionId}/{reference.RuleId}' does not exist in a completed project draft.");
        }
        foreach (var conceptId in conceptReferences.Where(id => !conceptIds.Contains(id))) result.Add(rulePath + ".conceptIds", $"Unknown project concept '{conceptId}'.");
        foreach (var hint in schemaHints) ValidateHint(hint, rulePath + ".schemaHints", workingRuleIds, result);
        ValidateSupport(rule.StructuredSupport, rulePath + ".structuredSupport", result);
        ValidateStrings(rule.OpenQuestions ?? [], rulePath + ".openQuestions", result);
        ValidateStrings(rule.Exclusions ?? [], rulePath + ".exclusions", result);
    }

    private static void ValidateEvidence(WorkingRuleEvidence evidence, string path, ValidationContext context, WorkingRuleSetValidationResult result)
    {
        Require(evidence.SourceNoteId, path, "Evidence sourceNoteId is required.", result);
        Require(evidence.Excerpt, path, "Evidence excerpt is required.", result);
        if (!context.SourceTextById.TryGetValue(evidence.SourceNoteId, out var source)) { result.Add(path, $"Evidence source note '{evidence.SourceNoteId}' is not part of the project."); return; }
        if (evidence.PassageId is not null)
        {
            if (!context.PassagesById.TryGetValue(evidence.PassageId, out var passage) || !string.Equals(passage.SourceNoteId, evidence.SourceNoteId, StringComparison.Ordinal)) result.Add(path, $"Evidence passage '{evidence.PassageId}' does not belong to source note '{evidence.SourceNoteId}'.");
            else if (!string.Equals(passage.Text, evidence.Excerpt, StringComparison.Ordinal)) result.Add(path, "Evidence excerpt must exactly match its cited source passage.");
        }
        else if (!source.Contains(evidence.Excerpt, StringComparison.Ordinal)) result.Add(path, "Evidence excerpt does not occur in its cited source note.");
    }

    private static void ValidateHint(SchemaHint hint, string path, IReadOnlySet<string> workingRuleIds, WorkingRuleSetValidationResult result)
    {
        Require(hint.SuggestedId, path, "Schema hint suggestedId is required.", result);
        Require(hint.SuggestedName, path, "Schema hint suggestedName is required.", result);
        Require(hint.Rationale, path, "Schema hint rationale is required.", result);
        RequireCollection(hint.SourceWorkingRuleIds, path + ".sourceWorkingRuleIds", "Schema hint sourceWorkingRuleIds is required.", result);
        var sourceWorkingRuleIds = hint.SourceWorkingRuleIds ?? [];
        if (sourceWorkingRuleIds.Count == 0) result.Add(path, "Schema hint requires at least one source working rule.");
        foreach (var ruleId in sourceWorkingRuleIds.Where(id => !workingRuleIds.Contains(id))) result.Add(path, $"Schema hint cites unknown working rule '{ruleId}'.");
    }

    private static void ValidateSupport(WorkingRuleStructuredSupport? support, string path, WorkingRuleSetValidationResult result)
    {
        if (support is null) return;
        RequireCollection(support.Limitations, path + ".limitations", "structuredSupport limitations is required.", result);
        if (support.AutomationStatus == WorkingRuleAutomationStatus.Executable) result.Add(path, "Executable structured support is not permitted in Working Rule Set v0.1 because WorldSeed has no named interpreter yet.");
        if (support.AutomationStatus != WorkingRuleAutomationStatus.None)
        {
            Require(support.RepresentationKind, path, "Non-empty structured support requires representationKind.", result);
            Require(support.RepresentationVersion, path, "Non-empty structured support requires representationVersion.", result);
        }
        if (!string.IsNullOrWhiteSpace(support.PayloadJson))
        {
            try { JsonDocument.Parse(support.PayloadJson); }
            catch (JsonException) { result.Add(path, "structuredSupport payloadJson must be valid JSON when supplied."); }
        }
        ValidateStrings(support.Limitations ?? [], path + ".limitations", result);
    }

    private static void ValidateStrings(IEnumerable<string> values, string path, WorkingRuleSetValidationResult result)
    {
        if (values.Any(string.IsNullOrWhiteSpace)) result.Add(path, "Values may not be blank.");
    }

    private static void AddDuplicates(IEnumerable<string> values, string path, string label, WorkingRuleSetValidationResult result)
    {
        foreach (var duplicate in values.Where(value => !string.IsNullOrWhiteSpace(value)).GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1)) result.Add(path, $"Duplicate {label} '{duplicate.Key}'.");
    }

    private static void Require(string? value, string path, string message, WorkingRuleSetValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value)) result.Add(path, message);
    }

    private static void RequireCollection<T>(IReadOnlyList<T>? values, string path, string message, WorkingRuleSetValidationResult result)
    {
        if (values is null) result.Add(path, message);
    }

    private sealed record ValidationContext(IReadOnlyDictionary<string, string> SourceTextById, IReadOnlyDictionary<string, SourcePassage> PassagesById, IReadOnlySet<DraftRuleReference> DraftRuleReferences)
    {
        public static ValidationContext FromProject(DesignProject project)
        {
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var source in project.SourceNotes.Concat(project.Sessions.SelectMany(session => session.SourceNotes)))
                if (!sources.ContainsKey(source.Id)) sources[source.Id] = source.OriginalText;
            var passages = SourcePassageCatalog.Create(sources).ToDictionary(passage => passage.Id, StringComparer.Ordinal);
            var drafts = project.Sessions
                .Where(session => session.LatestTurn?.Draft is not null)
                .SelectMany(session => session.LatestTurn!.Draft!.Rules.Select(rule => new DraftRuleReference(session.Id, rule.Id)))
                .ToHashSet();
            return new ValidationContext(sources, passages, drafts);
        }
    }
}
