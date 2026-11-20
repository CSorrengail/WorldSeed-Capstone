using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;
using WorldSeed.Publication;
using WorldSeed.WorkingRules;

namespace WorldSeed.Publication.Tests;

/// <summary>Uses the existing game-owned card/tower fixture, not universal RPG mechanics.</summary>
public sealed class WretchedWorkflowTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-20T12:00:00Z");

    [Fact]
    public void Approved_card_prompt_is_applied_and_published_with_reader_labels()
    {
        var (rules, schema, data) = Fixture();
        var originalData = data.DataJson;
        var proposal = Prompt(schema, 2);
        var rejected = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Rejected, "QA rejection.", Now);
        Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(proposal, rejected, schema, data, Now));

        // These are fixture designer decisions, not approval of real user content.
        var approval = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, "QA fixture approval.", Now);
        var prepared = ContentApplicationService.Prepare(proposal, approval, schema, data, Now);
        var application = ContentApplicationService.Complete(prepared.Application, prepared.UpdatedGameData, Now);
        Assert.Equal(ContentApplicationStatus.Applied, application.Status);
        Assert.True(GameDataService.Validate(schema, prepared.UpdatedGameData).IsValid);
        Assert.Equal(originalData, data.DataJson);

        var snapshot = PublicationSnapshotService.Create("Card And Journal Fixture", rules, schema, prepared.UpdatedGameData, Now);
        var draft = PublicationEditionService.CreateInitial(snapshot, Now);
        var ready = PublicationEditionService.CreateRevision(snapshot,
            new(draft.Title, draft.Subtitle, draft.SectionOrder, draft.IncludedSectionIds, draft.EntitySectionOrder, true, draft.Id, draft.Revision), draft, Now);

        Assert.Equal(PublicationEditionState.Ready, ready.State);
        Assert.Equal(2, snapshot.IncludedEntityIds.Count);
        Assert.Contains("Suit: Clubs; Value: 2", ready.Markdown);
        Assert.Contains("Suit: Hearts; King Revealed: false", ready.Markdown);
        Assert.Contains("The first day begins.", ready.Markdown);
        Assert.DoesNotContain("oracle-card.", ready.Markdown);
        Assert.DoesNotContain("failure-countdown.", ready.Markdown);
        Assert.DoesNotContain("private-oracle-prompt", ready.Markdown);
        Assert.Contains("private-oracle-prompt", ready.ManifestJson);
        Assert.Contains("oracle-card.suit", snapshot.Markdown);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    public void Card_value_outside_game_owned_limits_cannot_be_applied(int cardValue)
    {
        var (_, schema, data) = Fixture();
        var originalData = data.DataJson;
        var proposal = Prompt(schema, cardValue);
        var approval = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, "QA invalid candidate.", Now);

        Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(proposal, approval, schema, data, Now));
        Assert.Equal(originalData, data.DataJson);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Invalid_game_owned_token_count_blocks_publication(int tokenCount)
    {
        var (rules, schema, data) = Fixture();
        var root = JsonNode.Parse(data.DataJson)!.AsObject();
        root["entityInstances"]![0]!["values"]!["survival-session.salvation-tokens"] = tokenCount;
        var invalid = data with { DataJson = root.ToJsonString() };

        Assert.False(GameDataService.Validate(schema, invalid).IsValid);
        Assert.Throws<InvalidOperationException>(() => PublicationSnapshotService.Create("Invalid Fixture", rules, schema, invalid, Now));
    }

    private static ContentProposal Prompt(ActiveGameSchema schema, int value)
    {
        var candidate = JsonNode.Parse("""{"id":"private-oracle-prompt","entityTypeId":"oracle-prompt","values":{"oracle-prompt.text":"A faint signal reaches the airlock. What do you record in your journal?","oracle-prompt.card":{"oracle-card.suit":"clubs","oracle-card.value":2}}}""")!.AsObject();
        candidate["values"]!["oracle-prompt.card"]!["oracle-card.value"] = value;
        return ContentProposalLifecycleService.Create(schema.ProjectId, "oracle-prompt", candidate, schema.Id, schema.Revision,
            schema.WorkingRuleSetId, schema.WorkingRuleSetRevision, Now);
    }

    private static (WorkingRuleSet Rules, ActiveGameSchema Schema, ProjectGameData Data) Fixture()
    {
        var schemaJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wretched.schema.json"));
        var dataJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wretched.data.json"));
        var schemaRoot = JsonNode.Parse(schemaJson)!.AsObject();
        var rules = schemaRoot["rules"]!.AsArray().OfType<JsonObject>().Select(rule => new WorkingRule(
            rule["metadata"]!["id"]!.GetValue<string>(), rule["metadata"]!["name"]!.GetValue<string>(), WorkingRuleKind.Rule,
            rule["authoritativeText"]!.GetValue<string>(), [], [], [], [], null, [], [])).ToArray();
        var ruleSet = new WorkingRuleSet("wretched-rules", "wretched-fixture", "0.1", "Card Rules", null, 1, Now, Now, rules, [], [], [], []);
        var active = new ActiveGameSchema("wretched-active", ruleSet.ProjectId, "0.1", "Card And Journal", schemaJson,
            "wretched-proposal", ruleSet.Id, ruleSet.Revision, 1, Now, Now);
        var data = GameDataService.CreateEmpty(active, Now) with { DataJson = dataJson };
        Assert.True(GameDataService.Validate(active, data).IsValid);
        return (ruleSet, active, data);
    }
}
