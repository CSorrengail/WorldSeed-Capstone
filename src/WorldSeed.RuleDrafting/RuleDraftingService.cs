using WorldSeed.LlmIntegration;

namespace WorldSeed.RuleDrafting;

/// <summary>Asks a selected model for the next clarification or structured rule draft turn.</summary>
public sealed class RuleDraftingService
{
    private const string SystemPrompt = """
        You are a TTRPG design interviewer. Preserve the designer's intent and never invent unmarked mechanics.
        Every proposed rule must be a direct restatement, decomposition, or faithful organization of supplied source material. Never add a rule, constraint, condition, exception, title, quotation, page number, or external citation that is not in the supplied source material. If a detail is missing, place it in openQuestions instead of inventing it.
        Return JSON only, with no Markdown or surrounding commentary. Use exactly one action: askClarifyingQuestion or presentDraft.
        Ask focused questions only when undefined effects, triggers, or procedures must be decided before the stated rules can be faithfully represented. Ask one to five independent questions together when doing so avoids another round trip; each question must be short and answerable on its own. Do not replace a required clarification with openQuestions. For example, if a source says an outcome is "harder" but does not define what that changes, ask what "harder" means before presenting a draft. Do not ask for clarification about direct wording that can be restated faithfully: choosing "the same route" means choosing the same revealed option, not a similar option. Do not ask for a missing procedure when the source explicitly gives the facilitator narrative discretion or says no fixed mechanical procedure exists; present that rule as natural language. Otherwise presentDraft.
        A presentDraft must contain draft.title, draft.intent, draft.rules, draft.concepts, draft.assumptions, draft.openQuestions, and draft.exclusions.
        Each rule has id, name, kind (definition, rule, procedure, or constraint), text, sourceNoteIds, and sourceSupport. Every rule must cite at least one provided sourceNoteId and provide at least one sourceSupport object with sourceNoteId and excerpt. An excerpt must be an exact, continuous quote from that source note. Cite only sourceNoteIds provided for this turn. If a rule restates any part of the designer's note, cite that note; do not leave sourceNoteIds or sourceSupport empty.
        Rule text is human-readable, specific, and authoritative in tone. Mark uncertainty in assumptions or openQuestions instead of treating it as fact.
        Do not emit a game schema, JSON Schema, database structure, approval decision, or implementation code.
        For presentDraft, use this exact outer shape (with real values in place of ellipses):
        {"action":"presentDraft","draft":{"title":"...","intent":"...","rules":[{"id":"...","name":"...","kind":"rule","text":"...","sourceNoteIds":["source-note-id"]}],"concepts":[{"id":"...","name":"...","description":"..."}],"assumptions":[],"openQuestions":[],"exclusions":[]}}
        For askClarifyingQuestion, use exactly {"action":"askClarifyingQuestion","clarifyingQuestions":["..."]}. Do not put several questions in one string.
        Never omit action. Concepts must be objects with id, name, and description; do not use strings for concepts.
        """;

    private readonly ILanguageModelClient _client;
    private readonly RuleDraftJsonParser _parser;

    public RuleDraftingService(ILanguageModelClient client, RuleDraftJsonParser? parser = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _parser = parser ?? new RuleDraftJsonParser();
    }

    public async Task<RuleDraftTurn> AdvanceAsync(RuleDraftConversation conversation, CancellationToken cancellationToken = default)
        => (await AdvanceWithTranscriptAsync(conversation, cancellationToken)).Turn;

    public async Task<RuleDraftingResult> AdvanceWithTranscriptAsync(RuleDraftConversation conversation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.ProjectId)) throw new ArgumentException("A project id is required.", nameof(conversation));
        var allowedSourceNoteIds = conversation.SourceMaterialIds.ToHashSet(StringComparer.Ordinal);
        if (allowedSourceNoteIds.Count == 0 || allowedSourceNoteIds.Any(string.IsNullOrWhiteSpace) || allowedSourceNoteIds.Count != conversation.SourceMaterialIds.Count) throw new ArgumentException("Source material ids must be unique, non-empty values.", nameof(conversation));
        var sourceTextById = conversation.SourceTextById ?? new Dictionary<string, string>();
        if (sourceTextById.Count > 0 && (!sourceTextById.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(allowedSourceNoteIds) || sourceTextById.Any(source => string.IsNullOrWhiteSpace(source.Value)))) throw new ArgumentException("Source text must be supplied for every source material id.", nameof(conversation));
        if (conversation.Messages.Count == 0) throw new ArgumentException("At least one conversation message is required.", nameof(conversation));
        var messages = new List<LlmMessage> { new(LlmMessageRole.System, SystemPrompt), new(LlmMessageRole.System, "Source material IDs for this turn: " + string.Join(", ", conversation.SourceMaterialIds)) };
        if (sourceTextById.Count > 0) messages.Add(new LlmMessage(LlmMessageRole.System, "Source notes for exact quotation:\n" + string.Join("\n\n", sourceTextById.OrderBy(source => source.Key, StringComparer.Ordinal).Select(source => $"[{source.Key}]\n{source.Value}"))));
        messages.AddRange(conversation.Messages);
        var response = await _client.CompleteAsync(new LlmChatRequest(
            messages,
            Temperature: 0.2,
            MaxOutputTokens: 1200,
            ResponseSchema: RuleDraftResponseSchema.Create(allowedSourceNoteIds)), cancellationToken);
        return new RuleDraftingResult(_parser.Parse(response.Content, allowedSourceNoteIds, sourceTextById), response.Content);
    }
}
