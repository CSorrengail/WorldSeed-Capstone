using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldSeed.DesignSessions;

/// <summary>Simple local JSON persistence. GUI code selects an application-data directory rather than a repository path.</summary>
public sealed class JsonDesignSessionStore(string directory) : IDesignSessionStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _directory = string.IsNullOrWhiteSpace(directory) ? throw new ArgumentException("A storage directory is required.", nameof(directory)) : directory;

    public async Task SaveAsync(DesignSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session); ValidateSessionId(session.Id);
        Directory.CreateDirectory(_directory);
        var path = PathFor(session.Id); var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(session, Options), cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public async Task<DesignSession?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId); var path = PathFor(sessionId);
        if (!File.Exists(path)) return null;
        var session = JsonSerializer.Deserialize<DesignSession>(await File.ReadAllTextAsync(path, cancellationToken), Options);
        return session ?? throw new InvalidDataException("The design-session file contains no session.");
    }

    private string PathFor(string sessionId) => Path.Combine(_directory, sessionId + ".worldseed-session.json");
    private static void ValidateSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_'))) throw new ArgumentException("Session ids may contain only letters, numbers, hyphens, and underscores.", nameof(sessionId));
    }
}
