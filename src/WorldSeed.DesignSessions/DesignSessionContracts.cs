using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

namespace WorldSeed.DesignSessions;

/// <summary>Original user wording retained during the exploratory design conversation.</summary>
public sealed record DesignSourceNote(string Id, string OriginalText, DateTimeOffset CapturedAt);

/// <summary>Persistent, pre-canonical design work. It contains no API key and makes no approval decision.</summary>
public sealed record DesignSession(
    string Id,
    string ProjectId,
    string ModelProfileId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DesignSourceNote> SourceNotes,
    IReadOnlyList<LlmMessage> Messages,
    RuleDraftTurn? LatestTurn);

public interface IDesignSessionStore
{
    Task SaveAsync(DesignSession session, CancellationToken cancellationToken = default);
    Task<DesignSession?> LoadAsync(string sessionId, CancellationToken cancellationToken = default);
}
