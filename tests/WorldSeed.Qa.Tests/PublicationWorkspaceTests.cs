using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WorldSeed.App;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.Publication;
using WorldSeed.WorkingRules;

namespace WorldSeed.Qa.Tests;

public sealed class PublicationWorkspaceTests
{
    [AvaloniaFact]
    public async Task Creates_selects_and_previews_immutable_publication_snapshot()
    {
        var now = DateTimeOffset.Parse("2026-11-10T14:00:00Z");
        var project = DesignProjectService.Create("publication-project", "Publication Project");
        var rule = new WorkingRule("rule-1", "Creature Rule", WorkingRuleKind.Rule, "Creatures have names.", [], [], [], [], null, [], []);
        var rules = new WorkingRuleSet("publication-rules", project.Id, "0.1", "Rules", null, 1, now, now, [rule], [], [], [], []);
        var schemaJson = """{"id":"publication-schema","name":"Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creatures"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}],"valueTypes":[]}""";
        var schema = new ActiveGameSchema("active-publication", project.Id, "0.1", "Schema", schemaJson, "proposal", rules.Id, rules.Revision, 1, now, now);
        var root = JsonNode.Parse(GameDataService.CreateEmpty(schema, now).DataJson)!.AsObject();
        root["entityInstances"]!.AsArray().Add(JsonNode.Parse("""{"id":"glass-fox","entityTypeId":"creature","values":{"name":"Glass Fox"}}""")!.AsObject());
        var data = GameDataService.CreateEmpty(schema, now) with { DataJson = root.ToJsonString() };
        var snapshot = PublicationSnapshotService.Create(project.Name, rules, schema, data, now);
        using var model = new HttpClient(new RejectingHandler());
        using var api = new HttpClient(new PublicationApiHandler(snapshot)) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api);
        try
        {
            window.RestoreContentReviewWorkspace(project, rules, schema, data);
            window.RestorePublicationWorkspace(project, []);

            Assert.True(await window.CreatePublicationSnapshotAsync());

            Assert.Single(window.FindControl<ListBox>("PublicationSnapshotList")!.Items.Cast<object>());
            Assert.Contains("# Publication Project", window.FindControl<TextBox>("PublicationMarkdownText")!.Text);
            Assert.Contains("Glass Fox", window.FindControl<TextBox>("PublicationMarkdownText")!.Text);
            Assert.Contains(snapshot.GameDataFingerprint, window.FindControl<TextBox>("PublicationManifestText")!.Text);
            Assert.True(window.FindControl<Button>("ExportPublicationMarkdownButton")!.IsEnabled);
            Assert.True(window.FindControl<Button>("ExportPublicationManifestButton")!.IsEnabled);
            Assert.Contains("Review It Before Exporting", window.FindControl<TextBlock>("PublicationStatusText")!.Text);
        }
        finally { window.Close(); }
    }

    private sealed class PublicationApiHandler(PublicationSnapshot snapshot) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/publications", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(snapshot, options: Options) });
        }
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new InvalidOperationException("The model must not be called while creating a deterministic publication snapshot.");
    }
}
