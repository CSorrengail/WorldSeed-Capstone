using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using WorldSeed.App;
using WorldSeed.DesignSessions;

namespace WorldSeed.Qa.Tests;

public sealed class DesignIdeaSourceTests
{
    [AvaloniaFact]
    public void Dialog_requires_a_title_and_idea_then_preserves_exact_text()
    {
        var dialog = new DesignIdeaWindow();
        try
        {
            dialog.Show();
            Assert.False(dialog.TryAccept());
            Assert.Contains("Enter Both", dialog.FindControl<TextBlock>("ValidationText")!.Text);

            dialog.FindControl<TextBox>("TitleInput")!.Text = "  Habitat Moves  ";
            dialog.FindControl<TextBox>("IdeaInput")!.Text = "  Keep this spacing.\r\nAnd this line.  ";

            Assert.True(dialog.TryAccept());
            Assert.Equal("Habitat Moves", dialog.IdeaTitle);
            Assert.Equal("  Keep this spacing.\r\nAnd this line.  ", dialog.IdeaText);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public async Task Preserved_idea_is_saved_selected_and_ready_for_the_existing_source_flow()
    {
        var apiHandler = new ProjectCaptureHandler();
        using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("http://qa.invalid/") };
        using var model = new HttpClient(new OfflineHandler());
        var window = new MainWindow(model, api);
        try
        {
            var project = DesignProjectService.Create("idea-project", "Idea Project");
            typeof(MainWindow).GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, project);

            Assert.True(await window.PreserveDesignIdeaAsync("Habitat Moves", "Creatures may inherit a habitat move."));

            var saved = Assert.Single(apiHandler.SavedProjects);
            var source = Assert.Single(saved.SourceNotes);
            Assert.Equal("Creatures may inherit a habitat move.", source.OriginalText);
            Assert.Equal("Designer Idea: Habitat Moves", source.Origin?.DisplayName);
            Assert.Equal(0, window.FindControl<ListBox>("SourceNotesList")!.SelectedIndex);
            var list = window.FindControl<ListBox>("SourceNotesList")!;
            var row = Assert.IsType<Grid>(list.ItemTemplate!.Build(list.Items[0]));
            var status = Assert.IsType<TextBlock>(row.Children[0]);
            Assert.Equal("○", status.Text);
            Assert.Equal("Not started", ToolTip.GetTip(status));
            Assert.Equal("Designer Idea: Habitat Moves", Assert.IsType<TextBlock>(row.Children[1]).Text);
            Assert.Contains("Creatures may inherit", window.FindControl<TextBlock>("SourceText")!.Text);
            Assert.Contains("Start the selected source", window.FindControl<TextBlock>("StatusText")!.Text);
        }
        finally { window.Close(); }
    }

    private sealed class ProjectCaptureHandler : HttpMessageHandler
    {
        public List<DesignProject> SavedProjects { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("http://qa.invalid/api/projects/idea-project", request.RequestUri?.ToString());
            SavedProjects.Add((await request.Content!.ReadFromJsonAsync<DesignProject>(cancellationToken: cancellationToken))!);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Design-idea UI testing must not contact the model: " + request.RequestUri);
    }
}
