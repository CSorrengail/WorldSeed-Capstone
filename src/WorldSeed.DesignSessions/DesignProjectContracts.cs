using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldSeed.DesignSessions;

/// <summary>Local working container for source notes and their non-canonical drafting sessions.</summary>
public sealed record DesignProject(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DesignSourceNote> SourceNotes,
    IReadOnlyList<DesignSession> Sessions);

public sealed record DesignProjectSummary(string Id, string Name, DateTimeOffset UpdatedAt);

public interface IDesignProjectStore
{
    Task SaveAsync(DesignProject project, CancellationToken cancellationToken = default);
    Task<DesignProject?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DesignProjectSummary>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Atomic local JSON persistence for a working project. It contains no credentials or canonical revision.</summary>
public sealed class JsonDesignProjectStore(string directory) : IDesignProjectStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _directory = string.IsNullOrWhiteSpace(directory) ? throw new ArgumentException("A storage directory is required.", nameof(directory)) : directory;

    public async Task SaveAsync(DesignProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project); ValidateId(project.Id);
        if (string.IsNullOrWhiteSpace(project.Name)) throw new ArgumentException("A project name is required.", nameof(project));
        Directory.CreateDirectory(_directory);
        var path = PathFor(project.Id); var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(project, Options), cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public async Task<DesignProject?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateId(projectId); var path = PathFor(projectId);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<DesignProject>(await File.ReadAllTextAsync(path, cancellationToken), Options) ?? throw new InvalidDataException("The project file contains no project.");
    }

    public async Task<IReadOnlyList<DesignProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory)) return [];
        var projects = new List<DesignProjectSummary>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.worldseed-project.json"))
        {
            var project = JsonSerializer.Deserialize<DesignProject>(await File.ReadAllTextAsync(path, cancellationToken), Options);
            if (project is not null) projects.Add(new DesignProjectSummary(project.Id, project.Name, project.UpdatedAt));
        }
        return projects.OrderByDescending(project => project.UpdatedAt).ToArray();
    }

    private string PathFor(string id) => Path.Combine(_directory, id + ".worldseed-project.json");
    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_'))) throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(id));
    }
}

public static class DesignProjectService
{
    public static DesignProject Create(string id, string name, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A project id and name are required.");
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new DesignProject(id, name.Trim(), now, now, [], []);
    }

    public static DesignProject AddSources(DesignProject project, IEnumerable<DesignSourceNote> sources, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(sources);
        var additions = sources.ToArray();
        if (additions.Any(source => string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.OriginalText))) throw new ArgumentException("Every source needs an id and original text.", nameof(sources));
        if (project.SourceNotes.Select(source => source.Id).Concat(additions.Select(source => source.Id)).Distinct(StringComparer.Ordinal).Count() != project.SourceNotes.Count + additions.Length) throw new ArgumentException("Project source ids must be unique.", nameof(sources));
        return project with { SourceNotes = project.SourceNotes.Concat(additions).ToArray(), UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow() };
    }

    public static DesignProject RemoveSource(DesignProject project, string sourceId, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project with { SourceNotes = project.SourceNotes.Where(source => source.Id != sourceId).ToArray(), Sessions = project.Sessions.Where(session => !session.SourceNotes.Any(source => source.Id == sourceId)).ToArray(), UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow() };
    }

    public static DesignProject SaveSession(DesignProject project, DesignSession session, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(session);
        var sessions = project.Sessions.Where(existing => existing.Id != session.Id).Append(session).ToArray();
        return project with { Sessions = sessions, UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow() };
    }
}
