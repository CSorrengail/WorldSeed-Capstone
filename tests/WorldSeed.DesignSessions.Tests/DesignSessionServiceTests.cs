using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

namespace WorldSeed.DesignSessions.Tests;

public class DesignSessionServiceTests
{
    [Fact]
    public async Task Preserves_original_notes_messages_and_the_latest_validated_turn()
    {
        var model = new FakeModel("""{"action":"askClarifyingQuestion","clarifyingQuestion":"What makes a mark lasting?"}""");
        var service = new DesignSessionService(new RuleDraftingService(model));
        var source = new DesignSourceNote("source-001", "Magic leaves marks.", DateTimeOffset.UtcNow);
        var session = service.Start("session-001", "project-001", "local-model", source);
        session = service.AddDesignerMessage(session, "The marks persist between adventures.");

        var advanced = await service.AdvanceAsync(session);

        Assert.Equal("Magic leaves marks.", advanced.SourceNotes.Single().OriginalText);
        Assert.Equal(3, advanced.Messages.Count);
        Assert.Equal(LlmMessageRole.Assistant, advanced.Messages.Last().Role);
        Assert.Equal(RuleDraftAction.AskClarifyingQuestion, advanced.LatestTurn!.Action);
        Assert.Contains("source-001", model.Request!.Messages[1].Content);
    }

    [Fact]
    public async Task Expansion_workflow_requires_clarification_on_its_first_turn()
    {
        var model = new FakeModel("""{"action":"presentDraft","draft":{"title":"Magic","intent":"Define marks.","rules":[{"id":"marks","name":"Marks","kind":"rule","text":"Magic leaves marks.","sourceNoteIds":["source-001"],"sourceSupport":[{"sourceNoteId":"source-001","passageIds":["source-001:p001"]}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""");
        var service = new DesignSessionService(new RuleDraftingService(model));
        var source = new DesignSourceNote("source-001", "Magic leaves marks.", DateTimeOffset.UtcNow);
        var session = service.Start("session-001", "project-001", "local-model", source);

        var exception = await Assert.ThrowsAsync<RuleDraftFormatException>(() =>
            service.AdvanceAsync(session, "Ask clarification before drafting.", true));

        Assert.Contains("first expansion turn", exception.Message);
        Assert.Contains(model.Request!.Messages, message => message.Content == "Ask clarification before drafting.");
        Assert.Contains(model.Request.Messages, message => message.Content.Contains("Clearly label them Proposed Options"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expansion_requires_the_designer_choice_to_be_saved_before_drafting(bool saveAnswerSource)
    {
        var firstService = new DesignSessionService(new RuleDraftingService(new FakeModel(DesignChoicesResponse)));
        var source = new DesignSourceNote("goal", "Develop promise renewal.", DateTimeOffset.UtcNow);
        var session = await firstService.AdvanceAsync(firstService.Start("session", "project", "local", source), "Explore proposed options.", true);
        var answer = "Question: " + session.LatestTurn!.ClarifyingQuestions.Single() + "\nAnswer: Use A; reject B";
        session = firstService.AddDesignerMessage(session, answer, saveAnswerSource ? new DesignSourceNote("choice", answer, DateTimeOffset.UtcNow) : null);
        var passages = SourcePassageCatalog.Create(new Dictionary<string, string> { ["choice"] = answer });
        var choiceAnchors = System.Text.Json.JsonSerializer.Serialize(new[] { passages.First(passage => passage.Text.Contains("Proposed Option A:")).Id, passages.Last().Id });
        var model = new FakeModel("""{"action":"presentDraft","draft":{"title":"Promise Renewal","intent":"Record the chosen renewal rule.","rules":[{"id":"renewal","name":"Renewal","kind":"rule","text":"Promises are renewed at gatherings.","sourceNoteIds":["choice"],"sourceSupport":[{"sourceNoteId":"choice","passageIds":ANCHORS}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""".Replace("ANCHORS", choiceAnchors, StringComparison.Ordinal));
        var followup = new DesignSessionService(new RuleDraftingService(model));

        if (!saveAnswerSource)
        {
            var error = await Assert.ThrowsAsync<RuleDraftFormatException>(() => followup.AdvanceAsync(session, "Explore proposed options.", true));
            Assert.Contains("saved as source material", error.Message);
            Assert.Null(model.Request);
            return;
        }

        var result = await followup.AdvanceAsync(session, "Explore proposed options.", true);
        var rule = Assert.Single(result.LatestTurn!.Draft!.Rules);
        Assert.All(rule.SourceSupport, support => Assert.Equal("choice", support.SourceNoteId));
        Assert.Contains(rule.SourceSupport, support => support.Excerpt.Contains("Answer: Use A; reject B"));
        Assert.Contains(model.Request!.Messages, message => message.Content.Contains("Never adopt rejected or unselected alternatives"));
    }

    [Fact]
    public async Task Expansion_cannot_advance_again_without_a_new_designer_answer()
    {
        var model = new FakeModel(DesignChoicesResponse);
        var service = new DesignSessionService(new RuleDraftingService(model));
        var source = new DesignSourceNote("goal", "Develop promise renewal.", DateTimeOffset.UtcNow);
        var session = await service.AdvanceAsync(service.Start("session", "project", "local", source), "Explore proposed options.", true);
        var firstRequest = model.Request;

        await Assert.ThrowsAsync<RuleDraftFormatException>(() => service.AdvanceAsync(session, "Explore proposed options.", true));

        Assert.Same(firstRequest, model.Request);
    }

    [Fact]
    public async Task Saves_and_loads_a_session_without_storing_a_secret()
    {
        var directory = Path.Combine(Path.GetTempPath(), "worldseed-design-session-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonDesignSessionStore(directory);
            var session = new DesignSession("session-001", "project-001", "profile-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                [new DesignSourceNote("source-001", "Original note", DateTimeOffset.UtcNow)], [new LlmMessage(LlmMessageRole.User, "Original note")], null);

            await store.SaveAsync(session);
            var loaded = await store.LoadAsync("session-001");

            Assert.NotNull(loaded);
            Assert.Equal("profile-001", loaded!.ModelProfileId);
            Assert.Equal("Original note", loaded.SourceNotes.Single().OriginalText);
            Assert.True(File.Exists(Path.Combine(directory, "session-001.worldseed-session.json")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_an_unsafe_session_filename()
    {
        var store = new JsonDesignSessionStore(Path.GetTempPath());
        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync("../outside"));
    }

    [Fact]
    public async Task Saves_loads_and_updates_a_project_with_its_sessions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "worldseed-design-project-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var source = new DesignSourceNote("source-001", "Original note", DateTimeOffset.UtcNow);
            var project = DesignProjectService.Create("project-001", "Test project", "A saved project description.");
            project = DesignProjectService.AddSources(project, [source]);
            var session = new DesignSession("session-001", project.Id, "local", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], null);
            project = DesignProjectService.SaveSession(project, session);
            var store = new JsonDesignProjectStore(directory);

            await store.SaveAsync(project);
            var loaded = await store.LoadAsync(project.Id);
            var projects = await store.ListAsync();

            Assert.Equal("Test project", loaded!.Name);
            Assert.Equal("A saved project description.", loaded.Description);
            Assert.Equal("Original note", loaded.SourceNotes.Single().OriginalText);
            Assert.Equal("session-001", loaded.Sessions.Single().Id);
            Assert.Equal("project-001", projects.Single().Id);

            await store.DeleteAsync(project.Id);
            Assert.Null(await store.LoadAsync(project.Id));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Ends_unfinished_sessions_but_keeps_completed_drafts()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new DesignSourceNote("source-001", "Original note", now);
        var project = DesignProjectService.AddSources(DesignProjectService.Create("project-001", "Test project"), [source]);
        var unfinished = new DesignSession("session-open", project.Id, "local", now, now, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], new RuleDraftTurn(RuleDraftAction.AskClarifyingQuestion, "What happens next?", ["What happens next?"], null));
        var completed = new DesignSession("session-draft", project.Id, "local", now, now, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], new RuleDraftTurn(RuleDraftAction.PresentDraft, null, [], new StructuredRuleDraft("Draft", "Intent", [], [], [], [], [])));
        project = DesignProjectService.SaveSession(DesignProjectService.SaveSession(project, unfinished), completed);

        var cleaned = DesignProjectService.EndIncompleteSessions(project);

        Assert.Equal(["session-draft"], cleaned.Sessions.Select(session => session.Id));
        Assert.True(cleaned.UpdatedAt >= project.UpdatedAt);
    }

    private const string DesignChoicesResponse = """{"action":"askClarifyingQuestion","designQuestions":[{"question":"When are promises renewed?","options":[{"mechanic":"Promises are renewed at gatherings.","tradeoff":"More frequent bookkeeping."},{"mechanic":"Promises never expire.","tradeoff":"Less opportunity to reconsider."}]}]}""";

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
