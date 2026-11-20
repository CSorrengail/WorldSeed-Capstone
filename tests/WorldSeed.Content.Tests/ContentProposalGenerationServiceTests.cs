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

    [Fact]
    public async Task Uses_bounded_canonical_and_decision_context_and_records_exact_ids()
    {
        var model = new CapturingModel("""{"candidate":{"id":"glass-fox","entityTypeId":"creature","values":{"name":"Glass Fox","role":"scout"}},"schemaGaps":[]}""");
        var schema = JsonNode.Parse("""{"entityTypes":[{"metadata":{"id":"creature"},"properties":[{"metadata":{"id":"name"},"valueType":{"kind":"primitive","name":"string"}},{"metadata":{"id":"role"},"valueType":{"kind":"primitive","name":"string"}}]}]}""")!.AsObject();
        var rule = new WorkingRule("rule-1", "Creature Rule", WorkingRuleKind.Rule, "Creatures have a role.", [], [], [], [], null, [], []);
        var canonical = JsonNode.Parse("""{"id":"coal-stalker","entityTypeId":"creature","values":{"name":"Coal Stalker","role":"ambusher"}}""")!.AsObject();
        var decision = new ContentGenerationDecisionContext("decision-1", ContentProposalDecisionKind.Rejected, "Avoid another fire creature.");

        var result = await new ContentProposalGenerationService(model).GenerateAsync(new ContentProposalGenerationRequest("project", "creature", schema, [rule], "Suggest something new.", [canonical], [decision]));

        var prompt = string.Join("\n", model.Request!.Messages.Select(message => message.Content));
        Assert.Contains("coal-stalker", prompt);
        Assert.Contains("Avoid another fire creature", prompt);
        Assert.Equal(["coal-stalker"], result.GenerationContext!.CanonicalEntityIds);
        Assert.Equal(["decision-1"], result.GenerationContext.DesignerDecisionIds);
        Assert.Empty(result.SimilarityWarnings!);
    }

    [Fact]
    public async Task Rejects_duplicate_id_and_flags_obvious_value_similarity()
    {
        var schema = JsonNode.Parse("""{"entityTypes":[{"metadata":{"id":"creature"},"properties":[]}]}""")!.AsObject();
        var rule = new WorkingRule("rule-1", "Creature Rule", WorkingRuleKind.Rule, "Creatures exist.", [], [], [], [], null, [], []);
        var canonical = JsonNode.Parse("""{"id":"coal-stalker","entityTypeId":"creature","values":{"name":"Coal Stalker","role":"ambusher"}}""")!.AsObject();
        var duplicate = new ContentProposalGenerationService(new CapturingModel("""{"candidate":{"id":"COAL-STALKER","entityTypeId":"creature","values":{"name":"Other"}},"schemaGaps":[]}"""));
        await Assert.ThrowsAsync<ContentProposalGenerationFormatException>(() => duplicate.GenerateAsync(new ContentProposalGenerationRequest("project", "creature", schema, [rule], "New creature.", [canonical])));

        var similar = new ContentProposalGenerationService(new CapturingModel("""{"candidate":{"id":"new-id","entityTypeId":"creature","values":{"name":"Coal Stalker","role":"scout"}},"schemaGaps":[]}"""));
        var result = await similar.GenerateAsync(new ContentProposalGenerationRequest("project", "creature", schema, [rule], "New creature.", [canonical]));

        Assert.Contains("repeats the canonical name value", Assert.Single(result.SimilarityWarnings!).Reason);
    }

    [Fact]
    public async Task Runtime_contract_explains_nested_record_fields_constraints_and_case_sensitive_entries()
    {
        var schema = JsonNode.Parse("""
            {"entityTypes":[{"metadata":{"id":"prompt"},"properties":[
              {"metadata":{"id":"card"},"valueType":{"kind":"definedValueType","valueTypeId":"card-alias"},"required":true,"nullable":false},
              {"metadata":{"id":"cards"},"valueType":{"kind":"collection","itemType":{"kind":"definedValueType","valueTypeId":"card"}},"required":false,"nullable":false}]}],
             "valueTypes":[
              {"metadata":{"id":"suit"},"kind":"enumeration","entries":[{"id":"MixedCase","name":"Suit"}]},
              {"metadata":{"id":"card-alias"},"kind":"alias","targetType":{"kind":"definedValueType","valueTypeId":"card"}},
              {"metadata":{"id":"card"},"kind":"record","properties":[
                {"metadata":{"id":"card.suit"},"valueType":{"kind":"definedValueType","valueTypeId":"suit"},"required":true,"nullable":false},
                {"metadata":{"id":"card.value"},"valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false,"constraints":{"minimum":1,"maximum":13}}]}]}
            """)!.AsObject();
        var model = new CapturingModel("""{"candidate":{"id":"new-prompt","entityTypeId":"prompt","values":{"card":{"card.suit":"MixedCase","card.value":2}}},"schemaGaps":[]}""");
        var rule = new WorkingRule("rule", "Cards", WorkingRuleKind.Rule, "Cards have a suit and value.", [], [], [], [], null, [], []);

        await new ContentProposalGenerationService(model).GenerateAsync(new("project", "prompt", schema, [rule], "Create a card prompt."));

        var contract = model.Request!.Messages[1].Content;
        Assert.Contains("card.suit", contract);
        Assert.Contains("card.value", contract);
        Assert.Contains("\"maximum\":13", contract);
        Assert.Contains("\"MixedCase\"", contract);
        Assert.DoesNotContain("mixedcase", contract);
        var example = JsonNode.Parse(contract[(contract.LastIndexOf('\n') + 1)..])!;
        Assert.Equal("MixedCase", example["candidate"]!["values"]!["card"]!["card.suit"]!.GetValue<string>());
    }

    [Fact]
    public async Task Recursive_value_definitions_produce_a_bounded_contract()
    {
        var schema = JsonNode.Parse("""{"entityTypes":[{"metadata":{"id":"prompt"},"properties":[{"metadata":{"id":"loop"},"valueType":{"kind":"definedValueType","valueTypeId":"loop"},"required":true}]}],"valueTypes":[{"metadata":{"id":"loop"},"kind":"alias","targetType":{"kind":"definedValueType","valueTypeId":"loop"}}]}""")!.AsObject();
        var model = new CapturingModel("""{"candidate":{"id":"new-prompt","entityTypeId":"prompt","values":{}},"schemaGaps":[]}""");
        var rule = new WorkingRule("rule", "Cards", WorkingRuleKind.Rule, "Cards exist.", [], [], [], [], null, [], []);

        await new ContentProposalGenerationService(model).GenerateAsync(new("project", "prompt", schema, [rule], "Create a prompt."));

        Assert.Contains("request clarification", model.Request!.Messages[1].Content);
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
