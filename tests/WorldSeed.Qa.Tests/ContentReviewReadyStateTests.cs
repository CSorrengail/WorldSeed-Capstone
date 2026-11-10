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

            Assert.Contains("Saved requested revision as version 2", window.FindControl<TextBlock>("ContentProposalStatusText")!.Text);
            Assert.Contains("skirmisher", window.FindControl<TextBox>("ContentProposalEditor")!.Text);
            Assert.Contains("fits the active schema", window.FindControl<TextBlock>("ContentSchemaGapText")!.Text, StringComparison.OrdinalIgnoreCase);
            Assert.False(window.FindControl<Button>("OpenSchemaForContentGapButton")!.IsVisible);
            window.FindControl<ListBox>("ContentProposalList")!.SelectedIndex = 1;
            Assert.Contains("Use a valid creature role.", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.DoesNotContain("Replacement: None", window.FindControl<TextBox>("ContentProposalDecisionHistoryText")!.Text);
            Assert.Equal("Generate Proposal", generate.Content);
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

    private sealed class ProposalApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Contains("/content-proposals/", request.RequestUri!.AbsolutePath);
            Assert.True(request.Method == HttpMethod.Put || request.Method == HttpMethod.Post);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        }
    }
}
