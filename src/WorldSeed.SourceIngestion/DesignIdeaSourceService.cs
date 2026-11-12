using WorldSeed.DesignSessions;

namespace WorldSeed.SourceIngestion;

public static class DesignIdeaSourceService
{
    public static DesignSourceNote Create(string sourceNoteId, string title, string originalText, DateTimeOffset capturedAt)
    {
        if (string.IsNullOrWhiteSpace(sourceNoteId)) throw new ArgumentException("A source note id is required.", nameof(sourceNoteId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A design-idea title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(originalText)) throw new ArgumentException("A design idea is required.", nameof(originalText));
        return new DesignSourceNote(
            sourceNoteId.Trim(),
            originalText,
            capturedAt,
            new SourceNoteOrigin($"Designer Idea: {title.Trim()}"));
    }
}
