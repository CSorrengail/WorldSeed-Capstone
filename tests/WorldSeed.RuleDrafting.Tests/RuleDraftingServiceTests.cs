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
        Assert.Contains(model.Request.Messages, message => message.Content.Contains("Do not replace a required clarification with openQuestions"));
        Assert.Contains(model.Request.Messages, message => message.Content.Contains("means choosing the same revealed option"));
        Assert.NotNull(model.Request.ResponseSchema);
        Assert.Equal("source-001", model.Request.ResponseSchema!["properties"]!["draft"]!["properties"]!["rules"]!["items"]!["properties"]!["sourceNoteIds"]!["items"]!["enum"]![0]!.GetValue<string>());
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
    public async Task Allows_up_to_five_independent_clarifying_questions()
    {
        var model = new FakeModel("""{"action":"askClarifyingQuestion","clarifyingQuestions":["What triggers it?","Who chooses?"]}""");
        var service = new RuleDraftingService(model);

        var turn = await service.AdvanceAsync(new RuleDraftConversation("project-001", ["source-001"], [new(LlmMessageRole.User, "There is an effect.")]));

        Assert.Equal(["What triggers it?", "Who chooses?"], turn.ClarifyingQuestions);
        Assert.Equal("What triggers it?", turn.ClarifyingQuestion);
    }

    [Fact]
    public void Rejects_non_json_or_untraceable_rule_text()
    {
        var parser = new RuleDraftJsonParser();
        var sources = new HashSet<string>(["source-001"], StringComparer.Ordinal);
        Assert.Throws<RuleDraftFormatException>(() => parser.Parse("Here is your draft: magic is dangerous.", sources));
        var incomplete = Assert.Throws<RuleDraftFormatException>(() => parser.Parse("""{"action":"presentDraft""", sources));
        Assert.Contains("appears incomplete", incomplete.Message);
        Assert.Throws<RuleDraftFormatException>(() => parser.Parse("""
        {"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Text","sourceNoteIds":[]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}
        """, sources));
    }

    [Fact]
    public void Verifies_supporting_excerpts_against_the_original_source_note()
    {
        var parser = new RuleDraftJsonParser();
        var sourceIds = new HashSet<string>(["source-001"], StringComparer.Ordinal);
        var sourceText = new Dictionary<string, string> { ["source-001"] = "Magic leaves a lasting mark on the caster." };
        var valid = """{"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Magic leaves a mark.","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","excerpt":"Magic leaves a lasting mark"}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""";
        var unsupported = """{"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Magic harms the caster.","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","excerpt":"Magic harms the caster"}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""";

        var turn = parser.Parse(valid, sourceIds, sourceText);

        Assert.Equal("Magic leaves a lasting mark", turn.Draft!.Rules.Single().SourceSupport.Single().Excerpt);
        var exception = Assert.Throws<RuleDraftFormatException>(() => parser.Parse(unsupported, sourceIds, sourceText));
        Assert.Contains("does not occur", exception.Message);
    }

    [Fact]
    public void Splits_ellipsized_evidence_into_separate_exact_excerpts_and_normalizes_typography()
    {
        var parser = new RuleDraftJsonParser();
        var sourceIds = new HashSet<string>(["source-001"], StringComparer.Ordinal);
        var sourceText = new Dictionary<string, string> { ["source-001"] = "A combatant's turn starts. A tie is rerolled." };
        var response = """{"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Text","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","excerpt":"A combatant’s turn starts...A tie is rerolled."}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""";

        var turn = parser.Parse(response, sourceIds, sourceText);

        Assert.Equal(2, turn.Draft!.Rules.Single().SourceSupport.Count);
    }

    [Fact]
    public void Repairs_a_high_confidence_near_quote_to_the_literal_source_text()
    {
        var parser = new RuleDraftJsonParser();
        var sourceIds = new HashSet<string>(["source-001"], StringComparer.Ordinal);
        var sourceText = new Dictionary<string, string> { ["source-001"] = "Each combatant starts combat by rolling a d20 and subtracting their Reaction value. This is that combatant's starting Combat Initiative." };
        var response = """{"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Text","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","excerpt":"Each combatant starts combat by rolling a d20 and subtracting their Reaction value. This is the combatant's starting Combat Initiative."}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""";

        var turn = parser.Parse(response, sourceIds, sourceText);

        Assert.Equal(sourceText["source-001"], turn.Draft!.Rules.Single().SourceSupport.Single().Excerpt);
    }

    [Fact]
    public void Materializes_exact_evidence_from_model_selected_source_passages()
    {
        var parser = new RuleDraftJsonParser();
        var sourceIds = new HashSet<string>(["source-001"], StringComparer.Ordinal);
        var sourceText = new Dictionary<string, string> { ["source-001"] = "First original sentence. Second original sentence." };
        var passages = SourcePassageCatalog.Create(sourceText);
        var response = """{"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Text","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","passageIds":["source-001:p001","source-001:p002"]}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""";

        var turn = parser.Parse(response, sourceIds, sourceText, passages);

        Assert.Equal(["First original sentence.", "Second original sentence."], turn.Draft!.Rules.Single().SourceSupport.Select(support => support.Excerpt));
        Assert.All(turn.Draft.Rules.Single().SourceSupport, support => Assert.NotNull(support.PassageId));
    }

    [Fact]
    public async Task Rejects_a_model_rule_that_cites_an_unavailable_source()
    {
        var model = new FakeModel("""
        {"action":"presentDraft","draft":{"title":"Draft","intent":"Intent","rules":[{"id":"rule","name":"Rule","kind":"rule","text":"Text","sourceNoteIds":["invented-source"]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}
        """);
        var service = new RuleDraftingService(model);

        var exception = await Assert.ThrowsAsync<RuleDraftFormatException>(() => service.AdvanceAsync(new RuleDraftConversation("project-001", ["source-001"], [new(LlmMessageRole.User, "Idea")])));

        Assert.Contains("not available in this conversation", exception.Message);
    }

    [Fact]
    public async Task Retries_once_with_a_compact_prompt_when_the_initial_draft_is_truncated()
    {
        var model = new SequenceModel(
            """{"action":"presentDraft","draft":{"title":"Incomplete""",
            """{"action":"presentDraft","draft":{"title":"Combat","intent":"Resolve turn order.","rules":[{"id":"turn-order","name":"Turn Order","kind":"procedure","text":"A combatant with zero initiative takes a turn.","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","passageIds":["source-001:p001"]}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""");
        var service = new RuleDraftingService(model);
        var sourceText = new Dictionary<string, string> { ["source-001"] = "A combatant with zero initiative takes a turn." };
        var conversation = new RuleDraftConversation(
            "project-001",
            ["source-001"],
            [new(LlmMessageRole.User, "Describe initiative.")],
            sourceText,
            SourcePassageCatalog.Create(sourceText));

        var result = await service.AdvanceWithTranscriptAsync(conversation);

        Assert.Equal("Combat", result.Turn.Draft!.Title);
        Assert.Equal(2, model.Requests.Count);
        Assert.Equal(4800, model.Requests[0].MaxOutputTokens);
        Assert.Equal(2400, model.Requests[1].MaxOutputTokens);
        Assert.Contains(model.Requests[1].Messages, message => message.Content.Contains("previous response was cut off"));
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

    private sealed class SequenceModel(params string[] contents) : ILanguageModelClient
    {
        private readonly Queue<string> _contents = new(contents);
        public List<LlmChatRequest> Requests { get; } = [];

        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new LlmChatResponse(_contents.Dequeue(), "test-model", null));
        }
    }
}
