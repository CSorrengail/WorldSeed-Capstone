using WorldSeed.LlmIntegration;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation.Tests;

public class SchemaTranslationServiceTests
{
    [Fact]
    public async Task Returns_a_valid_noncanonical_schema_proposal_with_working_rule_mappings()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"A caster schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["casting-adds-mark"]}]}}""");
        var service = new SchemaTranslationService(model);

        var turn = await service.TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal("dangerous-magic", turn.Proposal!.Schema["id"]!.GetValue<string>());
        Assert.Equal("working-rules-dangerous-magic", turn.Proposal.WorkingRuleSetId);
        Assert.Equal(3, turn.Proposal.WorkingRuleSetRevision);
        Assert.Contains("small selected group", model.Request!.Messages[0].Content);
        Assert.Contains("workingRuleIds", model.Request.Messages[0].Content);
        Assert.Equal(2400, model.Request.MaxOutputTokens);
        Assert.DoesNotContain("Evidence", model.Request.Messages[1].Content);
    }

    [Fact]
    public async Task Rejects_proposals_with_invented_or_unmapped_working_rule_links()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"Bad mapping.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["invented-rule"]}]}}""");
        var service = new SchemaTranslationService(model);

        var exception = await Assert.ThrowsAsync<SchemaTranslationFormatException>(() => service.TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet())));

        Assert.Contains("must cite only working rule ids", exception.Message);
    }

    [Fact]
    public async Task Rejects_an_unbounded_rule_selection_before_calling_the_model()
    {
        var original = Assert.Single(CreateWorkingRuleSet().Rules);
        var ruleSet = CreateWorkingRuleSet() with { Rules = Enumerable.Range(1, SchemaTranslationService.MaxWorkingRulesPerProposal + 1).Select(index => original with { Id = "rule-" + index }).ToArray() };
        var model = new FakeModel("{}");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(ruleSet)));

        Assert.Contains("at most", exception.Message);
        Assert.Null(model.Request);
    }

    private static WorkingRuleSet CreateWorkingRuleSet()
    {
        var now = DateTimeOffset.Parse("2026-10-14T00:00:00Z");
        var rule = new WorkingRule("casting-adds-mark", "Casting Adds a Mark", WorkingRuleKind.Rule, "Casting magic gives the caster a lasting mark.", [new WorkingRuleEvidence("source-001", "source-001:p001", "Magic leaves marks.")], [new DraftRuleReference("session-001", "draft-001")], [], [], null, [], []);
        return new WorkingRuleSet("working-rules-dangerous-magic", "project-dangerous-magic", "0.1", "Dangerous Magic", null, 3, now, now, [rule], [], [], [], []);
    }

    private sealed class FakeModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) { Request = request; return Task.FromResult(new LlmChatResponse(content, "test-model", null)); }
    }
}
