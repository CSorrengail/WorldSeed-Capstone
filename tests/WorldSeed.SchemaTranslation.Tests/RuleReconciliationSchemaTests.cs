using System.Text.Json;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation.Tests;

public sealed class RuleReconciliationSchemaTests
{
    [Fact]
    public void Schema_preparation_preserves_structure_and_archived_provenance_and_refuses_changed_work()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new WorkingRule("first", "One", WorkingRuleKind.Rule, "First", [], [], [], [], null, [], []);
        var second = first with { Id = "second", AuthoritativeText = "Second" };
        var saved = new ProjectSchemaProposal("proposal", "project", "0.1", "Retain Summary", "{}", [new("promise", ["first", "second"])], "rules", 1, ["first", "second"], now, now);
        var snapshot = JsonSerializer.Serialize(saved);
        var audit = new RuleReconciliation("update", "project", "rules", 1, [first, second], "", "Combined", WorkingRuleKind.Rule, "Both", "Merge", null,
            RuleReconciliationState.Applied, now, snapshot, 2, now);
        var current = new WorkingRuleSet("rules", "project", "0.1", "Story", null, 2, now, now, [first with { AuthoritativeText = "Both" }], [], [], [], [], [audit]);

        var candidate = RuleReconciliationSchemaService.Prepare(current, audit, saved, now.AddSeconds(1));

        Assert.Equal(saved.SchemaJson, candidate.SchemaJson);
        Assert.Equal(saved.Id, candidate.Id);
        Assert.Equal(2, candidate.WorkingRuleSetRevision);
        Assert.Equal(new[] { "first" }, candidate.DefinitionSources[0].WorkingRuleIds);
        Assert.Equal(snapshot, audit.OriginalSchemaProposalJson);
        Assert.Equal(1, saved.WorkingRuleSetRevision);
        Assert.Throws<InvalidOperationException>(() => RuleReconciliationSchemaService.Prepare(current, audit, saved with { Summary = "New Pending Work" }, now));
    }
}
