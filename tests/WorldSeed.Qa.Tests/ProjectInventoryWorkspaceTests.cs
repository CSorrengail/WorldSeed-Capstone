using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using WorldSeed.App;
using WorldSeed.DesignSessions;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.Qa.Tests;

public sealed class ProjectInventoryWorkspaceTests
{
    [AvaloniaFact]
    public async Task Builds_and_displays_an_immutable_inventory_revision()
    {
        var project = DesignProjectService.Create("inventory-project", "Inventory Project");
        var inventory = CreateInventory(project.Id);
        var handler = new InventoryHandler(inventory);
        using var api = new HttpClient(handler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(new HttpClient(), api);
        try
        {
            SetField(window, "_project", project);

            Assert.True(await window.CreateProjectInventoryAsync());

            Assert.Equal(1, handler.CreateRequests);
            Assert.Contains("Inventory Revision 1 Saved", window.FindControl<TextBlock>("ProjectInventoryStatusText")!.Text);
            Assert.Contains("2 Source(s)", window.FindControl<TextBox>("ProjectInventorySummaryText")!.Text);
            Assert.Contains("Weapon: 4 Canonical", window.FindControl<TextBox>("ProjectInventorySummaryText")!.Text);
            var observation = Assert.Single((IEnumerable<object>)window.FindControl<ListBox>("ProjectInventoryObservationList")!.ItemsSource!);
            Assert.Contains("Unresolved Question (1)", observation.ToString());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Refuses_to_build_an_inventory_without_a_loaded_project()
    {
        var handler = new InventoryHandler(CreateInventory("inventory-project"));
        using var api = new HttpClient(handler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(new HttpClient(), api);
        try
        {
            Assert.False(await window.CreateProjectInventoryAsync());
            Assert.Equal(0, handler.CreateRequests);
            Assert.Contains("Select A Project", window.FindControl<TextBlock>("ProjectInventoryStatusText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Opens_roleplay_coverage_evidence_without_starting_a_session_or_changing_the_project()
    {
        var project = DesignProjectService.Create("roleplay", "Promises") with
        {
            SourceNotes = [new("promise-source", "A promise connects two participants. Breaking it changes their relationship.", DateTimeOffset.UtcNow)]
        };
        var observation = new InventoryObservation("gap", InventoryObservationKind.UncitedSourcePassage,
            "promise-source:p002", "Review the consequence of a broken promise.", ["promise-source", "promise-source:p002"]);
        var inventory = CreateInventory(project.Id) with { Observations = [observation] };
        var handler = new InventoryHandler(inventory);
        using var api = new HttpClient(handler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(new HttpClient(), api);
        try
        {
            SetField(window, "_project", project);
            Assert.True(await window.CreateProjectInventoryAsync());
            var selector = window.FindControl<ComboBox>("InventoryCoverageList")!;
            Assert.Equal(-1, selector.SelectedIndex);
            selector.SelectedIndex = 0;
            Assert.Contains("broken promise", window.FindControl<TextBox>("InventoryCoverageDetailText")!.Text);
            Assert.False(await window.DraftInventoryPassageAsync());
            Assert.Contains("Local Model", window.FindControl<TextBox>("InventoryCoverageDetailText")!.Text);
            Assert.Single(project.SourceNotes);
            Assert.True(window.OpenInventoryEvidence());
            Assert.Equal(0, window.FindControl<TabControl>("WorkspaceTabs")!.SelectedIndex);
            Assert.Contains(project.SourceNotes[0].OriginalText, window.FindControl<TextBlock>("SourceText")!.Text);
            Assert.Empty(project.Sessions);
            Assert.Equal(1, handler.CreateRequests);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Historical_evidence_missing_from_project_has_a_clear_recovery_message()
    {
        var project = DesignProjectService.Create("history", "History");
        var inventory = CreateInventory(project.Id) with
        {
            Observations = [new("gap", InventoryObservationKind.UncitedSourcePassage, "removed:p001", "Historical passage.", ["removed"])]
        };
        using var api = new HttpClient(new InventoryHandler(inventory)) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(new HttpClient(), api);
        try
        {
            SetField(window, "_project", project);
            Assert.True(await window.CreateProjectInventoryAsync());
            window.FindControl<ComboBox>("InventoryCoverageList")!.SelectedIndex = 0;
            Assert.False(window.OpenInventoryEvidence());
            Assert.Contains("Rebuild The Inventory", window.FindControl<TextBox>("InventoryCoverageDetailText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Opens_the_exact_content_proposal_for_a_reported_schema_gap()
    {
        var project = DesignProjectService.Create("social-game", "Social Game");
        var proposal = WorldSeed.Content.ContentProposalLifecycleService.Create(project.Id, "promise",
            System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"promise\",\"entityTypeId\":\"promise\",\"values\":{}}")!.AsObject(),
            "active", 3, "rules", 2, DateTimeOffset.UtcNow);
        var inventory = CreateInventory(project.Id) with
        {
            Observations = [new("gap", InventoryObservationKind.ReportedSchemaGap, "promise", "Relationship has no representation.", [proposal.Id])]
        };
        using var api = new HttpClient(new InventoryHandler(inventory)) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(new HttpClient(), api);
        try
        {
            SetField(window, "_project", project);
            var proposals = (List<WorldSeed.Content.ContentProposal>)typeof(MainWindow).GetField("_contentProposalItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            proposals.Add(proposal);
            Assert.True(await window.CreateProjectInventoryAsync());
            window.FindControl<ComboBox>("InventoryCoverageList")!.SelectedIndex = 0;
            Assert.True(window.OpenInventoryEvidence());
            Assert.Equal(4, window.FindControl<TabControl>("WorkspaceTabs")!.SelectedIndex);
            Assert.Equal(0, window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Add_missing_rules_skips_saving_and_preserves_schema_when_all_drafts_are_cited()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new DesignSourceNote("promise", "Promises connect participants.", now);
        var draftRule = new WorldSeed.RuleDrafting.RuleStatement("draft", "Promise", WorldSeed.RuleDrafting.RuleStatementKind.Rule,
            source.OriginalText, [source.Id], [new(source.Id, source.OriginalText)]);
        var draft = new WorldSeed.RuleDrafting.StructuredRuleDraft("Promises", "Describe promises.", [draftRule], [], [], [], []);
        var session = new DesignSession("session", "roleplay", "local", now, now, [source], [],
            new(WorldSeed.RuleDrafting.RuleDraftAction.PresentDraft, null, [], draft));
        var project = DesignProjectService.Create("roleplay", "Promises") with { SourceNotes = [source], Sessions = [session] };
        var rule = new WorldSeed.WorkingRules.WorkingRule("stable-id", "Promise", WorldSeed.WorkingRules.WorkingRuleKind.Rule,
            source.OriginalText, [new(source.Id, null, source.OriginalText)], [new(session.Id, draftRule.Id)], [], [], null, [], []);
        var rules = new WorldSeed.WorkingRules.WorkingRuleSet("rules", project.Id, "0.1", "Rules", null, 2, now, now, [rule], [], [], [], []);
        var schema = new WorldSeed.SchemaTranslation.ProjectSchemaProposal("proposal", project.Id, "0.1", "Schema", "{}", [], rules.Id, rules.Revision, [], now, now);
        using var api = new HttpClient(new InventoryHandler(CreateInventory(project.Id))) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(new HttpClient(), api);
        try
        {
            SetField(window, "_project", project);
            SetField(window, "_workingRuleSet", rules);
            SetField(window, "_schemaProposal", schema);
            var model = window.FindControl<ComboBox>("ModelSelector")!;
            model.ItemsSource = new[] { "qa-local" };
            model.SelectedIndex = 0;

            await window.BuildWorkingRulesAsync(true);

            Assert.Contains("No Changes Were Needed", window.FindControl<TextBlock>("WorkingRulesStatusText")!.Text);
            Assert.Same(schema, typeof(MainWindow).GetField("_schemaProposal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
            Assert.True(window.FindControl<Button>("AddMissingWorkingRulesButton")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Rule_build_shows_live_wait_progress_and_cleans_up_after_failure()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new DesignSourceNote("source", "Promises connect participants.", now);
        var draft = new WorldSeed.RuleDrafting.StructuredRuleDraft("Promises", "Describe promises.",
            [new("draft", "Promise", WorldSeed.RuleDrafting.RuleStatementKind.Rule, source.OriginalText, [source.Id], [new(source.Id, source.OriginalText)])], [], [], [], []);
        var project = DesignProjectService.Create("progress", "Progress") with
        {
            SourceNotes = [source], Sessions = [new("session", "progress", "local", now, now, [source], [], new(WorldSeed.RuleDrafting.RuleDraftAction.PresentDraft, null, [], draft))]
        };
        using var handler = new WaitingModelHandler();
        using var model = new HttpClient(handler);
        using var api = new HttpClient(new InventoryHandler(CreateInventory(project.Id))) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            SetField(window, "_project", project);
            var selector = window.FindControl<ComboBox>("ModelSelector")!;
            selector.ItemsSource = new[] { "fake" }; selector.SelectedIndex = 0;
            var operation = window.BuildWorkingRulesAsync(false);
            await handler.Started.Task;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var status = window.FindControl<TextBlock>("WorkingRulesStatusText")!;
            Assert.Contains("0 / 1 Source Rules Prepared", status.Text);
            Assert.Contains("Waiting For Local Model", status.Text);
            Assert.True(window.FindControl<ProgressBar>("WorkingRulesActivityBar")!.IsVisible);
            Assert.False(window.FindControl<Button>("BuildWorkingRulesButton")!.IsEnabled);
            SetField(window, "_lastInteractionAt", DateTimeOffset.UtcNow.AddDays(-1));
            typeof(MainWindow).GetMethod("InactivityTimer_Tick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [null, EventArgs.Empty]);
            Assert.True((DateTimeOffset)typeof(MainWindow).GetField("_lastInteractionAt", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)! > DateTimeOffset.UtcNow.AddMinutes(-1));
            var before = status.Text;
            await Task.Delay(1200);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotEqual(before, status.Text);
            handler.Release.TrySetResult(true);
            await operation;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Contains("Even After Recovery", status.Text);
            Assert.False(window.FindControl<ProgressBar>("WorkingRulesActivityBar")!.IsVisible);
            Assert.True(window.FindControl<Button>("BuildWorkingRulesButton")!.IsEnabled);
        }
        finally { handler.Release.TrySetResult(true); window.Close(); }
    }

    private sealed class WaitingModelHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { message = new { role = "assistant", content = "{}" }, done = true }) };
        }
    }

    [AvaloniaFact]
    public async Task Reloaded_checkpoint_offers_resume_blocks_stale_inputs_and_can_be_discarded()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new DesignSourceNote("source", "Promises connect participants.", now);
        var draft = new WorldSeed.RuleDrafting.StructuredRuleDraft("Promises", "Describe promises.",
            [new("draft", "Promise", WorldSeed.RuleDrafting.RuleStatementKind.Rule, source.OriginalText, [source.Id], [new(source.Id, source.OriginalText)])], [], [], [], []);
        var project = DesignProjectService.Create("checkpoint", "Checkpoint") with
        { SourceNotes = [source], Sessions = [new("session", "checkpoint", "local", now, now, [source], [], new(WorldSeed.RuleDrafting.RuleDraftAction.PresentDraft, null, [], draft))] };
        var checkpoint = new WorldSeed.WorkingRules.WorkingRulePreparation(project.Id,
            WorldSeed.WorkingRules.WorkingRulePreparationBinding.Fingerprint(project, null, true), true, now, now, []);
        using var api = new HttpClient(new InventoryHandler(CreateInventory(project.Id), checkpoint)) { BaseAddress = new Uri("http://qa.invalid/") };
        using var model = new HttpClient(new WaitingModelHandler());
        var window = new MainWindow(model, api);
        try
        {
            SetField(window, "_project", project);
            async Task Refresh() => await (Task)typeof(MainWindow).GetMethod("RefreshWorkingRulePreparationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [project.Id])!;
            await Refresh();
            Assert.True(window.FindControl<Border>("WorkingRulesCheckpointPanel")!.IsVisible);
            Assert.True(window.FindControl<Button>("ResumeWorkingRulesButton")!.IsEnabled);
            SetField(window, "_project", project with { Name = "Changed" });
            await Refresh();
            Assert.False(window.FindControl<Button>("ResumeWorkingRulesButton")!.IsEnabled);
            Assert.Contains("Outdated", window.FindControl<TextBlock>("WorkingRulesCheckpointText")!.Text);
            await window.DiscardWorkingRuleCheckpointAsync();
            Assert.False(window.FindControl<Border>("WorkingRulesCheckpointPanel")!.IsVisible);
            Assert.Equal(source.OriginalText, ((DesignProject)typeof(MainWindow).GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).SourceNotes[0].OriginalText);
        }
        finally { window.Close(); }
    }

    private static ProjectInventory CreateInventory(string projectId)
    {
        var now = DateTimeOffset.Parse("2026-11-13T18:00:00Z");
        return new ProjectInventory(
            $"project-inventory-{projectId}-r1", projectId, "0.1", 1, now,
            new ProjectInventoryBinding(now, "rules", 2, "proposal", now, "active", 3, "data", now, null, null),
            new InventorySourceCoverage(2, 500, 2, ["source-2"]),
            new InventoryRuleCoverage(8, new Dictionary<string, int> { ["Rule"] = 8 }, 8, [], ["How are reactions resolved?"], [], []),
            new InventorySchemaCoverage(
                new Dictionary<string, int> { ["entityTypes"] = 1, ["validations"] = 2 },
                [new InventoryContentTypeCoverage("weapon", "Weapon", 5, 3, 4, 2, 1, 1, true)], 2, false),
            new InventoryPublicationCoverage(0, null, null, 0),
            [new InventoryObservation("observation-1", InventoryObservationKind.UnresolvedQuestion, "question-1", "How are reactions resolved?", [])]);
    }

    private static void SetField(MainWindow window, string name, object value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

    private sealed class InventoryHandler(ProjectInventory inventory, WorldSeed.WorkingRules.WorkingRulePreparation? checkpoint = null) : HttpMessageHandler
    {
        public int CreateRequests { get; private set; }
        private WorldSeed.WorkingRules.WorkingRulePreparation? _preparation = checkpoint;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/working-rule-preparation", StringComparison.Ordinal))
            {
                if (request.Method == HttpMethod.Get) return _preparation is null ? new(HttpStatusCode.NotFound) : new(HttpStatusCode.OK) { Content = JsonContent.Create(_preparation) };
                if (request.Method == HttpMethod.Put) _preparation = await request.Content!.ReadFromJsonAsync<WorldSeed.WorkingRules.WorkingRulePreparation>(cancellationToken: cancellationToken);
                if (request.Method == HttpMethod.Delete) _preparation = null;
                return new(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/inventories", StringComparison.Ordinal))
            {
                CreateRequests++;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(inventory) };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
