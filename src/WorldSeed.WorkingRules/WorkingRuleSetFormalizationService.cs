using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;

namespace WorldSeed.WorkingRules;

/// <summary>
/// Consolidates completed source-level drafts into one project-level proposal. It deliberately
/// does not create a game schema or executable rules: those are later, separately reviewable stages.
/// </summary>
public sealed class WorkingRuleSetFormalizationService
{
    private const string SystemPrompt = """
        You consolidate completed TTRPG source-draft interpretations into one project-level Working Rule Set.
        The sources are collectively incomplete evidence for one cohesive ruleset; do not organize the result by source file.
        Preserve the designer's intent. Never invent mechanics, exceptions, values, entities, schema definitions, or facts not present in the supplied source-draft rules. When material is unclear, incomplete, or conflicting, record it in openQuestions, exclusions, or conflicts instead of filling the gap.
        Every working rule and concept must cite one or more supplied source-draft rules using only the short draft citation IDs shown below, such as d001. Cite all source-draft rules that materially support a consolidation. WorldSeed, not you, will attach their literal evidence passages.
        A schemaHint is optional, non-authoritative advice for a later schema-design stage. It must be justified by sourceWorkingRuleIds that are IDs of working rules in this same response. Emit an empty schemaHints array unless every required schemaHint field can be supplied honestly. Do not emit structuredSupport, executable logic, database tables, JSON Schema, source quotations, approval decisions, or implementation code.
        Return JSON only, with no Markdown or surrounding explanation. Use this exact shape:
        {"name":"...","description":"... or null","rules":[{"id":"...","name":"...","kind":"definition|rule|procedure|constraint","authoritativeText":"...","sourceDraftIds":["d001"],"conceptIds":["..."],"schemaHints":[{"kind":"entityType|valueType|schemaFragment|property|relationship|rule|procedure|validation","suggestedId":"...","suggestedName":"...","parentSuggestedId":"... or null","rationale":"...","sourceWorkingRuleIds":["..."]}],"openQuestions":[],"exclusions":[]}],"concepts":[{"id":"...","name":"...","description":"...","sourceDraftIds":["d001"]}],"openQuestions":[],"exclusions":[],"conflicts":[]}
        Keep the result concise and cohesive. Preserve each meaningful source-draft rule either by consolidating it into a rule, recording it as an exclusion, or identifying it as an open question or conflict.
        """;

    private readonly ILanguageModelClient _client;
    private readonly WorkingRuleSetJsonParser _parser;
    private readonly TimeProvider _timeProvider;

    public WorkingRuleSetFormalizationService(ILanguageModelClient client, WorkingRuleSetJsonParser? parser = null, TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _parser = parser ?? new WorkingRuleSetJsonParser();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<WorkingRuleSetFormalizationResult> CreateProposalAsync(DesignProject project, WorkingRuleSet? currentRuleSet = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sourceDrafts = CompletedDraftCatalog.Create(project);
        if (sourceDrafts.Count == 0) throw new ArgumentException("At least one completed source draft is required before creating project rules.", nameof(project));
        if (currentRuleSet is not null && !string.Equals(currentRuleSet.ProjectId, project.Id, StringComparison.Ordinal)) throw new ArgumentException("The current working rule set belongs to a different project.", nameof(currentRuleSet));
        var citations = DraftCitationCatalog.Create(sourceDrafts);

        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, SystemPrompt),
            new(LlmMessageRole.System, "Completed source-draft rules available for citation:\n" + FormatSourceDrafts(citations)),
            new(LlmMessageRole.User, "Create the project-level Working Rule Set proposal from the completed source-draft rules.")
        };
        if (currentRuleSet is not null)
            messages.Insert(2, new LlmMessage(LlmMessageRole.System, "Current working proposal to preserve or deliberately consolidate (it is not new source evidence):\n" + FormatCurrentRuleSet(currentRuleSet)));
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.1, MaxOutputTokens: 4800, RequireJsonObject: true), cancellationToken);
        var ruleSet = _parser.Parse(response.Content, project, citations, currentRuleSet, _timeProvider.GetUtcNow());
        return new WorkingRuleSetFormalizationResult(ruleSet, response.Content);
    }

    private static string FormatSourceDrafts(IEnumerable<DraftCitation> citations) => string.Join("\n\n", citations.Select(citation =>
        $"[{citation.Id}]\nName: {citation.Draft.Rule.Name}\nKind: {citation.Draft.Rule.Kind}\nText: {citation.Draft.Rule.Text}\nEvidence: " +
        string.Join(" | ", citation.Draft.Rule.SourceSupport.Select(support => $"{support.SourceNoteId}/{support.PassageId ?? "unanchored"}: {support.Excerpt}"))));

    private static string FormatCurrentRuleSet(WorkingRuleSet ruleSet) => string.Join("\n", ruleSet.Rules.Select(rule =>
        $"[{rule.Id}] {rule.Name} ({rule.Kind}): {rule.AuthoritativeText} | Source drafts: " +
        string.Join(", ", rule.SourceDraftRules.Select(reference => reference.SessionId + "/" + reference.RuleId))));
}
