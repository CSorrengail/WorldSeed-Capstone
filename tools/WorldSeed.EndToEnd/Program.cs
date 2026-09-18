using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;

var modelName = args.FirstOrDefault() ?? "qwen3:8b";
using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
var liveClient = new OllamaChatClient(httpClient, OllamaDefaults.CreateProfile("end-to-end-local", "End-to-end local model", modelName));
var recordingClient = new RecordingLanguageModelClient(liveClient);
var sessionService = new DesignSessionService(new RuleDraftingService(recordingClient));
var source = new DesignSourceNote(
    "source-note-001",
    "In this travel-focused fantasy game, an expedition begins when its guide reveals three route cards. Each traveler chooses one revealed route. A traveler who chooses the same route as another traveler gains one shared-supply token; otherwise, they gain one personal-supply token. After all choices are made, discard the unchosen route cards. This is the complete rule for the first test.",
    DateTimeOffset.UtcNow);
var session = sessionService.Start("end-to-end-session-001", "end-to-end-project", "end-to-end-local", source);

Console.WriteLine($"Running a local rule-drafting test with '{modelName}'.");
Console.WriteLine("Input source note: " + source.OriginalText);
Console.WriteLine();

try
{
    var advanced = await sessionService.AdvanceAsync(session);
    Console.WriteLine("Raw model response:");
    Console.WriteLine(recordingClient.LastResponse?.Content);
    Console.WriteLine();
    Console.WriteLine("WorldSeed validation result:");
    if (advanced.LatestTurn!.Action == RuleDraftAction.AskClarifyingQuestion)
    {
        Console.WriteLine("Accepted focused clarification: " + advanced.LatestTurn.ClarifyingQuestion);
    }
    else
    {
        var draft = advanced.LatestTurn.Draft!;
        Console.WriteLine($"Accepted draft '{draft.Title}' with {draft.Rules.Count} rule statements.");
        foreach (var rule in draft.Rules)
            Console.WriteLine($"- {rule.Id} [{rule.Kind}]: {rule.Text} (sources: {string.Join(", ", rule.SourceNoteIds)})");
        Console.WriteLine($"Assumptions: {draft.Assumptions.Count}; open questions: {draft.OpenQuestions.Count}; exclusions: {draft.Exclusions.Count}.");
    }
}
catch (Exception exception) when (exception is RuleDraftFormatException or LlmClientException or HttpRequestException or OperationCanceledException)
{
    Console.Error.WriteLine("WorldSeed rejected or could not obtain the model response: " + exception.Message);
    if (recordingClient.LastResponse is not null)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Raw model response retained for diagnosis:");
        Console.Error.WriteLine(recordingClient.LastResponse.Content);
    }
    Environment.ExitCode = 1;
}

file sealed class RecordingLanguageModelClient(ILanguageModelClient inner) : ILanguageModelClient
{
    public LlmChatResponse? LastResponse { get; private set; }

    public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        LastResponse = await inner.CompleteAsync(request, cancellationToken);
        return LastResponse;
    }
}
