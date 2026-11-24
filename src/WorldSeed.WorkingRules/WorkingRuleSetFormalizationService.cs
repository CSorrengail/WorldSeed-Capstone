using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;

namespace WorldSeed.WorkingRules;

/// <summary>
/// Formalizes completed source-level drafts in small, independently valid batches. This keeps local
/// model output bounded; later stages can reconcile related working rules across the whole project.
/// </summary>
public sealed class WorkingRuleSetFormalizationService
{
    private const int MaxCitationsPerBatch = 4;
    private const int MaxBatchOutputTokens = 1800;
    private const string SystemPrompt = """
        You formalize a small, bounded batch of completed TTRPG source-draft rules into traceable working rules.
        Preserve the designer's intent. Never invent mechanics, exceptions, values, entities, or facts that are not present in the supplied source-draft rules. Do not infer relationships with rules outside this batch. If material is unclear, record it as an open question or exclusion rather than filling the gap.
        Produce exactly one concise working rule for EACH supplied source-draft rule. Account for every supplied citation ID, not just the first one. Do not merge separate source-draft rules: cross-source reconciliation happens in a later stage. Every output rule must cite exactly the short draft citation ID that supports it, such as d001. WorldSeed, not you, will attach literal evidence passages.
        Do not produce concepts or schema hints in this step. Return empty concepts and schemaHints arrays. Do not emit source quotations, executable logic, database tables, JSON Schema, approval decisions, or implementation code.
        Return JSON only, with no Markdown or surrounding explanation. Use this exact shape:
        {"name":"...","description":"... or null","rules":[{"id":"...","name":"...","kind":"definition|rule|procedure|constraint","authoritativeText":"...","sourceDraftIds":["d001"],"conceptIds":[],"schemaHints":[],"openQuestions":[],"exclusions":[]}],"concepts":[],"openQuestions":[],"exclusions":[],"conflicts":[]}
        Keep each authoritativeText to the minimum wording needed to preserve its source-draft rule.
        """;

    private readonly ILanguageModelClient _client;
    private readonly WorkingRuleSetJsonParser _parser;
    private readonly WorkingRuleSetValidator _validator = new();
    private readonly TimeProvider _timeProvider;

    public WorkingRuleSetFormalizationService(ILanguageModelClient client, WorkingRuleSetJsonParser? parser = null, TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _parser = parser ?? new WorkingRuleSetJsonParser();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<WorkingRuleSetFormalizationResult> CreateProposalAsync(DesignProject project, WorkingRuleSet? currentRuleSet = null, CancellationToken cancellationToken = default, bool addMissingOnly = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!addMissingOnly && currentRuleSet?.Reconciliations?.Any(item => item.State == RuleReconciliationState.Applied) == true)
            throw new InvalidOperationException("Rebuilding from source drafts would undo accepted rule updates. Use Add New Source Rules or reconcile existing rules instead.");
        var sourceDrafts = CompletedDraftCatalog.Create(project, latestOnly: addMissingOnly);
        if (sourceDrafts.Count == 0) throw new ArgumentException("At least one completed source draft is required before creating project rules.", nameof(project));
        if (currentRuleSet is not null && !string.Equals(currentRuleSet.ProjectId, project.Id, StringComparison.Ordinal)) throw new ArgumentException("The current working rule set belongs to a different project.", nameof(currentRuleSet));

        if (addMissingOnly && currentRuleSet is not null)
        {
            var incorporated = currentRuleSet.Rules.SelectMany(rule => rule.SourceDraftRules).ToHashSet();
            sourceDrafts = sourceDrafts.Where(draft => !incorporated.Contains(draft.Reference)).ToArray();
            if (sourceDrafts.Count == 0) return new WorkingRuleSetFormalizationResult(currentRuleSet, string.Empty, 0);
        }

        var citations = DraftCitationCatalog.Create(sourceDrafts);
        var parsedBatches = new List<WorkingRuleSet>();
        var rawResponses = new List<string>();
        foreach (var batch in citations.Chunk(MaxCitationsPerBatch))
            parsedBatches.AddRange(await FormalizeBatchAsync(project, batch, rawResponses, cancellationToken));

        var ruleSet = CombineBatches(project, currentRuleSet, parsedBatches, _timeProvider.GetUtcNow(), addMissingOnly);
        // Draft-level uncertainty is not part of the model's per-rule input. Carry it
        // forward directly rather than asking another model to rediscover or resolve it.
        var includedSessions = sourceDrafts.Select(draft => draft.Reference.SessionId).ToHashSet(StringComparer.Ordinal);
        var includedDrafts = project.Sessions.Where(session => includedSessions.Contains(session.Id))
            .Select(session => session.LatestTurn!.Draft!).ToArray();
        ruleSet = ruleSet with
        {
            Reconciliations = currentRuleSet?.Reconciliations,
            OpenQuestions = DistinctNonBlank(ruleSet.OpenQuestions.Concat(includedDrafts.SelectMany(draft => draft.OpenQuestions))
                .Concat(includedDrafts.SelectMany(draft => draft.Assumptions).Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => "Unconfirmed Source-Draft Assumption: " + value.Trim()))),
            Exclusions = DistinctNonBlank(ruleSet.Exclusions.Concat(includedDrafts.SelectMany(draft => draft.Exclusions)))
        };
        var validation = _validator.Validate(ruleSet, project);
        if (!validation.IsValid) throw new WorkingRuleSetFormatException(string.Join(" ", validation.Issues.Select(issue => issue.Message)));
        return new WorkingRuleSetFormalizationResult(ruleSet, string.Join("\n\n", rawResponses), parsedBatches.Count);
    }

    private async Task<IReadOnlyList<WorkingRuleSet>> FormalizeBatchAsync(DesignProject project, IReadOnlyList<DraftCitation> citations, ICollection<string> rawResponses, CancellationToken cancellationToken, bool singleRetry = false)
    {
        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, SystemPrompt),
            new(LlmMessageRole.System, "Completed source-draft rules available for citation:\n" + FormatSourceDrafts(citations)),
            new(LlmMessageRole.User, "Create the working-rule batch for these source-draft rules.")
        };
        if (singleRetry) messages.Add(new(LlmMessageRole.System, "The previous attempt failed validation. Return a complete replacement for this one input rule, preserving its exact citation ID. Do not omit it or cite any other rule."));
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.1, MaxOutputTokens: MaxBatchOutputTokens, RequireJsonObject: true), cancellationToken);
        rawResponses.Add($"Batch response for {string.Join(", ", citations.Select(citation => citation.Id))}:\n{response.Content}");
        WorkingRuleSet ruleSet;
        try
        {
            ruleSet = _parser.Parse(response.Content, project, citations, now: _timeProvider.GetUtcNow());
            if (citations.Count == 1) EnsureBatchCoverage(ruleSet, citations);
        }
        catch (WorkingRuleSetFormatException) when (citations.Count > 1)
        {
            var midpoint = citations.Count / 2;
            var first = await FormalizeBatchAsync(project, citations.Take(midpoint).ToArray(), rawResponses, cancellationToken);
            var second = await FormalizeBatchAsync(project, citations.Skip(midpoint).ToArray(), rawResponses, cancellationToken);
            return first.Concat(second).ToArray();
        }
        catch (WorkingRuleSetFormatException) when (!singleRetry)
        { return await FormalizeBatchAsync(project, citations, rawResponses, cancellationToken, singleRetry: true); }
        catch (WorkingRuleSetFormatException exception)
        { throw new WorkingRuleSetFormatException($"Could not prepare source rule '{citations[0].Draft.Rule.Name}' after a focused retry. Existing project rules were not changed. Model detail: {exception.Message}"); }

        var covered = ruleSet.Rules.SelectMany(rule => rule.SourceDraftRules).ToHashSet();
        var missing = citations.Where(citation => !covered.Contains(citation.Draft.Reference)).ToArray();
        if (missing.Length == 0) return [ruleSet];
        // Parsing validates all references; a nonempty result therefore reduces
        // the remaining input. Keep it and request only omitted rules.
        if (missing.Length < citations.Count)
        {
            var recovered = await FormalizeBatchAsync(project, missing, rawResponses, cancellationToken);
            return new[] { ruleSet }.Concat(recovered).ToArray();
        }
        EnsureBatchCoverage(ruleSet, citations);
        return [ruleSet];
    }

    private static WorkingRuleSet CombineBatches(DesignProject project, WorkingRuleSet? currentRuleSet, IReadOnlyList<WorkingRuleSet> batches, DateTimeOffset timestamp, bool addMissingOnly)
    {
        var rules = new List<WorkingRule>();
        var concepts = new List<WorkingRuleConcept>();
        var openQuestions = new List<string>();
        var exclusions = new List<string>();
        var conflicts = new List<string>();
        if (addMissingOnly && currentRuleSet is not null)
        {
            rules.AddRange(currentRuleSet.Rules);
            concepts.AddRange(currentRuleSet.Concepts);
            openQuestions.AddRange(currentRuleSet.OpenQuestions);
            exclusions.AddRange(currentRuleSet.Exclusions);
            conflicts.AddRange(currentRuleSet.Conflicts);
        }

        var nextPrefix = 1;
        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            string prefix;
            do { prefix = $"b{nextPrefix++:D3}-"; }
            while (rules.Any(rule => rule.Id.StartsWith(prefix, StringComparison.Ordinal)) || concepts.Any(concept => concept.Id.StartsWith(prefix, StringComparison.Ordinal)));
            var ruleIds = batch.Rules.ToDictionary(rule => rule.Id, rule => prefix + rule.Id, StringComparer.Ordinal);
            var conceptIds = batch.Concepts.ToDictionary(concept => concept.Id, concept => prefix + concept.Id, StringComparer.Ordinal);
            concepts.AddRange(batch.Concepts.Select(concept => concept with { Id = conceptIds[concept.Id] }));
            rules.AddRange(batch.Rules.Select(rule => rule with
            {
                Id = ruleIds[rule.Id],
                ConceptIds = rule.ConceptIds.Select(id => conceptIds[id]).ToArray(),
                SchemaHints = rule.SchemaHints.Select(hint => hint with { SourceWorkingRuleIds = hint.SourceWorkingRuleIds.Select(id => ruleIds[id]).ToArray() }).ToArray()
            }));
            openQuestions.AddRange(batch.OpenQuestions);
            exclusions.AddRange(batch.Exclusions);
            conflicts.AddRange(batch.Conflicts);
        }

        return new WorkingRuleSet(
            currentRuleSet?.Id ?? "working-rules-" + project.Id,
            project.Id,
            "0.1",
            currentRuleSet?.Name ?? project.Name + " Working Rules",
            currentRuleSet?.Description ?? "Traceable project working proposal assembled from bounded source-draft batches.",
            currentRuleSet is null ? 1 : currentRuleSet.Revision + 1,
            currentRuleSet?.CreatedAt ?? timestamp,
            timestamp,
            rules,
            concepts,
            DistinctNonBlank(openQuestions),
            DistinctNonBlank(exclusions),
            DistinctNonBlank(conflicts));
    }

    private static void EnsureBatchCoverage(WorkingRuleSet ruleSet, IReadOnlyList<DraftCitation> citations)
    {
        var citedReferences = ruleSet.Rules.SelectMany(rule => rule.SourceDraftRules).ToHashSet();
        var missing = citations.Where(citation => !citedReferences.Contains(citation.Draft.Reference)).Select(citation => citation.Id).ToArray();
        if (missing.Length > 0) throw new WorkingRuleSetFormatException($"The model did not preserve source-draft citation(s) {string.Join(", ", missing)} in this working-rule batch.");
    }
    private static IReadOnlyList<string> DistinctNonBlank(IEnumerable<string> values) => values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
    private static string FormatSourceDrafts(IEnumerable<DraftCitation> citations) => string.Join("\n\n", citations.Select(citation =>
        $"[{citation.Id}]\nName: {citation.Draft.Rule.Name}\nKind: {citation.Draft.Rule.Kind}\nText: {citation.Draft.Rule.Text}"));
}
