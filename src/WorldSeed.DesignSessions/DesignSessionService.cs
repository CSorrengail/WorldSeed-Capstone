using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

namespace WorldSeed.DesignSessions;

/// <summary>Coordinates persistent designer messages with structured rule-drafting turns.</summary>
public sealed class DesignSessionService
{
    private readonly RuleDraftingService _ruleDrafting;
    private readonly TimeProvider _timeProvider;

    public DesignSessionService(RuleDraftingService ruleDrafting, TimeProvider? timeProvider = null)
    {
        _ruleDrafting = ruleDrafting ?? throw new ArgumentNullException(nameof(ruleDrafting));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DesignSession Start(string sessionId, string projectId, string modelProfileId, DesignSourceNote initialSource)
    {
        ValidateIdentity(sessionId, nameof(sessionId)); ValidateIdentity(projectId, nameof(projectId)); ValidateIdentity(modelProfileId, nameof(modelProfileId));
        ValidateSource(initialSource);
        var now = _timeProvider.GetUtcNow();
        return new DesignSession(sessionId, projectId, modelProfileId, now, now, [initialSource], [new LlmMessage(LlmMessageRole.User, initialSource.OriginalText)], null);
    }

    public DesignSession AddDesignerMessage(DesignSession session, string text, DesignSourceNote? additionalSource = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A designer message is required.", nameof(text));
        var sources = session.SourceNotes.ToList();
        if (additionalSource is not null)
        {
            ValidateSource(additionalSource);
            if (sources.Any(source => source.Id == additionalSource.Id)) throw new ArgumentException("A source note with that id already exists.", nameof(additionalSource));
            sources.Add(additionalSource);
        }
        var messages = session.Messages.Append(new LlmMessage(LlmMessageRole.User, text)).ToArray();
        return session with { SourceNotes = sources, Messages = messages, UpdatedAt = _timeProvider.GetUtcNow() };
    }

    public async Task<DesignSession> AdvanceAsync(DesignSession session, CancellationToken cancellationToken = default)
        => await AdvanceAsync(session, null, false, cancellationToken);

    public static DesignSession MarkReviewAsNonGameMaterial(DesignSession session, string reviewText, TimeProvider? timeProvider = null)
    {
        ValidateSession(session);
        var draft = session.LatestTurn?.Draft;
        if (session.LatestTurn?.Action != RuleDraftAction.PresentDraft || draft is null || !draft.OpenQuestions.Contains(reviewText, StringComparer.Ordinal))
            throw new ArgumentException("This Review Item Is No Longer Current.", nameof(reviewText));
        var primary = session.SourceNotes[0];
        var catalog = SourcePassageCatalog.Create(new Dictionary<string, string> { [primary.Id] = primary.OriginalText });
        var passage = catalog.FirstOrDefault(item => reviewText.StartsWith($"[{item.Id}]", StringComparison.Ordinal));
        // A transport fragment may cover only part of a long catalog passage.
        // Do not exclude its other facts when the designer classifies that fragment.
        if (passage is not null && reviewText.Contains("Original Text:", StringComparison.Ordinal) &&
            !reviewText.EndsWith("Original Text: " + passage.Text, StringComparison.Ordinal)) passage = null;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var decision = new SourceReviewDecision(reviewText, passage?.Id, now);
        return session with
        {
            UpdatedAt = now,
            Messages = session.Messages.Append(new LlmMessage(LlmMessageRole.User, "Designer Decision: Treat The Following Review Item As Non-Game Material, Not As A Game Rule. Preserve The Original Source And Do Not Extract It Again.\n" + reviewText)).ToArray(),
            SourceReviewDecisions = (session.SourceReviewDecisions ?? []).Append(decision).ToArray(),
            LatestTurn = session.LatestTurn with { Draft = draft with
            {
                OpenQuestions = draft.OpenQuestions.Where(item => item != reviewText).ToArray(),
                Exclusions = draft.Exclusions.Append("Designer Classified As Non-Game Material: " + reviewText).ToArray()
            } }
        };
    }

    public async Task<DesignSession> AdvanceAsync(DesignSession session, string? workflowInstruction, bool requireClarificationOnFirstTurn, CancellationToken cancellationToken = default, IProgress<RuleDraftProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateSession(session);
        var firstTurn = session.LatestTurn is null;
        if (!firstTurn && requireClarificationOnFirstTurn &&
            (session.Messages[^1].Role != LlmMessageRole.User ||
             !session.SourceNotes.Skip(1).Any(source => string.Equals(source.OriginalText, session.Messages[^1].Content, StringComparison.Ordinal))))
            throw new RuleDraftFormatException("An expansion requires an explicit designer answer saved as source material before continuing.");
        var excluded = (session.SourceReviewDecisions ?? []).Where(decision => decision.PassageId is not null).Select(decision => decision.PassageId!).ToHashSet(StringComparer.Ordinal);
        var conversation = new RuleDraftConversation(session.ProjectId, session.SourceNotes.Select(source => source.Id).ToArray(), session.Messages, session.SourceNotes.ToDictionary(source => source.Id, source => source.OriginalText, StringComparer.Ordinal), WorkflowInstruction: workflowInstruction, AllowDesignOptions: requireClarificationOnFirstTurn, RequireDesignChoices: requireClarificationOnFirstTurn && firstTurn, ExcludedPassageIds: excluded);
        var result = await _ruleDrafting.AdvanceWithTranscriptAsync(conversation, cancellationToken, progress);
        if (firstTurn && requireClarificationOnFirstTurn && result.Turn.Action != RuleDraftAction.AskClarifyingQuestion)
            throw new RuleDraftFormatException("An expansion request must begin with clarification before it can produce draft rules.");
        var messages = session.Messages.Append(new LlmMessage(LlmMessageRole.Assistant, result.RawModelResponse)).ToArray();
        var turn = result.Turn;
        if (turn.Draft is not null && session.SourceReviewDecisions is { Count: > 0 } decisions)
            turn = turn with { Draft = turn.Draft with
            {
                OpenQuestions = turn.Draft.OpenQuestions.Where(question => !decisions.Any(decision => decision.ReviewText == question)).ToArray(),
                Exclusions = turn.Draft.Exclusions.Concat(decisions.Select(decision => "Designer Classified As Non-Game Material: " + decision.ReviewText)).Distinct().ToArray()
            } };
        return session with { Messages = messages, LatestTurn = turn, UpdatedAt = _timeProvider.GetUtcNow() };
    }

    private static void ValidateSession(DesignSession session)
    {
        ValidateIdentity(session.Id, "session id"); ValidateIdentity(session.ProjectId, "project id"); ValidateIdentity(session.ModelProfileId, "model profile id");
        if (session.SourceNotes.Count == 0 || session.Messages.Count == 0) throw new ArgumentException("A design session requires source notes and messages.", nameof(session));
        foreach (var source in session.SourceNotes) ValidateSource(source);
        if (session.SourceNotes.Select(source => source.Id).Distinct(StringComparer.Ordinal).Count() != session.SourceNotes.Count) throw new ArgumentException("Design-session source note ids must be unique.", nameof(session));
    }

    private static void ValidateSource(DesignSourceNote source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIdentity(source.Id, "source note id");
        if (string.IsNullOrWhiteSpace(source.OriginalText)) throw new ArgumentException("Source notes require original text.", nameof(source));
    }

    private static void ValidateIdentity(string value, string parameterName) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName); }
}
