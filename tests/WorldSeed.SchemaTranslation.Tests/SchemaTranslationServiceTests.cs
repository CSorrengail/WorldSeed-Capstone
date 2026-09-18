using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.SchemaTranslation.Tests;

public class SchemaTranslationServiceTests
{
    private static readonly StructuredRuleDraft Draft = new("Dangerous Magic", "Magic leaves marks.", [new RuleStatement("casting-adds-mark", "Casting Adds a Mark", RuleStatementKind.Rule, "Casting creates a mark.", ["source-001"], [new RuleSourceSupport("source-001", "Magic leaves marks.")])], [], [], [], []);

    [Fact]
    public async Task Returns_a_valid_noncanonical_schema_proposal_with_rule_mappings()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"A caster schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","ruleIds":["casting-adds-mark"]}]}}""");
        var service = new SchemaTranslationService(model);

        var turn = await service.TranslateAsync(new SchemaTranslationRequest(Draft));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal("dangerous-magic", turn.Proposal!.Schema["id"]!.GetValue<string>());
        Assert.Contains("candidate canonical game schema", model.Request!.Messages[0].Content);
    }

    [Fact]
    public async Task Rejects_proposals_with_invented_or_unmapped_rule_links()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"Bad mapping.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","ruleIds":["invented-rule"]}]}}""");
        var service = new SchemaTranslationService(model);

        var exception = await Assert.ThrowsAsync<SchemaTranslationFormatException>(() => service.TranslateAsync(new SchemaTranslationRequest(Draft)));

        Assert.Contains("must cite only rule ids", exception.Message);
    }

    private sealed class FakeModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) { Request = request; return Task.FromResult(new LlmChatResponse(content, "test-model", null)); }
    }
}
