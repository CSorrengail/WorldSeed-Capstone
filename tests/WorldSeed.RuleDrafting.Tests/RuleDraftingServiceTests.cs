using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

namespace WorldSeed.RuleDrafting.Tests;

public class RuleDraftingServiceTests
{
    [Fact]
    public async Task Produces_a_valid_structured_draft_and_supplies_prompt_context()
    {
        var model = new FakeModel("""
        {"action":"presentDraft","draft":{"title":"Dangerous Magic","intent":"Magic leaves marks.","rules":[{"id":"casting-adds-mark","name":"Casting Adds a Mark","kind":"rule","text":"Casting magic gives the caster a lasting mark.","sourceNoteIds":["source-001"]}],"concepts":[{"id":"lasting-mark","name":"Lasting Mark","description":"A persistent magical consequence."}],"assumptions":[],"openQuestions":[],"exclusions":["No dice system is defined."]}}
        """);
        var service = new RuleDraftingService(model);
        var conversation = new RuleDraftConversation("project-001", ["source-001"], [new(LlmMessageRole.User, "Magic should be dangerous and leave marks.")]);

        var turn = await service.AdvanceAsync(conversation);

        Assert.Equal(RuleDraftAction.PresentDraft, turn.Action);
        Assert.Equal("Dangerous Magic", turn.Draft!.Title);
        Assert.Contains(model.Request!.Messages, message => message.Content.Contains("Source material IDs for this turn: source-001"));
        Assert.Contains(model.Request.Messages, message => message.Content.Contains("Return JSON only"));
    }

    [Fact]
    public async Task Allows_a_single_clarifying_question_before_a_draft()
    {
        var model = new FakeModel("""{"action":"askClarifyingQuestion","clarifyingQuestion":"What effect should a lasting mark have?"}""");
        var service = new RuleDraftingService(model);

        var turn = await service.AdvanceAsync(new RuleDraftConversation("project-001", ["source-001"], [new(LlmMessageRole.User, "Magic leaves marks.")]));

        Assert.Equal(RuleDraftAction.AskClarifyingQuestion, turn.Action);
        Assert.Equal("What effect should a lasting mark have?", turn.ClarifyingQuestion);
        Assert.Null(turn.Draft);
    }

    [Fact]
    public void Rejects_non_json_or_untraceable_rule_text()
    {
        var parser = new RuleDraftJsonParser();
        Assert.Throws<RuleDraftFormatException>(() => parser.Parse("Here is your draft: magic is dangerous."));
        Assert.Throws<RuleDraftFormatException>(() => parser.Parse("""
        {"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Text","sourceNoteIds":[]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}
        """));
    }

    private sealed class FakeModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse(content, "test-model", null));
        }
    }
}
