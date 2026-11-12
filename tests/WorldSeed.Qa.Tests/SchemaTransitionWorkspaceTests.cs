using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using WorldSeed.App;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.Qa.Tests;

public sealed class SchemaTransitionWorkspaceTests
{
    [AvaloniaFact]
    public async Task Compatible_update_requires_review_then_explicit_activation()
    {
        var workspace = CreateWorkspace(canActivate: true);
        using var api = new HttpClient(workspace.Handler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(api, workspace);
        try
        {
            await window.ActivateSchemaAsync();

            Assert.Equal(1, workspace.Handler.ReviewRequests);
            Assert.Equal(0, workspace.Handler.ActivationRequests);
            Assert.Contains("Existing canonical Game Data remains valid", window.FindControl<TextBlock>("SchemaStatusText")!.Text);
            Assert.Equal("Activate Compatible Update", window.FindControl<Button>("ActivateSchemaButton")!.Content);

            await window.ActivateSchemaAsync();

            Assert.Equal(1, workspace.Handler.ActivationRequests);
            Assert.Contains("Active schema revision 2 is ready", window.FindControl<TextBlock>("SchemaStatusText")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Data_work_review_blocks_activation_and_lists_issue()
    {
        var workspace = CreateWorkspace(canActivate: false);
        using var api = new HttpClient(workspace.Handler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = CreateWindow(api, workspace);
        try
        {
            await window.ActivateSchemaAsync();

            var status = window.FindControl<TextBlock>("SchemaStatusText")!.Text;
            Assert.Contains("RequiresDataWork", status);
            Assert.Contains("weapon-1", status);
            Assert.Contains("reach", status);
            Assert.Equal(0, workspace.Handler.ActivationRequests);
            Assert.Equal("Review Updated Proposal", window.FindControl<Button>("ActivateSchemaButton")!.Content);

            await window.ActivateSchemaAsync();
            Assert.Equal(0, workspace.Handler.ActivationRequests);
        }
        finally { window.Close(); }
    }

    private static MainWindow CreateWindow(HttpClient api, Workspace workspace)
    {
        var window = new MainWindow(new HttpClient(), api);
        SetField(window, "_project", workspace.Project);
        SetField(window, "_schemaProposal", workspace.Proposal);
        SetField(window, "_activeGameSchema", workspace.Active);
        SetField(window, "_projectGameData", workspace.Data);
        Invoke(window, "RefreshSchemaView");
        return window;
    }

    private static Workspace CreateWorkspace(bool canActivate)
    {
        var now = DateTimeOffset.Parse("2026-11-12T12:00:00Z");
        var project = DesignProjectService.Create("transition-project", "Transition Project");
        const string activeJson = """{"id":"schema","name":"Schema","version":"1","entityTypes":[]}""";
        const string proposalJson = """{"id":"schema","name":"Schema","version":"2","entityTypes":[]}""";
        var proposal = new ProjectSchemaProposal("proposal", project.Id, "0.1", "Update", proposalJson, [], "rules", 1, [], now, now.AddMinutes(1));
        var active = new ActiveGameSchema("active", project.Id, "0.1", "Schema", activeJson, "old-proposal", "rules", 1, 1, now, now);
        var data = new ProjectGameData("data", project.Id, "0.1", active.Id, 1,
            """{"id":"data","gameSchemaId":"schema","gameSchemaVersion":"1","entityInstances":[],"relationshipInstances":[]}""", now, now);
        var kind = canActivate ? SchemaTransitionKind.Compatible : SchemaTransitionKind.RequiresDataWork;
        var issues = canActivate ? Array.Empty<WorldSeed.SchemaValidation.SchemaValidationIssue>() :
            [new WorldSeed.SchemaValidation.SchemaValidationIssue("$.entityInstances[weapon-1].values.reach", "Missing required property 'reach'.")];
        var review = new SchemaTransitionReview(proposal.Id, proposal.UpdatedAt, 2, kind, canActivate,
            canActivate ? "Existing canonical Game Data remains valid without transforming any entries." : "Existing canonical Game Data does not conform.",
            ["Changed content type 'weapon'."], issues);
        var activated = active with { Revision = 2, SchemaJson = proposalJson, UpdatedAt = now.AddMinutes(2) };
        var rebound = data with { ActiveGameSchemaRevision = 2, DataJson = data.DataJson.Replace("\"1\"", "\"2\"", StringComparison.Ordinal), UpdatedAt = now.AddMinutes(2) };
        return new Workspace(project, proposal, active, data, new TransitionHandler(review, activated, rebound));
    }

    private static void SetField(MainWindow window, string name, object value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

    private static void Invoke(MainWindow window, string name) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [false]);

    private sealed record Workspace(DesignProject Project, ProjectSchemaProposal Proposal, ActiveGameSchema Active, ProjectGameData Data, TransitionHandler Handler);

    private sealed class TransitionHandler(SchemaTransitionReview review, ActiveGameSchema activated, ProjectGameData data) : HttpMessageHandler
    {
        public int ReviewRequests { get; private set; }
        public int ActivationRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/schema-transition", StringComparison.Ordinal))
            {
                ReviewRequests++;
                return Json(review);
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/active-schema", StringComparison.Ordinal))
            {
                ActivationRequests++;
                return Json(activated);
            }
            if (request.Method == HttpMethod.Get && path.EndsWith("/game-data", StringComparison.Ordinal)) return Json(data);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json<T>(T value) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        });
    }
}
