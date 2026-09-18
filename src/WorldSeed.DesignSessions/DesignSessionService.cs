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
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateSession(session);
        var conversation = new RuleDraftConversation(session.ProjectId, session.SourceNotes.Select(source => source.Id).ToArray(), session.Messages);
        var result = await _ruleDrafting.AdvanceWithTranscriptAsync(conversation, cancellationToken);
        var messages = session.Messages.Append(new LlmMessage(LlmMessageRole.Assistant, result.RawModelResponse)).ToArray();
        return session with { Messages = messages, LatestTurn = result.Turn, UpdatedAt = _timeProvider.GetUtcNow() };
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
