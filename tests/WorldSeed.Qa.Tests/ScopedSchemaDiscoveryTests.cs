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
            Assert.Single(apiHandler.Requests);
            Assert.Contains("Still Editable And Non-Canonical", window.FindControl<TextBlock>("SchemaStatusText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Queued_structure_clarification_resumes_the_same_plan_and_retains_the_answer()
    {
        var modelHandler = new SequencedOllamaHandler(
            DiscoveryResponse,
            """{"action":"askClarifyingQuestion","clarifyingQuestion":"Should habitats be reusable entries?"}""",
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
            Assert.Equal("Should habitats be reusable entries?", Assert.Single(saved.Clarifications!).Question);
            Assert.Contains("reusable entry", Assert.Single(saved.Clarifications!).Answer);
            Assert.Contains("Yes, each habitat", modelHandler.RequestBodies[2]);
            Assert.Empty(QueueIds(window));
            Assert.False(window.FindControl<Border>("SchemaQuestionPanel")!.IsVisible);
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
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
        public List<ProjectSchemaProposal> SavedProposals { get; } = [];
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri}");
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("http://qa.invalid/api/projects/scoped-schema-project/schema-proposal", request.RequestUri?.ToString());
            SavedProposals.Add((await request.Content!.ReadFromJsonAsync<ProjectSchemaProposal>(cancellationToken: cancellationToken))!);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
