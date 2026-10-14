using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Text.Json;
using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.SourceIngestion;
using WorldSeed.WorkingRules;

namespace WorldSeed.App;

public partial class MainWindow : Window
{
    private readonly HttpClient _http = new();
    private readonly HttpClient _apiHttp = new() { BaseAddress = new Uri("http://localhost:5080/") };
    private readonly IDesignProjectStore _projectStore;
    private readonly IWorkingRuleSetStore _workingRuleStore;
    private readonly List<DesignProjectSummary> _projectSummaries = [];
    private readonly List<ProjectRuleItem> _projectRules = [];
    private readonly List<WorkingRule> _workingRules = [];
    private int _projectLoadGeneration;
    private bool _updatingProjectSelector;
    private string? _pendingDeletionProjectId;
    private DesignProject? _project;
    private WorkingRuleSet? _workingRuleSet;
    private readonly List<TextBox> _answerInputs = [];
    private IReadOnlyList<string> _activeQuestions = [];

    public MainWindow()
    {
        _projectStore = new HttpDesignProjectStore(_apiHttp);
        _workingRuleStore = new HttpWorkingRuleSetStore(_apiHttp);
        InitializeComponent();
    }
    protected override void OnOpened(EventArgs e) { base.OnOpened(e); AppLog.Info("Application opened."); _ = InitializeAsync(); }

    private async Task InitializeAsync()
    {
        try
        {
            await RefreshModelsAsync();
            await ImportLegacyProjectsAsync();
            await RefreshProjectsAsync();
            await ChooseStartupProjectAsync();
        }
        catch (Exception ex) { AppLog.Error("Initialize application", ex); StatusText.Text = "WorldSeed could not load its local projects. See the diagnostic log."; }
    }

    /// <summary>Copies old file-backed projects into the local database once, without deleting their JSON originals.</summary>
    private async Task ImportLegacyProjectsAsync()
    {
        var imported = 0;
        foreach (var directory in LegacyProjectDirectories().Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
        {
            var legacyStore = new JsonDesignProjectStore(directory);
            foreach (var summary in await legacyStore.ListAsync())
            {
                if (await ProjectStore().LoadAsync(summary.Id) is not null) continue;
                var project = await legacyStore.LoadAsync(summary.Id);
                if (project is null) continue;
                await ProjectStore().SaveAsync(DesignProjectService.EndIncompleteSessions(project));
                imported++;
            }
        }
        if (imported > 0) AppLog.Info($"Imported {imported} legacy file-backed project(s) into the local database.");
    }

    private static IEnumerable<string> LegacyProjectDirectories()
    {
        var worldSeedDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed");
        yield return Path.Combine(worldSeedDirectory, "projects");
        var preferencePath = Path.Combine(worldSeedDirectory, "preferences.json");
        if (!File.Exists(preferencePath)) yield break;
        string? priorDirectory = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(preferencePath));
            if (document.RootElement.TryGetProperty("LastProjectDirectory", out var directory)) priorDirectory = directory.GetString();
        }
        catch (JsonException) { /* A legacy preference failure must not block startup. */ }
        if (!string.IsNullOrWhiteSpace(priorDirectory)) yield return priorDirectory;
    }

    private async Task RefreshProjectsAsync()
    {
        _updatingProjectSelector = true;
        try
        {
            _projectSummaries.Clear();
            _projectSummaries.AddRange(await ProjectStore().ListAsync());
            ProjectSelector.ItemsSource = _projectSummaries.Select(project => new ProjectListItem(project)).ToArray();
            if (_project is not null) ProjectSelector.SelectedIndex = _projectSummaries.FindIndex(project => project.Id == _project.Id);
        }
        finally { _updatingProjectSelector = false; }
        ProjectLocationText.Text = "Project database: http://localhost:5080 (Docker local stack)";
    }

    private async Task ChooseStartupProjectAsync()
    {
        var chooser = new StartupProjectWindow(_projectSummaries);
        if (await chooser.ShowDialog<bool>(this) is not true) { StatusText.Text = "Choose or create a project to begin."; return; }
        if (chooser.CreateNew) { await CreateProjectAsync(); return; }
        if (chooser.SelectedProjectId is not null) await LoadProjectAsync(chooser.SelectedProjectId);
    }

    private async void CreateProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await CreateProjectAsync();

    private async Task CreateProjectAsync()
    {
        try
        {
            var dialog = new NewProjectWindow();
            if (await dialog.ShowDialog<bool>(this) is not true || string.IsNullOrWhiteSpace(dialog.ProjectName)) return;
            _project = DesignProjectService.Create($"project-{Guid.NewGuid():N}", dialog.ProjectName, dialog.Description);
            _workingRuleSet = null;
            _projectLoadGeneration++;
            ClearProjectView();
            await ProjectStore().SaveAsync(_project);
            await RefreshProjectsAsync();
            RefreshNotes(); ShowSelectedSource();
            ProjectLocationText.Text = "Project database: http://localhost:5080 (Docker local stack)";
            StatusText.Text = $"Project '{_project.Name}' created in the local database.";
            AppLog.Info($"Created project '{_project.Id}' in the local database.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Create project", ex);
            StatusText.Text = "Could not create the project. The application remains open.";
            DraftText.Text = $"Project creation detail: {ex.GetType().Name}: {ex.Message}\n\nDiagnostic log: {AppLog.CurrentPath}";
        }
    }

    private async void ProjectSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingProjectSelector) return;
        var index = ProjectSelector.SelectedIndex;
        if (index < 0 || index >= _projectSummaries.Count) return;
        await LoadProjectAsync(_projectSummaries[index].Id);
    }

    private async Task LoadProjectAsync(string projectId)
    {
        ResetDeleteConfirmation();
        var generation = ++_projectLoadGeneration;
        try
        {
            var loaded = await ProjectStore().LoadAsync(projectId);
            if (generation != _projectLoadGeneration) return;
            _project = loaded;
            ClearProjectView();
            if (_project is not null)
            {
                await LoadWorkingRuleSetAsync(_project.Id);
                RefreshNotes(); ShowSelectedSource();
                ProjectLocationText.Text = "Project database: http://localhost:5080 (Docker local stack)";
                _updatingProjectSelector = true;
                ProjectSelector.SelectedIndex = _projectSummaries.FindIndex(project => project.Id == _project.Id);
                _updatingProjectSelector = false;
                StatusText.Text = $"Loaded project '{_project.Name}' from the local database.";
                AppLog.Info($"Loaded project '{_project.Id}'.");
            }
        }
        catch (Exception ex) { AppLog.Error("Load selected project", ex); StatusText.Text = "Could not load that project. See the diagnostic log."; }
    }

    private async void CheckServer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            using var response = await _apiHttp.GetAsync("health");
            response.EnsureSuccessStatusCode();
            StatusText.Text = "Local database service is available.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Check local database service", ex);
            StatusText.Text = "The local database service is unavailable. Start it with deploy\\worldseed-local.bat.";
        }
    }

    private async void DeleteProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_project is null) { StatusText.Text = "Select a project to delete."; return; }
        if (_pendingDeletionProjectId != _project.Id)
        {
            _pendingDeletionProjectId = _project.Id;
            DeleteProjectButton.Content = "Confirm delete";
            StatusText.Text = $"Click Confirm delete to permanently remove '{_project.Name}' from this folder.";
            return;
        }
        try
        {
            var id = _project.Id;
            await WorkingRuleStore().DeleteAsync(id);
            await ProjectStore().DeleteAsync(id);
            AppLog.Info($"Deleted project '{id}'.");
            _project = null;
            _projectLoadGeneration++;
            ClearProjectView();
            ResetDeleteConfirmation();
            await RefreshProjectsAsync();
            RefreshNotes(); ShowSelectedSource();
            StatusText.Text = "Project deleted.";
        }
        catch (Exception ex) { AppLog.Error("Delete project", ex); StatusText.Text = "Could not delete the project. See the diagnostic log."; ResetDeleteConfirmation(); }
    }

    private void ResetDeleteConfirmation()
    {
        _pendingDeletionProjectId = null;
        DeleteProjectButton.Content = "Delete";
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
        catch (Exception ex) { AppLog.Error("Import source notes", ex); StatusText.Text = ex.Message; }
    }

    private async void RemoveSelectedNote_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote; if (note is null) return;
        if (_project is null) return;
        try
        {
            _project = DesignProjectService.RemoveSource(_project, note.Id);
            await WorkingRuleStore().DeleteAsync(_project.Id);
            await ProjectStore().SaveAsync(_project);
            _workingRuleSet = null;
            RefreshWorkingRulesView();
            RefreshNotes(); ShowSelectedSource();
            StatusText.Text = $"{_project.SourceNotes.Count} original note(s) preserved. The working proposal was cleared because its evidence may have changed.";
        }
        catch (Exception ex) { AppLog.Error("Remove source note", ex); StatusText.Text = "Could not remove the note safely. See the diagnostic log."; }
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
        _project = DesignProjectService.SaveSession(_project, session);
        await ProjectStore().SaveAsync(_project);
        AppLog.Info($"Saved designer answers for session '{session.Id}' before drafting.");
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
        catch (RuleDraftFormatException ex) { AppLog.Error("Validate model draft", ex); StatusText.Text = "The model returned text WorldSeed could not safely use. Nothing was saved."; DraftText.Text = $"Format detail: {ex.Message}\n\nRaw model output (for diagnosis):\n{Preview(capture.Last?.Content)}"; }
        catch (Exception ex) { AppLog.Error("Draft source", ex); StatusText.Text = "Drafting failed before a usable result was produced."; DraftText.Text = $"{ex.GetType().Name}: {ex.Message}\n\nDiagnostic log: {AppLog.CurrentPath}"; }
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
                var input = new TextBox { PlaceholderText = "Your answer", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 58 }; _answerInputs.Add(input); QuestionsPanel.Children.Add(input);
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

    private async void BuildWorkingRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_project is null || ModelSelector.SelectedItem is not string model)
        {
            WorkingRulesStatusText.Text = "Select a project and a local model on the Sources tab first.";
            return;
        }
        BuildWorkingRulesButton.IsEnabled = false;
        WorkingRulesStatusText.Text = $"Consolidating completed source drafts with {model}. This can take a few seconds...";
        var capture = new CapturingClient(new OllamaChatClient(_http, OllamaDefaults.CreateProfile("local", "Local", model)));
        try
        {
            var result = await new WorkingRuleSetFormalizationService(capture).CreateProposalAsync(_project, _workingRuleSet);
            await WorkingRuleStore().SaveAsync(result.RuleSet);
            _workingRuleSet = result.RuleSet;
            RefreshWorkingRulesView();
            WorkingRulesStatusText.Text = $"Saved working proposal revision {_workingRuleSet.Revision}. It remains a project working proposal, not a game schema.";
            AppLog.Info($"Saved working rule set revision {_workingRuleSet.Revision} for project '{_project.Id}'.");
        }
        catch (WorkingRuleSetFormatException ex)
        {
            AppLog.Error("Validate working rule proposal", ex);
            WorkingRulesStatusText.Text = "The model returned a proposal WorldSeed could not safely use. Nothing was saved.";
            WorkingRuleText.Text = $"Format detail: {ex.Message}\n\nRaw model output (for diagnosis):\n{Preview(capture.Last?.Content)}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Build working rule proposal", ex);
            WorkingRulesStatusText.Text = "Working-rule formalization failed before a usable proposal was produced.";
            WorkingRuleText.Text = $"{ex.GetType().Name}: {ex.Message}\n\nDiagnostic log: {AppLog.CurrentPath}";
        }
        finally { BuildWorkingRulesButton.IsEnabled = true; }
    }

    private async void ReloadWorkingRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_project is null) { WorkingRulesStatusText.Text = "Select a project first."; return; }
        await LoadWorkingRuleSetAsync(_project.Id);
    }

    private async Task LoadWorkingRuleSetAsync(string projectId)
    {
        try
        {
            _workingRuleSet = await WorkingRuleStore().LoadAsync(projectId);
            RefreshWorkingRulesView();
        }
        catch (Exception ex)
        {
            AppLog.Error("Load working rule set", ex);
            _workingRuleSet = null;
            RefreshWorkingRulesView();
            WorkingRulesStatusText.Text = "Could not load the saved working proposal. See the diagnostic log.";
        }
    }

    private void RefreshWorkingRulesView()
    {
        var selected = WorkingRuleList.SelectedIndex is var index && index >= 0 && index < _workingRules.Count ? _workingRules[index].Id : null;
        _workingRules.Clear();
        if (_workingRuleSet is not null) _workingRules.AddRange(_workingRuleSet.Rules);
        WorkingRuleList.ItemsSource = _workingRules.Select(rule => new WorkingRuleListItem(rule)).ToArray();
        WorkingRuleList.SelectedIndex = selected is null ? -1 : _workingRules.FindIndex(rule => rule.Id == selected);
        if (_workingRuleSet is null)
        {
            WorkingRulesSummaryText.Text = "Create completed source drafts, then build one project-wide working proposal.";
            WorkingRulesStatusText.Text = "No project-level working proposal is loaded.";
            WorkingRuleText.Text = "Build or load a proposal to inspect its project-level rules and exact evidence.";
            return;
        }
        WorkingRulesSummaryText.Text = $"{_workingRuleSet.Name} - revision {_workingRuleSet.Revision}. {_workingRuleSet.Rules.Count} rule(s), {_workingRuleSet.Concepts.Count} concept(s).";
        WorkingRulesStatusText.Text = "Select a working rule to inspect its wording, source-draft links, and exact evidence.";
        if (_workingRules.Count > 0) WorkingRuleList.SelectedIndex = Math.Max(0, WorkingRuleList.SelectedIndex);
    }

    private void WorkingRuleList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (WorkingRuleList.SelectedIndex is var index && index >= 0 && index < _workingRules.Count) WorkingRuleText.Text = FormatWorkingRule(_workingRules[index]);
    }

    private void ClearProjectView()
    {
        SourceNotesList.SelectedIndex = -1;
        RuleList.SelectedIndex = -1;
        _projectRules.Clear();
        RuleList.ItemsSource = Array.Empty<ProjectRuleItem>();
        DraftText.Text = "Select a rule to review its wording and evidence.";
        QuestionsPanel.Children.Clear();
        QuestionHeading.Text = "";
        _workingRuleSet = null;
        _workingRules.Clear();
        WorkingRuleList.ItemsSource = Array.Empty<WorkingRuleListItem>();
        WorkingRuleList.SelectedIndex = -1;
        WorkingRulesSummaryText.Text = "Create completed source drafts, then build one project-wide working proposal.";
        WorkingRulesStatusText.Text = "No project-level working proposal is loaded.";
        WorkingRuleText.Text = "Build or load a proposal to inspect its project-level rules and exact evidence.";
    }

    private DesignSession? SessionFor(DesignSourceNote note) => _project?.Sessions.SingleOrDefault(session => session.SourceNotes.Any(source => source.Id == note.Id));
    private string StatusFor(DesignSourceNote note) => SessionFor(note) is not { LatestTurn: { } turn } ? "Not started" : turn.Action == RuleDraftAction.PresentDraft ? "Draft ready" : "Needs answers";
    private static string FormatDraft(StructuredRuleDraft draft) => $"{draft.Title}\n{draft.Intent}\n\n" + string.Join("\n\n", draft.Rules.Select(rule => $"{rule.Name} ({rule.Kind})\n{rule.Text}\n\nSource evidence:\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}"));
    private static string FormatRule(RuleStatement rule) => $"{rule.Name}\nType: {rule.Kind}\n\nProposed rule\n{rule.Text}\n\nSource evidence\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}";
    private static string FormatWorkingRule(WorkingRule rule) => $"{rule.Name}\nType: {rule.Kind}\n\nWorking rule\n{rule.AuthoritativeText}\n\nDerived from source drafts\n{string.Join("\n", rule.SourceDraftRules.Select(reference => $"- {reference.SessionId} / {reference.RuleId}"))}\n\nExact source evidence\n{string.Join("\n", rule.Evidence.Select(evidence => $"- {evidence.SourceNoteId} ({evidence.PassageId ?? "source text"}): {evidence.Excerpt}"))}\n\nOpen questions\n{(rule.OpenQuestions.Count == 0 ? "None" : string.Join("\n", rule.OpenQuestions.Select(question => $"- {question}")))}\n\nSchema hints\n{(rule.SchemaHints.Count == 0 ? "None" : string.Join("\n", rule.SchemaHints.Select(hint => $"- {hint.Kind}: {hint.SuggestedName} - {hint.Rationale}")))}";
    private IDesignProjectStore ProjectStore() => _projectStore;
    private IWorkingRuleSetStore WorkingRuleStore() => _workingRuleStore;
    private static string Preview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No model response was received.";
        var display = text;
        try
        {
            using var document = JsonDocument.Parse(text);
            display = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException) { /* Preserve malformed model output exactly for diagnosis. */ }
        return display.Length > 6000 ? display[..6000] + "\n[truncated]" : display;
    }
    protected override void OnClosed(EventArgs e)
    {
        EndIncompleteSessionsOnClose();
        _http.Dispose();
        _apiHttp.Dispose();
        base.OnClosed(e);
    }

    private void EndIncompleteSessionsOnClose()
    {
        try
        {
            var store = ProjectStore();
            var endedCount = 0;
            foreach (var summary in store.ListAsync().GetAwaiter().GetResult())
            {
                var project = store.LoadAsync(summary.Id).GetAwaiter().GetResult();
                if (project is null) continue;
                var cleaned = DesignProjectService.EndIncompleteSessions(project);
                if (ReferenceEquals(cleaned, project)) continue;
                endedCount += project.Sessions.Count - cleaned.Sessions.Count;
                store.SaveAsync(cleaned).GetAwaiter().GetResult();
            }
            if (endedCount > 0) AppLog.Info($"Ended {endedCount} incomplete design session(s) when the application closed.");
        }
        catch (Exception ex)
        {
            // Closing the window must still succeed; the next normal close can retry this housekeeping.
            AppLog.Error("End incomplete sessions on application close", ex);
        }
    }

    private sealed record SourceListItem(DesignSourceNote Note, string Status) { public override string ToString() => $"{Note.Origin?.DisplayName ?? Note.Id} — {Status}"; }
    private sealed record ProjectRuleItem(string SessionId, string SourceName, RuleStatement Rule)
    {
        public override string ToString() => $"{Rule.Name} — {SourceName}";
    }
    private sealed record WorkingRuleListItem(WorkingRule Rule)
    {
        public override string ToString() => $"{Rule.Name} - {Rule.Kind}";
    }
    private sealed record ProjectListItem(DesignProjectSummary Project)
    {
        public override string ToString() => Project.Name;
    }
    private sealed class CapturingClient(ILanguageModelClient inner) : ILanguageModelClient { public LlmChatResponse? Last { get; private set; } public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) => Last = await inner.CompleteAsync(request, cancellationToken); }
}
