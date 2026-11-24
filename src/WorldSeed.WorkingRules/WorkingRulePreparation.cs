using System.Security.Cryptography;
using System.Text.Json;
using WorldSeed.DesignSessions;

namespace WorldSeed.WorkingRules;

public sealed record PreparedWorkingRuleBatch(WorkingRuleSet RuleSet, string RawResponse);
public sealed record WorkingRulePreparation(string ProjectId, string InputFingerprint, bool AddMissingOnly,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<PreparedWorkingRuleBatch> Batches);

public interface IWorkingRulePreparationStore
{
    Task<WorkingRulePreparation?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveAsync(WorkingRulePreparation preparation, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class WorkingRulePreparationBinding
{
    public static IReadOnlyList<CompletedDraftRule> SelectDrafts(DesignProject project, WorkingRuleSet? current, bool addMissingOnly)
    {
        var drafts = CompletedDraftCatalog.Create(project, latestOnly: addMissingOnly);
        if (!addMissingOnly || current is null) return drafts;
        var incorporated = current.Rules.SelectMany(rule => rule.SourceDraftRules).ToHashSet();
        return drafts.Where(draft => !incorporated.Contains(draft.Reference)).ToArray();
    }

    public static string Fingerprint(DesignProject project, WorkingRuleSet? current, bool addMissingOnly)
    {
        var drafts = SelectDrafts(project, current, addMissingOnly);
        var sessions = drafts.Select(draft => draft.Reference.SessionId).ToHashSet(StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(new
        {
            Contract = "working-rule-preparation-v1", project.Id, project.Name, AddMissingOnly = addMissingOnly,
            CurrentRules = current, Drafts = drafts,
            Sources = project.Sessions.Where(session => sessions.Contains(session.Id))
                .Select(session => new { session.Id, session.SourceNotes, session.LatestTurn })
        });
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    public static void Validate(WorkingRulePreparation preparation, DesignProject project, WorkingRuleSet? current, bool addMissingOnly)
    {
        if (preparation.ProjectId != project.Id || preparation.AddMissingOnly != addMissingOnly ||
            preparation.InputFingerprint != Fingerprint(project, current, addMissingOnly))
            throw new InvalidOperationException("This Checkpoint Is Outdated Or Uses A Different Operation. Sources Or Project Rules Have Changed. Discard The Checkpoint Before Starting Again.");
        var allowed = SelectDrafts(project, current, addMissingOnly).Select(draft => draft.Reference).ToHashSet();
        var seen = new HashSet<DraftRuleReference>();
        foreach (var batch in preparation.Batches)
        {
            var validation = new WorkingRuleSetValidator().Validate(batch.RuleSet, project);
            if (!validation.IsValid) throw new InvalidOperationException("The Saved Checkpoint Contains Invalid Rules. Discard It Before Retrying.");
            foreach (var reference in batch.RuleSet.Rules.SelectMany(rule => rule.SourceDraftRules).Distinct())
                if (!allowed.Contains(reference) || !seen.Add(reference))
                    throw new InvalidOperationException("The Saved Checkpoint Contains Unexpected Or Duplicate Source References. Discard It Before Retrying.");
        }
    }
}
