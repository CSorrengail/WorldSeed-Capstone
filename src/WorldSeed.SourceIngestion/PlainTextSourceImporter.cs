using WorldSeed.DesignSessions;

namespace WorldSeed.SourceIngestion;

public sealed record PlainTextSourceRequest(string SourceNoteId, string FilePath);

/// <summary>Imports plain-text notes without rewriting, splitting, or storing an absolute path.</summary>
public sealed class PlainTextSourceImporter
{
    public async Task<IReadOnlyList<DesignSourceNote>> ImportAsync(IReadOnlyList<PlainTextSourceRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0) throw new ArgumentException("At least one plain-text source is required.", nameof(requests));
        if (requests.Select(request => request.SourceNoteId).Any(string.IsNullOrWhiteSpace) || requests.Select(request => request.SourceNoteId).Distinct(StringComparer.Ordinal).Count() != requests.Count) throw new ArgumentException("Source note ids must be unique and non-empty.", nameof(requests));
        var notes = new List<DesignSourceNote>();
        foreach (var request in requests)
        {
            if (!string.Equals(Path.GetExtension(request.FilePath), ".txt", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only .txt files are supported by the initial importer.", nameof(requests));
            var text = await File.ReadAllTextAsync(request.FilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"Source file '{Path.GetFileName(request.FilePath)}' is empty.", nameof(requests));
            notes.Add(new DesignSourceNote(request.SourceNoteId, text, DateTimeOffset.UtcNow, new SourceNoteOrigin(Path.GetFileName(request.FilePath))));
        }
        return notes;
    }
}
