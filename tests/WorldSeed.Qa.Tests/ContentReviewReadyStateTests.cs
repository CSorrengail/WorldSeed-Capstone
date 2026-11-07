using System.Text.Json.Nodes;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WorldSeed.App;
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
                ["entityTypes"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["metadata"] = new JsonObject { ["id"] = "creature", ["name"] = "Creature" },
                        ["properties"] = new JsonArray()
                    }
                },
                ["valueTypes"] = new JsonArray()
            };
            var active = new ActiveGameSchema("active-ready", project.Id, "0.1", "Ready Schema", schemaJson.ToJsonString(), "proposal-ready", rules.Id, rules.Revision, 1, now, now);

            window.RestoreContentReviewWorkspace(project, rules, active, GameDataService.CreateEmpty(active, now));

            var generate = window.FindControl<Button>("GenerateContentProposalButton")!;
            var modelSelector = window.FindControl<ComboBox>("ModelSelector")!;
            var typeSelector = window.FindControl<ComboBox>("ContentProposalEntityTypeList")!;
            var request = window.FindControl<TextBox>("ContentProposalRequestText")!;
            Assert.True(generate.IsEnabled);

            await window.GenerateContentProposalAsync();
            Assert.Equal("Select a local model first.", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            modelSelector.ItemsSource = new[] { "local-model" };
            modelSelector.SelectedIndex = 0;
            await window.GenerateContentProposalAsync();
            Assert.Equal("Select a content type first.", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            typeSelector.SelectedIndex = 0;
            await window.GenerateContentProposalAsync();
            Assert.Equal("Describe the content you want to propose.", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);

            request.Text = "Create one creature.";

            Assert.True(generate.IsEnabled,
                $"model={modelSelector.SelectedItem ?? "<null>"}; typeIndex={typeSelector.SelectedIndex}; request='{request.Text}'; schema='{window.FindControl<TextBlock>("ContentReviewSchemaText")!.Text}'");

            await window.GenerateContentProposalAsync();

            Assert.Equal(0, window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex);
            Assert.Contains("coal-stalker", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            Assert.Contains("Saved proposal version 1", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);
        }
        finally { window.Close(); }
    }

    private sealed class ModelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonSerializer.Serialize(new
            {
                model = "local-model",
                message = new { content = "{\"candidate\":{\"id\":\"coal-stalker\",\"entityTypeId\":\"creature\",\"values\":{}}}" }
            });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class ProposalApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Contains("/content-proposals/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        }
    }
}
