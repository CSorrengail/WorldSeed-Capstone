using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.WorkingRules;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;

var contentGenerationOption = Array.FindIndex(args, argument => string.Equals(argument, "--verify-content-generation", StringComparison.OrdinalIgnoreCase));
if (contentGenerationOption >= 0)
{
    var contentModel = contentGenerationOption + 1 < args.Length ? args[contentGenerationOption + 1] : "gemma3:12b";
    await ContentGenerationIntegrationCheck.RunAsync(contentModel);
    return;
}

var contentApiOption = Array.FindIndex(args, argument => string.Equals(argument, "--verify-content-api", StringComparison.OrdinalIgnoreCase));
if (contentApiOption >= 0)
{
    if (contentApiOption + 1 >= args.Length || !Uri.TryCreate(args[contentApiOption + 1], UriKind.Absolute, out var apiBaseUri))
        throw new ArgumentException("--verify-content-api requires an absolute loopback API URI.");

    await ContentApiIntegrationCheck.RunAsync(apiBaseUri);
    return;
}

if (args.Contains("--creature-mvp", StringComparer.OrdinalIgnoreCase))
{
    var output = args.SkipWhile(argument => !string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault()
        ?? Path.Combine("artifacts", "mvp-demonstration", "creature-mvp-results.json");
    var results = new JsonArray();
    foreach (var name in new[] { "emberbound.contract.json", "quiet-horizons.contract.json" })
        results.Add(CreatureMvpRunner.RunCreatureContract(name));
    var report = new JsonObject { ["format"] = "worldseed-creature-mvp-results-v1", ["systems"] = results };
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    await File.WriteAllTextAsync(output, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Creature MVP demonstration passed for {results.Count} systems. Results: {Path.GetFullPath(output)}");
    return;
}

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

file static class ContentApiIntegrationCheck
{
    public static async Task RunAsync(Uri apiBaseUri)
    {
        if (!apiBaseUri.IsLoopback) throw new ArgumentException("The content API check accepts only a loopback URI.", nameof(apiBaseUri));
        if (apiBaseUri.Port == 5080) throw new ArgumentException("The content API check refuses WorldSeed's normal development port 5080. Use an isolated ephemeral API port.", nameof(apiBaseUri));

        var baseUri = apiBaseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? apiBaseUri : new Uri(apiBaseUri.AbsoluteUri + "/");
        using var client = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(20) };
        var suffix = Guid.NewGuid().ToString("N");
        var projectId = "qa-content-api-" + suffix;
        var proposalId = "proposal-" + suffix;
        var projectCreated = false;

        try
        {
            using (var health = await client.GetAsync("health")) await RequireSuccessAsync(health, "health check");

            var project = DesignProjectService.Create(projectId, "Disposable Content API Check", "Created by the isolated WorldSeed integration verifier.");
            using (var response = await client.PutAsJsonAsync($"api/projects/{projectId}", project))
            {
                await RequireSuccessAsync(response, "project PUT");
                projectCreated = true;
            }

            var candidate = JsonNode.Parse("""
                {
                  "id": "ember-wolf",
                  "entityTypeId": "monster",
                  "values": {
                    "name": "Ember Wolf",
                    "stats": { "health": 18, "armor": 2 },
                    "traits": ["pack hunter", "fire resistant"]
                  }
                }
                """)!.AsObject();
            var proposal = new ContentProposal(proposalId, projectId, "monster", candidate, "schema-1", 3, "rules-1", 7, 1, DateTimeOffset.UtcNow);
            using (var response = await client.PutAsJsonAsync($"api/projects/{projectId}/content-proposals/{proposalId}", proposal))
                await RequireSuccessAsync(response, "proposal PUT");

            var revisionRequest = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.RevisionRequested, "Reduce its armor and distinguish its revised name.", DateTimeOffset.UtcNow);
            using (var response = await client.PostAsJsonAsync($"api/projects/{projectId}/content-proposals/{proposalId}/decisions", revisionRequest))
                await RequireSuccessAsync(response, "revision-request decision POST");

            var replacementCandidate = (JsonObject)candidate.DeepClone();
            replacementCandidate["id"] = "ember-wolf-revised";
            replacementCandidate["values"]!["name"] = "Ember Wolf Revised";
            replacementCandidate["values"]!["stats"]!["armor"] = 1;
            var replacement = ContentProposalLifecycleService.Create(projectId, "monster", replacementCandidate, "schema-1", 3, "rules-1", 7, DateTimeOffset.UtcNow, proposal);
            using (var response = await client.PutAsJsonAsync($"api/projects/{projectId}/content-proposals/{replacement.Id}", replacement))
                await RequireSuccessAsync(response, "replacement proposal PUT");
            var approval = ContentProposalLifecycleService.Decide(replacement, ContentProposalDecisionKind.Approved, "Approved after revision.", DateTimeOffset.UtcNow);
            using (var response = await client.PostAsJsonAsync($"api/projects/{projectId}/content-proposals/{replacement.Id}/decisions", approval))
                await RequireSuccessAsync(response, "approval decision POST");

            var rejectedCandidate = (JsonObject)candidate.DeepClone();
            rejectedCandidate["id"] = "ember-wolf-rejected";
            var rejected = ContentProposalLifecycleService.Create(projectId, "monster", rejectedCandidate, "schema-1", 3, "rules-1", 7, DateTimeOffset.UtcNow);
            using (var response = await client.PutAsJsonAsync($"api/projects/{projectId}/content-proposals/{rejected.Id}", rejected))
                await RequireSuccessAsync(response, "rejected proposal PUT");
            var rejection = ContentProposalLifecycleService.Decide(rejected, ContentProposalDecisionKind.Rejected, "Does not fit this project.", DateTimeOffset.UtcNow);
            using (var response = await client.PostAsJsonAsync($"api/projects/{projectId}/content-proposals/{rejected.Id}/decisions", rejection))
                await RequireSuccessAsync(response, "rejection decision POST");

            using var workspaceResponse = await client.GetAsync($"api/projects/{projectId}/content-proposals");
            await RequireSuccessAsync(workspaceResponse, "workspace GET");
            var workspace = JsonNode.Parse(await workspaceResponse.Content.ReadAsStringAsync())?.AsObject()
                ?? throw new InvalidOperationException("The content workspace response was not a JSON object.");
            var storedProposal = workspace["proposals"]?.AsArray().OfType<JsonObject>().SingleOrDefault(item => item["id"]?.GetValue<string>() == proposalId)
                ?? throw new InvalidOperationException("The saved proposal was not returned by the content workspace.");
            var storedReplacement = workspace["proposals"]?.AsArray().OfType<JsonObject>().SingleOrDefault(item => item["id"]?.GetValue<string>() == replacement.Id)
                ?? throw new InvalidOperationException("The replacement proposal was not returned by the content workspace.");
            var decisions = workspace["decisions"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
            var storedValues = storedProposal["candidate"]?["values"]?.AsObject()
                ?? throw new InvalidOperationException("The saved proposal candidate lost its values object.");

            if (storedValues["stats"]?["health"]?.GetValue<int>() != 18 || storedValues["traits"]?.AsArray().Count != 2)
                throw new InvalidOperationException("Nested content proposal fields did not round-trip through the API and MongoDB.");
            if (storedReplacement["replacesProposalId"]?.GetValue<string>() != proposalId || storedReplacement["version"]?.GetValue<int>() != 2)
                throw new InvalidOperationException("The replacement proposal lost its base-proposal or version linkage.");
            if (!HasDecision(decisions, proposalId, ContentProposalDecisionKind.RevisionRequested)
                || !HasDecision(decisions, replacement.Id, ContentProposalDecisionKind.Approved)
                || !HasDecision(decisions, rejected.Id, ContentProposalDecisionKind.Rejected))
                throw new InvalidOperationException("The complete designer decision history did not round-trip through the API and MongoDB.");

            var exportSchema = new ActiveGameSchema("schema-1", projectId, "0.1", "Integration Schema", "{\"id\":\"integration-schema\",\"version\":\"1\"}", "proposal", "rules-1", 7, 3, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var export = JsonNode.Parse(ContentExportService.ExportApproved(replacement, approval, exportSchema))!.AsObject();
            if (export["proposalId"]?.GetValue<string>() != replacement.Id || export["proposalVersion"]?.GetValue<int>() != 2 || export["designerDecision"]?["kind"]?.GetValue<string>() != "Approved")
                throw new InvalidOperationException("The approved replacement export lost proposal/version/decision provenance.");

            Console.WriteLine($"Content API integration check passed at {baseUri.GetLeftPart(UriPartial.Authority)}. Nested proposal, revision/approval/rejection history, replacement linkage, and approved export provenance all verified.");
        }
        finally
        {
            if (projectCreated)
            {
                try
                {
                    using var response = await client.DeleteAsync($"api/projects/{projectId}");
                    await RequireSuccessAsync(response, "project cleanup DELETE");
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Disposable project cleanup failed for '{projectId}': {exception.Message}");
                }
            }
        }
    }

    private static async Task RequireSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Content API {operation} returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }

    private static bool HasDecision(IEnumerable<JsonObject> decisions, string proposalId, ContentProposalDecisionKind kind) =>
        decisions.Any(item => item["proposalId"]?.GetValue<string>() == proposalId && item["kind"]?.GetValue<int>() == (int)kind);
}

file static class ContentGenerationIntegrationCheck
{
    public static async Task RunAsync(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("--verify-content-generation requires a local Ollama model name.");

        var contract = JsonNode.Parse(File.ReadAllText(FindFixture("emberbound.contract.json")))!.AsObject();
        var schema = contract["gameSchema"]!.AsObject();
        var fixtureRules = contract["workingRuleSet"]!["rules"]!.AsArray().OfType<JsonObject>();
        var rules = fixtureRules.Select(rule => new WorkingRule(
            rule["id"]!.GetValue<string>(),
            rule["id"]!.GetValue<string>(),
            Enum.Parse<WorkingRuleKind>(rule["kind"]!.GetValue<string>(), ignoreCase: true),
            rule["text"]!.GetValue<string>(),
            [], [], [], [], null, [], [])).ToArray();

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var recording = new RecordingLanguageModelClient(new OllamaChatClient(http, OllamaDefaults.CreateProfile("content-generation-check", "Content Generation Check", model)));
        try
        {
            var result = await new ContentProposalGenerationService(recording).GenerateAsync(new ContentProposalGenerationRequest(
                "qa-content-generation",
                "creature",
                schema,
                rules,
                "Create one original tactical creature named Coal Stalker. Keep every value within the supplied rules and schema. Use the skirmisher role."));

            var now = DateTimeOffset.UtcNow;
            var activeSchema = new ActiveGameSchema("qa-active-schema", "qa-content-generation", "0.1", "Emberbound", schema.ToJsonString(), "fixture", "working-rules-emberbound-demo", 1, 1, now, now);
            var gameData = GameDataService.CreateEmpty(activeSchema, now);
            var document = JsonNode.Parse(gameData.DataJson)!.AsObject();
            document["entityInstances"]!.AsArray().Add(result.Candidate.DeepClone());
            var validation = GameDataService.Validate(activeSchema, gameData with { DataJson = document.ToJsonString() });
            if (!validation.IsValid)
                throw new InvalidOperationException("The generated candidate failed deterministic schema validation: " + string.Join(" ", validation.Issues.Select(issue => $"{issue.Path}: {issue.Message}")));

            Console.WriteLine($"Local content generation check passed with '{model}'.");
            Console.WriteLine(result.Candidate.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            if (recording.LastResponse is not null)
            {
                Console.Error.WriteLine("Raw local-model response:");
                Console.Error.WriteLine(recording.LastResponse.Content);
            }
            throw;
        }
    }

    private static string FindFixture(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "qa", "fixtures", "mvp", "creatures", fileName);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Creature fixture was not found.");
    }
}

file static class CreatureMvpRunner
{
public static JsonObject RunCreatureContract(string fileName)
{
    var contract = JsonNode.Parse(File.ReadAllText(FindFixture(fileName)))!.AsObject();
    var system = contract["system"]!.AsObject();
    var projectId = system["id"]!.GetValue<string>();
    var rules = contract["workingRuleSet"]!.AsObject();
    var schemaJson = contract["gameSchema"]!.ToJsonString();
    var schema = new ActiveGameSchema("active-" + projectId, projectId, "0.1", system["name"]!.GetValue<string>(), schemaJson, "fixture-schema", rules["id"]!.GetValue<string>(), rules["revision"]!.GetValue<int>(), 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    var profile = BuildProfile(contract["balanceProfile"]!.AsObject());
    var outcomes = new JsonArray();
    foreach (var fixture in contract["fixtures"]!.AsObject())
    {
        var dataJson = fixture.Value!.AsObject()["data"]!.AsObject();
        var candidate = (JsonObject)dataJson["entityInstances"]!.AsArray()[0]!.DeepClone();
        var proposal = ContentProposalLifecycleService.Create(projectId, "creature", candidate, schema.Id, schema.Revision, rules["id"]!.GetValue<string>(), rules["revision"]!.GetValue<int>(), DateTimeOffset.UtcNow);
        var gameData = new ProjectGameData("fixture-" + fixture.Key, projectId, "0.1", schema.Id, schema.Revision, dataJson.ToJsonString(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var evaluation = new ContentEvaluationService().Evaluate(new ContentEvaluationRequest(schema, gameData, profile));
        var expected = fixture.Value!.AsObject();
        if (evaluation.MechanicalViolations.Count != expected["expectedMechanicalViolations"]!.AsArray().Count || evaluation.BalanceConcerns.Count != expected["expectedBalanceConcerns"]!.AsArray().Count)
            throw new InvalidOperationException($"Fixture outcome mismatch for {projectId}/{fixture.Key}.");
        var decisionKind = fixture.Key == "valid" ? ContentProposalDecisionKind.Approved : fixture.Key == "mechanicallyInvalid" ? ContentProposalDecisionKind.Rejected : ContentProposalDecisionKind.RevisionRequested;
        var decision = ContentProposalLifecycleService.Decide(proposal, decisionKind, decisionKind == ContentProposalDecisionKind.RevisionRequested ? "Reduce the demonstrated pressure." : null, DateTimeOffset.UtcNow);
        JsonNode? export = decisionKind == ContentProposalDecisionKind.Approved ? JsonNode.Parse(ContentExportService.ExportApproved(proposal, decision, schema)) : null;
        outcomes.Add(new JsonObject { ["fixture"] = fixture.Key, ["proposalId"] = proposal.Id, ["mechanicalViolations"] = evaluation.MechanicalViolations.Count, ["balanceConcerns"] = evaluation.BalanceConcerns.Count, ["designerDecision"] = decision.Kind.ToString(), ["exported"] = export is not null });
    }
    return new JsonObject { ["id"] = projectId, ["name"] = system["name"]!.DeepClone(), ["outcomes"] = outcomes };
}

private static ContentBalanceProfile BuildProfile(JsonObject source)
{
    var references = source["referenceCreatures"]!.AsArray().OfType<JsonObject>().ToArray();
    var signals = source["advisorySignals"]!.AsArray().OfType<JsonObject>().Select(signal =>
    {
        var propertyId = signal["propertyId"]!.GetValue<string>();
        var values = references.Where(reference => reference["values"]![propertyId] is not null).Select(reference => new BalanceReference(reference["id"]!.GetValue<string>(), reference["label"]!.GetValue<string>(), decimal.Parse(reference["values"]![propertyId]!.ToJsonString()))).ToArray();
        return new BalanceSignal(signal["id"]!.GetValue<string>(), propertyId, signal["measure"]?.GetValue<string>() == "collectionCount" ? BalanceMeasure.CollectionCount : BalanceMeasure.NumericValue, signal["minimumReferenceValue"]?.GetValue<decimal?>(), signal["maximumReferenceValue"]?.GetValue<decimal?>(), signal["message"]!.GetValue<string>(), values);
    }).ToArray();
    return new ContentBalanceProfile(source["id"]!.GetValue<string>(), "creature", source["sourceWorkingRuleIds"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray(), signals);
}

private static string FindFixture(string fileName)
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "qa", "fixtures", "mvp", "creatures", fileName);
        if (File.Exists(candidate)) return candidate;
    }
    throw new FileNotFoundException("Creature fixture was not found.");
}
}
