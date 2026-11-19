using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.LlmIntegration;
using WorldSeed.ProjectPlanning;
using WorldSeed.RuleDrafting;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;
using System.Text.Json.Nodes;

namespace WorldSeed.ProjectPlanning.Tests;

public sealed class DevelopmentPlanServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-13T20:00:00Z");

    [Fact]
    public async Task Generates_a_bounded_inventory_cited_plan_with_tradeoffs()
    {
        var model = new CapturingModel("""
            {
              "summary":"Resolve the combat question before expanding weapons.",
              "overallTradeoffs":["Delaying weapons improves consistency but slows visible content growth."],
              "recommendations":[{
                "id":"resolve-reactions","title":"Resolve Reactions","category":"ResolveSourceOrRule","priority":"Now",
                "goal":"Define reaction timing.","rationale":"The inventory records an unresolved reaction question.",
                "expectedOutcome":"A designer-reviewed reaction rule.","evidenceObservationIds":["observation-question"],
                "dependsOnRecommendationIds":[],"tradeoffs":["A detailed rule adds complexity."],
                "alternativeApproach":"Defer reactions from the first playable slice.",
                "differenceOfOpinion":"If rapid content growth is preferred, this can move after weapons.",
                "designerDecisionNeeded":"Choose whether reactions are required for the first playable slice."
              }]
            }
            """);

        var plan = await new DevelopmentPlanGenerationService(model).CreateAsync(Inventory(), "Prioritize visible weapon content.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(plan.Recommendations);
        Assert.Equal("observation-question", Assert.Single(plan.Recommendations[0].EvidenceObservationIds));
        Assert.Contains("rapid content", plan.Recommendations[0].DifferenceOfOpinion, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(model.LastRequest!.ResponseSchema);
        Assert.Contains("observation-question", model.LastRequest.ResponseSchema!.ToJsonString());
        Assert.Contains("Prioritize visible weapon content", model.LastRequest.Messages[1].Content);
        Assert.Contains("ExpandContent may cite EmptyContentType", model.LastRequest.Messages[0].Content);
    }

    [Fact]
    public void Rejects_a_recommendation_category_not_supported_by_its_observation()
    {
        var inventory = InventoryWithObservation(new InventoryObservation(
            "empty-weapon", InventoryObservationKind.EmptyContentType, "weapon", "Weapon has no canonical entries.", []));
        var unsupported = Recommendation() with
        {
            Id = "resolve-conflicts",
            Category = DevelopmentRecommendationCategory.ResolveSourceOrRule,
            EvidenceObservationIds = ["empty-weapon"]
        };

        var exception = Assert.Throws<ArgumentException>(() => DevelopmentPlanService.Create(inventory, "Plan", [], [unsupported], null, Now));

        Assert.Contains("does not support its category", exception.Message);
    }

    [Fact]
    public async Task Rejects_recommendations_that_invent_inventory_evidence()
    {
        var content = """{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"x","title":"X","category":"ExpandContent","priority":"Now","goal":"X","rationale":"X","expectedOutcome":"X","evidenceObservationIds":["invented"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""";

        var exception = await Assert.ThrowsAsync<DevelopmentPlanFormatException>(() =>
            new DevelopmentPlanGenerationService(new CapturingModel(content)).CreateAsync(Inventory(), null, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("retried 2 times", exception.Message);
        Assert.Contains("existing inventory observations", exception.Message);
    }

    [Fact]
    public async Task Corrects_an_unsupported_recommendation_category_before_saving_a_plan()
    {
        var inventory = InventoryWithObservation(new InventoryObservation(
            "empty-weapon", InventoryObservationKind.EmptyContentType, "weapon", "Weapon has no canonical entries.", []));
        var invalid = """{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"weapons","title":"Weapons","category":"ResolveSourceOrRule","priority":"Now","goal":"Add weapons.","rationale":"Weapons are empty.","expectedOutcome":"Weapon entries.","evidenceObservationIds":["empty-weapon"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""";
        var corrected = invalid.Replace("ResolveSourceOrRule", "ExpandContent", StringComparison.Ordinal);
        var model = new SequencedModel(invalid, corrected);

        var plan = await new DevelopmentPlanGenerationService(model).CreateAsync(inventory, null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(DevelopmentRecommendationCategory.ExpandContent, Assert.Single(plan.Recommendations).Category);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("does not support its category", model.Requests[1].Messages[^1].Content);
        Assert.Contains("empty-weapon", model.Requests[1].Messages[^1].Content);
        Assert.Contains("EmptyContentType", model.Requests[1].Messages[^1].Content);
        Assert.Contains("Allowed categories", model.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task Response_contract_pairs_categories_with_only_compatible_evidence()
    {
        var model = new CapturingModel("""{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"question","title":"Question","category":"ResolveSourceOrRule","priority":"Now","goal":"Resolve timing.","rationale":"Unresolved timing.","expectedOutcome":"Reviewed timing.","evidenceObservationIds":["observation-question"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""");
        var inventory = Inventory() with { Observations = [Inventory().Observations[0], new("empty", InventoryObservationKind.EmptyContentType, "promise", "No promises.", [])] };

        await new DevelopmentPlanGenerationService(model).CreateAsync(inventory, null, cancellationToken: TestContext.Current.CancellationToken);

        var branches = model.LastRequest!.ResponseSchema!["properties"]!["recommendations"]!["items"]!["anyOf"]!.AsArray().OfType<JsonObject>().ToArray();
        var resolve = branches.Single(branch => branch["properties"]!["category"]!["enum"]![0]!.GetValue<string>() == "ResolveSourceOrRule");
        var content = branches.Single(branch => branch["properties"]!["category"]!["enum"]![0]!.GetValue<string>() == "ExpandContent");
        Assert.Equal(new[] { "observation-question" }, resolve["properties"]!["evidenceObservationIds"]!["items"]!["enum"]!.AsArray().Select(id => id!.GetValue<string>()));
        Assert.Equal(new[] { "empty" }, content["properties"]!["evidenceObservationIds"]!["items"]!["enum"]!.AsArray().Select(id => id!.GetValue<string>()));
        Assert.Contains("allowedRecommendationCategories", model.LastRequest.Messages[1].Content);
    }

    [Fact]
    public async Task Large_inventory_context_keeps_late_requested_questions_and_reports_omissions()
    {
        var observations = Enumerable.Range(0, 100).Select(index => new InventoryObservation("passage-" + index,
            InventoryObservationKind.UncitedSourcePassage, "source", "Review travel notes.", [])).Append(Inventory().Observations[0]).ToArray();
        var model = new CapturingModel("""{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"question","title":"Question","category":"ResolveSourceOrRule","priority":"Now","goal":"Resolve timing.","rationale":"Unresolved timing.","expectedOutcome":"Reviewed timing.","evidenceObservationIds":["observation-question"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""");

        await new DevelopmentPlanGenerationService(model).CreateAsync(Inventory() with { Observations = observations }, "Resolve reactions.", cancellationToken: TestContext.Current.CancellationToken);

        var input = JsonNode.Parse(model.LastRequest!.Messages[1].Content)!;
        var sent = input["inventory"]!["observations"]!.AsArray();
        Assert.InRange(sent.Count, 1, DevelopmentPlanGenerationService.MaximumContextObservations);
        Assert.Equal(101 - sent.Count, input["inventory"]!["omittedObservationCount"]!.GetValue<int>());
        Assert.True(model.LastRequest.Messages[1].Content.Length <= DevelopmentPlanGenerationService.MaximumInputCharacters);
        Assert.Contains(sent, observation => observation!["Id"]!.GetValue<string>() == "observation-question");
        Assert.DoesNotContain(sent, observation => observation!["Id"]!.GetValue<string>() == "passage-99");
    }

    [Fact]
    public async Task Empty_inventory_is_rejected_before_requesting_a_model_plan()
    {
        var model = new CapturingModel("not called");

        await Assert.ThrowsAsync<ArgumentException>(() => new DevelopmentPlanGenerationService(model).CreateAsync(Inventory() with { Observations = [] }, null, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(model.LastRequest);
    }

    [Fact]
    public async Task Planner_bounds_large_descriptions_without_changing_saved_inventory()
    {
        var question = Inventory().Observations[0];
        var observations = Enumerable.Range(0, 80).Select(index => new InventoryObservation("passage-" + index,
            InventoryObservationKind.UncitedSourcePassage, "source", new string('x', 2000), Enumerable.Range(0, 20).Select(value => "evidence-" + value).ToArray())).Append(question).ToArray();
        var inventory = Inventory() with { Observations = observations, Rules = Inventory().Rules with { Exclusions = [new string('y', 20000)] } };
        var model = new CapturingModel("""{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"question","title":"Question","category":"ResolveSourceOrRule","priority":"Now","goal":"Resolve timing.","rationale":"Unresolved timing.","expectedOutcome":"Reviewed timing.","evidenceObservationIds":["observation-question"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""");

        await new DevelopmentPlanGenerationService(model).CreateAsync(inventory, "Resolve reactions.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(model.LastRequest!.Messages[1].Content.Length <= DevelopmentPlanGenerationService.MaximumInputCharacters);
        var input = JsonNode.Parse(model.LastRequest.Messages[1].Content)!;
        var clipped = input["inventory"]!["observations"]!.AsArray().First(item => item!["kind"]!.GetValue<string>() == "UncitedSourcePassage")!;
        Assert.True(clipped["descriptionTruncated"]!.GetValue<bool>());
        Assert.Equal(4, clipped["evidenceIds"]!.AsArray().Count);
        Assert.Equal(2000, inventory.Observations[0].Description.Length);
        Assert.Equal(20000, inventory.Rules.Exclusions[0].Length);
    }

    [Fact]
    public async Task Planner_retries_keep_original_context_without_accumulating_large_rejected_outputs()
    {
        var valid = """{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"question","title":"Question","category":"ResolveSourceOrRule","priority":"Now","goal":"Resolve timing.","rationale":"Unresolved timing.","expectedOutcome":"Reviewed timing.","evidenceObservationIds":["observation-question"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""";
        var model = new SequencedModel(new string('x', 10000), new string('y', 10000), valid);

        await new DevelopmentPlanGenerationService(model).CreateAsync(Inventory(), "Resolve reactions.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, model.Requests.Count);
        foreach (var retry in model.Requests.Skip(1))
        {
            Assert.Equal(4, retry.Messages.Count);
            Assert.Equal(model.Requests[0].Messages[1], retry.Messages[1]);
            Assert.True(retry.Messages[2].Content.Length < 2000);
            Assert.Contains("truncated", retry.Messages[2].Content);
        }
    }

    [Fact]
    public void Preserves_plan_and_recommendation_decisions_separately()
    {
        var recommendation = Recommendation();
        var plan = DevelopmentPlanService.Create(Inventory(), "Plan", [], [recommendation], null, Now);

        var planDecision = DevelopmentPlanService.DecidePlan(plan, DevelopmentPlanDecisionKind.RevisionRequested, "Move content expansion first.", Now);
        var itemDecision = DevelopmentPlanService.DecideRecommendation(plan, recommendation.Id, DevelopmentRecommendationDecisionKind.Deferred, "Revisit after combat rules.", Now);

        Assert.Equal(DevelopmentPlanDecisionKind.RevisionRequested, planDecision.Kind);
        Assert.Equal(DevelopmentRecommendationDecisionKind.Deferred, itemDecision.Kind);
        Assert.Equal(recommendation.Id, itemDecision.RecommendationId);
    }

    [Fact]
    public void Creates_an_inventory_bound_expansion_source_only_from_an_accepted_recommendation()
    {
        var inventory = Inventory();
        var project = DesignProjectService.Create(inventory.ProjectId, "Planning Project");
        var recommendation = Recommendation();
        var plan = DevelopmentPlanService.Create(inventory, "Plan", [], [recommendation], null, Now);
        var acceptance = DevelopmentPlanService.DecideRecommendation(
            plan, recommendation.Id, DevelopmentRecommendationDecisionKind.Accepted,
            "Keep reactions optional for the first pass.", Now);

        var (request, source) = ExpansionRequestService.Create(project, inventory, plan, recommendation, acceptance, Now);

        Assert.Equal(inventory.Id, request.InventoryId);
        Assert.Equal(plan.Id, request.PlanId);
        Assert.Equal(acceptance.Id, request.AcceptanceDecisionId);
        Assert.Equal(source.Id, request.SourceNoteId);
        Assert.Contains("not approve any new mechanic", source.OriginalText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("How are reactions resolved?", source.OriginalText);
        Assert.Contains("Keep reactions optional", source.OriginalText);
        Assert.Contains("first turn", ExpansionRequestService.ClarificationInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_to_expand_a_rejected_recommendation()
    {
        var inventory = Inventory();
        var project = DesignProjectService.Create(inventory.ProjectId, "Planning Project");
        var recommendation = Recommendation();
        var plan = DevelopmentPlanService.Create(inventory, "Plan", [], [recommendation], null, Now);
        var rejection = DevelopmentPlanService.DecideRecommendation(
            plan, recommendation.Id, DevelopmentRecommendationDecisionKind.Rejected, null, Now);

        var exception = Assert.Throws<ArgumentException>(() =>
            ExpansionRequestService.Create(project, inventory, plan, recommendation, rejection, Now));

        Assert.Contains("explicitly accepted", exception.Message);
    }

    [Fact]
    public void Derives_expansion_progress_through_clarification_draft_and_working_rules()
    {
        var inventory = Inventory();
        var project = DesignProjectService.Create(inventory.ProjectId, "Planning Project");
        var recommendation = Recommendation();
        var plan = DevelopmentPlanService.Create(inventory, "Plan", [], [recommendation], null, Now);
        var acceptance = DevelopmentPlanService.DecideRecommendation(plan, recommendation.Id, DevelopmentRecommendationDecisionKind.Accepted, null, Now);
        var (request, source) = ExpansionRequestService.Create(project, inventory, plan, recommendation, acceptance, Now);
        project = DesignProjectService.AddSources(project, [source]);
        Assert.Equal(ExpansionProgressStage.ReadyForClarification, ExpansionRequestService.EvaluateProgress(request, project, null).Stage);

        var question = new RuleDraftTurn(RuleDraftAction.AskClarifyingQuestion, "How does it work?", ["How does it work?"], null);
        var session = new DesignSession("session-expansion", project.Id, "local", Now, Now, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], question);
        project = DesignProjectService.SaveSession(project, session);
        Assert.Equal(ExpansionProgressStage.ClarificationInProgress, ExpansionRequestService.EvaluateProgress(request, project, null).Stage);

        var draftRule = new RuleStatement("reaction-rule", "Reaction Rule", RuleStatementKind.Rule, "Reactions resolve immediately.", [source.Id], [new RuleSourceSupport(source.Id, "Reactions resolve immediately.")]);
        session = session with { LatestTurn = new RuleDraftTurn(RuleDraftAction.PresentDraft, null, [], new StructuredRuleDraft("Reactions", "Define reactions.", [draftRule], [], [], [], [])) };
        project = DesignProjectService.SaveSession(project, session);
        Assert.Equal(ExpansionProgressStage.DraftReady, ExpansionRequestService.EvaluateProgress(request, project, null).Stage);

        var evidence = new WorkingRuleEvidence(source.Id, null, "Reactions resolve immediately.");
        var workingRule = new WorkingRule("reaction-rule", "Reaction Rule", WorkingRuleKind.Rule, "Reactions resolve immediately.", [evidence], [new DraftRuleReference(session.Id, draftRule.Id)], [], [], null, [], []);
        var workingRules = new WorkingRuleSet("rules", project.Id, "0.1", "Rules", null, 4, Now, Now, [workingRule], [], [], [], []);
        var progress = ExpansionRequestService.EvaluateProgress(request, project, workingRules);
        Assert.Equal(ExpansionProgressStage.WorkingRulesIncorporated, progress.Stage);
        Assert.Equal(4, progress.WorkingRuleSetRevision);
        Assert.Equal([workingRule.Id], progress.WorkingRuleIds);

        Assert.Throws<ArgumentException>(() => ExpansionRequestService.DecideSchema(request, progress, ExpansionSchemaDecisionKind.NoSchemaChangeRequired, null, Now));
        var noSchema = ExpansionRequestService.DecideSchema(request, progress, ExpansionSchemaDecisionKind.NoSchemaChangeRequired, "This rule is entirely procedural prose.", Now.AddMinutes(1));
        Assert.Equal(ExpansionProgressStage.NoSchemaChangeRequired, ExpansionRequestService.EvaluateProgress(request, project, workingRules, schemaDecisions: [noSchema]).Stage);

        var schemaWork = ExpansionRequestService.DecideSchema(request, progress, ExpansionSchemaDecisionKind.SchemaWorkRequested, null, Now.AddMinutes(2));
        var schemaJson = "{\"id\":\"schema\",\"name\":\"Schema\",\"version\":\"1\",\"entityTypes\":[],\"valueTypes\":[]}";
        var proposal = new ProjectSchemaProposal("proposal", project.Id, "0.1", "Reaction schema", schemaJson, [], workingRules.Id, workingRules.Revision, [workingRule.Id], Now, Now.AddMinutes(3));
        progress = ExpansionRequestService.EvaluateProgress(request, project, workingRules, proposal, schemaDecisions: [noSchema, schemaWork]);
        Assert.Equal(ExpansionProgressStage.SchemaProposalReady, progress.Stage);
        var activeSchemaJson = """
            {
              "id": "schema",
              "name": "Schema",
              "version": "1",
              "entityTypes": [],
              "valueTypes": []
            }
            """;
        var active = new ActiveGameSchema("active", project.Id, "0.1", "Schema", activeSchemaJson, proposal.Id, workingRules.Id, workingRules.Revision, 2, Now, Now.AddMinutes(4));
        progress = ExpansionRequestService.EvaluateProgress(request, project, workingRules, proposal, active, [noSchema, schemaWork]);
        Assert.Equal(ExpansionProgressStage.SchemaActivated, progress.Stage);
    }

    private static ProjectInventory Inventory() => new(
        "inventory", "project", "0.1", 2, Now,
        new ProjectInventoryBinding(Now, "rules", 3, "proposal", Now, "schema", 2, "data", Now, null, null),
        new InventorySourceCoverage(2, 100, 2, []),
        new InventoryRuleCoverage(5, new Dictionary<string, int>(), 5, [], ["How are reactions resolved?"], [], []),
        new InventorySchemaCoverage(new Dictionary<string, int>(), [], 0, false),
        new InventoryPublicationCoverage(0, null, null, 0),
        [new InventoryObservation("observation-question", InventoryObservationKind.UnresolvedQuestion, "question-1", "How are reactions resolved?", [])]);

    private static ProjectInventory InventoryWithObservation(InventoryObservation observation) => Inventory() with { Observations = [observation] };

    private static DevelopmentRecommendation Recommendation() => new(
        "resolve-reactions", "Resolve Reactions", DevelopmentRecommendationCategory.ResolveSourceOrRule,
        DevelopmentRecommendationPriority.Now, "Define reaction timing.", "An unresolved question exists.",
        "A reviewed rule.", ["observation-question"], [], [], null, null, "Choose timing.");

    private sealed class CapturingModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? LastRequest { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new LlmChatResponse(content, "test-model", null));
        }
    }

    private sealed class SequencedModel(params string[] responses) : ILanguageModelClient
    {
        private int _index;
        public List<LlmChatRequest> Requests { get; } = [];
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var response = responses[Math.Min(_index++, responses.Length - 1)];
            return Task.FromResult(new LlmChatResponse(response, "test-model", null));
        }
    }
}
