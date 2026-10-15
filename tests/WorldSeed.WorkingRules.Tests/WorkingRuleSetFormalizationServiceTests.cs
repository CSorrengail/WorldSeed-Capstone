using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.WorkingRules;

namespace WorldSeed.WorkingRules.Tests;

public class WorkingRuleSetFormalizationServiceTests
{
    [Fact]
    public async Task Consolidates_completed_source_drafts_and_materializes_exact_evidence()
    {
        var project = CreateProject();
        var model = new CapturingModel("""
        {"name":"Combat Rules","description":"Current combat proposal.","rules":[{"id":"combat-turn-order","name":"Combat Turn Order","kind":"procedure","authoritativeText":"At the beginning of combat, the combatant with the lowest initiative takes a turn.","sourceDraftIds":["d001"],"conceptIds":["initiative"],"schemaHints":[{"kind":"property","suggestedId":"combatant.initiative","suggestedName":"Initiative","parentSuggestedId":"combatant","rationale":"Turn order uses initiative.","sourceWorkingRuleIds":["combat-turn-order"]}],"openQuestions":[],"exclusions":[]}],"concepts":[{"id":"initiative","name":"Initiative","description":"A value that determines combat turn order.","sourceDraftIds":["d001"]}],"openQuestions":[],"exclusions":[],"conflicts":[]}
        """);
        var now = DateTimeOffset.Parse("2026-10-14T00:00:00Z");
        var service = new WorkingRuleSetFormalizationService(model, timeProvider: new FixedTimeProvider(now));

        var result = await service.CreateProposalAsync(project);

        var rule = Assert.Single(result.RuleSet.Rules);
        Assert.Equal("working-rules-project-initiative", result.RuleSet.Id);
        Assert.Equal(1, result.RuleSet.Revision);
        Assert.Equal(now, result.RuleSet.CreatedAt);
        Assert.Equal("source-initiative:p001", Assert.Single(rule.Evidence).PassageId);
        Assert.Equal("At the beginning of combat, the combatant with the lowest initiative takes a turn.", rule.Evidence.Single().Excerpt);
        Assert.Contains(model.Request!.Messages, message => message.Content.Contains("[d001]"));
        Assert.Contains(model.Request.Messages, message => message.Content.Contains("Never invent mechanics"));
        Assert.True(model.Request.RequireJsonObject);
    }

    [Fact]
    public async Task Replaces_the_working_proposal_with_the_next_revision()
    {
        var project = CreateProject();
        var model = new CapturingModel(ValidResponse);
        var existing = new WorkingRuleSet("existing-id", project.Id, "0.1", "Old", null, 4, DateTimeOffset.Parse("2026-10-01T00:00:00Z"), DateTimeOffset.Parse("2026-10-02T00:00:00Z"), [], [], [], [], []);
        var now = DateTimeOffset.Parse("2026-10-14T00:00:00Z");

        var result = await new WorkingRuleSetFormalizationService(model, timeProvider: new FixedTimeProvider(now)).CreateProposalAsync(project, existing);

        Assert.Equal("existing-id", result.RuleSet.Id);
        Assert.Equal(5, result.RuleSet.Revision);
        Assert.Equal(existing.CreatedAt, result.RuleSet.CreatedAt);
        Assert.Equal(now, result.RuleSet.UpdatedAt);
        Assert.Contains(model.Request!.Messages, message => message.Content.Contains("Current working proposal"));
    }

    [Fact]
    public async Task Rejects_unknown_source_draft_references_before_a_rule_set_can_be_saved()
    {
        var model = new CapturingModel(ValidResponse.Replace("d001", "d999", StringComparison.Ordinal));

        var error = await Assert.ThrowsAsync<WorkingRuleSetFormatException>(() => new WorkingRuleSetFormalizationService(model).CreateProposalAsync(CreateProject()));

        Assert.Contains("citation 'd999' is not available", error.Message);
    }

    [Fact]
    public async Task Ignores_an_incomplete_optional_schema_hint_while_preserving_the_working_rule()
    {
        var response = ValidResponse.Replace("\"schemaHints\":[]", "\"schemaHints\":[{\"kind\":\"property\",\"suggestedId\":\"combatant.initiative\",\"rationale\":\"Incomplete optional suggestion.\",\"sourceWorkingRuleIds\":[\"combat-turn-order\"]}]", StringComparison.Ordinal);

        var result = await new WorkingRuleSetFormalizationService(new CapturingModel(response)).CreateProposalAsync(CreateProject());

        Assert.Empty(Assert.Single(result.RuleSet.Rules).SchemaHints);
    }

    [Fact]
    public async Task Requires_a_completed_source_draft()
    {
        var emptyProject = DesignProjectService.Create("empty", "Empty");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => new WorkingRuleSetFormalizationService(new CapturingModel("{}")).CreateProposalAsync(emptyProject));

        Assert.Contains("completed source draft", error.Message);
    }

    private const string ValidResponse = """
    {"name":"Combat Rules","description":null,"rules":[{"id":"combat-turn-order","name":"Combat Turn Order","kind":"procedure","authoritativeText":"At the beginning of combat, the combatant with the lowest initiative takes a turn.","sourceDraftIds":["d001"],"conceptIds":[],"schemaHints":[],"openQuestions":[],"exclusions":[]}],"concepts":[],"openQuestions":[],"exclusions":[],"conflicts":[]}
    """;

    private static DesignProject CreateProject()
    {
        var now = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var source = new DesignSourceNote("source-initiative", "At the beginning of combat, the combatant with the lowest initiative takes a turn.", now, new SourceNoteOrigin("initiative.txt"));
        var project = DesignProjectService.AddSources(DesignProjectService.Create("project-initiative", "Initiative Project"), [source]);
        var draftRule = new RuleStatement("draft-turn-order", "Turn Order", RuleStatementKind.Procedure, "The lowest initiative acts first.", [source.Id], [new RuleSourceSupport(source.Id, source.OriginalText, "source-initiative:p001")]);
        var draft = new StructuredRuleDraft("Initiative", "Define turn order.", [draftRule], [], [], [], []);
        var turn = new RuleDraftTurn(RuleDraftAction.PresentDraft, null, [], draft);
        var session = new DesignSession("session-initiative", project.Id, "local", now, now, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], turn);
        return DesignProjectService.SaveSession(project, session);
    }

    private sealed class CapturingModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse(content, "test", null));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
