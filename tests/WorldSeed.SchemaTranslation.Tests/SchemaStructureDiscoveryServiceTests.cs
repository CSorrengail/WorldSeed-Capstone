using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation.Tests;

public sealed class SchemaStructureDiscoveryServiceTests
{
    [Fact]
    public async Task Discovers_bounded_content_and_relationship_jobs_without_manual_groups()
    {
        var model = new FakeModel("""{"plans":[{"id":"weapon","name":"Weapon","kind":"contentType","workingRuleIds":["weapon-stats","weapon-use"],"rationale":"Both rules describe individual weapons."},{"id":"weapon-uses-property","name":"Weapon Uses Property","kind":"relationship","workingRuleIds":["weapon-use"],"rationale":"The rule directly links the two concepts.","fromContentTypeId":"weapon","toContentTypeId":"weaponProperty"}]}""");
        var rules = new[]
        {
            Rule("weapon-stats", "Weapon Stats", "Weapons have attack value and scaling."),
            Rule("weapon-use", "Weapon Properties", "Weapons use weapon properties.")
        };
        var set = RuleSet(rules);

        var plans = await new SchemaStructureDiscoveryService(model).DiscoverAsync(set, JsonNode.Parse("""{"entityTypes":[],"valueTypes":[]}""")!.AsObject());

        Assert.Equal(2, plans.Count);
        var content = Assert.Single(plans, plan => plan.Kind == SchemaStructurePlanKind.ContentType);
        Assert.Equal(new[] { "weapon-stats", "weapon-use" }, content.WorkingRuleIds);
        var relationship = Assert.Single(plans, plan => plan.Kind == SchemaStructurePlanKind.Relationship);
        Assert.Equal("weapon", relationship.FromContentTypeId);
        Assert.Equal("weaponProperty", relationship.ToContentTypeId);
        Assert.Contains("existingSchema", model.Request!.Messages[1].Content);
    }

    [Fact]
    public async Task Retains_local_support_and_discards_outside_batch_rule_ids()
    {
        var model = new FakeModel("""{"plans":[{"id":"weapon","name":"Weapon","kind":"contentType","workingRuleIds":["weapon","other-rule"],"rationale":"Partially supported."}]}""");

        var plans = await new SchemaStructureDiscoveryService(model).DiscoverAsync(RuleSet([Rule("weapon", "Weapon", "A weapon.")]), null);

        var plan = Assert.Single(plans);
        Assert.Equal(new[] { "weapon" }, plan.WorkingRuleIds);
    }

    private static WorkingRuleSet RuleSet(IReadOnlyList<WorkingRule> rules) => new("working-rules", "project", "0.1", "Test", null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, rules, [], [], [], []);
    private static WorkingRule Rule(string id, string name, string text) => new(id, name, WorkingRuleKind.Definition, text, [], [], [], [], null, [], []);

    private sealed class FakeModel(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse(content, "test-model", null));
        }
    }
}
