using MongoDB.Driver;
using WorldSeed.DesignSessions;

namespace WorldSeed.Persistence.Mongo;

/// <summary>MongoDB implementation of project persistence. Source text is stored only after a designer explicitly imports it.</summary>
public sealed class MongoDesignProjectStore : IDesignProjectStore
{
    private readonly IMongoCollection<DesignProject> _projects;

    public MongoDesignProjectStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _projects = database.GetCollection<DesignProject>("designProjects");
    }

    public async Task SaveAsync(DesignProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Id)) throw new ArgumentException("A project id is required.", nameof(project));
        if (string.IsNullOrWhiteSpace(project.Name)) throw new ArgumentException("A project name is required.", nameof(project));
        await _projects.ReplaceOneAsync(
            stored => stored.Id == project.Id,
            project,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<DesignProject?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateId(projectId);
        return await _projects.Find(stored => stored.Id == projectId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DesignProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
        => await _projects.Find(FilterDefinition<DesignProject>.Empty)
            .SortByDescending(project => project.UpdatedAt)
            .Project(project => new DesignProjectSummary(project.Id, project.Name, project.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateId(projectId);
        await _projects.DeleteOneAsync(stored => stored.Id == projectId, cancellationToken);
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await _projects.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: cancellationToken);
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(id));
    }
}
