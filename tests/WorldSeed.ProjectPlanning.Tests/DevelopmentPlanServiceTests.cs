using WorldSeed.LlmIntegration;
using WorldSeed.ProjectPlanning;

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
    }

    [Fact]
    public async Task Rejects_recommendations_that_invent_inventory_evidence()
    {
        var content = """{"summary":"Plan","overallTradeoffs":[],"recommendations":[{"id":"x","title":"X","category":"ExpandContent","priority":"Now","goal":"X","rationale":"X","expectedOutcome":"X","evidenceObservationIds":["invented"],"dependsOnRecommendationIds":[],"tradeoffs":[],"alternativeApproach":null,"differenceOfOpinion":null,"designerDecisionNeeded":null}]}""";

        var exception = await Assert.ThrowsAsync<DevelopmentPlanFormatException>(() =>
            new DevelopmentPlanGenerationService(new CapturingModel(content)).CreateAsync(Inventory(), null, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("existing inventory observations", exception.Message);
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

    private static ProjectInventory Inventory() => new(
        "inventory", "project", "0.1", 2, Now,
        new ProjectInventoryBinding(Now, "rules", 3, "proposal", Now, "schema", 2, "data", Now, null, null),
        new InventorySourceCoverage(2, 100, 2, []),
        new InventoryRuleCoverage(5, new Dictionary<string, int>(), 5, [], ["How are reactions resolved?"], [], []),
        new InventorySchemaCoverage(new Dictionary<string, int>(), [], 0, false),
        new InventoryPublicationCoverage(0, null, null, 0),
        [new InventoryObservation("observation-question", InventoryObservationKind.UnresolvedQuestion, "question-1", "How are reactions resolved?", [])]);

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
}
