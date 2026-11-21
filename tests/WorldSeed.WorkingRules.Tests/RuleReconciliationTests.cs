using System.Text.Json;
using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.WorkingRules.Tests;

public sealed class RuleReconciliationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-20T12:00:00Z");
    [Fact]
    public async Task Pending_proposal_survives_reload_then_materializes_once_with_original_evidence()
    {
        var rules = Fixture();
        var proposal = await Propose(rules);
        var pending = RuleReconciliationService.AddProposal(rules, proposal, Now);
        pending = JsonSerializer.Deserialize<WorkingRuleSet>(JsonSerializer.Serialize(pending))!;
        Assert.Equal(2, pending.Rules.Count);
        Assert.Equal(1, pending.Revision);

        var applied = RuleReconciliationService.Apply(pending, proposal.Id, Now);

        Assert.Equal(2, applied.Revision);
        Assert.Equal("first", Assert.Single(applied.Rules).Id);
        Assert.Equal(2, applied.Rules[0].Evidence.Count);
        Assert.Equal(2, applied.Rules[0].SourceDraftRules.Count);
        var audit = Assert.Single(applied.Reconciliations!);
        Assert.Equal(RuleReconciliationState.Applied, audit.State);
        Assert.Equal(2, audit.AppliedRevision);
        Assert.Equal("Original Two", audit.Originals[1].AuthoritativeText);
        Assert.Same(applied, RuleReconciliationService.Apply(applied, proposal.Id, Now));
        Assert.Equal("Original One", rules.Rules[0].AuthoritativeText);
    }

    [Fact]
    public async Task Stale_changed_foreign_and_rejected_proposals_cannot_replace_rules()
    {
        var rules = Fixture(); var proposal = await Propose(rules);
        var pending = RuleReconciliationService.AddProposal(rules, proposal, Now);
        Assert.Throws<InvalidOperationException>(() => RuleReconciliationService.Apply(pending with { Revision = 2 }, proposal.Id, Now));
        Assert.Throws<InvalidOperationException>(() => RuleReconciliationService.Apply(pending with { Rules = [rules.Rules[0] with { AuthoritativeText = "Changed" }, rules.Rules[1]] }, proposal.Id, Now));
        Assert.Throws<InvalidOperationException>(() => RuleReconciliationService.AddProposal(rules with { ProjectId = "other" }, proposal, Now));
        var rejected = RuleReconciliationService.Reject(pending, proposal.Id, Now);
        Assert.Throws<InvalidOperationException>(() => RuleReconciliationService.Apply(rejected, proposal.Id, Now));
        Assert.Equal(rules.Rules, rejected.Rules);
    }

    [Fact]
    public async Task Clarification_requires_an_answer_and_rebuild_cannot_undo_applied_choices()
    {
        var rules = Fixture();
        var question = await new RuleReconciliationService(new Model("""{"name":"Promise","kind":"rule","text":null,"rationale":"Sources differ.","question":"Which timing should apply?"}"""))
            .ProposeAsync(rules, ["first", "second"], "", null, Now);
        var pending = RuleReconciliationService.AddProposal(rules, question, Now);
        Assert.Throws<InvalidOperationException>(() => RuleReconciliationService.Apply(pending, question.Id, Now));
        var proposal = await Propose(rules);
        var applied = RuleReconciliationService.Apply(RuleReconciliationService.AddProposal(rules, proposal, Now), proposal.Id, Now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkingRuleSetFormalizationService(new Model("unused"))
            .CreateProposalAsync(DesignProjectService.Create("project", "Story"), applied));
    }

    private static Task<RuleReconciliation> Propose(WorkingRuleSet rules) =>
        new RuleReconciliationService(new Model("""{"name":"Promises","kind":"rule","text":"A promise is recorded once at the gathering.","rationale":"Combines both requirements.","question":null}"""))
            .ProposeAsync(rules, ["first", "second"], "Record once.", null, Now);
    private static WorkingRuleSet Fixture()
    {
        var first = new WorkingRule("first", "One", WorkingRuleKind.Rule, "Original One", [new("source1", null, "Original One")], [new("session1", "rule1")], [], [], null, [], []);
        var second = first with { Id = "second", AuthoritativeText = "Original Two", Evidence = [new("source2", null, "Original Two")], SourceDraftRules = [new("session2", "rule2")] };
        return new("rules", "project", "0.1", "Story", null, 1, Now, Now, [first, second], [], [], [], []);
    }
    private sealed class Model(string output) : ILanguageModelClient
    {
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new LlmChatResponse(output, "test", null));
    }
}
