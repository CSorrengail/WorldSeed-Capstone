using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.WorkingRules;

var modelName = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? "gemma3:12b";
var runSuite = args.Contains("--suite", StringComparer.OrdinalIgnoreCase);
using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
var liveClient = new OllamaChatClient(httpClient, OllamaDefaults.CreateProfile("end-to-end-local", "End-to-end local model", modelName));
var scenarios = runSuite ? EndToEndScenario.ReliabilitySuite : [EndToEndScenario.CompleteRule];
var failed = false;

Console.WriteLine($"Running {(runSuite ? "the reliability suite" : "a local draft-to-working-rules test")} with '{modelName}'.");
Console.WriteLine();

foreach (var scenario in scenarios)
{
    Console.WriteLine($"=== {scenario.Name} ===");
    Console.WriteLine("Input source note: " + scenario.SourceText);
    var recordingClient = new RecordingLanguageModelClient(liveClient);
    var sessionService = new DesignSessionService(new RuleDraftingService(recordingClient));
    var source = new DesignSourceNote("source-note-001", scenario.SourceText, DateTimeOffset.UtcNow);
    var session = sessionService.Start($"end-to-end-{scenario.Id}", "end-to-end-project", "end-to-end-local", source);

    try
    {
        var advanced = await sessionService.AdvanceAsync(session);
        var turn = advanced.LatestTurn!;
        Console.WriteLine("Raw model response:");
        Console.WriteLine(recordingClient.LastResponse?.Content);
        Console.WriteLine();
        Console.WriteLine("WorldSeed validation result:");
        if (turn.Action != scenario.ExpectedAction)
        {
            Console.WriteLine($"Accepted, but test expectation failed: expected {scenario.ExpectedAction}; received {turn.Action}.");
            failed = true;
        }
        else if (turn.Action == RuleDraftAction.AskClarifyingQuestion)
        {
            Console.WriteLine("Accepted focused clarification: " + turn.ClarifyingQuestion);
        }
        else
        {
            var draft = turn.Draft!;
            Console.WriteLine($"Accepted draft '{draft.Title}' with {draft.Rules.Count} rule statements.");
            foreach (var rule in draft.Rules)
                Console.WriteLine($"- {rule.Id} [{rule.Kind}]: {rule.Text} (sources: {string.Join(", ", rule.SourceNoteIds)})");
            Console.WriteLine($"Assumptions: {draft.Assumptions.Count}; open questions: {draft.OpenQuestions.Count}; exclusions: {draft.Exclusions.Count}.");

            var project = DesignProjectService.AddSources(DesignProjectService.Create("end-to-end-project", "End-to-end project"), [source]);
            project = DesignProjectService.SaveSession(project, advanced);
            var formalizationClient = new RecordingLanguageModelClient(liveClient);
            try
            {
                var proposal = await new WorkingRuleSetFormalizationService(formalizationClient).CreateProposalAsync(project);
                Console.WriteLine($"Accepted Working Rule Set '{proposal.RuleSet.Name}' with {proposal.RuleSet.Rules.Count} project rule(s).");
                foreach (var rule in proposal.RuleSet.Rules)
                    Console.WriteLine($"- {rule.Id} [{rule.Kind}]: {rule.AuthoritativeText} (draft evidence: {string.Join(", ", rule.SourceDraftRules.Select(reference => reference.SessionId + "/" + reference.RuleId))})");
            }
            catch (Exception exception) when (exception is WorkingRuleSetFormatException or LlmClientException or HttpRequestException or OperationCanceledException)
            {
                Console.Error.WriteLine("WorldSeed rejected or could not obtain the working-rule proposal: " + exception.Message);
                if (formalizationClient.LastResponse is not null)
                {
                    Console.Error.WriteLine("Raw working-rule response retained for diagnosis:");
                    Console.Error.WriteLine(formalizationClient.LastResponse.Content);
                }
                failed = true;
            }
        }
    }
    catch (Exception exception) when (exception is RuleDraftFormatException or LlmClientException or HttpRequestException or OperationCanceledException)
    {
        Console.Error.WriteLine("WorldSeed rejected or could not obtain the model response: " + exception.Message);
        if (recordingClient.LastResponse is not null)
        {
            Console.Error.WriteLine("Raw model response retained for diagnosis:");
            Console.Error.WriteLine(recordingClient.LastResponse.Content);
        }
        failed = true;
    }
    Console.WriteLine();
}

Environment.ExitCode = failed ? 1 : 0;

file sealed record EndToEndScenario(string Id, string Name, string SourceText, RuleDraftAction ExpectedAction)
{
    public static readonly EndToEndScenario CompleteRule = new(
        "complete-rule",
        "Complete procedural rule",
        "In this travel-focused fantasy game, an expedition begins when its guide reveals three route cards. Each traveler chooses one revealed route. A traveler who chooses the same route as another traveler gains one shared-supply token; otherwise, they gain one personal-supply token. After all choices are made, discard the unchosen route cards. This is the complete rule for the first test.",
        RuleDraftAction.PresentDraft);

    public static readonly EndToEndScenario AmbiguousRule = new(
        "ambiguous-rule",
        "Ambiguous rule requiring clarification",
        "When a character becomes exhausted, they mark one fatigue. Exhausted characters face a harder journey. This is all the designer has decided so far.",
        RuleDraftAction.AskClarifyingQuestion);

    public static readonly EndToEndScenario NaturalLanguageRule = new(
        "natural-language-rule",
        "Facilitator-driven natural-language rule",
        "At the end of a journey, the Lighthouse asks a traveler to make a promise. The facilitator decides the promise's consequence in the context of the story. There is intentionally no fixed mechanical effect, table, randomizer, or procedure beyond this wording.",
        RuleDraftAction.PresentDraft);

    public static IReadOnlyList<EndToEndScenario> ReliabilitySuite => [CompleteRule, AmbiguousRule, NaturalLanguageRule];
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
