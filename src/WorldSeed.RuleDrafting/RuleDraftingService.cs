using WorldSeed.LlmIntegration;

namespace WorldSeed.RuleDrafting;

/// <summary>Asks a selected model for the next clarification or structured rule draft turn.</summary>
public sealed class RuleDraftingService
{
    private const string SystemPrompt = """
        You are a TTRPG design interviewer. Preserve the designer's intent and never invent unmarked mechanics.
        Return JSON only, with no Markdown or surrounding commentary. Use exactly one action: askClarifyingQuestion or presentDraft.
        Ask one focused question only when the missing answer materially changes a rule. Otherwise presentDraft.
        A presentDraft must contain draft.title, draft.intent, draft.rules, draft.concepts, draft.assumptions, draft.openQuestions, and draft.exclusions.
        Each rule has id, name, kind (definition, rule, procedure, or constraint), text, and sourceNoteIds. Cite only sourceNoteIds provided for this turn.
        Rule text is human-readable, specific, and authoritative in tone. Mark uncertainty in assumptions or openQuestions instead of treating it as fact.
        Do not emit a game schema, JSON Schema, database structure, approval decision, or implementation code.
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
        if (conversation.Messages.Count == 0) throw new ArgumentException("At least one conversation message is required.", nameof(conversation));
        var messages = new List<LlmMessage> { new(LlmMessageRole.System, SystemPrompt), new(LlmMessageRole.System, "Source material IDs for this turn: " + string.Join(", ", conversation.SourceMaterialIds)) };
        messages.AddRange(conversation.Messages);
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.2), cancellationToken);
        return new RuleDraftingResult(_parser.Parse(response.Content, allowedSourceNoteIds), response.Content);
    }
}
