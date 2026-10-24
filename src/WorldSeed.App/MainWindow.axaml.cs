using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.SchemaTranslation;
using WorldSeed.SourceIngestion;
using WorldSeed.WorkingRules;

namespace WorldSeed.App;

public partial class MainWindow : Window
{
    // A local model may need time to load into memory and generate a complete JSON proposal.
    // Keep the desktop timeout distinct from the short API/database calls.
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly HttpClient _apiHttp = new() { BaseAddress = new Uri("http://localhost:5080/") };
    private readonly IDesignProjectStore _projectStore;
    private readonly IWorkingRuleSetStore _workingRuleStore;
    private readonly IProjectSchemaProposalStore _schemaProposalStore;
    private readonly HttpGameDataWorkspaceClient _gameDataWorkspace;
    private readonly WorldSeedSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _inactivityTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly List<DesignProjectSummary> _projectSummaries = [];
    private readonly List<ProjectRuleItem> _projectRules = [];
    private readonly List<WorkingRule> _workingRules = [];
    private readonly List<string> _schemaQueueRuleIds = [];
    private readonly List<SchemaClarification> _schemaClarifications = [];
    private readonly List<GameDataEntityTypeItem> _gameDataEntityTypes = [];
    private readonly List<GameDataEntityInstanceItem> _gameDataEntityInstances = [];
    private int _projectLoadGeneration;
    private bool _updatingProjectSelector;
    private string? _pendingDeletionProjectId;
    private DesignProject? _project;
    private WorkingRuleSet? _workingRuleSet;
    private ProjectSchemaProposal? _schemaProposal;
    private ActiveGameSchema? _activeGameSchema;
    private ProjectGameData? _projectGameData;
    private string? _pendingSchemaQuestion;
    private bool _processingSchemaQueue;
    private readonly List<TextBox> _answerInputs = [];
    private IReadOnlyList<string> _activeQuestions = [];
    private WorldSeedSettings _settings = WorldSeedSettings.Default;
    private DateTimeOffset _lastInteractionAt = DateTimeOffset.UtcNow;
    private bool _settingsDialogOpen;

    private enum SchemaQueueItemResult { Saved, NeedsClarification, Failed }

    public MainWindow()
    {
        _projectStore = new HttpDesignProjectStore(_apiHttp);
        _workingRuleStore = new HttpWorkingRuleSetStore(_apiHttp);
        _schemaProposalStore = new HttpProjectSchemaProposalStore(_apiHttp);
        _gameDataWorkspace = new HttpGameDataWorkspaceClient(_apiHttp);
        InitializeComponent();
        _settings = _settingsStore.Load();
        _inactivityTimer.Tick += InactivityTimer_Tick;
        _inactivityTimer.Start();
    }
    protected override void OnOpened(EventArgs e) { base.OnOpened(e); RegisterInteraction(); AppLog.Info("Application opened."); _ = InitializeAsync(); }
    protected override void OnPointerMoved(PointerEventArgs e) { RegisterInteraction(); base.OnPointerMoved(e); }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { RegisterInteraction(); base.OnPointerPressed(e); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { RegisterInteraction(); base.OnPointerWheelChanged(e); }
    protected override void OnKeyDown(KeyEventArgs e) { RegisterInteraction(); base.OnKeyDown(e); }
    protected override void OnTextInput(TextInputEventArgs e) { RegisterInteraction(); base.OnTextInput(e); }

    private void RegisterInteraction() => _lastInteractionAt = DateTimeOffset.UtcNow;

    private void InactivityTimer_Tick(object? sender, EventArgs e)
    {
        if (_settingsDialogOpen || _settings.IdleTimeoutMinutes == 0) return;
        var timeout = TimeSpan.FromMinutes(_settings.IdleTimeoutMinutes);
        if (DateTimeOffset.UtcNow - _lastInteractionAt < timeout) return;
        _inactivityTimer.Stop();
        AppLog.Info($"Closing application after {_settings.IdleTimeoutMinutes} minutes without interaction.");
        Close();
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (_settingsDialogOpen) return;
        _settingsDialogOpen = true;
        RegisterInteraction();
        try
        {
            var window = new SettingsWindow(_settings.IdleTimeoutMinutes);
            if (await window.ShowDialog<bool>(this) is not true) return;
            _settings = new WorldSeedSettings(window.IdleTimeoutMinutes);
            _settingsStore.Save(_settings);
            RegisterInteraction();
            var behavior = _settings.IdleTimeoutMinutes == 0 ? "Automatic closing is disabled." : $"WorldSeed will close after {_settings.IdleTimeoutMinutes} minutes without interaction.";
            StatusText.Text = behavior;
            AppLog.Info(behavior);
        }
        catch (Exception ex)
        {
            AppLog.Error("Save desktop settings", ex);
            StatusText.Text = "WorldSeed could not save the desktop settings. See the diagnostic log.";
        }
        finally
        {
            _settingsDialogOpen = false;
            RegisterInteraction();
        }
    }

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
                await LoadSchemaProposalAsync(_project.Id);
                await LoadGameDataWorkspaceAsync(_project.Id);
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
        WorkingRulesStatusText.Text = $"Building bounded working-rule batches with {model}. Each batch is validated before it is combined...";
        var capture = new CapturingClient(new OllamaChatClient(_http, OllamaDefaults.CreateProfile("local", "Local", model)));
        try
        {
            var result = await new WorkingRuleSetFormalizationService(capture).CreateProposalAsync(_project, _workingRuleSet);
            await WorkingRuleStore().SaveAsync(result.RuleSet);
            _workingRuleSet = result.RuleSet;
            _schemaProposal = null;
            _schemaQueueRuleIds.Clear();
            _pendingSchemaQuestion = null;
            _schemaClarifications.Clear();
            SchemaQuestionPanel.IsVisible = false;
            RefreshWorkingRulesView();
            RefreshSchemaView();
            WorkingRulesStatusText.Text = $"Saved working proposal revision {_workingRuleSet.Revision} from {result.BatchCount} validated batch(es). It remains a project working proposal, not a game schema.";
            AppLog.Info($"Saved working rule set revision {_workingRuleSet.Revision} for project '{_project.Id}'.");
        }
        catch (WorkingRuleSetFormatException ex)
        {
            AppLog.Error("Validate working rule proposal", ex);
            WorkingRulesStatusText.Text = "The model returned a proposal WorldSeed could not safely use. Nothing was saved.";
            WorkingRuleText.Text = $"Format detail: {ex.Message}\n\nRaw model output (for diagnosis):\n{Preview(capture.Last?.Content)}";
        }
        catch (LlmClientException ex) when (ex.InnerException is TaskCanceledException)
        {
            AppLog.Error("Build working rule proposal timeout", ex);
            WorkingRulesStatusText.Text = "The local model did not finish within 10 minutes. Nothing was saved; try a smaller/faster model or reduce the source material for this pass.";
            WorkingRuleText.Text = $"The request timed out while waiting for Ollama.\n\nDiagnostic log: {AppLog.CurrentPath}";
        }
        catch (TaskCanceledException ex)
        {
            AppLog.Error("Build working rule proposal timeout", ex);
            WorkingRulesStatusText.Text = "The local model did not finish within 10 minutes. Nothing was saved; try a smaller/faster model or reduce the source material for this pass.";
            WorkingRuleText.Text = $"The request timed out while waiting for Ollama.\n\nDiagnostic log: {AppLog.CurrentPath}";
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

    private async Task LoadSchemaProposalAsync(string projectId)
    {
        try
        {
            _schemaProposal = await SchemaProposalStore().LoadAsync(projectId);
            _schemaClarifications.Clear();
            if (_schemaProposal?.Clarifications is not null) _schemaClarifications.AddRange(_schemaProposal.Clarifications);
            RefreshSchemaView();
        }
        catch (Exception ex)
        {
            AppLog.Error("Load schema proposal", ex);
            _schemaProposal = null;
            RefreshSchemaView();
            SchemaStatusText.Text = "Could not load the saved schema proposal. See the diagnostic log.";
        }
    }

    private async Task LoadGameDataWorkspaceAsync(string projectId)
    {
        try
        {
            _activeGameSchema = await _gameDataWorkspace.LoadActiveSchemaAsync(projectId);
            _projectGameData = _activeGameSchema is null ? null : await _gameDataWorkspace.LoadGameDataAsync(projectId);
            RefreshGameDataView();
        }
        catch (Exception ex)
        {
            AppLog.Error("Load game data workspace", ex);
            _activeGameSchema = null;
            _projectGameData = null;
            RefreshGameDataView();
            GameDataStatusText.Text = "Could not load the game-data workspace. See the diagnostic log.";
        }
    }

    private async void ActivateSchema_Click(object? sender, RoutedEventArgs e)
    {
        if (_project is null || _schemaProposal is null)
        {
            SchemaStatusText.Text = "Save a schema proposal before making it active.";
            return;
        }
        ActivateSchemaButton.IsEnabled = false;
        ActivateSchemaFromDataButton.IsEnabled = false;
        try
        {
            _activeGameSchema = await _gameDataWorkspace.ActivateAsync(_project.Id);
            _projectGameData = await _gameDataWorkspace.LoadGameDataAsync(_project.Id);
            RefreshGameDataView();
            RefreshSchemaView();
            var detail = _projectGameData is null ? "No game-data document was returned." : "An empty game-data document is ready for entries.";
            SchemaStatusText.Text = $"Active schema revision {_activeGameSchema.Revision} is ready. {detail}";
            AppLog.Info($"Activated schema revision {_activeGameSchema.Revision} for project '{_project.Id}'.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Activate saved schema proposal", ex);
            var detail = ex is HttpRequestException ? ex.Message : "See the diagnostic log.";
            SchemaStatusText.Text = "Could not activate the saved schema proposal. " + detail;
            GameDataStatusText.Text = "Activation failed. Existing game data was not changed.";
        }
        finally
        {
            RefreshSchemaActivationControls();
            RefreshGameDataControls();
        }
    }

    private async void BuildSchema_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ProcessSchemaQueueAsync();

    private void ClearSchemaQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (_processingSchemaQueue || !string.IsNullOrWhiteSpace(_pendingSchemaQuestion)) return;
        _schemaQueueRuleIds.Clear();
        RefreshSchemaView();
        SchemaStatusText.Text = "Schema work queue cleared.";
    }

    private async void SubmitSchemaAnswer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_pendingSchemaQuestion)) return;
        var answer = SchemaAnswerText.Text?.Trim();
        if (string.IsNullOrWhiteSpace(answer))
        {
            SchemaStatusText.Text = "Enter an answer before continuing the schema proposal.";
            return;
        }
        _schemaClarifications.Add(new SchemaClarification(_pendingSchemaQuestion, answer));
        _pendingSchemaQuestion = null;
        SchemaQuestionPanel.IsVisible = false;
        await ProcessSchemaQueueAsync();
    }

    private async Task ProcessSchemaQueueAsync()
    {
        if (!string.IsNullOrWhiteSpace(_pendingSchemaQuestion))
        {
            SchemaStatusText.Text = "Answer the schema clarification below before requesting another proposal.";
            SchemaQuestionPanel.IsVisible = true;
            return;
        }
        if (_project is null || _workingRuleSet is null || ModelSelector.SelectedItem is not string model)
        {
            SchemaStatusText.Text = "Load a project, Working Rule Set, and local model first.";
            return;
        }
        if (_schemaQueueRuleIds.Count == 0)
        {
            SchemaStatusText.Text = "Add one or more Working Rules to the schema work queue first.";
            return;
        }
        _processingSchemaQueue = true;
        BuildSchemaButton.IsEnabled = false;
        try
        {
            while (_schemaQueueRuleIds.Count > 0)
            {
                var ruleId = _schemaQueueRuleIds[0];
                var rule = _workingRules.SingleOrDefault(candidate => candidate.Id == ruleId);
                if (rule is null)
                {
                    _schemaQueueRuleIds.RemoveAt(0);
                    continue;
                }
                SchemaStatusText.Text = $"Processing {SchemaRulePicker.DisplayName(rule)} ({_schemaQueueRuleIds.Count} queued) with {model}...";
                SchemaText.Text = string.Empty;
                RefreshSchemaQueueControls();
                var result = await ProcessSchemaQueueItemAsync(rule, model);
                if (result != SchemaQueueItemResult.Saved) return;
                _schemaQueueRuleIds.RemoveAt(0);
                RefreshSchemaView();
            }
            SchemaStatusText.Text = "Finished the schema work queue. The saved proposal remains non-canonical and tied to this Working Rule Set revision.";
        }
        finally
        {
            _processingSchemaQueue = false;
            RefreshSchemaQueueControls();
        }
    }

    private async Task<SchemaQueueItemResult> ProcessSchemaQueueItemAsync(WorkingRule rule, string model)
    {
        var capture = new CapturingClient(new OllamaChatClient(_http, OllamaDefaults.CreateProfile("local", "Local", model)));
        try
        {
            var baseSchema = SavedSchemaForExtension();
            var selectedRuleSet = _workingRuleSet! with
            {
                Rules = [rule],
                Concepts = _workingRuleSet.Concepts.Where(concept => rule.ConceptIds.Contains(concept.Id, StringComparer.Ordinal)).ToArray()
            };
            var turn = await new SchemaTranslationService(capture).TranslateAsync(new SchemaTranslationRequest(selectedRuleSet, baseSchema, _schemaClarifications));
            if (turn.Action == SchemaTranslationAction.AskClarifyingQuestion)
            {
                SchemaStatusText.Text = "The model needs clarification before it can process the current queued rule.";
                _pendingSchemaQuestion = turn.ClarifyingQuestion ?? "No clarification question was supplied.";
                SchemaQuestionText.Text = _pendingSchemaQuestion;
                SchemaAnswerText.Text = string.Empty;
                SchemaQuestionPanel.IsVisible = true;
                SchemaText.Text = "Queue paused. Answer the question below to continue the current rule, then the remaining queue.";
                return SchemaQueueItemResult.NeedsClarification;
            }
            var proposal = turn.Proposal ?? throw new SchemaTranslationFormatException("The model did not return a schema proposal.");
            var mergedSchema = SchemaProposalMerger.MergeSchemas(baseSchema, proposal.Schema);
            var mergedSources = SchemaProposalMerger.MergeDefinitionSources(_schemaProposal?.DefinitionSources ?? Array.Empty<DefinitionSource>(), proposal.DefinitionSources);
            var mergedRuleIds = SchemaProposalMerger.MergeWorkingRuleIds(_schemaProposal?.SelectedWorkingRuleIds ?? Array.Empty<string>(), [rule.Id]);
            var now = DateTimeOffset.UtcNow;
            _schemaProposal = new ProjectSchemaProposal(
                "schema-proposal-" + _project!.Id,
                _project.Id,
                "0.1",
                _schemaProposal is null ? proposal.Summary : $"Accumulated schema proposal. Latest addition: {proposal.Summary}",
                mergedSchema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                mergedSources,
                proposal.WorkingRuleSetId,
                proposal.WorkingRuleSetRevision,
                mergedRuleIds,
                _schemaProposal?.CreatedAt ?? now,
                now,
                _schemaClarifications.ToArray());
            await SchemaProposalStore().SaveAsync(_schemaProposal);
            _pendingSchemaQuestion = null;
            SchemaQuestionPanel.IsVisible = false;
            AppLog.Info($"Saved schema proposal addition for project '{_project.Id}' from queued Working Rule '{rule.Id}'.");
            return SchemaQueueItemResult.Saved;
        }
        catch (SchemaTranslationFormatException ex)
        {
            AppLog.Error("Validate queued schema proposal", ex);
            SchemaStatusText.Text = "The current queued rule could not be saved. It remains at the front of the queue.";
            SchemaText.Text = $"Format detail: {ex.Message}\n\nRaw model output (for diagnosis):\n{Preview(capture.Last?.Content)}";
            return SchemaQueueItemResult.Failed;
        }
        catch (Exception ex)
        {
            AppLog.Error("Build queued schema proposal", ex);
            SchemaStatusText.Text = "Schema translation failed before a usable proposal was produced. The current rule remains queued.";
            SchemaText.Text = $"{ex.GetType().Name}: {ex.Message}\n\nDiagnostic log: {AppLog.CurrentPath}";
            return SchemaQueueItemResult.Failed;
        }
    }

    private void RefreshSchemaView()
    {
        var availableIds = _workingRules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        _schemaQueueRuleIds.RemoveAll(id => !availableIds.Contains(id));
        RefreshSchemaQueueControls();
        SchemaRuleGroupsPanel.Children.Clear();
        foreach (var group in SchemaRulePicker.Group(_workingRules))
        {
            var groupPanel = new StackPanel { Spacing = 4, Margin = new Thickness(8, 4, 0, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
            // Keep unfinished candidates first. Retained, mapped rules move to the bottom without being hidden.
            foreach (var rule in group.Rules.OrderBy(IsSchemaProcessed))
            {
                var addButton = new Button
                {
                    Content = _schemaQueueRuleIds.Contains(rule.Id, StringComparer.Ordinal) ? "Queued" : "Add to queue",
                    Tag = rule.Id,
                    IsEnabled = !_processingSchemaQueue && !_schemaQueueRuleIds.Contains(rule.Id, StringComparer.Ordinal),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 4)
                };
                addButton.Click += AddSchemaRuleToQueue_Click;
                var name = new TextBlock
                {
                    Text = SchemaRulePicker.DisplayName(rule),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var status = new TextBlock
                {
                    Text = SchemaProposalStatus(rule),
                    Margin = new Thickness(0, 0, 0, 4),
                    Opacity = 0.65,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(addButton);
                row.Children.Add(name);
                groupPanel.Children.Add(row);
                groupPanel.Children.Add(status);
            }
            SchemaRuleGroupsPanel.Children.Add(new Expander
            {
                Header = $"{group.Kind} ({group.Rules.Count})",
                IsExpanded = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = groupPanel
            });
        }
        if (_schemaProposal is null)
        {
            RefreshSchemaActivationControls();
            SchemaStatusText.Text = _workingRuleSet is null ? "Load a Working Rule Set, then add rules to the schema queue." : "Add Working Rules to the queue. Each item will be proposed and saved separately.";
            SchemaText.Text = "No schema proposal is loaded.";
            return;
        }
        RefreshSchemaActivationControls();
        SchemaStatusText.Text = $"Proposal based on Working Rule Set revision {_schemaProposal.WorkingRuleSetRevision}; { _schemaProposal.SelectedWorkingRuleIds.Count } selected rule(s).";
        SchemaText.Text = $"Summary\n{_schemaProposal.Summary}\n\nSchema clarifications\n{FormatSchemaClarifications(_schemaProposal.Clarifications)}\n\nSelected Working Rules\n{string.Join("\n", _schemaProposal.SelectedWorkingRuleIds.Select(id => "- " + id))}\n\nDefinition sources\n{string.Join("\n", _schemaProposal.DefinitionSources.Select(source => $"- {source.DefinitionId}: {string.Join(", ", source.WorkingRuleIds)}"))}\n\nSchema JSON\n{PrettyJson(_schemaProposal.SchemaJson)}";
    }

    private void RefreshSchemaActivationControls()
    {
        var enabled = _schemaProposal is not null && !_processingSchemaQueue;
        ActivateSchemaButton.IsEnabled = enabled;
        ActivateSchemaButton.Content = _activeGameSchema is null ? "Use saved proposal as active schema" : "Update active schema from saved proposal";
        ActivateSchemaFromDataButton.IsEnabled = enabled;
        ActivateSchemaFromDataButton.Content = ActivateSchemaButton.Content;
    }

    private void AddSchemaRuleToQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (_processingSchemaQueue || sender is not Button { Tag: string ruleId }) return;
        if (SchemaRulePicker.TryEnqueue(_schemaQueueRuleIds, ruleId))
        {
            RefreshSchemaView();
            SchemaStatusText.Text = $"Added {SchemaRulePicker.DisplayName(_workingRules.Single(rule => rule.Id == ruleId))} to the schema work queue.";
        }
    }

    private void RemoveSchemaRuleFromQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (_processingSchemaQueue || sender is not Button { Tag: string ruleId }) return;
        _schemaQueueRuleIds.Remove(ruleId);
        RefreshSchemaView();
    }

    private void RefreshSchemaQueueControls()
    {
        SchemaQueuePanel.Children.Clear();
        if (_schemaQueueRuleIds.Count == 0)
        {
            SchemaQueuePanel.Children.Add(new TextBlock { Text = "Queue is empty. Add Working Rules below.", Opacity = 0.65, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            for (var index = 0; index < _schemaQueueRuleIds.Count; index++)
            {
                var ruleId = _schemaQueueRuleIds[index];
                var rule = _workingRules.SingleOrDefault(candidate => candidate.Id == ruleId);
                if (rule is null) continue;
                var label = new TextBlock
                {
                    Text = $"{index + 1}. {SchemaRulePicker.DisplayName(rule)}" + (index == 0 ? " (next)" : string.Empty),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var remove = new Button
                {
                    Content = "Remove",
                    Tag = ruleId,
                    IsEnabled = !_processingSchemaQueue && string.IsNullOrWhiteSpace(_pendingSchemaQuestion),
                    Padding = new Thickness(8, 3)
                };
                remove.Click += RemoveSchemaRuleFromQueue_Click;
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
                row.Children.Add(label);
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                SchemaQueuePanel.Children.Add(row);
            }
        }
        var queued = _schemaQueueRuleIds.Count;
        SchemaQueueText.Text = queued == 0
            ? "No working rules are queued."
            : $"{queued} rule(s) queued. Each is sent to the model individually.";
        BuildSchemaButton.IsEnabled = queued > 0 && !_processingSchemaQueue && string.IsNullOrWhiteSpace(_pendingSchemaQuestion);
        ClearSchemaQueueButton.IsEnabled = queued > 0 && !_processingSchemaQueue && string.IsNullOrWhiteSpace(_pendingSchemaQuestion);
    }

    private string SchemaProposalStatus(WorkingRule rule)
    {
        if (_schemaProposal is null) return "No saved proposal";
        if (IsCitedBySavedProposal(rule)) return "Cited by saved proposal";
        if (SchemaDefinesLike(rule)) return "Appears in saved schema (name match; source link missing)";
        if (_schemaProposal.SelectedWorkingRuleIds.Contains(rule.Id, StringComparer.Ordinal)) return "Used as proposal input (not cited)";
        return "Not in saved proposal";
    }

    private bool IsSchemaProcessed(WorkingRule rule) => IsCitedBySavedProposal(rule) || SchemaDefinesLike(rule);

    private bool IsCitedBySavedProposal(WorkingRule rule) => _schemaProposal?.DefinitionSources.Any(source => source.WorkingRuleIds.Contains(rule.Id, StringComparer.Ordinal)) == true;

    private bool SchemaDefinesLike(WorkingRule rule)
    {
        if (_schemaProposal is null) return false;
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            ComparableSchemaName(SchemaRulePicker.DisplayName(rule))
        };
        foreach (var hint in rule.SchemaHints) expected.Add(ComparableSchemaName(hint.SuggestedName));
        try
        {
            var schema = JsonNode.Parse(_schemaProposal.SchemaJson) as JsonObject;
            foreach (var collection in new[] { "schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations" })
                foreach (var definition in (schema?[collection] as JsonArray)?.OfType<JsonObject>() ?? [])
                {
                    var metadata = definition["metadata"] as JsonObject;
                    var name = metadata?["name"]?.GetValue<string>() ?? definition["name"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name) && expected.Contains(ComparableSchemaName(name))) return true;
                }
        }
        catch (JsonException) { /* Saved schema diagnostics remain visible elsewhere. */ }
        return false;
    }

    private JsonObject? SavedSchemaForExtension()
    {
        if (_schemaProposal is null) return null;
        try
        {
            return JsonNode.Parse(_schemaProposal.SchemaJson) as JsonObject
                ?? throw new SchemaTranslationFormatException("The saved schema proposal is not a JSON object. It cannot be safely extended.");
        }
        catch (JsonException exception)
        {
            throw new SchemaTranslationFormatException("The saved schema proposal is invalid JSON. It cannot be safely extended: " + exception.Message);
        }
    }

    private static string ComparableSchemaName(string name) => string.Join(' ', name
        .Split([' ', '-', '_', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(word => !new[] { "definition", "rule", "procedure", "constraint" }.Contains(word, StringComparer.OrdinalIgnoreCase))
        .Select(word => word.ToLowerInvariant()));

    private void RefreshWorkingRulesView()
    {
        var selected = WorkingRuleList.SelectedIndex is var index && index >= 0 && index < _workingRules.Count ? _workingRules[index].Id : null;
        _workingRules.Clear();
        if (_workingRuleSet is not null) _workingRules.AddRange(_workingRuleSet.Rules);
        WorkingRuleList.ItemsSource = _workingRules.Select(rule => new WorkingRuleListItem(rule)).ToArray();
        if (_schemaProposal is null) _schemaQueueRuleIds.Clear();
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
        RefreshSchemaView();
    }

    private void WorkingRuleList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (WorkingRuleList.SelectedIndex is var index && index >= 0 && index < _workingRules.Count) WorkingRuleText.Text = FormatWorkingRule(_workingRules[index]);
    }

    private void RefreshGameDataView()
    {
        var selectedTypeId = GameDataEntityTypeList.SelectedIndex is var typeIndex && typeIndex >= 0 && typeIndex < _gameDataEntityTypes.Count
            ? _gameDataEntityTypes[typeIndex].Id
            : null;
        var selectedInstanceId = GameDataEntityList.SelectedIndex is var instanceIndex && instanceIndex >= 0 && instanceIndex < _gameDataEntityInstances.Count
            ? _gameDataEntityInstances[instanceIndex].Id
            : null;
        _gameDataEntityTypes.Clear();
        _gameDataEntityInstances.Clear();

        if (_activeGameSchema is null || _projectGameData is null)
        {
            ActiveSchemaText.Text = _activeGameSchema is null
                ? "Activate a saved schema proposal to begin entering game data."
                : "The active schema is loaded, but its game-data document is unavailable.";
            GameDataEntityTypeList.ItemsSource = Array.Empty<GameDataEntityTypeItem>();
            GameDataEntityList.ItemsSource = Array.Empty<GameDataEntityInstanceItem>();
            GameDataEntityEditor.Text = "Select an entry or add one from an entity type.";
            GameDataJsonText.Text = "Activate a schema to create a game-data document.";
            GameDataStatusText.Text = _activeGameSchema is null ? "No active schema is loaded." : "No game-data document is loaded.";
            RefreshGameDataControls();
            return;
        }

        try
        {
            var schema = ParseJsonObject(_activeGameSchema.SchemaJson, "The active schema snapshot is not a JSON object.");
            var data = ParseJsonObject(_projectGameData.DataJson, "The saved game-data document is not a JSON object.");
            foreach (var entity in (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var metadata = entity["metadata"] as JsonObject;
                var id = metadata?["id"]?.GetValue<string>();
                var name = metadata?["name"]?.GetValue<string>() ?? id;
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name)) _gameDataEntityTypes.Add(new GameDataEntityTypeItem(id, name));
            }
            foreach (var instance in (data["entityInstances"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var id = instance["id"]?.GetValue<string>();
                var entityTypeId = instance["entityTypeId"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(entityTypeId)) continue;
                var typeName = _gameDataEntityTypes.SingleOrDefault(type => type.Id == entityTypeId)?.Name ?? entityTypeId;
                _gameDataEntityInstances.Add(new GameDataEntityInstanceItem(id, entityTypeId, typeName));
            }
            ActiveSchemaText.Text = $"{_activeGameSchema.Name}\nRevision {_activeGameSchema.Revision}; based on Working Rule Set revision {_activeGameSchema.WorkingRuleSetRevision}.";
            GameDataEntityTypeList.ItemsSource = _gameDataEntityTypes.ToArray();
            GameDataEntityList.ItemsSource = _gameDataEntityInstances.ToArray();
            GameDataEntityTypeList.SelectedIndex = selectedTypeId is null ? -1 : _gameDataEntityTypes.FindIndex(type => type.Id == selectedTypeId);
            GameDataEntityList.SelectedIndex = selectedInstanceId is null ? -1 : _gameDataEntityInstances.FindIndex(item => item.Id == selectedInstanceId);
            GameDataJsonText.Text = PrettyJson(_projectGameData.DataJson);
            GameDataStatusText.Text = $"{_gameDataEntityInstances.Count} saved entr{(_gameDataEntityInstances.Count == 1 ? "y" : "ies")}. Edit one entry at a time, then validate and save the complete document.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Render game data workspace", ex);
            GameDataStatusText.Text = "Could not display the saved game-data document. See the diagnostic log.";
            GameDataJsonText.Text = _projectGameData.DataJson;
        }
        RefreshGameDataControls();
    }

    private void RefreshGameDataControls()
    {
        var hasDocument = _activeGameSchema is not null && _projectGameData is not null;
        AddGameDataEntityButton.IsEnabled = hasDocument && GameDataEntityTypeList.SelectedIndex >= 0;
        RemoveGameDataEntityButton.IsEnabled = hasDocument && GameDataEntityList.SelectedIndex >= 0;
        ApplyGameDataEntityButton.IsEnabled = hasDocument && GameDataEntityList.SelectedIndex >= 0;
        ValidateAndSaveGameDataButton.IsEnabled = hasDocument;
        ApplyFullGameDataButton.IsEnabled = hasDocument;
        GameDataJsonText.IsReadOnly = !hasDocument;
        RefreshSchemaActivationControls();
    }

    private void GameDataEntityList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var index = GameDataEntityList.SelectedIndex;
        if (_projectGameData is null || index < 0 || index >= _gameDataEntityInstances.Count)
        {
            RefreshGameDataControls();
            return;
        }
        try
        {
            var root = ParseJsonObject(_projectGameData.DataJson, "The saved game-data document is not a JSON object.");
            var selected = _gameDataEntityInstances[index];
            var entry = (root["entityInstances"] as JsonArray)?.OfType<JsonObject>().SingleOrDefault(candidate => candidate["id"]?.GetValue<string>() == selected.Id);
            GameDataEntityEditor.Text = entry is null ? "Selected entry is no longer present." : PrettyJson(entry.ToJsonString());
        }
        catch (Exception ex)
        {
            AppLog.Error("Select game-data entry", ex);
            GameDataEntityEditor.Text = "Could not load this entry. See the diagnostic log.";
        }
        RefreshGameDataControls();
    }

    private void GameDataEntityTypeList_SelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshGameDataControls();

    private void AddGameDataEntity_Click(object? sender, RoutedEventArgs e)
    {
        var index = GameDataEntityTypeList.SelectedIndex;
        if (_projectGameData is null || index < 0 || index >= _gameDataEntityTypes.Count) return;
        try
        {
            var root = ParseJsonObject(_projectGameData.DataJson, "The saved game-data document is not a JSON object.");
            var entries = root["entityInstances"] as JsonArray ?? throw new GameDataFormatException("Game data requires an entityInstances array.");
            var type = _gameDataEntityTypes[index];
            var id = $"{type.Id}-{Guid.NewGuid():N}"[..Math.Min(type.Id.Length + 9, type.Id.Length + 33)];
            entries.Add(new JsonObject { ["id"] = id, ["entityTypeId"] = type.Id, ["values"] = new JsonObject() });
            SetGameDataDocument(root);
            RefreshGameDataView();
            GameDataEntityList.SelectedIndex = _gameDataEntityInstances.FindIndex(item => item.Id == id);
            GameDataStatusText.Text = $"Added a new {type.Name} entry. Fill its values, then validate and save.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Add game-data entity", ex);
            GameDataStatusText.Text = "Could not add the entry. See the diagnostic log.";
        }
    }

    private void RemoveGameDataEntity_Click(object? sender, RoutedEventArgs e)
    {
        var index = GameDataEntityList.SelectedIndex;
        if (_projectGameData is null || index < 0 || index >= _gameDataEntityInstances.Count) return;
        try
        {
            var root = ParseJsonObject(_projectGameData.DataJson, "The saved game-data document is not a JSON object.");
            var id = _gameDataEntityInstances[index].Id;
            var entries = root["entityInstances"] as JsonArray ?? throw new GameDataFormatException("Game data requires an entityInstances array.");
            var entryIndex = entries.Select((node, itemIndex) => (node, itemIndex)).FirstOrDefault(item => item.node is JsonObject entry && entry["id"]?.GetValue<string>() == id).itemIndex;
            if (entryIndex >= entries.Count || entries[entryIndex] is not JsonObject entry || entry["id"]?.GetValue<string>() != id) entryIndex = -1;
            if (entryIndex < 0) throw new GameDataFormatException("The selected entry no longer exists.");
            entries.RemoveAt(entryIndex);
            SetGameDataDocument(root);
            RefreshGameDataView();
            GameDataStatusText.Text = "Removed the selected entry. Validate and save to persist the change.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Remove game-data entity", ex);
            GameDataStatusText.Text = "Could not remove the entry. See the diagnostic log.";
        }
    }

    private void ApplyGameDataEntity_Click(object? sender, RoutedEventArgs e)
    {
        if (TryApplySelectedGameDataEntry(out var message))
            GameDataStatusText.Text = message;
        else
            GameDataStatusText.Text = message;
    }

    private void ApplyFullGameData_Click(object? sender, RoutedEventArgs e)
    {
        if (_projectGameData is null) return;
        try
        {
            var root = ParseJsonObject(GameDataJsonText.Text ?? string.Empty, "The full game-data editor must contain a JSON object.");
            SetGameDataDocument(root);
            RefreshGameDataView();
            GameDataStatusText.Text = "Applied full-document changes. Validate and save to persist them.";
        }
        catch (GameDataFormatException ex)
        {
            GameDataStatusText.Text = ex.Message;
        }
    }

    private async void ValidateAndSaveGameData_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeGameSchema is null || _projectGameData is null) return;
        if (!TryApplySelectedGameDataEntry(out var applyMessage))
        {
            GameDataStatusText.Text = applyMessage;
            return;
        }
        try
        {
            var validation = GameDataService.Validate(_activeGameSchema, _projectGameData);
            if (!validation.IsValid)
            {
                GameDataStatusText.Text = "Validation failed: " + string.Join(" ", validation.Issues.Select(issue => issue.Message));
                return;
            }
            ValidateAndSaveGameDataButton.IsEnabled = false;
            await _gameDataWorkspace.SaveGameDataAsync(_projectGameData);
            GameDataStatusText.Text = "Game data is valid and saved in the local database.";
            AppLog.Info($"Saved validated game data for project '{_projectGameData.ProjectId}'.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Save game data", ex);
            GameDataStatusText.Text = "Could not save game data. " + (ex is HttpRequestException ? ex.Message : "See the diagnostic log.");
        }
        finally
        {
            RefreshGameDataControls();
        }
    }

    private bool TryApplySelectedGameDataEntry(out string message)
    {
        message = "";
        var index = GameDataEntityList.SelectedIndex;
        if (_projectGameData is null || index < 0 || index >= _gameDataEntityInstances.Count)
        {
            message = "Select an entry before applying changes.";
            return false;
        }
        try
        {
            var edited = ParseJsonObject(GameDataEntityEditor.Text ?? string.Empty, "An entry must be a JSON object.");
            var id = edited["id"]?.GetValue<string>();
            var entityTypeId = edited["entityTypeId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(entityTypeId) || edited["values"] is not JsonObject)
                throw new GameDataFormatException("An entry requires a non-empty id, an entityTypeId, and a values object.");
            if (_gameDataEntityTypes.All(type => type.Id != entityTypeId))
                throw new GameDataFormatException($"'{entityTypeId}' is not an entity type in the active schema.");
            var root = ParseJsonObject(_projectGameData.DataJson, "The saved game-data document is not a JSON object.");
            var entries = root["entityInstances"] as JsonArray ?? throw new GameDataFormatException("Game data requires an entityInstances array.");
            var originalId = _gameDataEntityInstances[index].Id;
            var entryIndex = entries.Select((node, itemIndex) => (node, itemIndex)).FirstOrDefault(item => item.node is JsonObject entry && entry["id"]?.GetValue<string>() == originalId).itemIndex;
            if (entryIndex >= entries.Count || entries[entryIndex] is not JsonObject entry || entry["id"]?.GetValue<string>() != originalId) entryIndex = -1;
            if (entryIndex < 0) throw new GameDataFormatException("The selected entry no longer exists.");
            if (entries.OfType<JsonObject>().Any(entry => entry["id"]?.GetValue<string>() == id && entry["id"]?.GetValue<string>() != originalId))
                throw new GameDataFormatException("Every entity entry needs a unique id.");
            entries[entryIndex] = edited;
            SetGameDataDocument(root);
            RefreshGameDataView();
            GameDataEntityList.SelectedIndex = _gameDataEntityInstances.FindIndex(item => item.Id == id);
            message = "Applied entry changes. Validate and save to persist them.";
            return true;
        }
        catch (GameDataFormatException ex)
        {
            message = ex.Message;
            return false;
        }
        catch (JsonException)
        {
            message = "The entry editor must contain complete JSON before changes can be applied.";
            return false;
        }
    }

    private void SetGameDataDocument(JsonObject root)
    {
        if (_projectGameData is null) return;
        _projectGameData = _projectGameData with { DataJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), UpdatedAt = DateTimeOffset.UtcNow };
    }

    private static JsonObject ParseJsonObject(string json, string message)
    {
        try { return JsonNode.Parse(json) as JsonObject ?? throw new GameDataFormatException(message); }
        catch (JsonException) { throw new GameDataFormatException(message); }
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
        _schemaProposal = null;
        _activeGameSchema = null;
        _projectGameData = null;
        _pendingSchemaQuestion = null;
        _schemaClarifications.Clear();
        _workingRules.Clear();
        WorkingRuleList.ItemsSource = Array.Empty<WorkingRuleListItem>();
        WorkingRuleList.SelectedIndex = -1;
        WorkingRulesSummaryText.Text = "Create completed source drafts, then build one project-wide working proposal.";
        WorkingRulesStatusText.Text = "No project-level working proposal is loaded.";
        WorkingRuleText.Text = "Build or load a proposal to inspect its project-level rules and exact evidence.";
        _schemaQueueRuleIds.Clear();
        SchemaRuleGroupsPanel.Children.Clear();
        SchemaQueuePanel.Children.Clear();
        SchemaQueueText.Text = "No working rules are queued.";
        BuildSchemaButton.IsEnabled = false;
        ClearSchemaQueueButton.IsEnabled = false;
        SchemaStatusText.Text = "Load a Working Rule Set, then select related rules to build a schema proposal.";
        SchemaText.Text = "No schema proposal is loaded.";
        SchemaQuestionText.Text = string.Empty;
        SchemaAnswerText.Text = string.Empty;
        SchemaQuestionPanel.IsVisible = false;
        _gameDataEntityTypes.Clear();
        _gameDataEntityInstances.Clear();
        GameDataEntityTypeList.ItemsSource = Array.Empty<GameDataEntityTypeItem>();
        GameDataEntityList.ItemsSource = Array.Empty<GameDataEntityInstanceItem>();
        GameDataEntityEditor.Text = "Select an entry or add one from an entity type.";
        GameDataJsonText.Text = "Activate a schema to create a game-data document.";
        ActiveSchemaText.Text = "Activate a saved schema proposal to begin entering game data.";
        GameDataStatusText.Text = "No active schema is loaded.";
        RefreshGameDataControls();
    }

    private DesignSession? SessionFor(DesignSourceNote note) => _project?.Sessions.SingleOrDefault(session => session.SourceNotes.Any(source => source.Id == note.Id));
    private string StatusFor(DesignSourceNote note) => SessionFor(note) is not { LatestTurn: { } turn } ? "Not started" : turn.Action == RuleDraftAction.PresentDraft ? "Draft ready" : "Needs answers";
    private static string FormatDraft(StructuredRuleDraft draft) => $"{draft.Title}\n{draft.Intent}\n\n" + string.Join("\n\n", draft.Rules.Select(rule => $"{rule.Name} ({rule.Kind})\n{rule.Text}\n\nSource evidence:\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}"));
    private static string FormatRule(RuleStatement rule) => $"{rule.Name}\nType: {rule.Kind}\n\nProposed rule\n{rule.Text}\n\nSource evidence\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}";
    private static string FormatWorkingRule(WorkingRule rule) => $"{rule.Name}\nType: {rule.Kind}\n\nWorking rule\n{rule.AuthoritativeText}\n\nDerived from source drafts\n{string.Join("\n", rule.SourceDraftRules.Select(reference => $"- {reference.SessionId} / {reference.RuleId}"))}\n\nExact source evidence\n{string.Join("\n", rule.Evidence.Select(evidence => $"- {evidence.SourceNoteId} ({evidence.PassageId ?? "source text"}): {evidence.Excerpt}"))}\n\nOpen questions\n{(rule.OpenQuestions.Count == 0 ? "None" : string.Join("\n", rule.OpenQuestions.Select(question => $"- {question}")))}\n\nSchema hints\n{(rule.SchemaHints.Count == 0 ? "None" : string.Join("\n", rule.SchemaHints.Select(hint => $"- {hint.Kind}: {hint.SuggestedName} - {hint.Rationale}")))}";
    private static string FormatSchemaClarifications(IReadOnlyList<SchemaClarification>? clarifications) => clarifications is not { Count: > 0 }
        ? "None"
        : string.Join("\n\n", clarifications.Select((clarification, index) => $"{index + 1}. Question: {clarification.Question}\nAnswer: {clarification.Answer}"));
    private IDesignProjectStore ProjectStore() => _projectStore;
    private IWorkingRuleSetStore WorkingRuleStore() => _workingRuleStore;
    private IProjectSchemaProposalStore SchemaProposalStore() => _schemaProposalStore;
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
    private static string PrettyJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException) { return text; }
    }
    protected override void OnClosed(EventArgs e)
    {
        _inactivityTimer.Stop();
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
        public override string ToString() => Rule.Name;
    }
    private sealed record ProjectListItem(DesignProjectSummary Project)
    {
        public override string ToString() => Project.Name;
    }
    private sealed record GameDataEntityTypeItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }
    private sealed record GameDataEntityInstanceItem(string Id, string EntityTypeId, string EntityTypeName)
    {
        public override string ToString() => $"{EntityTypeName}: {Id}";
    }
    private sealed class CapturingClient(ILanguageModelClient inner) : ILanguageModelClient { public LlmChatResponse? Last { get; private set; } public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) => Last = await inner.CompleteAsync(request, cancellationToken); }
}
