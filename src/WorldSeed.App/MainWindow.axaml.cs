using Avalonia.Controls;
using Avalonia.Platform.Storage;
using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.SourceIngestion;

namespace WorldSeed.App;

public partial class MainWindow : Window
{
    private readonly HttpClient _http = new();
    private readonly List<DesignProjectSummary> _projectSummaries = [];
    private readonly List<ProjectRuleItem> _projectRules = [];
    private string _projectDirectory = DefaultProjectDirectory;
    private DesignProject? _project;
    private readonly List<TextBox> _answerInputs = [];
    private IReadOnlyList<string> _activeQuestions = [];

    public MainWindow() => InitializeComponent();
    protected override void OnOpened(EventArgs e) { base.OnOpened(e); _ = InitializeAsync(); }

    private async Task InitializeAsync()
    {
        await RefreshModelsAsync();
        await RefreshProjectsAsync();
    }

    private async Task RefreshProjectsAsync()
    {
        _projectSummaries.Clear();
        _projectSummaries.AddRange(await ProjectStore().ListAsync());
        ProjectSelector.ItemsSource = _projectSummaries.Select(project => new ProjectListItem(project)).ToArray();
        if (_project is null && _projectSummaries.Count > 0) _project = await ProjectStore().LoadAsync(_projectSummaries[0].Id);
        if (_project is not null) ProjectSelector.SelectedIndex = _projectSummaries.FindIndex(project => project.Id == _project.Id);
        ProjectLocationText.Text = $"Project folder: {_projectDirectory}";
    }

    private async void CreateProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new NewProjectWindow();
        if (await dialog.ShowDialog<bool>(this) is not true || string.IsNullOrWhiteSpace(dialog.ProjectName)) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose where to save the WorldSeed project", AllowMultiple = false });
        var selectedDirectory = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedDirectory)) { StatusText.Text = "Project creation canceled: no save location was chosen."; return; }
        _projectDirectory = selectedDirectory;
        _project = DesignProjectService.Create($"project-{Guid.NewGuid():N}", dialog.ProjectName, dialog.Description);
        await ProjectStore().SaveAsync(_project);
        await RefreshProjectsAsync();
        RefreshNotes(); ShowSelectedSource();
        ProjectLocationText.Text = $"Saved in: {_projectDirectory}";
        StatusText.Text = $"Project '{_project.Name}' created locally.";
    }

    private async void ProjectSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var index = ProjectSelector.SelectedIndex;
        if (index < 0 || index >= _projectSummaries.Count) return;
        _project = await ProjectStore().LoadAsync(_projectSummaries[index].Id);
        RefreshNotes(); ShowSelectedSource();
        if (_project is not null) { ProjectLocationText.Text = $"Saved in: {_projectDirectory}"; StatusText.Text = $"Loaded project '{_project.Name}'."; }
    }

    private async void OpenProjectFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder containing WorldSeed projects", AllowMultiple = false });
        var selectedDirectory = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedDirectory)) return;
        _projectDirectory = selectedDirectory;
        _project = null;
        await RefreshProjectsAsync();
        RefreshNotes(); ShowSelectedSource();
        StatusText.Text = _project is null ? "No WorldSeed projects were found in that folder." : $"Loaded project '{_project.Name}'.";
    }

    private async Task RefreshModelsAsync()
    {
        try
        {
            var models = await new OllamaModelCatalog(_http).GetInstalledModelsAsync();
            ModelSelector.ItemsSource = models.Select(model => model.Name).ToArray();
            ModelSelector.SelectedItem = models.Any(model => model.Name == "gemma3:12b") ? "gemma3:12b" : models.FirstOrDefault()?.Name;
            StatusText.Text = models.Count == 0 ? "No local models found." : "Local models refreshed.";
        }
        catch { StatusText.Text = "Could not reach Ollama. Start it, then refresh."; }
    }

    private async void CheckLocalModels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await RefreshModelsAsync();

    private void RefreshNotes()
    {
        var selectedId = SelectedNote?.Id;
        var notes = _project?.SourceNotes ?? [];
        SourceNotesList.ItemsSource = notes.Select(note => new SourceListItem(note, StatusFor(note))).ToArray();
        var selectedIndex = notes.ToList().FindIndex(note => note.Id == selectedId);
        SourceNotesList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (notes.Count > 0 ? 0 : -1);
    }

    private async void AddNotes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, FileTypeFilter = [new FilePickerFileType("Plain text") { Patterns = ["*.txt"] }] });
        var requests = files.Select(file => new PlainTextSourceRequest($"source-{Guid.NewGuid():N}", file.TryGetLocalPath() ?? "")).Where(request => request.FilePath.Length > 0).ToArray();
        if (requests.Length == 0) return;
        if (_project is null) { StatusText.Text = "Create or select a project before adding source files."; return; }
        try { _project = DesignProjectService.AddSources(_project, await new PlainTextSourceImporter().ImportAsync(requests)); await ProjectStore().SaveAsync(_project); RefreshNotes(); StatusText.Text = $"{_project.SourceNotes.Count} original note(s) preserved in '{_project.Name}'."; }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void RemoveSelectedNote_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote; if (note is null) return;
        if (_project is null) return;
        _project = DesignProjectService.RemoveSource(_project, note.Id); _ = ProjectStore().SaveAsync(_project); RefreshNotes(); ShowSelectedSource();
        StatusText.Text = $"{_project.SourceNotes.Count} original note(s) preserved.";
    }

    private void SourceNotesList_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ShowSelectedSource();
    private void RuleList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RuleList.SelectedIndex is var index && index >= 0 && index < _projectRules.Count) DraftText.Text = FormatRule(_projectRules[index].Rule);
    }
    private DesignSourceNote? SelectedNote => _project is not null && SourceNotesList.SelectedIndex is var index && index >= 0 && index < _project.SourceNotes.Count ? _project.SourceNotes[index] : null;

    private async void CreateSession_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote;
        if (_project is null || note is null || ModelSelector.SelectedItem is not string model) { StatusText.Text = "Select a project, one source file, and a local model."; return; }
        if (SessionFor(note) is not null) { StatusText.Text = "This source already has a session. Answer its questions or review its draft."; return; }
        var service = CreateService(model, out var capture);
        await AdvanceAndSaveAsync(note, service.Start($"session-{Guid.NewGuid():N}", _project.Id, "local", note), service, capture, model);
    }

    private async void SubmitAnswers_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote;
        var session = note is null ? null : SessionFor(note);
        if (_project is null || note is null || session is null || ModelSelector.SelectedItem is not string model) return;
        var answers = _answerInputs.Select(input => input.Text?.Trim() ?? "").ToArray();
        if (answers.Any(string.IsNullOrWhiteSpace)) { StatusText.Text = "Please answer every question before continuing."; return; }
        var text = string.Join("\n\n", _activeQuestions.Zip(answers, (question, answer) => $"Question: {question}\nAnswer: {answer}"));
        var service = CreateService(model, out var capture);
        session = service.AddDesignerMessage(session, text, new DesignSourceNote($"answer-{Guid.NewGuid():N}", text, DateTimeOffset.UtcNow, new SourceNoteOrigin("Designer answers")));
        await AdvanceAndSaveAsync(note, session, service, capture, model);
    }

    private DesignSessionService CreateService(string model, out CapturingClient capture)
    {
        capture = new CapturingClient(new OllamaChatClient(_http, OllamaDefaults.CreateProfile("local", "Local", model)));
        return new DesignSessionService(new RuleDraftingService(capture));
    }

    private async Task AdvanceAndSaveAsync(DesignSourceNote note, DesignSession session, DesignSessionService service, CapturingClient capture, string model)
    {
        DraftButton.IsEnabled = false; SubmitAnswersButton.IsEnabled = false;
        StatusText.Text = $"Working on {note.Origin?.DisplayName ?? note.Id} with {model}. This can take a few seconds…";
        try
        {
            var result = await service.AdvanceAsync(session);
            if (_project is null) throw new InvalidOperationException("No project is selected.");
            _project = DesignProjectService.SaveSession(_project, result); await ProjectStore().SaveAsync(_project); await RefreshProjectsAsync(); RefreshNotes(); ShowSelectedSource();
            StatusText.Text = result.LatestTurn!.Action == RuleDraftAction.PresentDraft ? "Validated draft saved in this project. Review each rule and its exact source excerpt." : "Clarification needed; the project was saved locally. Answer the questions in the Conversation panel.";
        }
        catch (RuleDraftFormatException ex) { StatusText.Text = "The model returned text WorldSeed could not safely use. Nothing was saved."; DraftText.Text = $"Format detail: {ex.Message}\n\nRaw model output (for diagnosis):\n{Preview(capture.Last?.Content)}"; }
        catch (Exception ex) { StatusText.Text = "Drafting failed before a usable result was produced."; DraftText.Text = $"{ex.GetType().Name}: {ex.Message}"; }
        finally { DraftButton.IsEnabled = true; SubmitAnswersButton.IsEnabled = true; }
    }

    private void ShowSelectedSource()
    {
        QuestionsPanel.Children.Clear(); _answerInputs.Clear(); _activeQuestions = []; SubmitAnswersButton.IsVisible = false;
        RefreshRules();
        var note = SelectedNote;
        if (note is null) { SourceText.Text = "Select a source file to inspect its original note."; QuestionHeading.Text = ""; DraftText.Text = "A validated draft will appear here with its source excerpts."; return; }
        SourceText.Text = $"Original note — {note.Origin?.DisplayName ?? note.Id}\n\n{note.OriginalText}";
        var session = SessionFor(note);
        if (session?.LatestTurn is null) { QuestionHeading.Text = "No session yet"; DraftText.Text = "Start this source when ready. It will remain separate from other imported files."; return; }
        var turn = session.LatestTurn;
        if (turn.Action == RuleDraftAction.AskClarifyingQuestion)
        {
            _activeQuestions = turn.ClarifyingQuestions.Count > 0 ? turn.ClarifyingQuestions : turn.ClarifyingQuestion is null ? [] : [turn.ClarifyingQuestion];
            SourceText.Text = "WorldSeed needs the answers below before it can create a traceable rule draft. Your original note remains preserved in this project.";
            QuestionHeading.Text = "Questions to answer";
            foreach (var question in _activeQuestions)
            {
                QuestionsPanel.Children.Add(new TextBlock { Text = question, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
                var input = new TextBox { PlaceholderText = "Your answer", AcceptsReturn = true, MinHeight = 58 }; _answerInputs.Add(input); QuestionsPanel.Children.Add(input);
            }
            SubmitAnswersButton.IsVisible = _answerInputs.Count > 0;
            DraftText.Text = "No processed rules yet. The model needs the answers shown in the Conversation panel before it can create a traceable draft.";
            return;
        }
        QuestionHeading.Text = "Draft ready";
        var firstRule = _projectRules.FindIndex(item => item.SessionId == session.Id);
        if (firstRule >= 0) RuleList.SelectedIndex = firstRule;
    }

    private void RefreshRules()
    {
        var selectedRule = RuleList.SelectedIndex is var index && index >= 0 && index < _projectRules.Count ? _projectRules[index] : null;
        _projectRules.Clear();
        if (_project is not null)
        {
            _projectRules.AddRange(_project.Sessions
                .Where(session => session.LatestTurn?.Action == RuleDraftAction.PresentDraft && session.LatestTurn.Draft is not null)
                .SelectMany(session => session.LatestTurn!.Draft!.Rules.Select(rule => new ProjectRuleItem(session.Id, session.SourceNotes.FirstOrDefault()?.Origin?.DisplayName ?? "Source", rule))));
        }
        RuleList.ItemsSource = _projectRules;
        RuleList.SelectedIndex = selectedRule is null ? -1 : _projectRules.FindIndex(item => item.SessionId == selectedRule.SessionId && item.Rule.Id == selectedRule.Rule.Id);
    }

    private DesignSession? SessionFor(DesignSourceNote note) => _project?.Sessions.SingleOrDefault(session => session.SourceNotes.Any(source => source.Id == note.Id));
    private string StatusFor(DesignSourceNote note) => SessionFor(note) is not { LatestTurn: { } turn } ? "Not started" : turn.Action == RuleDraftAction.PresentDraft ? "Draft ready" : "Needs answers";
    private static string FormatDraft(StructuredRuleDraft draft) => $"{draft.Title}\n{draft.Intent}\n\n" + string.Join("\n\n", draft.Rules.Select(rule => $"{rule.Name} ({rule.Kind})\n{rule.Text}\n\nSource evidence:\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}"));
    private static string FormatRule(RuleStatement rule) => $"{rule.Name}\nType: {rule.Kind}\n\nProposed rule\n{rule.Text}\n\nSource evidence\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}";
    private JsonDesignProjectStore ProjectStore() => new(_projectDirectory);
    private static string DefaultProjectDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed", "projects");
    private static string Preview(string? text) => string.IsNullOrWhiteSpace(text) ? "No model response was received." : text.Length > 3000 ? text[..3000] + "\n[truncated]" : text;
    protected override void OnClosed(EventArgs e) { _http.Dispose(); base.OnClosed(e); }

    private sealed record SourceListItem(DesignSourceNote Note, string Status) { public override string ToString() => $"{Note.Origin?.DisplayName ?? Note.Id} — {Status}"; }
    private sealed record ProjectRuleItem(string SessionId, string SourceName, RuleStatement Rule)
    {
        public override string ToString() => $"{Rule.Name} — {SourceName}";
    }
    private sealed record ProjectListItem(DesignProjectSummary Project)
    {
        public override string ToString() => $"{Project.Name} — updated {Project.UpdatedAt.LocalDateTime:g}";
    }
    private sealed class CapturingClient(ILanguageModelClient inner) : ILanguageModelClient { public LlmChatResponse? Last { get; private set; } public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) => Last = await inner.CompleteAsync(request, cancellationToken); }
}
