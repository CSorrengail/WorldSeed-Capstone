using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;
using WorldSeed.WorkingRules;
using WorldSeed.LlmIntegration;

namespace WorldSeed.Content.Tests;

public sealed class EvaluationAssistanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mechanical_suggestion_requires_real_rules_properties_and_approval()
    {
        var proposal = EvaluationAssistanceService.Create("project", "creature", EvaluationProposalKind.MechanicalValidation, "Bound threat values",
            JsonNode.Parse("""{"title":"Bound Threat","ruleText":"Threat must be between one and ten.","targetPropertyIds":["threat"],"suggestedConstraints":{"threat":{"minimum":1,"maximum":10}}}""")!.AsObject(), ["rule-threat"], Schema(), Rules(), Now);
        var approval = EvaluationAssistanceService.Decide(proposal, EvaluationProposalDecisionKind.Approved, "Use this bound.", Now);

        Assert.Equal("Bound Threat", EvaluationAssistanceService.BuildApprovedSchemaChangeDraft(proposal, approval).Title);
        Assert.Throws<InvalidOperationException>(() => EvaluationAssistanceService.BuildApprovedSchemaChangeDraft(proposal,
            approval with { Kind = EvaluationProposalDecisionKind.Rejected }));
        Assert.Throws<ArgumentException>(() => EvaluationAssistanceService.Create("project", "creature", EvaluationProposalKind.MechanicalValidation, "Bad",
            JsonNode.Parse("""{"title":"Bad","ruleText":"Bad","targetPropertyIds":["unknown"],"suggestedConstraints":{"unknown":{"minimum":1}}}""")!.AsObject(), ["rule-threat"], Schema(), Rules(), Now));
    }

    [Fact]
    public void Advisory_profile_is_game_owned_source_backed_and_not_applied_by_creation()
    {
        var candidate = JsonNode.Parse("""{"id":"creature-threat","entityTypeId":"creature","sourceWorkingRuleIds":["rule-threat"],"signals":[{"id":"threat-window","propertyId":"threat","measure":"numericValue","minimumReferenceValue":1,"maximumReferenceValue":10,"message":"Compare threat with this game's stated range.","references":[{"id":"rule-range","label":"Threat Rule","value":10,"rationale":"The game's own upper reference."}]}]}""")!.AsObject();
        var proposal = EvaluationAssistanceService.Create("project", "creature", EvaluationProposalKind.AdvisoryBalanceProfile, "Evaluate creature threat", candidate, ["rule-threat"], Schema(), Rules(), Now);
        var approval = EvaluationAssistanceService.Decide(proposal, EvaluationProposalDecisionKind.Approved, null, Now);

        var profile = EvaluationAssistanceService.BuildApprovedBalanceProfile(proposal, approval, Now);
        Assert.Equal("creature", profile.Profile.EntityTypeId);
        Assert.Equal(["rule-threat"], profile.Profile.SourceWorkingRuleIds);
        Assert.Equal(1, profile.Revision);
    }

    [Fact]
    public void Parser_rejects_hallucinated_rule_citations()
    {
        var response = """{"summary":"Suggestion","sourceWorkingRuleIds":["invented"],"candidate":{"title":"Test"}}""";
        Assert.Throws<EvaluationAssistanceFormatException>(() => EvaluationAssistanceGenerationService.ParseResponse(response, new HashSet<string> { "rule-threat" }));
    }

    [Theory]
    [InlineData("magicItem", "Magic Item", "charges", "Magic Item Charges")]
    [InlineData("promise", "Promise", "fulfillment", "Promise Fulfillment")]
    public async Task Generation_selects_relevant_rules_beyond_the_first_thirty(string typeId, string typeName, string field, string ruleName)
    {
        var baseline = Rules().Rules[0];
        var rules = Enumerable.Range(0, 40).Select(index => baseline with
        {
            Id = "unrelated-" + index, Name = "Travel", AuthoritativeText = "Journeys follow roads."
        }).Append(baseline with { Id = "relevant", Name = ruleName, AuthoritativeText = $"{typeName} {field} ranges from one to ten." }).ToArray();
        var schema = new JsonObject { ["entityTypes"] = new JsonArray(new JsonObject
        {
            ["metadata"] = new JsonObject { ["id"] = typeId, ["name"] = typeName },
            ["properties"] = new JsonArray(new JsonObject { ["metadata"] = new JsonObject { ["id"] = field, ["name"] = field } })
        }) };
        var model = new CapturingModel("""{"summary":"Compare supplied bounds.","sourceWorkingRuleIds":["relevant"],"candidate":{"title":"Comparison"}}""");

        var result = await new EvaluationAssistanceGenerationService(model).GenerateAsync(new("project", typeId,
            EvaluationProposalKind.AdvisoryBalanceProfile, schema, rules, $"Compare {field} references."));

        Assert.Equal(new[] { "relevant" }, result.SourceWorkingRuleIds);
        var context = model.Request!.Messages[2].Content;
        Assert.Contains("[relevant]", context);
        Assert.DoesNotContain("[unrelated-", context);
    }

    [Fact]
    public async Task Generation_bounds_context_and_rejects_citations_to_omitted_rules()
    {
        var rules = Enumerable.Range(0, 40).Select(index => Rules().Rules[0] with { Id = "rule-" + index }).ToArray();
        var model = new CapturingModel("""{"summary":"Suggestion","sourceWorkingRuleIds":["rule-39"],"candidate":{"title":"Comparison"}}""");

        var error = await Assert.ThrowsAsync<EvaluationAssistanceFormatException>(() => new EvaluationAssistanceGenerationService(model)
            .GenerateAsync(new("project", "creature", EvaluationProposalKind.MechanicalValidation,
                JsonNode.Parse(Schema().SchemaJson)!.AsObject(), rules, "Evaluate threat.")));

        Assert.Contains("not supplied", error.Message);
        var context = model.Request!.Messages[2].Content;
        Assert.Contains("selection of 30 from 40", context);
        Assert.Contains("[rule-0]", context);
        Assert.Contains("[rule-29]", context);
        Assert.DoesNotContain("[rule-30]", context);
    }

    private sealed class CapturingModel(string response) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse(response, "fixture", null));
        }
    }

    private static WorkingRuleSet Rules() => new("rules", "project", "0.1", "Rules", null, 2, Now, Now,
        [new WorkingRule("rule-threat", "Threat", WorkingRuleKind.Constraint, "Threat values range from one to ten.", [], [], [], [], null, [], [])], [], [], [], []);

    private static ActiveGameSchema Schema() => new("active", "project", "0.1", "Schema", """
        {"id":"schema","name":"Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"threat","name":"Threat"},"valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false}]}]}
        """, "proposal", "rules", 2, 3, Now, Now);
}
