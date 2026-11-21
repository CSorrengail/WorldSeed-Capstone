using System.Text.Json;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

/// <summary>Explicit preparation only; original provenance remains in the audit,
/// while active schema/data change only through normal reviewed activation.</summary>
public static class RuleReconciliationSchemaService
{
    public static ProjectSchemaProposal Prepare(WorkingRuleSet rules, RuleReconciliation applied,
        ProjectSchemaProposal saved, DateTimeOffset now)
    {
        if (applied.State != RuleReconciliationState.Applied || applied.AppliedRevision != rules.Revision ||
            applied.ProjectId != rules.ProjectId || saved.ProjectId != rules.ProjectId ||
            saved.WorkingRuleSetId != rules.Id || saved.WorkingRuleSetRevision != applied.BaseRevision ||
            applied.OriginalSchemaProposalJson != JsonSerializer.Serialize(saved))
            throw new InvalidOperationException("The schema proposal or Working Rules changed. Review the current schema work before preparing this update.");
        var removed = applied.Originals.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var retained = applied.Originals[0].Id;
        return saved with
        {
            WorkingRuleSetRevision = rules.Revision,
            SelectedWorkingRuleIds = saved.SelectedWorkingRuleIds.Select(id => removed.Contains(id) ? retained : id).Distinct().ToArray(),
            DefinitionSources = saved.DefinitionSources.Select(source => source with
            { WorkingRuleIds = source.WorkingRuleIds.Select(id => removed.Contains(id) ? retained : id).Distinct().ToArray() }).ToArray(),
            UpdatedAt = now
        };
    }
}
