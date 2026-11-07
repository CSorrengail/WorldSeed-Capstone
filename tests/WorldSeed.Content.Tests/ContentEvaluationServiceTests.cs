using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;

namespace WorldSeed.Content.Tests;

public class ContentEvaluationServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T00:00:00Z");

    [Fact]
    public void Reports_advisory_numeric_concerns_without_turning_them_into_mechanical_failures()
    {
        var result = new ContentEvaluationService().Evaluate(Request(Data(25, ["a"])));

        Assert.True(result.IsMechanicallyValid);
        var concern = Assert.Single(result.BalanceConcerns);
        Assert.Equal("vitality-band", concern.SignalId);
        Assert.Equal(25, concern.ObservedValue);
    }

    [Fact]
    public void Reports_structural_violations_without_attempting_balance_measurement()
    {
        var result = new ContentEvaluationService().Evaluate(Request(Data(0, ["a"])));

        Assert.False(result.IsMechanicallyValid);
        Assert.Contains(result.MechanicalViolations, issue => issue.Message.Contains("below its minimum", StringComparison.Ordinal));
        Assert.Empty(result.BalanceConcerns);
    }

    [Fact]
    public void Supports_collection_count_signals_for_non_tactical_games()
    {
        var request = Request(Data(10, ["a", "b", "c"]), new ContentBalanceProfile("narrative", "creature", ["rule"], [new BalanceSignal("tag-band", "creature.tags", BalanceMeasure.CollectionCount, null, 2, "Too many tags.", [])]));

        var result = new ContentEvaluationService().Evaluate(request);

        Assert.True(result.IsMechanicallyValid);
        Assert.Equal("tag-band", Assert.Single(result.BalanceConcerns).SignalId);
    }

    private static ContentEvaluationRequest Request(ProjectGameData data, ContentBalanceProfile? profile = null)
    {
        var active = new ActiveGameSchema("active", "project", "0.1", "Test", Schema(), "proposal", "rules", 1, 1, Now, Now);
        profile ??= new ContentBalanceProfile("tactical", "creature", ["rule"], [new BalanceSignal("vitality-band", "creature.vitality", BalanceMeasure.NumericValue, null, 20, "Vitality exceeds its local reference band.", [new BalanceReference("sentinel", "Sentinel", 18)])]);
        return new ContentEvaluationRequest(active, data with { ActiveGameSchemaId = active.Id, ActiveGameSchemaRevision = active.Revision }, profile);
    }

    private static ProjectGameData Data(int vitality, string[] tags)
    {
        var document = new JsonObject
        {
            ["id"] = "data",
            ["gameSchemaId"] = "test-schema",
            ["gameSchemaVersion"] = "1",
            ["entityInstances"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "candidate",
                    ["entityTypeId"] = "creature",
                    ["values"] = new JsonObject
                    {
                        ["creature.vitality"] = vitality,
                        ["creature.tags"] = new JsonArray(tags.Select(tag => (JsonNode?)tag).ToArray())
                    }
                }
            },
            ["relationshipInstances"] = new JsonArray()
        };
        return new ProjectGameData("data", "project", "0.1", "active", 1, document.ToJsonString(), Now, Now);
    }

    private static string Schema() => """{"id":"test-schema","name":"Test","version":"1","entityTypes":[{"metadata":{"id":"creature","name":"Creature"},"properties":[{"metadata":{"id":"creature.vitality","name":"Vitality"},"valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false,"constraints":{"minimum":1,"maximum":30}},{"metadata":{"id":"creature.tags","name":"Tags"},"valueType":{"kind":"collection","itemType":{"kind":"primitive","name":"string"},"maximumItems":3},"required":true,"nullable":false}]}]}""";
}
