using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

namespace WorldSeed.RuleDrafting.Tests;

public class BoundedSourceIntakeTests
{
    private const string SourceId = "source-1b3b69e75dc64dfd9a7d077080083603";

    [Fact]
    public async Task Batches_realistic_source_ids_and_restores_every_permanent_citation()
    {
        var source = string.Join(" ", Enumerable.Range(1, 14).Select(index => $"At level {index}, gain {index} training points."));
        var model = new ExtractionModel();
        var result = await new RuleDraftingService(model).AdvanceWithTranscriptAsync(Conversation(source));

        Assert.Equal(3, model.Requests.Count);
        Assert.Equal(14, result.Turn.Draft!.Rules.Count);
        Assert.Equal(14, result.Turn.Draft.Rules.Select(rule => rule.Id).Distinct().Count());
        Assert.All(model.Requests, request =>
        {
            Assert.InRange(Targets(request).Count, 1, RuleDraftingService.MaximumIntakePassages);
            Assert.DoesNotContain(request.Messages, message => message.Content.Contains(SourceId));
            Assert.DoesNotContain(request.Messages, message => message.Content == source);
            Assert.Equal(RuleDraftingService.MaximumIntakeOutputTokens, request.MaxOutputTokens);
        });
        var catalog = SourcePassageCatalog.Create(new Dictionary<string, string> { [SourceId] = source });
        Assert.All(catalog, passage => Assert.Contains(result.Turn.Draft.Rules.SelectMany(rule => rule.SourceSupport),
            support => support.SourceNoteId == SourceId && support.PassageId == passage.Id && support.Excerpt == passage.Text));
        Assert.Empty(new StructuredRuleDraftValidator().Validate(result.Turn.Draft, true, new HashSet<string> { SourceId }, new Dictionary<string, string> { [SourceId] = source }));
        Assert.Contains(SourceId, result.RawModelResponse);
        Assert.DoesNotContain(":segment", result.RawModelResponse);
    }

    [Fact]
    public async Task Incomplete_responses_split_input_instead_of_reducing_whole_file_output_budget()
    {
        var model = new ExtractionModel((request, _) => Targets(request).Count > 2 ? "{\"action\":\"presentDraft\"" : null);
        var result = await new RuleDraftingService(model).AdvanceAsync(Conversation("Gain one point. Gain two points. Gain three points. Gain four points."));

        Assert.Equal(3, model.Requests.Count);
        Assert.Equal(4, result.Draft!.Rules.Count);
        Assert.Equal(new[] { 4, 2, 2 }, model.Requests.Select(request => Targets(request).Count));
        Assert.All(model.Requests, request => Assert.Equal(3200, request.MaxOutputTokens));
    }

    [Theory]
    [InlineData("What would you like me to do with this text?")]
    [InlineData("Are you interested in a specific aspect of these class descriptions?")]
    public async Task Generic_task_questions_are_corrected_before_returning_to_designer(string question)
    {
        var model = new ExtractionModel((_, call) => call == 1
            ? new JsonObject { ["action"] = "askClarifyingQuestion", ["clarifyingQuestions"] = new JsonArray(question) }.ToJsonString() : null);
        var result = await new RuleDraftingService(model).AdvanceAsync(Conversation("Every character gains one point."));

        Assert.Equal(RuleDraftAction.PresentDraft, result.Action);
        Assert.Empty(result.ClarifyingQuestions);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains(model.Requests[1].Messages, message => message.Content.Contains("task is already specified"));
    }

    [Fact]
    public async Task Metadata_only_batches_are_accounted_for_without_inventing_rules()
    {
        var source = string.Join(" ", Enumerable.Repeat("This chapter contains examples.", 6)) + " Every character gains one point.";
        var model = new ExtractionModel((request, _) => Targets(request).All(target => target.Text.StartsWith("This chapter"))
            ? Response([], new JsonArray(Targets(request).Select(target => (JsonNode)new JsonObject
            { ["passageId"] = target.Id, ["disposition"] = "metadata", ["reason"] = "Chapter navigation." }).ToArray())) : null);
        var result = await new RuleDraftingService(model).AdvanceAsync(Conversation(source));

        Assert.Single(result.Draft!.Rules);
        Assert.Equal(6, result.Draft.Exclusions.Count);
    }

    [Fact]
    public async Task Long_passage_fragments_restore_parent_anchors_and_share_saved_answers()
    {
        var source = new string('x', 2600) + ".";
        var answerId = "answer-375ba01077484e2fabc7eb2e07545bf2";
        var answer = "Each point is a training point.";
        var model = new ExtractionModel();
        var conversation = Conversation(source) with
        {
            SourceMaterialIds = [SourceId, answerId],
            SourceTextById = new Dictionary<string, string> { [SourceId] = source, [answerId] = answer },
            Messages = [new(LlmMessageRole.User, source), new(LlmMessageRole.User, answer)]
        };
        var result = await new RuleDraftingService(model).AdvanceAsync(conversation);

        Assert.Equal(2, model.Requests.Count);
        Assert.All(model.Requests, request => Assert.Contains(request.Messages, message => message.Content.StartsWith("Saved designer answers:") && message.Content.Contains(answer)));
        Assert.All(result.Draft!.Rules.SelectMany(rule => rule.SourceSupport), support =>
        {
            Assert.Equal(SourceId + ":p001", support.PassageId);
            Assert.Equal(source, support.Excerpt);
        });
    }

    private static RuleDraftConversation Conversation(string source) => new("project", [SourceId],
        [new(LlmMessageRole.User, source)], new Dictionary<string, string> { [SourceId] = source });

    private static List<(string Id, string Note, string Text)> Targets(LlmChatRequest request) => Regex.Matches(
        request.Messages.Single(message => message.Content.StartsWith("Target passages:\n")).Content,
        @"\[(p\d+) \| (n\d+)\] ([^\n]+)").Select(match => (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value)).ToList();

    private static string Response(JsonArray rules, JsonArray? notes = null) => new JsonObject
    {
        ["action"] = "presentDraft",
        ["draft"] = new JsonObject { ["title"] = "Training", ["intent"] = "Organize training rules.", ["rules"] = rules,
            ["concepts"] = new JsonArray(), ["assumptions"] = new JsonArray(), ["openQuestions"] = new JsonArray(), ["exclusions"] = new JsonArray() },
        ["passageNotes"] = notes ?? new JsonArray()
    }.ToJsonString();

    private sealed class ExtractionModel(Func<LlmChatRequest, int, string?>? intercept = null) : ILanguageModelClient
    {
        public List<LlmChatRequest> Requests { get; } = [];
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var response = intercept?.Invoke(request, Requests.Count) ?? Response(new JsonArray(Targets(request).Select((target, index) =>
                (JsonNode)new JsonObject { ["id"] = "rule-" + index, ["name"] = "Training Rule", ["kind"] = "rule", ["text"] = target.Text,
                    ["sourceNoteIds"] = new JsonArray(target.Note), ["sourceSupport"] = new JsonArray(new JsonObject
                    { ["sourceNoteId"] = target.Note, ["passageIds"] = new JsonArray(target.Id) }) }).ToArray()));
            return Task.FromResult(new LlmChatResponse(response, "deterministic-test-model", null));
        }
    }
}
