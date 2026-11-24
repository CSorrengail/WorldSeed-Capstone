using MongoDB.Driver;
using WorldSeed.WorkingRules;

namespace WorldSeed.Persistence.Mongo;

public sealed class MongoWorkingRulePreparationStore(IMongoDatabase database) : IWorkingRulePreparationStore
{
    // Mongo's _id gives one atomically replaced checkpoint per project.
    private sealed record Document(string Id, WorkingRulePreparation Preparation);
    private readonly IMongoCollection<Document> _documents = database.GetCollection<Document>("workingRulePreparations");
    public async Task<WorkingRulePreparation?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
        => (await _documents.Find(document => document.Id == projectId).FirstOrDefaultAsync(cancellationToken))?.Preparation;
    public async Task SaveAsync(WorkingRulePreparation preparation, CancellationToken cancellationToken = default)
        => await _documents.ReplaceOneAsync(document => document.Id == preparation.ProjectId,
            new Document(preparation.ProjectId, preparation), new ReplaceOptions { IsUpsert = true }, cancellationToken);
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
        => await _documents.DeleteOneAsync(document => document.Id == projectId, cancellationToken);
}
