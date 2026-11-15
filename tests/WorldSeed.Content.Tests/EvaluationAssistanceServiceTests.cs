using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;
using WorldSeed.WorkingRules;

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

    private static WorkingRuleSet Rules() => new("rules", "project", "0.1", "Rules", null, 2, Now, Now,
        [new WorkingRule("rule-threat", "Threat", WorkingRuleKind.Constraint, "Threat values range from one to ten.", [], [], [], [], null, [], [])], [], [], [], []);

    private static ActiveGameSchema Schema() => new("active", "project", "0.1", "Schema", """
        {"id":"schema","name":"Schema","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"threat","name":"Threat"},"valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false}]}]}
        """, "proposal", "rules", 2, 3, Now, Now);
}
