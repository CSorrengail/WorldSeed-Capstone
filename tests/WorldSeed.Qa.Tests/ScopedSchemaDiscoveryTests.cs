using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.App;
using WorldSeed.DesignSessions;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.Qa.Tests;

public sealed class ScopedSchemaDiscoveryTests
{
    [AvaloniaFact]
    public async Task Queued_structure_discovery_uses_only_selected_rules_and_does_not_activate()
    {
        var modelHandler = new SequencedOllamaHandler(
            DiscoveryResponse,
            ProposalResponse);
        var apiHandler = new SchemaProposalCaptureHandler();
        using var model = new HttpClient(modelHandler);
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api);
        try
        {
            Queue(window, "habitat-definition", "habitat-rule");

            await window.BuildQueuedGameStructuresAsync();

            Assert.Equal(2, modelHandler.RequestBodies.Count);
            Assert.Contains("habitat-definition", modelHandler.RequestBodies[0]);
            Assert.Contains("habitat-rule", modelHandler.RequestBodies[0]);
            Assert.DoesNotContain("unrelated-combat", modelHandler.RequestBodies[0]);
            var saved = Assert.Single(apiHandler.SavedProposals);
            Assert.Equal(new[] { "habitat-definition", "habitat-rule" }, saved.SelectedWorkingRuleIds.Order());
            Assert.Equal(new[] { "habitat-definition", "habitat-rule" }, Assert.Single(saved.DefinitionSources).WorkingRuleIds.Order());
            Assert.Empty(QueueIds(window));
            Assert.Single(apiHandler.Requests, request => request.StartsWith("PUT ", StringComparison.Ordinal));
            Assert.DoesNotContain(apiHandler.Requests, request => request.Contains("active-schema", StringComparison.Ordinal));
            Assert.Contains("Still Editable And Non-Canonical", window.FindControl<TextBlock>("SchemaStatusText")!.Text);
            var log = File.ReadAllText(AppLog.CurrentPath);
            Assert.Contains("MessageCharacters=", log);
            Assert.Contains("SavedSchemaCharacters=", log);
            Assert.DoesNotContain("Creatures may inherit one movement option from their habitat.", log);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Queued_structure_clarification_resumes_the_same_plan_and_retains_the_answer()
    {
        var modelHandler = new SequencedOllamaHandler(
            DiscoveryResponse,
            """{"action":"askClarifyingQuestion","clarifyingQuestion":"Should habitats be reusable entries?","clarificationContext":{"workingRuleIds":["habitat-definition"],"detail":"Whether habitats have individual entries.","reason":"This determines whether to create a content type."}}""",
            ProposalResponse);
        var apiHandler = new SchemaProposalCaptureHandler();
        using var model = new HttpClient(modelHandler);
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api);
        try
        {
            Queue(window, "habitat-definition", "habitat-rule");
            await window.BuildQueuedGameStructuresAsync();

            Assert.Empty(apiHandler.SavedProposals);
            Assert.True(window.FindControl<Border>("SchemaQuestionPanel")!.IsVisible);
            Assert.Contains("reusable entries", window.FindControl<TextBlock>("SchemaQuestionText")!.Text);
            Assert.Equal(2, QueueIds(window).Count);

            window.FindControl<TextBox>("SchemaAnswerText")!.Text = "Yes, each habitat is a reusable entry.";
            await window.SubmitSchemaAnswerAsync();

            var saved = Assert.Single(apiHandler.SavedProposals);
            Assert.Contains("Should habitats be reusable entries?", Assert.Single(saved.Clarifications!).Question);
            Assert.Contains("habitat-definition", saved.Clarifications![0].Question);
            Assert.Contains("reusable entry", Assert.Single(saved.Clarifications!).Answer);
            Assert.Contains("Yes, each habitat", modelHandler.RequestBodies[2]);
            Assert.Empty(QueueIds(window));
            Assert.False(window.FindControl<Border>("SchemaQuestionPanel")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Failed_structure_is_skipped_while_successes_are_saved_and_removed_from_queue(bool saveFailure, bool usesQueue)
    {
        var discovery = """{"plans":[{"id":"habitat","name":"Habitat","kind":"contentType","workingRuleIds":["habitat-definition"],"rationale":"Habitat."},{"id":"bad","name":"Middle Broken","kind":"contentType","workingRuleIds":["habitat-rule"],"rationale":"Fails."},{"id":"region","name":"Region","kind":"contentType","workingRuleIds":["unrelated-combat"],"rationale":"Region."}]}""";
        var handler = new SequencedOllamaHandler(saveFailure
            ? [discovery, ProposalFor("habitat", "habitat-definition"), ProposalFor("bad", "habitat-rule"), ProposalFor("region", "unrelated-combat")]
            : [discovery, ProposalFor("habitat", "habitat-definition"), "{}", "{}", "{}", ProposalFor("region", "unrelated-combat")]);
        var store = new SchemaProposalCaptureHandler { FailDefinitionId = saveFailure ? "bad" : null };
        using var model = new HttpClient(handler);
        using var api = new HttpClient(store) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api);
        try
        {
            Queue(window, "habitat-definition", "habitat-rule", "unrelated-combat");
            if (usesQueue) await window.BuildQueuedGameStructuresAsync();
            else await (Task)typeof(MainWindow).GetMethod("BuildGameStructuresAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            Assert.Equal(2, store.SavedProposals.Count);
            var saved = store.SavedProposals[^1];
            Assert.Contains("habitat", saved.SchemaJson);
            Assert.Contains("region", saved.SchemaJson);
            Assert.DoesNotContain(saved.DefinitionSources, mapping => mapping.DefinitionId == "bad");
            Assert.Equal(new[] { "habitat-rule" }, QueueIds(window));
            Assert.Contains("Saved 2", window.FindControl<TextBlock>("SchemaStatusText")!.Text);
            Assert.Contains("Broken", window.FindControl<TextBox>("SchemaText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Queue_selection_preserves_each_category_expansion_state()
    {
        using var model = new HttpClient(); using var api = new HttpClient();
        var window = CreateWindow(model, api);
        try
        {
            var panel = window.FindControl<StackPanel>("SchemaRuleGroupsPanel")!;
            var groups = panel.Children.OfType<Expander>().ToArray();
            Assert.True(groups.Length >= 2);
            groups[0].IsExpanded = false; groups[1].IsExpanded = true;
            var tag = groups[0].Tag;
            Queue(window, "habitat-definition");
            Assert.False(panel.Children.OfType<Expander>().Single(group => Equals(group.Tag, tag)).IsExpanded);
            Assert.True(panel.Children.OfType<Expander>().Single(group => Equals(group.Tag, groups[1].Tag)).IsExpanded);
        }
        finally { window.Close(); }
    }

    private static string ProposalFor(string id, string ruleId) => JsonSerializer.Serialize(new
    {
        action = "presentProposal", proposal = new
        {
            summary = "Defines " + id,
            schema = new { id = "scoped-schema", name = "Scoped Schema", version = "0.1", entityTypes = new[] { new { metadata = new { id, name = id }, properties = Array.Empty<object>() } } },
            definitionSources = new[] { new { definitionId = id, workingRuleIds = new[] { ruleId } } }
        }
    });

    [AvaloniaFact]
    public void Hide_completed_rules_filters_only_the_list_and_preserves_hidden_category_state()
    {
        using var model = new HttpClient(); using var api = new HttpClient();
        var window = CreateWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            SetField(window, "_schemaProposal", new ProjectSchemaProposal("schema", "scoped-schema-project", "0.1", "Schema", "{}",
                [new("habitat", ["habitat-definition"])], "working-rules-scoped", 1, ["habitat-definition"], now, now));
            Queue(window, "habitat-definition");
            var panel = window.FindControl<StackPanel>("SchemaRuleGroupsPanel")!;
            var definition = panel.Children.OfType<Expander>().Single(group => Equals(group.Tag, "Definition"));
            definition.IsExpanded = false;
            var filter = window.FindControl<CheckBox>("HideCompletedSchemaRulesCheck")!;
            Assert.False(filter.IsChecked);
            void Toggle(bool value)
            {
                filter.IsChecked = value;
                typeof(MainWindow).GetMethod("HideCompletedSchemaRules_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [null, new Avalonia.Interactivity.RoutedEventArgs()]);
            }
            Toggle(true);
            Assert.DoesNotContain(panel.Children.OfType<Expander>(), group => Equals(group.Tag, "Definition"));
            Assert.Equal(new[] { "habitat-definition" }, QueueIds(window));
            Toggle(false);
            Assert.False(panel.Children.OfType<Expander>().Single(group => Equals(group.Tag, "Definition")).IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task First_batch_is_saved_before_later_discovery_with_duplicate_names_fails()
    {
        var store = new SchemaProposalCaptureHandler();
        var handler = new SequencedOllamaHandler(DiscoveryResponse, ProposalResponse,
            """{"plans":[{"id":"late","name":"First","name":"Second","kind":"contentType","workingRuleIds":["late-rule"],"rationale":"Ambiguous."}]}""");
        using var model = new HttpClient(handler);
        using var api = new HttpClient(store) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api);
        try
        {
            var rules = (WorkingRuleSet)typeof(MainWindow).GetField("_workingRuleSet", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            SetField(window, "_workingRuleSet", rules with { Rules = rules.Rules.Concat(Enumerable.Range(1, 9).Select(index => Rule("filler-" + index, "Filler", WorkingRuleKind.Rule, "A rule."))).Append(Rule("late-rule", "Late", WorkingRuleKind.Rule, "Another rule.")).ToArray() });
            Invoke(window, "RefreshWorkingRulesView");
            handler.OnRequest = number =>
            {
                if (number != 3) return;
                Assert.Single(store.SavedProposals);
                SetField(window, "_lastInteractionAt", DateTimeOffset.UtcNow.AddDays(-1));
                typeof(MainWindow).GetMethod("InactivityTimer_Tick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [null, EventArgs.Empty]);
                Assert.True((DateTimeOffset)typeof(MainWindow).GetField("_lastInteractionAt", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)! > DateTimeOffset.UtcNow.AddMinutes(-1));
            };
            await (Task)typeof(MainWindow).GetMethod("BuildGameStructuresAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            Assert.Single(store.SavedProposals);
            Assert.Equal(3, handler.RequestBodies.Count);
            Assert.Contains("habitat", handler.RequestBodies[2]);
            Assert.Contains("Saved 1", window.FindControl<TextBlock>("SchemaStatusText")!.Text);
            Assert.Contains("duplicate JSON", window.FindControl<TextBox>("SchemaText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Streaming_discovery_waits_for_later_content_types_before_building_relationships()
    {
        var first = """{"plans":[{"id":"habitat","name":"Habitat","kind":"contentType","workingRuleIds":["habitat-definition"],"rationale":"A habitat."},{"id":"belongsTo","name":"Belongs To","kind":"relationship","workingRuleIds":["habitat-rule"],"rationale":"Habitats belong to regions.","fromContentTypeId":"habitat","toContentTypeId":"region"}]}""";
        var second = """{"plans":[{"id":"region","name":"Region","kind":"contentType","workingRuleIds":["late-rule"],"rationale":"A region."}]}""";
        var relationship = """{"action":"presentProposal","proposal":{"summary":"Links habitats and regions.","schema":{"id":"scoped-schema","name":"Scoped Schema","version":"0.1","relationships":[{"metadata":{"id":"belongsTo","name":"Belongs To"},"from":{"entityTypeId":"habitat","minimumPerOppositeInstance":0},"to":{"entityTypeId":"region","minimumPerOppositeInstance":0},"attributes":[]}]},"definitionSources":[{"definitionId":"belongsTo","workingRuleIds":["habitat-rule"]}]}}""";
        var store = new SchemaProposalCaptureHandler();
        var handler = new SequencedOllamaHandler(first, ProposalFor("habitat", "habitat-definition"), second, ProposalFor("region", "late-rule"), relationship);
        using var model = new HttpClient(handler);
        using var api = new HttpClient(store) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(model, api);
        try
        {
            var rules = (WorkingRuleSet)typeof(MainWindow).GetField("_workingRuleSet", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            SetField(window, "_workingRuleSet", rules with { Rules = rules.Rules.Select(rule => rule.Id == "habitat-rule" ? rule with { AuthoritativeText = "Habitats belong to regions." } : rule)
                .Concat(Enumerable.Range(1, 9).Select(index => Rule("filler-" + index, "Filler", WorkingRuleKind.Rule, "A rule."))).Append(Rule("late-rule", "Region", WorkingRuleKind.Definition, "A region is a reusable place.")).ToArray() });
            Invoke(window, "RefreshWorkingRulesView");
            handler.OnRequest = number => { if (number == 5) Assert.Equal(2, store.SavedProposals.Count); };
            await (Task)typeof(MainWindow).GetMethod("BuildGameStructuresAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            Assert.Equal(3, store.SavedProposals.Count);
            Assert.Contains(store.SavedProposals[^1].DefinitionSources, source => source.DefinitionId == "belongsTo");
        }
        finally { window.Close(); }
    }

    private static MainWindow CreateWindow(HttpClient model, HttpClient api)
    {
        var window = new MainWindow(model, api);
        var now = DateTimeOffset.Parse("2026-11-11T12:00:00Z");
        var project = DesignProjectService.Create("scoped-schema-project", "Scoped Schema Project");
        var rules = new[]
        {
            Rule("habitat-definition", "Habitat Definition", WorkingRuleKind.Definition, "A habitat is a reusable kind of environment."),
            Rule("habitat-rule", "Habitat Movement", WorkingRuleKind.Rule, "Creatures may inherit one movement option from their habitat."),
            Rule("unrelated-combat", "Unrelated Combat", WorkingRuleKind.Rule, "Combatants gain one action each turn.")
        };
        var ruleSet = new WorkingRuleSet("working-rules-scoped", project.Id, "0.1", "Scoped Rules", null, 1, now, now, rules, [], [], [], []);
        SetField(window, "_project", project);
        SetField(window, "_workingRuleSet", ruleSet);
        Invoke(window, "RefreshWorkingRulesView");
        var selector = window.FindControl<ComboBox>("ModelSelector")!;
        selector.ItemsSource = new[] { "test-model" };
        selector.SelectedIndex = 0;
        return window;
    }

    private static WorkingRule Rule(string id, string name, WorkingRuleKind kind, string text) =>
        new(id, name, kind, text, [], [], [], [], null, [], []);

    private static void Queue(MainWindow window, params string[] ids)
    {
        QueueIds(window).AddRange(ids);
        Invoke(window, "RefreshSchemaView");
    }

    private static List<string> QueueIds(MainWindow window) =>
        (List<string>)typeof(MainWindow).GetField("_schemaQueueRuleIds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    private static void SetField(MainWindow window, string name, object value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

    private static void Invoke(MainWindow window, string name)
    {
        var method = typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, method.GetParameters().Length == 0 ? null : [false]);
    }

    private const string DiscoveryResponse = """
        {"plans":[{"id":"habitat","name":"Habitat","kind":"contentType","workingRuleIds":["habitat-definition","habitat-rule"],"rationale":"Both rules define reusable habitats."}]}
        """;

    private const string ProposalResponse = """
        {"action":"presentProposal","proposal":{"summary":"Defines habitats.","schema":{"id":"scoped-schema","name":"Scoped Schema","version":"0.1","entityTypes":[{"metadata":{"id":"habitat","name":"Habitat"},"properties":[]}]},"definitionSources":[{"definitionId":"habitat","workingRuleIds":["habitat-definition","habitat-rule"]}]}}
        """;

    private sealed class SequencedOllamaHandler(params string[] contents) : HttpMessageHandler
    {
        private int _next;
        public List<string> RequestBodies { get; } = [];
        public Action<int>? OnRequest { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            OnRequest?.Invoke(RequestBodies.Count);
            var content = contents[Math.Min(_next++, contents.Length - 1)];
            var response = new JsonObject
            {
                ["model"] = "test-model",
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToJsonString(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SchemaProposalCaptureHandler : HttpMessageHandler
    {
        public string? FailDefinitionId { get; init; }
        public List<ProjectSchemaProposal> SavedProposals { get; } = [];
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri}");
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/development-plans", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("http://qa.invalid/api/projects/scoped-schema-project/schema-proposal", request.RequestUri?.ToString());
            var proposal = (await request.Content!.ReadFromJsonAsync<ProjectSchemaProposal>(cancellationToken: cancellationToken))!;
            if (FailDefinitionId is not null && proposal.DefinitionSources.Any(mapping => mapping.DefinitionId == FailDefinitionId))
                throw new HttpRequestException("Simulated save failure.");
            SavedProposals.Add(proposal);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
