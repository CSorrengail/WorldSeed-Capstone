using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.Content.Tests;

public class ContentProposalGenerationServiceTests
{
    [Fact]
    public void Parses_only_the_requested_entity_type()
    {
        var candidate = ContentProposalGenerationService.ParseCandidate("""{"candidate":{"id":"wisp","entityTypeId":"creature","values":{"name":"Wisp"}}}""", "creature");
        Assert.Equal("wisp", candidate["id"]!.GetValue<string>());
    }

    [Fact]
    public void Rejects_wrong_entity_type_and_non_json()
    {
        Assert.Throws<ContentProposalGenerationFormatException>(() => ContentProposalGenerationService.ParseCandidate("not json", "creature"));
        Assert.Throws<ContentProposalGenerationFormatException>(() => ContentProposalGenerationService.ParseCandidate("""{"candidate":{"id":"item","entityTypeId":"item","values":{}}}""", "creature"));
    }

    [Fact]
    public async Task Prompt_distinguishes_enumeration_values_from_schema_descriptors()
    {
        var model = new CapturingModel("""{"candidate":{"id":"chainmail","entityTypeId":"armorClass","values":{"weightCategory":"heavy"}},"schemaGaps":[{"requestedConcept":"armor value","reason":"The active schema has no armor-value property.","suggestedSchemaChange":"Add an armorValue property supported by a game rule."}]}""");
        var schema = JsonNode.Parse("""
            {"entityTypes":[{"metadata":{"id":"armorClass"},"properties":[{"metadata":{"id":"weightCategory"},"valueType":{"kind":"definedValueType","valueTypeId":"armorWeightCategory"},"required":true,"nullable":false}]}],"valueTypes":[{"metadata":{"id":"armorWeightCategory"},"kind":"enumeration","entries":[{"id":"light"},{"id":"medium"},{"id":"heavy"}]}]}
            """)!.AsObject();
        var rule = new WorkingRule("rule-1", "Armor Weight", WorkingRuleKind.Rule, "Chainmail is heavy armor.", [], [], [], [], null, [], []);

        var result = await new ContentProposalGenerationService(model).GenerateAsync(new ContentProposalGenerationRequest("project", "armorClass", schema, [rule], "The lightest heavy armor should be chainmail, which also has the worst armor value for the weight."));

        var prompt = string.Join("\n", model.Request!.Messages.Select(message => message.Content));
        Assert.Contains("values.weightCategory (required): one JSON string entry ID", prompt);
        Assert.Contains("\"light\", \"medium\", \"heavy\"", prompt);
        Assert.Contains("\"weightCategory\":\"light\"", prompt);
        Assert.Contains("never put kind, valueTypeId", prompt);
        Assert.Contains("Report every requested attribute, comparison, or behavior", prompt);
        Assert.Equal("heavy", result.Candidate["values"]!["weightCategory"]!.GetValue<string>());
        Assert.Equal("armor value", Assert.Single(result.SchemaGaps!).RequestedConcept);
    }

    [Fact]
    public void Rejects_malformed_schema_gap_but_preserves_legacy_unreported_state()
    {
        var legacy = ContentProposalGenerationService.ParseResponse("""{"candidate":{"id":"wisp","entityTypeId":"creature","values":{}}}""", "creature");
        Assert.Null(legacy.SchemaGaps);
        Assert.Throws<ContentProposalGenerationFormatException>(() => ContentProposalGenerationService.ParseResponse("""{"candidate":{"id":"wisp","entityTypeId":"creature","values":{}},"schemaGaps":[{"requestedConcept":"armor"}]}""", "creature"));
    }

    private sealed class CapturingModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse(content, "test", null));
        }
    }
}
