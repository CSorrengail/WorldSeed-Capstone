using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WorldSeed.App;
using WorldSeed.Content;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.WorkingRules;

namespace WorldSeed.Qa.Tests;

public sealed class ContentReviewReadyStateTests
{
    [AvaloniaFact]
    public async Task Loaded_workspace_keeps_generation_available_and_explains_missing_setup()
    {
        using var model = new HttpClient(new ModelHandler());
        using var api = new HttpClient(new ProposalApiHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var project = DesignProjectService.Create("ready-project", "Ready Project");
            var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "A creature is game content.", [], [], [], [], null, [], []);
            var rules = new WorkingRuleSet("ready-rules", project.Id, "0.1", "Ready Rules", null, 1, now, now, [rule], [], [], [], []);
            var schemaJson = new JsonObject
            {
                ["id"] = "ready-schema",
                ["name"] = "Ready Schema",
                ["version"] = "1.0",
                ["valueTypes"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["metadata"] = new JsonObject { ["id"] = "creature-role", ["name"] = "Creature Role" },
                        ["kind"] = "enumeration",
                        ["entries"] = new JsonArray { new JsonObject { ["id"] = "skirmisher", ["name"] = "Skirmisher" } }
                    }
                },
                ["entityTypes"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["metadata"] = new JsonObject { ["id"] = "creature", ["name"] = "Creature" },
                        ["properties"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["metadata"] = new JsonObject { ["id"] = "creature.role", ["name"] = "Role" },
                                ["valueType"] = new JsonObject { ["kind"] = "definedValueType", ["valueTypeId"] = "creature-role" },
                                ["required"] = true,
                                ["nullable"] = false
                            }
                        }
                    }
                }
            };
            var active = new ActiveGameSchema("active-ready", project.Id, "0.1", "Ready Schema", schemaJson.ToJsonString(), "proposal-ready", rules.Id, rules.Revision, 1, now, now);

            window.RestoreContentReviewWorkspace(project, rules, active, GameDataService.CreateEmpty(active, now));

            var generate = window.FindControl<Button>("GenerateContentProposalButton")!;
            var modelSelector = window.FindControl<ComboBox>("ModelSelector")!;
            var typeSelector = window.FindControl<ComboBox>("ContentProposalEntityTypeList")!;
            var request = window.FindControl<TextBox>("ContentProposalRequestText")!;
            Assert.True(generate.IsEnabled);
            Assert.Single(window.FindControl<ComboBox>("ContentExpansionStageTypeList")!.Items.Cast<object>());
            Assert.Single(window.FindControl<ComboBox>("EvaluationEntityTypeList")!.Items.Cast<object>());
            Assert.True(window.FindControl<Button>("GenerateEvaluationProposalButton")!.IsEnabled);

            await window.GenerateContentProposalAsync();
            Assert.Equal("Select a local model first.", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            modelSelector.ItemsSource = new[] { "local-model" };
            modelSelector.SelectedIndex = 0;
            await window.GenerateContentProposalAsync();
            Assert.Equal("Select a content type first.", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            typeSelector.SelectedIndex = 0;
            Assert.Contains("No Balance Profile Is Saved", window.FindControl<TextBlock>("ContentBalanceProfileStatusText")!.Text);
            await window.GenerateContentProposalAsync();
            Assert.Equal("Describe the content you want to propose.", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            request.Text = "Create one creature.";

            Assert.True(generate.IsEnabled,
                $"model={modelSelector.SelectedItem ?? "<null>"}; typeIndex={typeSelector.SelectedIndex}; request='{request.Text}'; schema='{window.FindControl<TextBlock>("ContentReviewSchemaText")!.Text}'");

            await window.GenerateContentProposalAsync();

            Assert.Equal(0, window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex);
            Assert.Contains("coal-stalker", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            Assert.Contains("Saved proposal version 1", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);
            Assert.Contains("Allowed entry IDs: 'skirmisher'", window.FindControl<TextBox>("ContentMechanicalValidationText")!.Text);
            Assert.Contains("Open Game-Specific Balance Profile", window.FindControl<TextBox>("ContentBalanceEvaluationText")!.Text);
            Assert.Contains("threat rating", window.FindControl<TextBlock>("ContentSchemaGapText")!.Text);
            Assert.True(window.FindControl<Button>("OpenSchemaForContentGapButton")!.IsVisible);

            window.FindControl<TextBox>("ContentProposalDecisionNoteText")!.Text = "Use a valid creature role.";
            Assert.True(await window.RequestContentProposalRevisionAsync());
            Assert.Equal("Generate Requested Revision", generate.Content);
            Assert.Contains("Use a valid creature role.", request.Text);
            Assert.Contains("Existing Candidate", request.Text);
            Assert.Contains("Allowed entry IDs: 'skirmisher'", request.Text);

            await window.GenerateContentProposalAsync();

            Assert.Contains("Saved replacement version 2 against schema revision 1", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);
            Assert.Contains("skirmisher", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            Assert.Contains("fits the active schema", window.FindControl<TextBlock>("ContentSchemaGapText")!.Text, StringComparison.OrdinalIgnoreCase);
            Assert.False(window.FindControl<Button>("OpenSchemaForContentGapButton")!.IsVisible);
            Assert.Single(window.FindControl<ListBox>("ContentProposalList")!.Items.Cast<object>());
            Assert.Contains("Version 2 (Current)", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.Contains("Version 1", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.Contains("Use a valid creature role.", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.Equal("Generate Proposal", generate.Content);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Historical_proposal_can_be_re_evaluated_as_one_lineage_against_current_schema()
    {
        using var model = new HttpClient(new ReevaluateModelHandler());
        using var api = new HttpClient(new ProposalApiHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var project = DesignProjectService.Create("rebase-project", "Rebase Project");
            var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "Creatures have roles and may have reach.", [], [], [], [], null, [], []);
            var rules = new WorkingRuleSet("rebase-rules", project.Id, "0.1", "Rebase Rules", null, 1, now, now, [rule], [], [], [], []);
            var currentSchemaJson = JsonNode.Parse("""
                {"id":"rebase-schema","name":"Rebase Schema","version":"1.1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"role","name":"Role"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false},{"metadata":{"id":"reach","name":"Reach"},"valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false}]}],"valueTypes":[]}
                """)!.AsObject();
            var active = new ActiveGameSchema("active-rebase", project.Id, "0.1", "Rebase Schema", currentSchemaJson.ToJsonString(), "proposal-ready", rules.Id, rules.Revision, 2, now, now);
            var historical = ContentProposalLifecycleService.Create(project.Id, "creature", JsonNode.Parse("""{"id":"coal-stalker","entityTypeId":"creature","values":{"role":"skirmisher"}}""")!.AsObject(), "active-rebase", 1, rules.Id, rules.Revision, now, schemaGaps: [new ContentSchemaGap("reach", "No reach property existed.")], designerRequest: "Create a coal stalker with reach 2.");

            window.RestoreContentReviewWorkspace(project, rules, active, GameDataService.CreateEmpty(active, now), proposals: [historical]);
            window.FindControl<ComboBox>("ModelSelector")!.ItemsSource = new[] { "local-model" };
            window.FindControl<ComboBox>("ModelSelector")!.SelectedIndex = 0;
            window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex = 0;

            Assert.True(window.FindControl<Button>("ReevaluateContentProposalButton")!.IsVisible);
            Assert.Contains("Historical", window.FindControl<TextBox>("ContentMechanicalValidationText")!.Text);
            Assert.True(await window.ReevaluateContentProposalAsync());

            Assert.Single(window.FindControl<ListBox>("ContentProposalList")!.Items.Cast<object>());
            Assert.Contains("\"reach\": 2", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            Assert.Contains("Version 2 (Current)", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.Contains("Version 1", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.Contains("Create a coal stalker with reach 2.", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.False(window.FindControl<Button>("ReevaluateContentProposalButton")!.IsVisible);
            Assert.StartsWith("Passed:", window.FindControl<TextBox>("ContentMechanicalValidationText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Suggest_new_content_uses_bounded_canonical_and_decision_context()
    {
        var modelHandler = new ContextModelHandler();
        using var model = new HttpClient(modelHandler);
        using var api = new HttpClient(new ProposalApiHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var project = DesignProjectService.Create("context-project", "Context Project");
            var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "Creatures have names and roles.", [], [], [], [], null, [], []);
            var rules = new WorkingRuleSet("context-rules", project.Id, "0.1", "Context Rules", null, 1, now, now, [rule], [], [], [], []);
            var schemaJson = JsonNode.Parse("""{"id":"context-schema","name":"Context Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false},{"metadata":{"id":"role","name":"Role"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""")!.AsObject();
            var active = new ActiveGameSchema("active-context", project.Id, "0.1", "Context Schema", schemaJson.ToJsonString(), "proposal-context", rules.Id, rules.Revision, 1, now, now);
            var gameDataRoot = JsonNode.Parse(GameDataService.CreateEmpty(active, now).DataJson)!.AsObject();
            gameDataRoot["entityInstances"]!.AsArray().Add(JsonNode.Parse("""{"id":"coal-stalker","entityTypeId":"creature","values":{"name":"Coal Stalker","role":"ambusher"}}""")!.AsObject());
            var gameData = GameDataService.CreateEmpty(active, now) with { DataJson = gameDataRoot.ToJsonString() };
            var prior = ContentProposalLifecycleService.Create(project.Id, "creature", JsonNode.Parse("""{"id":"ember-wolf","entityTypeId":"creature","values":{"name":"Ember Wolf","role":"hunter"}}""")!.AsObject(), active.Id, active.Revision, rules.Id, rules.Revision, now, designerRequest: "Create an ember wolf.");
            var decision = ContentProposalLifecycleService.Decide(prior, ContentProposalDecisionKind.Rejected, "Avoid another fire creature.", now);

            window.RestoreContentReviewWorkspace(project, rules, active, gameData, proposals: [prior], decisions: [decision]);
            window.FindControl<ComboBox>("ModelSelector")!.ItemsSource = new[] { "local-model" };
            window.FindControl<ComboBox>("ModelSelector")!.SelectedIndex = 0;
            window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.SelectedIndex = 0;

            Assert.True(await window.SuggestContentProposalAsync());

            Assert.Contains("Propose one original Creature", window.FindControl<TextBox>("ContentProposalRequestText")!.Text);
            Assert.Contains("coal-stalker", window.FindControl<TextBlock>("ContentGenerationContextText")!.Text);
            Assert.Contains(decision.Id, window.FindControl<TextBlock>("ContentGenerationContextText")!.Text);
            Assert.Contains("glass-fox", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            Assert.Contains("coal-stalker", modelHandler.RequestBody);
            Assert.Contains("Avoid another fire creature", modelHandler.RequestBody);
            Assert.Equal(2, window.FindControl<ListBox>("ContentProposalList")!.Items.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Expansion_queue_generates_separate_proposals_sequentially()
    {
        var modelHandler = new ExpansionModelHandler();
        using var model = new HttpClient(modelHandler);
        using var api = new HttpClient(new ProposalApiHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var project = DesignProjectService.Create("expansion-project", "Expansion Project");
            var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "Creatures have names.", [], [], [], [], null, [], []);
            var rules = new WorkingRuleSet("expansion-rules", project.Id, "0.1", "Expansion Rules", null, 1, now, now, [rule], [], [], [], []);
            var schemaJson = JsonNode.Parse("""{"id":"expansion-schema","name":"Expansion Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""")!.AsObject();
            var active = new ActiveGameSchema("active-expansion", project.Id, "0.1", "Expansion Schema", schemaJson.ToJsonString(), "proposal-expansion", rules.Id, rules.Revision, 1, now, now);

            window.RestoreContentReviewWorkspace(project, rules, active, GameDataService.CreateEmpty(active, now));
            window.FindControl<ComboBox>("ModelSelector")!.ItemsSource = new[] { "local-model" };
            window.FindControl<ComboBox>("ModelSelector")!.SelectedIndex = 0;
            window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.SelectedIndex = 0;
            window.FindControl<TextBox>("ContentProposalRequestText")!.Text = "Create a varied forest encounter set.";
            window.FindControl<ComboBox>("ContentExpansionCountList")!.SelectedIndex = 1;

            Assert.Equal(3, await window.GenerateContentExpansionAsync());

            Assert.Equal(3, modelHandler.RequestBodies.Count);
            Assert.DoesNotContain("queue-one", modelHandler.RequestBodies[0]);
            Assert.Contains("queue-one", modelHandler.RequestBodies[1]);
            Assert.Contains("queue-one", modelHandler.RequestBodies[2]);
            Assert.Contains("queue-two", modelHandler.RequestBodies[2]);
            Assert.Equal(3, window.FindControl<ListBox>("ContentProposalList")!.Items.Count);
            Assert.Equal(3, window.FindControl<ListBox>("ContentExpansionQueueList")!.Items.Count);
            Assert.All(window.FindControl<ListBox>("ContentExpansionQueueList")!.Items.Cast<object>(), item => Assert.Contains("Ready For Review", item.ToString()));
            Assert.Contains("3 Of 3 Separate Proposals", window.FindControl<TextBlock>("ContentExpansionStatusText")!.Text);
            Assert.False(window.FindControl<Button>("ApplyApprovedContentButton")!.IsEnabled);
            window.FindControl<ListBox>("ContentExpansionQueueList")!.SelectedIndex = 0;
            Assert.Contains("queue-one", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            window.FindControl<ListBox>("ContentExpansionQueueList")!.SelectedIndex = 1;
            Assert.Contains("queue-two", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Expansion_queue_stops_before_starting_another_model_request()
    {
        var modelHandler = new BlockingExpansionModelHandler();
        using var model = new HttpClient(modelHandler);
        using var api = new HttpClient(new ProposalApiHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var project = DesignProjectService.Create("stopped-expansion-project", "Stopped Expansion Project");
            var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "Creatures have names.", [], [], [], [], null, [], []);
            var rules = new WorkingRuleSet("stopped-expansion-rules", project.Id, "0.1", "Expansion Rules", null, 1, now, now, [rule], [], [], [], []);
            var schemaJson = JsonNode.Parse("""{"id":"stopped-expansion-schema","name":"Expansion Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""")!.AsObject();
            var active = new ActiveGameSchema("active-stopped-expansion", project.Id, "0.1", "Expansion Schema", schemaJson.ToJsonString(), "proposal-expansion", rules.Id, rules.Revision, 1, now, now);
            window.RestoreContentReviewWorkspace(project, rules, active, GameDataService.CreateEmpty(active, now));
            window.FindControl<ComboBox>("ModelSelector")!.ItemsSource = new[] { "local-model" };
            window.FindControl<ComboBox>("ModelSelector")!.SelectedIndex = 0;
            window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.SelectedIndex = 0;
            window.FindControl<TextBox>("ContentProposalRequestText")!.Text = "Create a forest encounter set.";
            window.FindControl<ComboBox>("ContentExpansionCountList")!.SelectedIndex = 1;

            var expansion = window.GenerateContentExpansionAsync();
            await modelHandler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(window.FindControl<ComboBox>("ModelSelector")!.IsEnabled);
            Assert.False(window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.IsEnabled);
            Assert.False(window.FindControl<TextBox>("ContentProposalRequestText")!.IsEnabled);
            Assert.False(window.FindControl<Button>("NewProjectButton")!.IsEnabled);
            Assert.False(window.FindControl<Button>("DeleteProjectButton")!.IsEnabled);
            Assert.True(window.FindControl<Button>("StopContentExpansionButton")!.IsEnabled);
            window.RequestContentExpansionStop();
            modelHandler.AllowResponse.TrySetResult();

            Assert.Equal(1, await expansion);
            Assert.Equal(1, modelHandler.RequestCount);
            Assert.Contains("Expansion Stopped", window.FindControl<TextBlock>("ContentExpansionStatusText")!.Text);
            Assert.Equal(3, window.FindControl<ListBox>("ContentExpansionQueueList")!.Items.Count);
            Assert.Equal(2, window.FindControl<ListBox>("ContentExpansionQueueList")!.Items.Cast<object>().Count(item => item.ToString()!.Contains("Stopped")));
            Assert.True(window.FindControl<ComboBox>("ModelSelector")!.IsEnabled);
            Assert.True(window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.IsEnabled);
            Assert.True(window.FindControl<TextBox>("ContentProposalRequestText")!.IsEnabled);
            Assert.True(window.FindControl<Button>("NewProjectButton")!.IsEnabled);
            Assert.True(window.FindControl<Button>("DeleteProjectButton")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Replacing_workspace_invalidates_an_in_flight_expansion_without_saving_stale_results()
    {
        var modelHandler = new BlockingExpansionModelHandler();
        using var model = new HttpClient(modelHandler);
        using var api = new HttpClient(new ProposalApiHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var first = CreateExpansionWorkspace("first", now);
            var second = CreateExpansionWorkspace("second", now.AddMinutes(1));
            window.RestoreContentReviewWorkspace(first.Project, first.Rules, first.Schema, first.Data);
            window.FindControl<ComboBox>("ModelSelector")!.ItemsSource = new[] { "local-model" };
            window.FindControl<ComboBox>("ModelSelector")!.SelectedIndex = 0;
            window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.SelectedIndex = 0;
            window.FindControl<TextBox>("ContentProposalRequestText")!.Text = "Create a forest encounter set.";

            var expansion = window.GenerateContentExpansionAsync();
            await modelHandler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            window.RestoreContentReviewWorkspace(second.Project, second.Rules, second.Schema, second.Data);
            modelHandler.AllowResponse.TrySetResult();

            Assert.Equal(0, await expansion);
            Assert.Empty(window.FindControl<ListBox>("ContentExpansionQueueList")!.Items.Cast<object>());
            Assert.Empty(window.FindControl<ListBox>("ContentProposalList")!.Items.Cast<object>());
            Assert.Equal("No Expansion Queue Is Running.", window.FindControl<TextBlock>("ContentExpansionStatusText")!.Text);
            Assert.DoesNotContain("Could not generate", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("InvalidOperationException", window.FindControl<TextBox>("ContentProposalEditor")!.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally { window.Close(); }
    }

    private static (DesignProject Project, WorkingRuleSet Rules, ActiveGameSchema Schema, ProjectGameData Data) CreateExpansionWorkspace(string suffix, DateTimeOffset now)
    {
        var project = DesignProjectService.Create($"{suffix}-expansion-project", $"{suffix} Expansion Project");
        var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "Creatures have names.", [], [], [], [], null, [], []);
        var rules = new WorkingRuleSet($"{suffix}-expansion-rules", project.Id, "0.1", "Expansion Rules", null, 1, now, now, [rule], [], [], [], []);
        var schemaJson = JsonNode.Parse($$"""{"id":"{{suffix}}-expansion-schema","name":"Expansion Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""")!.AsObject();
        var schema = new ActiveGameSchema($"active-{suffix}-expansion", project.Id, "0.1", "Expansion Schema", schemaJson.ToJsonString(), "proposal-expansion", rules.Id, rules.Revision, 1, now, now);
        return (project, rules, schema, GameDataService.CreateEmpty(schema, now));
    }

    [AvaloniaFact]
    public async Task Approved_proposal_requires_warning_confirmation_then_becomes_canonical()
    {
        var now = DateTimeOffset.UtcNow;
        var project = DesignProjectService.Create("apply-project", "Apply Project");
        var rule = new WorkingRule("creature-definition", "Creature Definition", WorkingRuleKind.Definition, "Creatures have names.", [], [], [], [], null, [], []);
        var rules = new WorkingRuleSet("apply-rules", project.Id, "0.1", "Apply Rules", null, 1, now, now, [rule], [], [], [], []);
        var schemaJson = JsonNode.Parse("""{"id":"apply-schema","name":"Apply Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""")!.AsObject();
        var active = new ActiveGameSchema("active-apply", project.Id, "0.1", "Apply Schema", schemaJson.ToJsonString(), "proposal-apply", rules.Id, rules.Revision, 1, now, now);
        var gameData = GameDataService.CreateEmpty(active, now);
        var proposal = ContentProposalLifecycleService.Create(project.Id, "creature", JsonNode.Parse("""{"id":"glass-fox","entityTypeId":"creature","values":{"name":"Glass Fox"}}""")!.AsObject(), active.Id, active.Revision, rules.Id, rules.Revision, now, schemaGaps: [new ContentSchemaGap("lore", "Lore is not represented by this schema.")], designerRequest: "Create a glass fox.");
        var approval = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, "Use it.", now);
        var prepared = ContentApplicationService.Prepare(proposal, approval, active, gameData, now);
        var applied = ContentApplicationService.Complete(prepared.Application, prepared.UpdatedGameData, now.AddSeconds(1));
        var apiHandler = new ApplicationApiHandler(applied, prepared.UpdatedGameData);
        var postApplyModelHandler = new PostApplyModelHandler();
        using var model = new HttpClient(postApplyModelHandler);
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            window.RestoreContentReviewWorkspace(project, rules, active, gameData, proposals: [proposal], decisions: [approval]);
            window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex = 0;
            var apply = window.FindControl<Button>("ApplyApprovedContentButton")!;
            Assert.True(apply.IsEnabled);

            Assert.False(await window.ApplyApprovedContentAsync());
            Assert.Equal(0, apiHandler.ApplyCalls);
            Assert.Equal("Confirm Apply With Warnings", apply.Content);
            Assert.Contains("advisory", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text, StringComparison.OrdinalIgnoreCase);

            Assert.True(await window.ApplyApprovedContentAsync());
            Assert.Equal(1, apiHandler.ApplyCalls);
            Assert.Equal("Applied To Game Data", apply.Content);
            Assert.False(apply.IsEnabled);
            Assert.Contains("glass-fox", window.FindControl<TextBox>("GameDataJsonText")!.Text);
            Assert.Contains("application event", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            window.FindControl<ComboBox>("ModelSelector")!.ItemsSource = new[] { "local-model" };
            window.FindControl<ComboBox>("ModelSelector")!.SelectedIndex = 0;
            window.FindControl<ComboBox>("ContentProposalEntityTypeList")!.SelectedIndex = 0;
            Assert.True(await window.SuggestContentProposalAsync());
            Assert.Contains("glass-fox", postApplyModelHandler.RequestBody);
            Assert.Contains("stone-moth", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
        }
        finally { window.Close(); }
    }

    private sealed class ModelHandler : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var first = Interlocked.Increment(ref _requestCount) == 1;
            var role = first ? "wrong" : "skirmisher";
            var schemaGaps = first
                ? "[{\"requestedConcept\":\"threat rating\",\"reason\":\"The active schema has no threat-rating property.\",\"suggestedSchemaChange\":\"Add a game-specific threat-rating rule and property.\"}]"
                : "[]";
            var body = JsonSerializer.Serialize(new
            {
                model = "local-model",
                message = new { content = $"{{\"candidate\":{{\"id\":\"coal-stalker\",\"entityTypeId\":\"creature\",\"values\":{{\"creature.role\":\"{role}\"}}}},\"schemaGaps\":{schemaGaps}}}" }
            });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class ReevaluateModelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonSerializer.Serialize(new
            {
                model = "local-model",
                message = new { content = "{\"candidate\":{\"id\":\"coal-stalker\",\"entityTypeId\":\"creature\",\"values\":{\"role\":\"skirmisher\",\"reach\":2}},\"schemaGaps\":[]}" }
            });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class ContextModelHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = JsonSerializer.Serialize(new
            {
                model = "local-model",
                message = new { content = "{\"candidate\":{\"id\":\"glass-fox\",\"entityTypeId\":\"creature\",\"values\":{\"name\":\"Glass Fox\",\"role\":\"scout\"}},\"schemaGaps\":[]}" }
            });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private sealed class ExpansionModelHandler : HttpMessageHandler
    {
        private int _requestCount;
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var index = Interlocked.Increment(ref _requestCount);
            var id = index switch { 1 => "queue-one", 2 => "queue-two", _ => "queue-three" };
            var body = JsonSerializer.Serialize(new
            {
                model = "local-model",
                message = new { content = $"{{\"candidate\":{{\"id\":\"{id}\",\"entityTypeId\":\"creature\",\"values\":{{\"name\":\"Queue Creature {index}\"}}}},\"schemaGaps\":[]}}" }
            });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private sealed class BlockingExpansionModelHandler : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestStarted.TrySetResult();
            await AllowResponse.Task.WaitAsync(cancellationToken);
            var body = JsonSerializer.Serialize(new
            {
                model = "local-model",
                message = new { content = "{\"candidate\":{\"id\":\"queue-one\",\"entityTypeId\":\"creature\",\"values\":{\"name\":\"Queue Creature\"}},\"schemaGaps\":[]}" }
            });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private sealed class ProposalApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Contains("/content-proposals/", request.RequestUri!.AbsolutePath);
            Assert.True(request.Method == HttpMethod.Put || request.Method == HttpMethod.Post);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        }
    }

    private sealed class ApplicationApiHandler(ContentApplication application, ProjectGameData gameData) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public int ApplyCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/apply", StringComparison.Ordinal))
            {
                ApplyCalls++;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(application, options: JsonOptions) });
            }
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/game-data", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(gameData, options: JsonOptions) });
            if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.Contains("/content-proposals/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
            throw new InvalidOperationException($"Unexpected QA request: {request.Method} {request.RequestUri}");
        }
    }

    private sealed class PostApplyModelHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = JsonSerializer.Serialize(new { model = "local-model", message = new { content = "{\"candidate\":{\"id\":\"stone-moth\",\"entityTypeId\":\"creature\",\"values\":{\"name\":\"Stone Moth\"}},\"schemaGaps\":[]}" } });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }
}
