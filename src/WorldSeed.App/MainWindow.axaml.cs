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
    private readonly List<DesignSourceNote> _notes = [];
    private readonly Dictionary<string, DesignSession> _sessions = new(StringComparer.Ordinal);
    private readonly List<TextBox> _answerInputs = [];
    private IReadOnlyList<string> _activeQuestions = [];

    public MainWindow() => InitializeComponent();
    protected override void OnOpened(EventArgs e) { base.OnOpened(e); _ = RefreshModelsAsync(); }

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
        SourceNotesList.ItemsSource = _notes.Select(note => new SourceListItem(note, StatusFor(note))).ToArray();
        var selectedIndex = _notes.FindIndex(note => note.Id == selectedId);
        SourceNotesList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_notes.Count > 0 ? 0 : -1);
    }

    private async void AddNotes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, FileTypeFilter = [new FilePickerFileType("Plain text") { Patterns = ["*.txt"] }] });
        var requests = files.Select(file => new PlainTextSourceRequest($"source-{Guid.NewGuid():N}", file.TryGetLocalPath() ?? "")).Where(request => request.FilePath.Length > 0).ToArray();
        if (requests.Length == 0) return;
        try { _notes.AddRange(await new PlainTextSourceImporter().ImportAsync(requests)); RefreshNotes(); StatusText.Text = $"{_notes.Count} original note(s) preserved. Each file has its own session."; }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void RemoveSelectedNote_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote; if (note is null) return;
        _notes.Remove(note); _sessions.Remove(note.Id); RefreshNotes(); ShowSelectedSource();
        StatusText.Text = $"{_notes.Count} original note(s) preserved.";
    }

    private void SourceNotesList_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ShowSelectedSource();
    private DesignSourceNote? SelectedNote => SourceNotesList.SelectedIndex is var index && index >= 0 && index < _notes.Count ? _notes[index] : null;

    private async void CreateSession_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote;
        if (note is null || ModelSelector.SelectedItem is not string model) { StatusText.Text = "Select one source file and a local model."; return; }
        if (_sessions.ContainsKey(note.Id)) { StatusText.Text = "This source already has a session. Answer its questions or review its draft."; return; }
        var service = CreateService(model, out var capture);
        await AdvanceAndSaveAsync(note, service.Start($"session-{Guid.NewGuid():N}", "local-project", "local", note), service, capture, model);
    }

    private async void SubmitAnswers_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var note = SelectedNote;
        if (note is null || !_sessions.TryGetValue(note.Id, out var session) || ModelSelector.SelectedItem is not string model) return;
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
            var result = await service.AdvanceAsync(session); _sessions[note.Id] = result; await Store().SaveAsync(result); RefreshNotes(); ShowSelectedSource();
            StatusText.Text = result.LatestTurn!.Action == RuleDraftAction.PresentDraft ? "Validated draft saved locally. Review each rule and its exact source excerpt." : "Clarification needed; the session was saved locally. Answer the questions in the Conversation panel.";
        }
        catch (RuleDraftFormatException ex) { StatusText.Text = "The model returned text WorldSeed could not safely use. Nothing was saved."; DraftText.Text = $"Format detail: {ex.Message}\n\nRaw model output (for diagnosis):\n{Preview(capture.Last?.Content)}"; }
        catch (Exception ex) { StatusText.Text = "Drafting failed before a usable result was produced."; DraftText.Text = $"{ex.GetType().Name}: {ex.Message}"; }
        finally { DraftButton.IsEnabled = true; SubmitAnswersButton.IsEnabled = true; }
    }

    private void ShowSelectedSource()
    {
        QuestionsPanel.Children.Clear(); _answerInputs.Clear(); _activeQuestions = []; SubmitAnswersButton.IsVisible = false;
        var note = SelectedNote;
        if (note is null) { SourceText.Text = "Select a source file to inspect its original note."; QuestionHeading.Text = ""; DraftText.Text = "A validated draft will appear here with its source excerpts."; return; }
        SourceText.Text = $"Original note — {note.Origin?.DisplayName ?? note.Id}\n\n{note.OriginalText}";
        if (!_sessions.TryGetValue(note.Id, out var session) || session.LatestTurn is null) { QuestionHeading.Text = "No session yet"; DraftText.Text = "Start this source when ready. It will remain separate from other imported files."; return; }
        var turn = session.LatestTurn;
        if (turn.Action == RuleDraftAction.AskClarifyingQuestion)
        {
            _activeQuestions = turn.ClarifyingQuestions.Count > 0 ? turn.ClarifyingQuestions : turn.ClarifyingQuestion is null ? [] : [turn.ClarifyingQuestion];
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
        QuestionHeading.Text = "Draft ready"; DraftText.Text = FormatDraft(turn.Draft!);
    }

    private string StatusFor(DesignSourceNote note) => !_sessions.TryGetValue(note.Id, out var session) || session.LatestTurn is null ? "Not started" : session.LatestTurn.Action == RuleDraftAction.PresentDraft ? "Draft ready" : "Needs answers";
    private static string FormatDraft(StructuredRuleDraft draft) => $"{draft.Title}\n{draft.Intent}\n\n" + string.Join("\n\n", draft.Rules.Select(rule => $"{rule.Name} ({rule.Kind})\n{rule.Text}\n\nSource evidence:\n{string.Join("\n", rule.SourceSupport.Select(support => $"• {support.SourceNoteId}: “{support.Excerpt}”"))}"));
    private static JsonDesignSessionStore Store() => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed", "sessions"));
    private static string Preview(string? text) => string.IsNullOrWhiteSpace(text) ? "No model response was received." : text.Length > 3000 ? text[..3000] + "\n[truncated]" : text;
    protected override void OnClosed(EventArgs e) { _http.Dispose(); base.OnClosed(e); }

    private sealed record SourceListItem(DesignSourceNote Note, string Status) { public override string ToString() => $"{Note.Origin?.DisplayName ?? Note.Id} — {Status}"; }
    private sealed class CapturingClient(ILanguageModelClient inner) : ILanguageModelClient { public LlmChatResponse? Last { get; private set; } public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default) => Last = await inner.CompleteAsync(request, cancellationToken); }
}
