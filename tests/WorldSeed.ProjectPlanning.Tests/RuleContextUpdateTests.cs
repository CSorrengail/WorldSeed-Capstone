using WorldSeed.GameData;
using WorldSeed.ProjectPlanning;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.ProjectPlanning.Tests;

public sealed class RuleContextUpdateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-19T12:00:00Z");

    [Fact]
    public void Reviewed_narrative_expansion_prepares_only_a_new_rule_binding()
    {
        var (decision, rules, active, saved) = Fixture();

        var candidate = ExpansionRequestService.PrepareRuleContextUpdate(decision, rules, active, saved, Now.AddMinutes(1));

        Assert.Equal(saved.Id, candidate.Id);
        Assert.Equal(saved.SchemaJson, candidate.SchemaJson);
        Assert.Equal(saved.Summary, candidate.Summary);
        Assert.Equal(saved.DefinitionSources, candidate.DefinitionSources);
        Assert.Equal(saved.CreatedAt, candidate.CreatedAt);
        Assert.Equal(2, candidate.WorkingRuleSetRevision);
        Assert.Equal(new[] { "new-rule", "old-rule" }, candidate.SelectedWorkingRuleIds);
        Assert.Equal(1, saved.WorkingRuleSetRevision);
        Assert.Equal(1, active.WorkingRuleSetRevision);
    }

    [Fact]
    public void Pending_schema_work_and_stale_or_wrong_decisions_are_refused()
    {
        var (decision, rules, active, saved) = Fixture();

        Assert.Throws<ArgumentException>(() => ExpansionRequestService.PrepareRuleContextUpdate(decision, rules, active,
            saved with { SchemaJson = saved.SchemaJson.Replace("Story", "Pending Story", StringComparison.Ordinal) }, Now));
        Assert.Throws<ArgumentException>(() => ExpansionRequestService.PrepareRuleContextUpdate(decision with { Kind = ExpansionSchemaDecisionKind.SchemaWorkRequested }, rules, active, saved, Now));
        Assert.Throws<ArgumentException>(() => ExpansionRequestService.PrepareRuleContextUpdate(decision with { WorkingRuleSetRevision = 1 }, rules, active, saved, Now));
        Assert.Throws<ArgumentException>(() => ExpansionRequestService.PrepareRuleContextUpdate(decision with { ProjectId = "other" }, rules, active, saved, Now));
        Assert.Throws<ArgumentException>(() => ExpansionRequestService.PrepareRuleContextUpdate(decision, rules with { Rules = [rules.Rules[1]] }, active, saved, Now));
    }

    private static (ExpansionSchemaDecision, WorkingRuleSet, ActiveGameSchema, ProjectSchemaProposal) Fixture()
    {
        var oldRule = new WorkingRule("old-rule", "Promises", WorkingRuleKind.Rule, "Promises are recorded.", [], [], [], [], null, [], []);
        var newRule = oldRule with { Id = "new-rule", Name = "Promise Renewal", AuthoritativeText = "Renew promises at gatherings." };
        var rules = new WorkingRuleSet("rules", "project", "0.1", "Story Rules", null, 2, Now, Now, [oldRule, newRule], [], [], [], []);
        const string json = """{"id":"story","name":"Story","version":"1","entityTypes":[{"metadata":{"id":"promise","name":"Promise"},"properties":[]}]}""";
        var saved = new ProjectSchemaProposal("proposal", "project", "0.1", "Keep this summary.", json,
            [new DefinitionSource("promise", ["old-rule"])], "rules", 1, ["old-rule"], Now, Now);
        var active = new ActiveGameSchema("active", "project", "0.1", "Story", json, saved.Id, "rules", 1, 1, Now, Now);
        var decision = new ExpansionSchemaDecision("decision", "project", "request", ExpansionSchemaDecisionKind.NoSchemaChangeRequired, 2, ["new-rule"], "Prose only.", Now);
        return (decision, rules, active, saved);
    }
}
