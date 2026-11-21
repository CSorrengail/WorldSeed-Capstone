using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using WorldSeed.WorkingRules;

namespace WorldSeed.Persistence.Mongo.Tests;

public sealed class WorkingRuleAuditDocumentTests
{
    [Fact]
    public void Old_rule_documents_and_new_reconciliation_history_round_trip()
    {
        var now = DateTimeOffset.UtcNow;
        var rule = new WorkingRule("rule", "Promise", WorkingRuleKind.Rule, "Record promises.", [], [], [], [], null, [], []);
        var rules = new WorkingRuleSet("rules", "project", "0.1", "Story", null, 1, now, now, [rule], [], [], [], []);
        var legacy = rules.ToBsonDocument();
        legacy.Remove("Reconciliations");
        Assert.Null(BsonSerializer.Deserialize<WorkingRuleSet>(legacy).Reconciliations);
        var audit = new RuleReconciliation("update", "project", "rules", 1, [rule], "Guidance", "Revised Promise", WorkingRuleKind.Rule,
            "Record promises once.", "Remove duplication.", null, RuleReconciliationState.Applied, now, "{\"saved\":true}", 2, now);
        var restored = BsonSerializer.Deserialize<WorkingRuleSet>((rules with { Reconciliations = [audit] }).ToBson());
        var saved = Assert.Single(restored.Reconciliations!);
        Assert.Equal(audit.OriginalSchemaProposalJson, saved.OriginalSchemaProposalJson);
        Assert.Equal(rule.AuthoritativeText, saved.Originals[0].AuthoritativeText);
        Assert.Equal(2, saved.AppliedRevision);
    }
}
