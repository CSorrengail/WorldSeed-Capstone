using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WorldSeed.App;
using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.WorkingRules;

namespace WorldSeed.Qa.Tests;

public sealed class ContentReviewWorkspaceTests
{
    [AvaloniaFact]
    public void Content_review_workspace_is_present_and_refuses_generation_without_project_context()
    {
        using var model = new HttpClient(new OfflineHandler());
        using var api = new HttpClient(new OfflineHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new TestMainWindow(model, api);
        try
        {
            Assert.NotNull(window.FindControl<ComboBox>("ContentProposalEntityTypeList"));
            Assert.NotNull(window.FindControl<TextBox>("ContentProposalRequestText"));
            Assert.NotNull(window.FindControl<TextBox>("ContentProposalEditor"));
            Assert.Contains("Select A Proposal", window.FindControl<TextBox>("ContentMechanicalValidationText")!.Text!);
            Assert.Contains("Select A Proposal", window.FindControl<TextBox>("ContentBalanceEvaluationText")!.Text!);
            Assert.NotNull(window.FindControl<TextBox>("ContentBalanceProfileEditor"));
            Assert.NotNull(window.FindControl<ComboBox>("ContentExpansionCountList"));
            Assert.NotNull(window.FindControl<ListBox>("ContentExpansionQueueList"));
            Assert.False(window.FindControl<Button>("StartContentExpansionButton")!.IsEnabled);
            Assert.NotNull(window.FindControl<ComboBox>("ContentExpansionStageTypeList"));
            Assert.NotNull(window.FindControl<ListBox>("PlannedContentExpansionStageList"));
            Assert.NotNull(window.FindControl<ListBox>("SavedContentExpansionRunList"));
            Assert.NotNull(window.FindControl<TextBox>("ContentExpansionRunDetailText"));
            Assert.False(window.FindControl<Button>("GenerateNextExpansionStageButton")!.IsEnabled);
            Assert.NotNull(window.FindControl<ComboBox>("EvaluationEntityTypeList"));
            Assert.NotNull(window.FindControl<ComboBox>("EvaluationKindList"));
            Assert.NotNull(window.FindControl<ListBox>("EvaluationProposalList"));
            Assert.False(window.FindControl<Button>("GenerateEvaluationProposalButton")!.IsEnabled);
            Assert.False(window.FindControl<Button>("UseApprovedEvaluationProposalButton")!.IsEnabled);
            Assert.NotNull(window.FindControl<ListBox>("ContentProposalList"));
            Assert.False(window.FindControl<Button>("GenerateContentProposalButton")!.IsEnabled);
            Assert.False(window.FindControl<Button>("SaveContentBalanceProfileButton")!.IsEnabled);
            Assert.False(window.FindControl<Button>("ApproveContentProposalButton")!.IsEnabled);
            Assert.Contains("Activate a schema", window.FindControl<TextBlock>("ContentReviewSchemaText")!.Text!, StringComparison.OrdinalIgnoreCase);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Review_validates_reference_targets_in_canonical_data_and_explains_prose_limitations()
    {
        using var model = new HttpClient(new OfflineHandler());
        using var api = new HttpClient(new OfflineHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new TestMainWindow(model, api);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var rule = new WorkingRule("rule", "Themes", WorkingRuleKind.Rule, "Scenes use an existing theme.", [], [], [], [], null, [], []);
            var rules = new WorkingRuleSet("rules", "project", "0.1", "Story", null, 1, now, now, [rule], [], [], [], []);
            const string schemaJson = """{"id":"story","name":"Story","version":"1","entityTypes":[{"metadata":{"id":"theme","name":"Theme"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]},{"metadata":{"id":"scene","name":"Scene"},"properties":[{"metadata":{"id":"theme","name":"Theme"},"valueType":{"kind":"reference","allowedEntityTypeIds":["theme"]},"required":true,"nullable":false}]}]}""";
            var schema = new ActiveGameSchema("active", "project", "0.1", "Story", schemaJson, "schema-proposal", "rules", 1, 1, now, now);
            var data = GameDataService.CreateEmpty(schema, now);
            var root = JsonNode.Parse(data.DataJson)!.AsObject();
            root["entityInstances"]!.AsArray().Add(JsonNode.Parse("""{"id":"theme-private","entityTypeId":"theme","values":{"name":"Hope"}}"""));
            data = data with { DataJson = root.ToJsonString() };
            var proposal = ContentProposalLifecycleService.Create("project", "scene", JsonNode.Parse("""{"id":"scene-private","entityTypeId":"scene","values":{"theme":"theme-private"}}""")!.AsObject(), schema.Id, 1, "rules", 1, now);
            window.RestoreContentReviewWorkspace(DesignProjectService.Create("project", "Story"), rules, schema, data, proposals: [proposal]);
            window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex = 0;
            var result = window.FindControl<TextBox>("ContentMechanicalValidationText")!.Text;
            Assert.Contains("Passed", result);
            Assert.Contains("Prose-Only", result);
            Assert.DoesNotContain("scene-private", data.DataJson);
        }
        finally { window.Close(); }
    }

    private sealed class TestMainWindow(HttpClient model, HttpClient api) : MainWindow(model, api) { }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The content-review shell test must not contact a service: " + request.RequestUri);
    }
}
