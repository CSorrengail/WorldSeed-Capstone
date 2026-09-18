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
