using System.Text.Json.Nodes;
using WorldSeed.SchemaValidation;

namespace WorldSeed.SchemaValidation.Tests;

/// <summary>
/// Keeps the approved two-system MVP contract deterministic before the proposal,
/// balance, decision-history, and export services consume these fixtures.
/// </summary>
public class MvpDemonstrationContractTests
{
    [Theory]
    [InlineData("emberbound.contract.json")]
    [InlineData("quiet-horizons.contract.json")]
    public void Contract_has_valid_schema_traceable_rules_and_expected_fixture_outcomes(string fileName)
    {
        var contract = ReadContract(fileName);
        var schema = contract["gameSchema"]!.AsObject();
        var schemaValidation = new GameSchemaValidator().Validate(schema);
        Assert.True(schemaValidation.IsValid, string.Join(Environment.NewLine, schemaValidation.Issues));

        var ruleIds = contract["workingRuleSet"]!["rules"]!.AsArray()
            .OfType<JsonObject>()
            .Select(rule => rule["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var passageIds = contract["sourceNotes"]!.AsArray()
            .OfType<JsonObject>()
            .SelectMany(note => note["passages"]!.AsArray().OfType<JsonObject>())
            .Select(passage => passage["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(contract["workingRuleSet"]!["rules"]!.AsArray().OfType<JsonObject>(), rule =>
            Assert.All(rule["sourcePassageIds"]!.AsArray().Select(id => id!.GetValue<string>()), id => Assert.Contains(id, passageIds)));

        var profile = contract["balanceProfile"]!.AsObject();
        Assert.All(profile["sourceWorkingRuleIds"]!.AsArray().Select(id => id!.GetValue<string>()), id => Assert.Contains(id, ruleIds));

        var creature = schema["entityTypes"]!.AsArray().OfType<JsonObject>()
            .Single(type => type["metadata"]!["id"]!.GetValue<string>() == "creature");
        var propertyIds = creature["properties"]!.AsArray().OfType<JsonObject>()
            .Select(property => property["metadata"]!["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(profile["advisorySignals"]!.AsArray().OfType<JsonObject>(), signal =>
            Assert.Contains(signal["propertyId"]!.GetValue<string>(), propertyIds));

        var fixtures = contract["fixtures"]!.AsObject();
        Assert.True(new GameDataValidator().Validate(schema, fixtures["valid"]!["data"]).IsValid);
        Assert.True(new GameDataValidator().Validate(schema, fixtures["balanceConcern"]!["data"]).IsValid);

        var invalid = new GameDataValidator().Validate(schema, fixtures["mechanicallyInvalid"]!["data"]);
        Assert.False(invalid.IsValid);
        foreach (var expectedIssue in fixtures["mechanicallyInvalid"]!["expectedMechanicalViolations"]!.AsArray().Select(value => value!.GetValue<string>()))
            Assert.Contains(invalid.Issues, issue => issue.Message.Contains(expectedIssue, StringComparison.Ordinal));

        Assert.Empty(fixtures["balanceConcern"]!["expectedMechanicalViolations"]!.AsArray());
        Assert.NotEmpty(fixtures["balanceConcern"]!["expectedBalanceConcerns"]!.AsArray());
        Assert.Contains("approve", contract["designerWorkflow"]!["decisions"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Contains("reject", contract["designerWorkflow"]!["decisions"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Contains("requestRevision", contract["designerWorkflow"]!["decisions"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Contains("content", contract["exportContract"]!["requiredFields"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Contains("designerDecision", contract["exportContract"]!["requiredFields"]!.AsArray().Select(value => value!.GetValue<string>()));
    }

    [Fact]
    public void Contracts_express_mechanically_distinct_models_without_universal_dice_or_challenge_rating()
    {
        var emberbound = ReadContract("emberbound.contract.json").ToJsonString();
        var quietHorizons = ReadContract("quiet-horizons.contract.json").ToJsonString();
        var quietHorizonsSchema = ReadContract("quiet-horizons.contract.json")["gameSchema"]!.ToJsonString();

        Assert.Contains("technique-impact", emberbound, StringComparison.Ordinal);
        Assert.Contains("escalation-clock-impact", quietHorizons, StringComparison.Ordinal);
        Assert.DoesNotContain("challenge-rating", emberbound, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("challenge-rating", quietHorizons, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dice", quietHorizonsSchema, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject ReadContract(string fileName)
    {
        var path = FindFile("qa", "fixtures", "mvp", "creatures", fileName);
        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    private static string FindFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Repository fixture was not found.");
    }
}
