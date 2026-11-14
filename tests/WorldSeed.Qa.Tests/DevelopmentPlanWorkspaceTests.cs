using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WorldSeed.App;
using WorldSeed.DesignSessions;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.Qa.Tests;

public sealed class DevelopmentPlanWorkspaceTests
{
    [AvaloniaFact]
    public async Task Generates_and_displays_an_inventory_grounded_plan_without_executing_it()
    {
        var inventory = Inventory();
        var modelHandler = new SequencedOllamaHandler(PlanResponse("Resolve Reactions", "The designer may prefer visible content first."));
        var apiHandler = new DevelopmentPlanApiHandler(inventory);
        using var model = new HttpClient(modelHandler) { BaseAddress = new Uri("http://model.invalid/") };
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api, inventory);
        try
        {
            Assert.True(await window.GenerateDevelopmentPlanAsync());

            var plan = Assert.Single(apiHandler.SavedPlans);
            Assert.Equal(inventory.Id, plan.InventoryId);
            Assert.Equal("observation-question", Assert.Single(plan.Recommendations[0].EvidenceObservationIds));
            Assert.Contains("Resolve Reactions", window.FindControl<TextBox>("DevelopmentRecommendationDetailText")!.Text);
            Assert.Contains("designer may prefer visible content", window.FindControl<TextBox>("DevelopmentRecommendationDetailText")!.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Nothing Has Been Authorized Or Generated", window.FindControl<TextBlock>("DevelopmentPlanStatusText")!.Text);
            Assert.Empty(apiHandler.PlanDecisions);
            Assert.Empty(apiHandler.RecommendationDecisions);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Preserves_item_revision_guidance_and_generates_a_linked_replacement_plan()
    {
        var inventory = Inventory();
        var modelHandler = new SequencedOllamaHandler(
            PlanResponse("Resolve Reactions", null),
            PlanResponse("Expand Weapons First", "This differs from the original ordering."));
        var apiHandler = new DevelopmentPlanApiHandler(inventory);
        using var model = new HttpClient(modelHandler) { BaseAddress = new Uri("http://model.invalid/") };
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api, inventory);
        try
        {
            Assert.True(await window.GenerateDevelopmentPlanAsync());
            window.FindControl<TextBox>("DevelopmentPlanGuidanceText")!.Text = "Move visible content ahead of reaction work.";
            Assert.True(await window.DecideDevelopmentRecommendationAsync(DevelopmentRecommendationDecisionKind.RevisionRequested));
            Assert.Equal(DevelopmentRecommendationDecisionKind.RevisionRequested, Assert.Single(apiHandler.RecommendationDecisions).Kind);
            Assert.Equal("Generate Requested Revision", window.FindControl<Button>("GenerateDevelopmentPlanButton")!.Content);

            Assert.True(await window.GenerateDevelopmentPlanAsync());

            Assert.Equal(2, apiHandler.SavedPlans.Count);
            Assert.Equal(2, apiHandler.SavedPlans[1].Version);
            Assert.Equal(apiHandler.SavedPlans[0].Id, apiHandler.SavedPlans[1].ReplacesPlanId);
            Assert.Contains("Move visible content ahead", modelHandler.RequestBodies[1]);
            Assert.Contains("Saved Development Plan Version 2", window.FindControl<TextBlock>("DevelopmentPlanStatusText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Accepted_recommendation_opens_a_traceable_expansion_source()
    {
        var inventory = Inventory();
        var modelHandler = new SequencedOllamaHandler(PlanResponse("Resolve Reactions", null));
        var apiHandler = new DevelopmentPlanApiHandler(inventory);
        using var model = new HttpClient(modelHandler) { BaseAddress = new Uri("http://model.invalid/") };
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api, inventory);
        try
        {
            Assert.True(await window.GenerateDevelopmentPlanAsync());
            Assert.True(await window.DecideDevelopmentRecommendationAsync(DevelopmentRecommendationDecisionKind.Accepted));
            Assert.True(window.FindControl<Button>("StartExpansionRequestButton")!.IsEnabled);

            Assert.True(await window.StartExpansionRequestAsync());

            Assert.Equal(0, window.FindControl<TabControl>("WorkspaceTabs")!.SelectedIndex);
            Assert.Contains("Expansion Request - Resolve Reactions", window.FindControl<TextBlock>("SourceText")!.Text);
            Assert.Contains("Start The Selected Source To Begin Clarification", window.FindControl<TextBlock>("StatusText")!.Text);
            Assert.Contains("No Rules Or Schema Were Changed", window.FindControl<TextBlock>("StatusText")!.Text);
            Assert.Single(apiHandler.ExpansionRequests);
            Assert.Contains("ReadyForClarification", Assert.Single((IEnumerable<object>)window.FindControl<ListBox>("ExpansionRequestList")!.ItemsSource!).ToString());

            apiHandler.ProgressStage = ExpansionProgressStage.DraftReady;
            Assert.True(await window.StartExpansionRequestAsync());
            Assert.Equal("Review Source Draft", window.FindControl<Button>("StartExpansionRequestButton")!.Content);
            Assert.Contains("Source Draft Ready", window.FindControl<TextBlock>("ExpansionRequestStatusText")!.Text);

            apiHandler.ProgressStage = ExpansionProgressStage.WorkingRulesIncorporated;
            Assert.True(await window.StartExpansionRequestAsync());
            Assert.Equal(1, window.FindControl<TabControl>("WorkspaceTabs")!.SelectedIndex);
            Assert.Equal("View Incorporated Rules", window.FindControl<Button>("StartExpansionRequestButton")!.Content);
            Assert.Contains("Working Rule Set Revision 4", window.FindControl<TextBlock>("ExpansionRequestStatusText")!.Text);
            Assert.Single(apiHandler.ExpansionRequests);
        }
        finally { window.Close(); }
    }

    private static MainWindow CreateWindow(HttpClient model, HttpClient api, ProjectInventory inventory)
    {
        var window = new MainWindow(model, api);
        SetField(window, "_project", DesignProjectService.Create(inventory.ProjectId, "Planning Project"));
        var inventories = (List<ProjectInventory>)typeof(MainWindow).GetField("_projectInventories", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        inventories.Add(inventory);
        var selector = window.FindControl<ComboBox>("ModelSelector")!;
        selector.ItemsSource = new[] { "qa-model" };
        selector.SelectedIndex = 0;
        return window;
    }

    private static void SetField(MainWindow window, string name, object value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

    private static ProjectInventory Inventory()
    {
        var now = DateTimeOffset.Parse("2026-11-13T20:00:00Z");
        return new ProjectInventory(
            "inventory-planning-r2", "planning-project", "0.1", 2, now,
            new ProjectInventoryBinding(now, "rules", 3, "proposal", now, "schema", 2, "data", now, null, null),
            new InventorySourceCoverage(2, 100, 2, []),
            new InventoryRuleCoverage(5, new Dictionary<string, int>(), 5, [], ["How are reactions resolved?"], [], []),
            new InventorySchemaCoverage(new Dictionary<string, int>(), [], 0, false),
            new InventoryPublicationCoverage(0, null, null, 0),
            [new InventoryObservation("observation-question", InventoryObservationKind.UnresolvedQuestion, "question-1", "How are reactions resolved?", [])]);
    }

    private static string PlanResponse(string title, string? difference) => JsonSerializer.Serialize(new
    {
        summary = "Resolve the recorded question before execution.",
        overallTradeoffs = new[] { "Resolving rules first delays visible content." },
        recommendations = new[]
        {
            new
            {
                id = "resolve-reactions", title, category = "ResolveSourceOrRule", priority = "Now",
                goal = "Define reaction timing.", rationale = "The inventory records an unresolved question.",
                expectedOutcome = "A designer-reviewed reaction rule.", evidenceObservationIds = new[] { "observation-question" },
                dependsOnRecommendationIds = Array.Empty<string>(), tradeoffs = new[] { "A detailed rule adds complexity." },
                alternativeApproach = "Defer reactions from the first slice.", differenceOfOpinion = difference,
                designerDecisionNeeded = "Choose whether reactions belong in the first slice."
            }
        }
    });

    private sealed class SequencedOllamaHandler(params string[] contents) : HttpMessageHandler
    {
        private int _next;
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var content = contents[Math.Min(_next++, contents.Length - 1)];
            var response = new JsonObject
            {
                ["model"] = "qa-model",
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToJsonString(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class DevelopmentPlanApiHandler(ProjectInventory inventory) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
        public List<DevelopmentPlanProposal> SavedPlans { get; } = [];
        public List<DevelopmentPlanDecision> PlanDecisions { get; } = [];
        public List<DevelopmentRecommendationDecision> RecommendationDecisions { get; } = [];
        public List<ExpansionRequest> ExpansionRequests { get; } = [];
        public ExpansionProgressStage ProgressStage { get; set; } = ExpansionProgressStage.ReadyForClarification;
        private DesignProject Project { get; set; } = DesignProjectService.Create(inventory.ProjectId, "Planning Project");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == $"/api/projects/{inventory.ProjectId}")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Project, options: Options) };
            if (request.Method == HttpMethod.Get && path.EndsWith("/development-plans", StringComparison.Ordinal))
            {
                var progress = ExpansionRequests.Select(item => new ExpansionRequestProgress(
                    item.Id,
                    ProgressStage,
                    ProgressStage == ExpansionProgressStage.ReadyForClarification ? null : "session-expansion",
                    ProgressStage is ExpansionProgressStage.DraftReady or ExpansionProgressStage.WorkingRulesIncorporated ? 1 : 0,
                    ProgressStage == ExpansionProgressStage.WorkingRulesIncorporated ? 4 : null,
                    ProgressStage == ExpansionProgressStage.WorkingRulesIncorporated ? ["reaction-rule"] : [])).ToArray();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new DevelopmentPlanWorkspace(SavedPlans, PlanDecisions, RecommendationDecisions, ExpansionRequests, progress), options: Options)
                };
            }
            if (request.Method == HttpMethod.Put && path.Contains("/development-plans/", StringComparison.Ordinal))
            {
                SavedPlans.Add((await request.Content!.ReadFromJsonAsync<DevelopmentPlanProposal>(Options, cancellationToken))!);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/expansion-request", StringComparison.Ordinal))
            {
                if (ExpansionRequests.Count > 0)
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ExpansionRequests[0], options: Options) };
                var plan = SavedPlans.Single(item => path.Contains(item.Id, StringComparison.Ordinal));
                var recommendation = plan.Recommendations.Single(item => path.Contains(item.Id, StringComparison.Ordinal));
                var acceptance = RecommendationDecisions.Last(item => item.PlanId == plan.Id && item.RecommendationId == recommendation.Id && item.Kind == DevelopmentRecommendationDecisionKind.Accepted);
                var (expansion, source) = ExpansionRequestService.Create(Project, inventory, plan, recommendation, acceptance, inventory.CapturedAt.AddMinutes(2));
                ExpansionRequests.Add(expansion);
                Project = DesignProjectService.AddSources(Project, [source]);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expansion, options: Options) };
            }
            if (request.Method == HttpMethod.Post && path.Contains("/recommendations/", StringComparison.Ordinal) && path.EndsWith("/decisions", StringComparison.Ordinal))
            {
                var plan = SavedPlans.Single(item => path.Contains(item.Id, StringComparison.Ordinal));
                var recommendation = plan.Recommendations.Single(item => path.Contains(item.Id, StringComparison.Ordinal));
                var input = (await request.Content!.ReadFromJsonAsync<DevelopmentRecommendationDecisionRequest>(Options, cancellationToken))!;
                var decision = DevelopmentPlanService.DecideRecommendation(plan, recommendation.Id, input.Kind, input.Note, inventory.CapturedAt.AddMinutes(1));
                RecommendationDecisions.Add(decision);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(decision, options: Options) };
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/decisions", StringComparison.Ordinal))
            {
                var plan = SavedPlans.Single(item => path.Contains(item.Id, StringComparison.Ordinal));
                var input = (await request.Content!.ReadFromJsonAsync<DevelopmentPlanDecisionRequest>(Options, cancellationToken))!;
                var decision = DevelopmentPlanService.DecidePlan(plan, input.Kind, input.Note, inventory.CapturedAt.AddMinutes(1));
                PlanDecisions.Add(decision);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(decision, options: Options) };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
