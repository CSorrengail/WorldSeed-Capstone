using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;
using Xunit.Abstractions;

namespace WorldSeed.SchemaTranslation.Tests;

public sealed class SchemaClarificationLocalModelTests(ITestOutputHelper output)
{
    private sealed class LocalSchemaFactAttribute : FactAttribute
    {
        public LocalSchemaFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("WORLDSEED_QA_SCHEMA_MODEL") is null)
                Skip = "Explicit opt-in required for bounded local Ollama verification.";
        }
    }

    [LocalSchemaFact]
    public async Task Large_schema_translation_and_genuine_clarification_use_bounded_local_context()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var model = Environment.GetEnvironmentVariable("WORLDSEED_QA_SCHEMA_MODEL")!;
        var budget = new BudgetClient(new OllamaChatClient(http, OllamaDefaults.CreateProfile("qa", "QA Local", model)), output);
        var baseline = JsonNode.Parse("""{"id":"qa-schema","name":"QA Schema","version":"0.1","entityTypes":[],"valueTypes":[]}""")!.AsObject();
        for (var index = 0; index < 60; index++) baseline["entityTypes"]!.AsArray().Add(new JsonObject
        {
            ["metadata"] = new JsonObject { ["id"] = "existing" + index, ["name"] = "Existing " + index, ["description"] = new string('x', 1000) }, ["properties"] = new JsonArray()
        });
        var original = baseline.ToJsonString();
        var service = new SchemaTranslationService(budget);
        var selectedCase = Environment.GetEnvironmentVariable("WORLDSEED_QA_SCHEMA_CASE");
        if (selectedCase != "clarification")
        {
        var proposal = await service.TranslateAsync(new(Rules("r-location", "Location", "A location is an independently created place entry with text fields display name and narrative description."), baseline,
            StructureGoal: "Create one content type named 'Location' from these rules."));
        Assert.Equal(SchemaTranslationAction.PresentProposal, proposal.Action);
        Assert.Single(proposal.Proposal!.Schema["entityTypes"]!.AsArray());
        }
        if (selectedCase == "proposal") return;
        var clarification = await service.TranslateAsync(new(Rules("r-omen", "Omen Severity", "An omen is an independently created entry. Its severity must be exactly one entry from a fixed enumeration, but the allowed enumeration entries have not yet been defined."), baseline,
            StructureGoal: "Create one content type named 'Omen' from these rules."));
        Assert.Equal(SchemaTranslationAction.AskClarifyingQuestion, clarification.Action);
        Assert.Contains("r-omen", clarification.ClarifyingQuestion);
        Assert.Contains("Why An Answer Is Needed", clarification.ClarifyingQuestion);
        Assert.Equal(original, baseline.ToJsonString());
        output.WriteLine($"CaseSelection={selectedCase ?? "both"}; ModelCalls={budget.Calls}; SavedDataWrites=0");
    }

    private static WorkingRuleSet Rules(string id, string name, string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new("qa-rules", "qa-project", "0.1", "QA Rules", null, 1, now, now,
            [new(id, name, WorkingRuleKind.Definition, text, [], [], [], [], null, [], [])], [], [], [], []);
    }
    private sealed class BudgetClient(ILanguageModelClient inner, ITestOutputHelper output) : ILanguageModelClient
    {
        public int Calls { get; private set; }
        public async Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            var limit = int.TryParse(Environment.GetEnvironmentVariable("WORLDSEED_QA_SCHEMA_CALL_LIMIT"), out var configured) ? Math.Clamp(configured, 1, 3) : 3;
            if (++Calls > limit)
            {
                var correction = request.Messages.Last().Content;
                const string marker = "WorldSeed rejected the prior proposal for this reason: ";
                var start = correction.IndexOf(marker, StringComparison.Ordinal);
                if (start >= 0) output.WriteLine("LastRejection=" + correction[(start + marker.Length)..]);
                throw new LlmClientException("Local verification model-call budget exhausted; no further calls were made.");
            }
            var response = await inner.CompleteAsync(request, cancellationToken);
            var evidence = Environment.GetEnvironmentVariable("WORLDSEED_QA_MODEL_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                Directory.CreateDirectory(evidence);
                await File.WriteAllTextAsync(Path.Combine(evidence, "synthetic-response-" + Guid.NewGuid().ToString("N") + ".json"), response.Content, cancellationToken);
            }
            output.WriteLine($"Request={Calls}; MessageCharacters={request.Messages.Sum(message => message.Content.Length)}; InputTokens={response.Usage?.InputTokens}; OutputTokens={response.Usage?.OutputTokens}; FinishReason={response.FinishReason}");
            return response;
        }
    }
}
