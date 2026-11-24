using System.Text.Json;
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
        Assert.DoesNotContain(model.Request!.Messages, message => message.Content.Contains("Current working proposal"));
        Assert.Equal(1, result.BatchCount);
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
    public async Task Uses_small_independently_validated_batches_for_larger_projects()
    {
        var model = new SequencedModel([ResponseFor("d001", "d002", "d003", "d004"), ResponseFor("d005")]);

        var result = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(CreateProjectWithCompletedDrafts(5));

        Assert.Equal(2, result.BatchCount);
        Assert.Equal(5, result.RuleSet.Rules.Count);
        Assert.Equal(2, model.Requests.Count);
        Assert.All(model.Requests, request => Assert.Equal(1800, request.MaxOutputTokens));
        Assert.Contains("[d004]", model.Requests[0].Messages[1].Content);
        Assert.DoesNotContain("[d005]", model.Requests[0].Messages[1].Content);
        Assert.Contains(result.RuleSet.Rules, rule => rule.Id == "b001-rule-d001");
        Assert.Contains(result.RuleSet.Rules, rule => rule.Id == "b002-rule-d005");
    }

    [Fact]
    public async Task Splits_an_incomplete_batch_before_failing_the_entire_proposal()
    {
        var model = new SequencedModel(["{\"name\":\"incomplete", ResponseFor("d001", "d002"), ResponseFor("d003", "d004"), ResponseFor("d005")]);

        var result = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(CreateProjectWithCompletedDrafts(5));

        Assert.Equal(3, result.BatchCount);
        Assert.Equal(5, result.RuleSet.Rules.Count);
        Assert.Equal(4, model.Requests.Count);
        Assert.Contains("Batch response for d001, d002, d003, d004", result.RawModelResponse);
    }

    [Fact]
    public async Task Requires_a_completed_source_draft()
    {
        var emptyProject = DesignProjectService.Create("empty", "Empty");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => new WorkingRuleSetFormalizationService(new CapturingModel("{}")).CreateProposalAsync(emptyProject));

        Assert.Contains("completed source draft", error.Message);
    }

    [Fact]
    public async Task Progress_counts_unique_prepared_rules_and_reports_recovery_before_saving()
    {
        var model = new SequencedModel(["{", ResponseFor("d001"), "{}", ResponseFor("d002"), ResponseFor("d003", "d004")]);
        var progress = new RecordedProgress();
        await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(CreateProjectWithCompletedDrafts(4), progress: progress);
        Assert.Equal(0, progress.Values[0].PreparedRules);
        Assert.Equal(4, progress.Values[^1].PreparedRules);
        Assert.Equal("Ready To Save", progress.Values[^1].Activity);
        Assert.Equal(5, progress.Values[^1].RequestNumber);
        Assert.All(progress.Values, item => { Assert.Equal(4, item.TotalRules); Assert.Equal(1, item.TotalBatches); Assert.InRange(item.PreparedRules, 0, 4); });
        Assert.Equal(progress.Values.Select(item => item.PreparedRules).Order(), progress.Values.Select(item => item.PreparedRules));
        Assert.Contains(progress.Values, item => item.Activity == "Recovering Missing Rules");
        Assert.Contains(progress.Values, item => item.Activity == "Retrying Rule");
        Assert.Contains(progress.Values, item => item.Activity == "Retrying Invalid Batch");
    }

    private sealed class RecordedProgress : IProgress<WorkingRuleSetProgress>
    {
        public List<WorkingRuleSetProgress> Values { get; } = [];
        public void Report(WorkingRuleSetProgress value) => Values.Add(value);
    }

    [Fact]
    public async Task Recovers_omitted_citations_without_reprocessing_valid_rules()
    {
        var model = new SequencedModel([ResponseFor("d001"), ResponseFor("d002"), ResponseFor("d003", "d004")]);
        var result = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(CreateProjectWithCompletedDrafts(4), addMissingOnly: true);
        Assert.Equal(4, result.RuleSet.Rules.Count);
        Assert.Equal(4, result.RuleSet.Rules.SelectMany(rule => rule.SourceDraftRules).Distinct().Count());
        Assert.Equal(3, model.Requests.Count);
        Assert.DoesNotContain("[d001]", model.Requests[1].Messages[1].Content);
        Assert.DoesNotContain("[d002]", model.Requests[2].Messages[1].Content);
    }

    [Fact]
    public async Task Retries_a_bad_single_rule_once_then_reports_the_named_rule()
    {
        var model = new SequencedModel(["{}", "{}"]);
        var error = await Assert.ThrowsAsync<WorkingRuleSetFormatException>(() => new WorkingRuleSetFormalizationService(model).CreateProposalAsync(CreateProject()));
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("after a focused retry", error.Message);
        Assert.Contains("Existing project rules were not changed", error.Message);
    }

    [Fact]
    public async Task Incremental_recovery_preserves_existing_rules_and_avoids_id_collisions()
    {
        var project = CreateProjectWithCompletedDrafts(2);
        var original = await new WorkingRuleSetFormalizationService(new CapturingModel(ResponseFor("d001"))).CreateProposalAsync(project with { Sessions = [project.Sessions[0]] });
        var edited = original.RuleSet with
        {
            Rules = [original.RuleSet.Rules[0] with { AuthoritativeText = "Designer-reviewed wording." }],
            OpenQuestions = ["What happens next?"], Exclusions = ["No fixed effect."], Conflicts = ["Review timing."]
        };
        var model = new CapturingModel(ResponseFor("d001"));

        var recovered = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(project, edited, addMissingOnly: true);

        Assert.Equal(2, recovered.RuleSet.Rules.Count);
        Assert.Equal(edited.Rules[0], recovered.RuleSet.Rules[0]);
        Assert.NotEqual(recovered.RuleSet.Rules[0].Id, recovered.RuleSet.Rules[1].Id);
        Assert.Equal("session-2", Assert.Single(recovered.RuleSet.Rules[1].SourceDraftRules).SessionId);
        Assert.Equal(edited.Revision + 1, recovered.RuleSet.Revision);
        Assert.Equal(edited.OpenQuestions, recovered.RuleSet.OpenQuestions);
        Assert.Equal(edited.Exclusions, recovered.RuleSet.Exclusions);
        Assert.Equal(edited.Conflicts, recovered.RuleSet.Conflicts);
        Assert.DoesNotContain("Source rule 1", model.Request!.Messages[1].Content);
    }

    [Fact]
    public async Task Incremental_recovery_with_nothing_missing_skips_model_and_revision()
    {
        var project = CreateProject();
        var existing = await new WorkingRuleSetFormalizationService(new CapturingModel(ValidResponse)).CreateProposalAsync(project);
        var model = new CapturingModel("must not be called");

        var result = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(project, existing.RuleSet, addMissingOnly: true);

        Assert.Same(existing.RuleSet, result.RuleSet);
        Assert.Equal(0, result.BatchCount);
        Assert.Null(model.Request);
    }

    [Fact]
    public async Task Incremental_recovery_uses_latest_completed_session_for_each_source()
    {
        var project = CreateProject();
        var old = project.Sessions[0];
        var latest = old with { Id = "latest-session", UpdatedAt = old.UpdatedAt.AddMinutes(1) };
        project = project with { Sessions = [old, latest] };
        var model = new CapturingModel(ValidResponse);

        var result = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(project, addMissingOnly: true);

        Assert.Equal("latest-session", Assert.Single(Assert.Single(result.RuleSet.Rules).SourceDraftRules).SessionId);
    }

    [Fact]
    public async Task Preserves_source_draft_uncertainty_without_promoting_assumptions_to_rules()
    {
        var project = CreateProject();
        var session = project.Sessions[0];
        var draft = session.LatestTurn!.Draft! with
        {
            OpenQuestions = ["How are ties resolved?", "How are ties resolved?"],
            Exclusions = ["No tie procedure was provided."],
            Assumptions = ["Ties might be simultaneous.", " "]
        };
        project = project with { Sessions = [session with { LatestTurn = session.LatestTurn with { Draft = draft } }] };
        var model = new CapturingModel(ValidResponse);

        var result = await new WorkingRuleSetFormalizationService(model).CreateProposalAsync(project);

        Assert.Equal(new[] { "How are ties resolved?", "Unconfirmed Source-Draft Assumption: Ties might be simultaneous." }, result.RuleSet.OpenQuestions);
        Assert.Equal(new[] { "No tie procedure was provided." }, result.RuleSet.Exclusions);
        Assert.Single(result.RuleSet.Rules);
        Assert.DoesNotContain("simultaneous", result.RuleSet.Rules[0].AuthoritativeText);
        Assert.Equal(1, result.BatchCount);
    }

    [Fact]
    public async Task Incremental_recovery_carries_only_newly_incorporated_draft_uncertainty()
    {
        var project = CreateProjectWithCompletedDrafts(2);
        var original = await new WorkingRuleSetFormalizationService(new CapturingModel(ResponseFor("d001")))
            .CreateProposalAsync(project with { Sessions = [project.Sessions[0]] });
        var sessions = project.Sessions.Select((session, index) => session with
        {
            LatestTurn = session.LatestTurn! with
            {
                Draft = session.LatestTurn.Draft! with
                {
                    OpenQuestions = [index == 0 ? "Old question removed by designer." : "New unresolved procedure."],
                    Exclusions = [index == 0 ? "Old exclusion removed by designer." : "New excluded mechanic."]
                }
            }
        }).ToArray();
        project = project with { Sessions = sessions };

        var result = await new WorkingRuleSetFormalizationService(new CapturingModel(ResponseFor("d001")))
            .CreateProposalAsync(project, original.RuleSet, addMissingOnly: true);

        Assert.Equal(new[] { "New unresolved procedure." }, result.RuleSet.OpenQuestions);
        Assert.Equal(new[] { "New excluded mechanic." }, result.RuleSet.Exclusions);
        Assert.Equal(original.RuleSet.Rules[0], result.RuleSet.Rules[0]);
        Assert.Equal(2, result.RuleSet.Rules.Count);
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

    private static DesignProject CreateProjectWithCompletedDrafts(int count)
    {
        var now = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var project = DesignProjectService.Create("project-batches", "Batched Project");
        for (var index = 1; index <= count; index++)
        {
            var source = new DesignSourceNote($"source-{index}", $"Source rule {index}.", now, new SourceNoteOrigin($"source-{index}.txt"));
            project = DesignProjectService.AddSources(project, [source]);
            var rule = new RuleStatement($"draft-{index}", $"Rule {index}", RuleStatementKind.Rule, $"Source rule {index}.", [source.Id], [new RuleSourceSupport(source.Id, source.OriginalText, $"{source.Id}:p001")]);
            var draft = new StructuredRuleDraft($"Draft {index}", "Define one rule.", [rule], [], [], [], []);
            var turn = new RuleDraftTurn(RuleDraftAction.PresentDraft, null, [], draft);
            project = DesignProjectService.SaveSession(project, new DesignSession($"session-{index}", project.Id, "local", now, now, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], turn));
        }
        return project;
    }

    private static string ResponseFor(params string[] citationIds) => JsonSerializer.Serialize(new
    {
        name = "Batch Rules",
        description = (string?)null,
        rules = citationIds.Select(citationId => new
        {
            id = "rule-" + citationId,
            name = "Rule from " + citationId,
            kind = "rule",
            authoritativeText = "Working rule from " + citationId + ".",
            sourceDraftIds = new[] { citationId },
            conceptIds = Array.Empty<string>(),
            schemaHints = Array.Empty<object>(),
            openQuestions = Array.Empty<string>(),
            exclusions = Array.Empty<string>()
        }),
        concepts = Array.Empty<object>(),
        openQuestions = Array.Empty<string>(),
        exclusions = Array.Empty<string>(),
        conflicts = Array.Empty<string>()
    });

    private sealed class CapturingModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse(content, "test", null));
        }
    }

    private sealed class SequencedModel(IEnumerable<string> contents) : ILanguageModelClient
    {
        private readonly Queue<string> _contents = new(contents);
        public List<LlmChatRequest> Requests { get; } = [];

        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new LlmChatResponse(_contents.Dequeue(), "test", null));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
