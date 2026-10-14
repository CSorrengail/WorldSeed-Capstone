using MongoDB.Driver;
using WorldSeed.WorkingRules;

namespace WorldSeed.Persistence.Mongo;

/// <summary>Persists the one active working rule set for each project in the local MongoDB database.</summary>
public sealed class MongoWorkingRuleSetStore : IWorkingRuleSetStore
{
    private readonly IMongoCollection<WorkingRuleSet> _ruleSets;

    public MongoWorkingRuleSetStore(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _ruleSets = database.GetCollection<WorkingRuleSet>("workingRuleSets");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<WorkingRuleSet>(Builders<WorkingRuleSet>.IndexKeys.Ascending(ruleSet => ruleSet.ProjectId), new CreateIndexOptions { Unique = true, Name = "one-working-rule-set-per-project" });
        await _ruleSets.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(WorkingRuleSet ruleSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        await _ruleSets.ReplaceOneAsync(
            stored => stored.ProjectId == ruleSet.ProjectId,
            ruleSet,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<WorkingRuleSet?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        return await _ruleSets.Find(stored => stored.ProjectId == projectId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await _ruleSets.DeleteOneAsync(stored => stored.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Project ids may contain only letters, numbers, hyphens, and underscores.", nameof(projectId));
    }
}
