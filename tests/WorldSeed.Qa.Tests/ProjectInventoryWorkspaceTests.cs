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
            Assert.Contains("Unresolved Question", observation.ToString());
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

    private sealed class InventoryHandler(ProjectInventory inventory) : HttpMessageHandler
    {
        public int CreateRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/inventories", StringComparison.Ordinal))
            {
                CreateRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(inventory) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
