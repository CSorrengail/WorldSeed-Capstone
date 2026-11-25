using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation.Tests;

public sealed class SchemaRuleAccountingTests
{
    private static WorkingRuleSet Rules()
    {
        var now = DateTimeOffset.UtcNow;
        return new("rules", "project", "0.1", "Rules", null, 1, now, now,
            [new("r1", "Design Intent", WorkingRuleKind.Rule, "Scenes should encourage cooperative storytelling.", [], [], [], [], null, [], [])], [], [], [], []);
    }
    private static JsonObject Baseline() => JsonNode.Parse("""{"id":"schema","name":"Game","version":"0.1","entityTypes":[{"metadata":{"id":"scene","name":"Scene"},"properties":[{"metadata":{"id":"mood","name":"Mood"},"valueType":{"kind":"primitive","name":"string"},"required":false,"nullable":false}]}]}""")!.AsObject();
    private static string NoChange(string disposition, params string[] ids) => JsonSerializer.Serialize(new
    {
        action = "recordOutcomes", outcomes = new[] { new { workingRuleId = "r1", disposition, reason = "This rule needs no new field.", definitionIds = ids } }
    });
    private static ProjectSchemaProposal Saved(JsonObject schema, WorkingRuleSet rules, params SchemaRuleAssessment[] assessments) =>
        new("proposal", "project", "0.1", "Summary", schema.ToJsonString(), [], rules.Id, rules.Revision, ["r1"], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, RuleAssessments: assessments);

    [Theory]
    [InlineData("retainAsText")]
    [InlineData("excludeFromMechanics")]
    [InlineData("needsClarification")]
    public void Nonstructural_outcome_needs_no_schema_and_preserves_original_rules(string disposition)
    {
        var rules = Rules(); var original = JsonSerializer.Serialize(rules);
        var turn = new SchemaTranslationJsonParser().Parse(NoChange(disposition), rules);
        Assert.Equal(SchemaTranslationAction.RecordOutcomes, turn.Action);
        Assert.Null(turn.Proposal);
        Assert.Equal(disposition, Assert.Single(turn.Outcomes!).Disposition);
        Assert.Equal(original, JsonSerializer.Serialize(rules));
    }

    [Fact]
    public void Reuse_links_a_saved_definition_without_echoing_or_changing_it()
    {
        var schema = Baseline(); var original = schema.ToJsonString();
        var turn = new SchemaTranslationJsonParser().Parse(NoChange("alreadyRepresented", "scene"), Rules(), schema);
        Assert.Equal("scene", Assert.Single(Assert.Single(turn.Outcomes!).DefinitionIds));
        Assert.Equal(original, schema.ToJsonString());
    }

    [Theory]
    [InlineData("alreadyRepresented", "missing")]
    [InlineData("create", "scene")]
    [InlineData("extend", "scene")]
    [InlineData("retainAsText", "scene")]
    public void No_change_branch_cannot_claim_missing_or_structural_definitions(string disposition, string id) =>
        Assert.Throws<SchemaTranslationFormatException>(() => new SchemaTranslationJsonParser().Parse(NoChange(disposition, id), Rules(), Baseline()));

    [Fact]
    public void Duplicate_foreign_and_missing_rule_outcomes_are_rejected()
    {
        var parser = new SchemaTranslationJsonParser();
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse(NoChange("retainAsText").Replace("r1", "foreign"), Rules()));
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse("""{"action":"recordOutcomes","outcomes":[]}""", Rules()));
        var rules = Rules(); rules = rules with { Rules = [rules.Rules[0], rules.Rules[0] with { Id = "r2" }] };
        var duplicate = """{"action":"recordOutcomes","outcomes":[{"workingRuleId":"r1","disposition":"retainAsText","reason":"Design goal","definitionIds":[]},{"workingRuleId":"r1","disposition":"retainAsText","reason":"Design goal","definitionIds":[]}]}""";
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse(duplicate, rules));
    }

    private static string Extension(JsonObject baseline, string collection = "entityTypes")
    {
        var schema = new JsonObject { ["id"] = "schema", ["name"] = "Game", ["version"] = "0.1", ["entityTypes"] = new JsonArray() };
        var definition = JsonNode.Parse("""{"metadata":{"id":"scene","name":"Scene"},"properties":[{"metadata":{"id":"stakes","name":"Stakes"},"valueType":{"kind":"primitive","name":"string"},"required":false,"nullable":false}]}""")!;
        schema[collection] = new JsonArray(definition);
        if (collection == "valueTypes") definition["kind"] = "record";
        return new JsonObject { ["action"] = "presentProposal", ["proposal"] = new JsonObject
        {
            ["summary"] = "Adds stakes.", ["schema"] = schema,
            ["definitionSources"] = JsonNode.Parse("""[{"definitionId":"scene","workingRuleIds":["r1"]}]"""),
            ["outcomes"] = JsonNode.Parse("""[{"workingRuleId":"r1","disposition":"extend","reason":"The selected rule adds optional stakes.","definitionIds":["scene"]}]""")
        }}.ToJsonString();
    }

    [Fact]
    public void Explicit_extension_uses_controlled_merger_and_preserves_existing_fields()
    {
        var schema = Baseline(); var original = schema.ToJsonString();
        var turn = new SchemaTranslationJsonParser().Parse(Extension(schema), Rules(), schema);
        var merged = SchemaProposalMerger.MergeSchemas(schema, turn.Proposal!.Schema, true);
        Assert.Equal(2, merged["entityTypes"]![0]!["properties"]!.AsArray().Count);
        Assert.Equal(original, schema.ToJsonString());
    }

    [Fact]
    public void Extension_cannot_change_collection_existing_type_or_requirement()
    {
        var schema = Baseline(); var parser = new SchemaTranslationJsonParser();
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse(Extension(schema, "valueTypes"), Rules(), schema));
        var conflicting = Extension(schema).Replace("stakes", "mood").Replace("Stakes", "Mood").Replace("\"name\":\"string\"", "\"name\":\"integer\"");
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse(conflicting, Rules(), schema));
        conflicting = Extension(schema).Replace("stakes", "mood").Replace("Stakes", "Mood").Replace("\"required\":false", "\"required\":true");
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse(conflicting, Rules(), schema));
        var tightened = JsonNode.Parse(Extension(schema).Replace("stakes", "mood").Replace("Stakes", "Mood"))!;
        tightened["proposal"]!["schema"]!["entityTypes"]![0]!["properties"]![0]!["constraints"] = new JsonObject { ["minimum"] = 1 };
        Assert.Throws<SchemaTranslationFormatException>(() => parser.Parse(tightened.ToJsonString(), Rules(), schema));
    }

    [Fact]
    public void Structural_outcome_cannot_cite_an_unrelated_mapping()
    {
        var schema = Baseline(); var output = Extension(schema).Replace("\"disposition\":\"extend\"", "\"disposition\":\"retainAsText\"");
        Assert.Throws<SchemaTranslationFormatException>(() => new SchemaTranslationJsonParser().Parse(output, Rules(), schema));
    }

    [Fact]
    public void Accounting_survives_serialization_and_unrelated_additions_but_not_changed_target_or_rules()
    {
        var rules = Rules(); var schema = Baseline();
        var outcome = new SchemaRuleOutcome("r1", "alreadyRepresented", "Uses Scene.", ["scene"]);
        var assessment = SchemaRuleAccounting.Bind(outcome, rules, schema, DateTimeOffset.UtcNow);
        var saved = JsonSerializer.Deserialize<ProjectSchemaProposal>(JsonSerializer.Serialize(Saved(schema, rules, assessment)))!;
        Assert.True(SchemaRuleAccounting.IsComplete(SchemaRuleAccounting.Current(saved, rules, rules.Rules[0])));
        schema["entityTypes"]!.AsArray().Add(JsonNode.Parse("""{"metadata":{"id":"place","name":"Place"},"properties":[]}"""));
        Assert.NotNull(SchemaRuleAccounting.Current(saved with { SchemaJson = schema.ToJsonString() }, rules, rules.Rules[0]));
        schema["entityTypes"]![0]!["properties"]![0]!["required"] = true;
        Assert.Null(SchemaRuleAccounting.Current(saved with { SchemaJson = schema.ToJsonString() }, rules, rules.Rules[0]));
        Assert.Null(SchemaRuleAccounting.Current(saved, rules with { Revision = 2 }, rules.Rules[0]));
        var changed = rules.Rules[0] with { AuthoritativeText = "Changed rule" };
        Assert.Null(SchemaRuleAccounting.Current(saved, rules with { Rules = [changed] }, changed));
    }

    [Fact]
    public void Api_history_guard_refuses_stale_or_tampered_assessments_and_history_loss()
    {
        var rules = Rules(); var schema = Baseline();
        var assessment = SchemaRuleAccounting.Bind(new("r1", "retainAsText", "Design goal.", []), rules, schema, DateTimeOffset.UtcNow);
        var saved = Saved(schema, rules, assessment);
        SchemaRuleAccounting.ValidateHistory(saved, rules, null);
        Assert.Throws<SchemaTranslationFormatException>(() => SchemaRuleAccounting.ValidateHistory(saved, rules with { Revision = 2 }, null));
        Assert.Throws<SchemaTranslationFormatException>(() => SchemaRuleAccounting.ValidateHistory(saved with { RuleAssessments = [assessment with { RuleFingerprint = "wrong" }] }, rules, null));
        Assert.Throws<SchemaTranslationFormatException>(() => SchemaRuleAccounting.ValidateHistory(saved with { RuleAssessments = [] }, rules, saved));
    }

    [Fact]
    public void Accounting_includes_referenced_definition_dependencies()
    {
        var rules = Rules(); var schema = Baseline();
        schema["valueTypes"] = JsonNode.Parse("""[{"metadata":{"id":"moods","name":"Moods"},"kind":"enumeration","entries":[{"id":"calm","name":"Calm"}]}]""");
        schema["entityTypes"]![0]!["properties"]![0]!["valueType"] = JsonNode.Parse("""{"kind":"definedValueType","valueTypeId":"moods"}""");
        var assessment = SchemaRuleAccounting.Bind(new("r1", "alreadyRepresented", "Scene fields.", ["scene"]), rules, schema, DateTimeOffset.UtcNow);
        Assert.Equal(2, assessment.DefinitionFingerprints.Count);
        var saved = Saved(schema, rules, assessment);
        schema["valueTypes"]![0]!["entries"]!.AsArray().Add(JsonNode.Parse("""{"id":"tense","name":"Tense"}"""));
        Assert.Null(SchemaRuleAccounting.Current(saved with { SchemaJson = schema.ToJsonString() }, rules, rules.Rules[0]));
    }

    [Fact]
    public void A_structural_proposal_can_also_retain_another_rule_as_text()
    {
        var rules = Rules(); rules = rules with { Rules = [rules.Rules[0], rules.Rules[0] with { Id = "r2", Name = "Design Guidance" }] };
        var response = JsonNode.Parse(Extension(Baseline()))!;
        response["proposal"]!["outcomes"]!.AsArray().Add(JsonNode.Parse("""{"workingRuleId":"r2","disposition":"retainAsText","reason":"Design guidance needs no field.","definitionIds":[]}"""));
        var result = new SchemaTranslationJsonParser().Parse(response.ToJsonString(), rules, Baseline());
        Assert.Equal(2, result.Outcomes!.Count);
        Assert.Single(result.Proposal!.DefinitionSources);
    }

    [Fact]
    public async Task Pending_outcome_uses_answerable_clarification_path_and_native_contract_exposes_noop()
    {
        var model = new Model(NoChange("needsClarification"));
        var result = await new SchemaTranslationService(model).TranslateAsync(new(Rules()));
        Assert.Equal(SchemaTranslationAction.AskClarifyingQuestion, result.Action);
        Assert.Contains("Design Intent (r1)", result.ClarifyingQuestion);
        var branches = model.Request!.ResponseSchema!["oneOf"]!.AsArray();
        Assert.Contains(branches, branch => branch!["properties"]!["action"]!["enum"]![0]!.GetValue<string>() == "recordOutcomes");
    }

    private sealed class Model(string content) : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        { Request = request; return Task.FromResult(new LlmChatResponse(content, "fake", null)); }
    }
}
