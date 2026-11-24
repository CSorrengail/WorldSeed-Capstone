using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using WorldSeed.App;
using WorldSeed.DesignSessions;
using WorldSeed.RuleDrafting;
using WorldSeed.LlmIntegration;

namespace WorldSeed.Qa.Tests;

public sealed class DesignIdeaSourceTests
{
    [AvaloniaFact]
    public void Rule_actions_and_workspace_help_use_plain_language()
    {
        using var api = new HttpClient(new OfflineHandler());
        using var model = new HttpClient(new OfflineHandler());
        var window = new MainWindow(model, api);
        try
        {
            Assert.Equal("Add New Source Rules", window.FindControl<Button>("AddMissingWorkingRulesButton")!.Content);
            Assert.Equal("Rebuild Project Rules", window.FindControl<Button>("BuildWorkingRulesButton")!.Content);
            var icons = window.GetLogicalDescendants().OfType<HelpIcon>().ToArray();
            Assert.True(icons.Length >= 6);
            Assert.Contains(icons, icon => icon.HelpText.Contains("A proposal is a saved candidate"));
            Assert.Contains(icons, icon => icon.HelpText.Contains("project-wide rules"));
            Assert.All(icons, icon => Assert.Equal(icon.HelpText, Assert.IsType<TextBlock>(ToolTip.GetTip(icon)).Text));
        }
        finally { window.Close(); }
    }

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

    [AvaloniaFact]
    public async Task Reloaded_source_review_has_inputs_above_source_and_persists_non_game_resolution()
    {
        var handler = new ProjectCaptureHandler();
        using var api = new HttpClient(handler) { BaseAddress = new Uri("http://qa.invalid/") };
        using var model = new HttpClient(new OfflineHandler());
        var window = new MainWindow(model, api);
        try
        {
            var source = new DesignSourceNote("source", "A rule. License notice.", DateTimeOffset.UtcNow);
            var review = "[source:p002] Extraction Needs Review: License notice.";
            var draft = new StructuredRuleDraft("Rules", "Organize rules.", [], [], [], [review], []);
            var now = DateTimeOffset.UtcNow;
            var session = new DesignSession("session", "idea-project", "local", now, now, [source], [new(LlmMessageRole.User, source.OriginalText)], new(RuleDraftAction.PresentDraft, null, [], draft));
            var project = DesignProjectService.AddSources(DesignProjectService.Create("idea-project", "Project"), [source]);
            project = DesignProjectService.SaveSession(project, session);
            project = System.Text.Json.JsonSerializer.Deserialize<DesignProject>(System.Text.Json.JsonSerializer.Serialize(DesignProjectService.EndIncompleteSessions(project)))!;
            typeof(MainWindow).GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, project);
            typeof(MainWindow).GetMethod("RefreshNotes", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);

            var panel = window.FindControl<StackPanel>("QuestionsPanel")!;
            Assert.Single(panel.Children.OfType<TextBox>());
            Assert.Contains(panel.Children.OfType<Button>(), button => Equals(button.Content, "Mark As Non-Game Material"));
            var submit = window.FindControl<Button>("SubmitAnswersButton")!;
            Assert.True(submit.IsVisible);
            Assert.Equal("Save Answers And Retry Source", submit.Content);
            Assert.Equal("Retry Selected Source", window.FindControl<Button>("DraftButton")!.Content);
            var parent = Assert.IsType<StackPanel>(panel.Parent);
            Assert.True(parent.Children.IndexOf(panel) < parent.Children.IndexOf(window.FindControl<TextBlock>("SourceText")!));
            var selector = window.FindControl<ComboBox>("ModelSelector")!;
            selector.ItemsSource = new[] { "fake" }; selector.SelectedIndex = 0;
            typeof(MainWindow).GetMethod("SubmitAnswers_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [null, new Avalonia.Interactivity.RoutedEventArgs()]);
            Assert.Contains("answer every question", window.FindControl<TextBlock>("StatusText")!.Text);
            Assert.Empty(handler.SavedProjects);

            await window.MarkSourceReviewAsNonGameMaterialAsync(session.Id, review);
            var saved = Assert.Single(handler.SavedProjects);
            Assert.Empty(saved.Sessions[0].LatestTurn!.Draft!.OpenQuestions);
            Assert.Single(saved.Sessions[0].SourceReviewDecisions!);
            Assert.Equal(source.OriginalText, saved.SourceNotes[0].OriginalText);
            Assert.False(submit.IsVisible);
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
