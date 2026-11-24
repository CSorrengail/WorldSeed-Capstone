using System.Text.Json;
using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

namespace WorldSeed.DesignSessions.Tests;

public class SourceReviewTests
{
    [Fact]
    public async Task Non_game_decision_survives_reload_and_retry_without_extracting_its_passage()
    {
        var source = new DesignSourceNote("source-001", "Characters begin at level 1. License notice.", DateTimeOffset.UtcNow);
        var model = new Model();
        var service = new DesignSessionService(new RuleDraftingService(model));
        var draft = new StructuredRuleDraft("Levels", "Organize levels.",
            [new("start", "Start", RuleStatementKind.Rule, "Characters begin at level 1.", [source.Id], [new(source.Id, "Characters begin at level 1.", source.Id + ":p001")])], [], [],
            ["[source-001:p002] Extraction Needs Review: License notice."], []);
        var session = service.Start("session", "project", "local", source) with { LatestTurn = new(RuleDraftAction.PresentDraft, null, [], draft) };
        var classified = DesignSessionService.MarkReviewAsNonGameMaterial(session, draft.OpenQuestions[0]);
        classified = JsonSerializer.Deserialize<DesignSession>(JsonSerializer.Serialize(classified))!;

        Assert.Empty(classified.LatestTurn!.Draft!.OpenQuestions);
        Assert.Single(classified.LatestTurn.Draft.Rules);
        Assert.Equal(source.OriginalText, classified.SourceNotes[0].OriginalText);
        Assert.Equal("source-001:p002", Assert.Single(classified.SourceReviewDecisions!).PassageId);
        var retried = await service.AdvanceAsync(classified);
        Assert.Equal(session.Id, retried.Id);
        Assert.Single(retried.LatestTurn!.Draft!.Rules);
        Assert.Contains(retried.LatestTurn.Draft.Exclusions, item => item.Contains("License notice."));
        Assert.DoesNotContain("License notice.", model.Request!.Messages.Single(message => message.Content.StartsWith("Target passages:")).Content);
        Assert.Throws<ArgumentException>(() => DesignSessionService.MarkReviewAsNonGameMaterial(classified, draft.OpenQuestions[0]));
    }

    [Fact]
    public void Completed_review_and_saved_answers_survive_close_housekeeping()
    {
        var source = new DesignSourceNote("source", "Rules.", DateTimeOffset.UtcNow);
        var service = new DesignSessionService(new RuleDraftingService(new Model()));
        var session = service.Start("session", "project", "local", source) with
        { LatestTurn = new(RuleDraftAction.PresentDraft, null, [], new("Rules", "Organize rules.", [], [], [], ["What happens next?"], [])) };
        session = service.AddDesignerMessage(session, "Next step is narrative.", new("answer", "Next step is narrative.", DateTimeOffset.UtcNow));
        var project = DesignProjectService.SaveSession(DesignProjectService.Create("project", "Project"), session);
        var restored = JsonSerializer.Deserialize<DesignProject>(JsonSerializer.Serialize(DesignProjectService.EndIncompleteSessions(project)))!;
        Assert.Equal("What happens next?", Assert.Single(Assert.Single(restored.Sessions).LatestTurn!.Draft!.OpenQuestions));
        Assert.Equal("Next step is narrative.", restored.Sessions[0].SourceNotes[1].OriginalText);
    }

    private sealed class Model : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse("""{"action":"presentDraft","draft":{"title":"Levels","intent":"Organize levels.","rules":[{"id":"start","name":"Start","kind":"rule","text":"Characters begin at level 1.","sourceNoteIds":["n1"],"sourceSupport":[{"sourceNoteId":"n1","passageIds":["p1"]}]}],"concepts":[],"assumptions":[],"openQuestions":[],"exclusions":[]}}""", "fake", null));
        }
    }
}
