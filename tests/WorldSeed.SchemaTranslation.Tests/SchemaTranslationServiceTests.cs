using WorldSeed.LlmIntegration;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation.Tests;

public class SchemaTranslationServiceTests
{
    [Fact]
    public async Task Returns_a_valid_noncanonical_schema_proposal_with_working_rule_mappings()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"A caster schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["casting-adds-mark"]}]}}""");
        var service = new SchemaTranslationService(model);

        var turn = await service.TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal("dangerous-magic", turn.Proposal!.Schema["id"]!.GetValue<string>());
        Assert.Equal("working-rules-dangerous-magic", turn.Proposal.WorkingRuleSetId);
        Assert.Equal(3, turn.Proposal.WorkingRuleSetRevision);
        Assert.Contains("small selected group", model.Request!.Messages[0].Content);
        Assert.Contains("workingRuleIds", model.Request.Messages[0].Content);
        Assert.Equal(SchemaTranslationService.MaxOutputTokens, model.Request.MaxOutputTokens);
        Assert.DoesNotContain("Evidence", model.Request.Messages[1].Content);
    }

    [Fact]
    public async Task Sends_designer_schema_clarifications_back_to_the_model()
    {
        var model = new FakeModel("""{"action":"askClarifyingQuestion","clarifyingQuestion":"What weight categories are used?"}""");
        var clarifications = new[] { new SchemaClarification("What weight categories are used?", "Light, medium, and heavy.") };

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet(), Clarifications: clarifications));

        Assert.Equal(SchemaTranslationAction.AskClarifyingQuestion, turn.Action);
        Assert.Contains("schemaClarifications", model.Request!.Messages[1].Content);
        Assert.Contains("Light, medium, and heavy.", model.Request.Messages[1].Content);
    }

    [Fact]
    public async Task Rejects_proposals_with_invented_or_unmapped_working_rule_links()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"Bad mapping.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["invented-rule"]}]}}""");
        var service = new SchemaTranslationService(model);

        var exception = await Assert.ThrowsAsync<SchemaTranslationFormatException>(() => service.TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet())));

        Assert.Contains("must cite only working rule ids", exception.Message);
    }

    [Fact]
    public async Task Rejects_an_unbounded_rule_selection_before_calling_the_model()
    {
        var original = Assert.Single(CreateWorkingRuleSet().Rules);
        var ruleSet = CreateWorkingRuleSet() with { Rules = Enumerable.Range(1, SchemaTranslationService.MaxWorkingRulesPerProposal + 1).Select(index => original with { Id = "rule-" + index }).ToArray() };
        var model = new FakeModel("{}");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(ruleSet)));

        Assert.Contains("at most", exception.Message);
        Assert.Null(model.Request);
    }

    [Fact]
    public async Task Retries_once_after_an_incomplete_json_response()
    {
        var valid = """{"action":"presentProposal","proposal":{"summary":"A caster schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["casting-adds-mark"]}]}}""";
        var model = new FakeModel("{\"action\":\"presentProposal\",\"proposal\":", valid);

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("prior response could not be parsed", model.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task Accepts_a_complete_legacy_present_proposal_wrapper()
    {
        var model = new FakeModel("""{"presentProposal":{"proposal":{"summary":"A caster schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["casting-adds-mark"]}]}}}""");

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task Normalizes_lossless_model_shape_variants_before_validation()
    {
        var model = new FakeModel("""{"action":"presentProposal","proposal":{"summary":"A weapon schema.","schema":{"id":"weapons","name":"Weapons","version":"0.1.0","entityTypes":[{"id":"weapon","name":"Weapon","metadata":{"name":"Weapon"},"properties":[{"name":"name","valueType":"string","required":true,"nullable":false,"metadata":{"name":"Name"}}]}]},"definitionSources":{"weapon":["casting-adds-mark"]}}}""");

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        var weapon = turn.Proposal!.Schema["entityTypes"]!.AsArray()[0]!.AsObject();
        var property = weapon["properties"]!.AsArray()[0]!.AsObject();
        Assert.Equal("weapon", weapon["metadata"]!["id"]!.GetValue<string>());
        Assert.Equal("name", property["metadata"]!["id"]!.GetValue<string>());
        Assert.Equal("primitive", property["valueType"]!["kind"]!.GetValue<string>());
        Assert.Equal("weapon", Assert.Single(turn.Proposal.DefinitionSources).DefinitionId);
    }

    [Fact]
    public void Groups_schema_candidates_by_kind_and_enforces_the_six_rule_limit()
    {
        var original = Assert.Single(CreateWorkingRuleSet().Rules);
        var rules = new[]
        {
            original with { Id = "definition", Name = "Definition - Weapon", Kind = WorkingRuleKind.Definition },
            original with { Id = "procedure", Name = "Attack procedure", Kind = WorkingRuleKind.Procedure },
            original with { Id = "rule", Name = "Attack rule", Kind = WorkingRuleKind.Rule }
        };

        var groups = SchemaRulePicker.Group(rules);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules) Assert.True(SchemaRulePicker.TrySetSelected(selected, rule.Id, true, SchemaTranslationService.MaxWorkingRulesPerProposal));
        foreach (var index in Enumerable.Range(0, 3)) Assert.True(SchemaRulePicker.TrySetSelected(selected, "extra-" + index, true, SchemaTranslationService.MaxWorkingRulesPerProposal));

        Assert.Equal(new[] { WorkingRuleKind.Definition, WorkingRuleKind.Rule, WorkingRuleKind.Procedure }, groups.Select(group => group.Kind));
        Assert.Equal("Definition - Weapon", Assert.Single(groups[0].Rules).Name);
        Assert.Equal("Weapon", SchemaRulePicker.DisplayName(groups[0].Rules[0]));
        Assert.False(SchemaRulePicker.TrySetSelected(selected, "too-many", true, SchemaTranslationService.MaxWorkingRulesPerProposal));
        Assert.Equal(SchemaTranslationService.MaxWorkingRulesPerProposal, selected.Count);
    }

    [Fact]
    public async Task Retries_once_when_the_first_schema_is_structurally_invalid()
    {
        var invalid = """{"action":"presentProposal","proposal":{"summary":"Incomplete.","schema":{"id":"armor","name":"Armor","version":"0.1.0","entityTypes":[{"metadata":{"id":"armorClass","name":"Armor Class"},"properties":[{"metadata":{"id":"weight","name":"Weight"},"valueType":"enum","required":true,"nullable":false}]}]},"definitionSources":[{"definitionId":"armorClass","workingRuleIds":["casting-adds-mark"]},{"definitionId":"materialCost","workingRuleIds":["casting-adds-mark"]}]}}""";
        var corrected = """{"action":"presentProposal","proposal":{"summary":"Armor class.","schema":{"id":"armor","name":"Armor","version":"0.1.0","entityTypes":[{"metadata":{"id":"armorClass","name":"Armor Class"},"properties":[{"metadata":{"id":"weight","name":"Weight"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]}]},"definitionSources":[{"definitionId":"armorClass","workingRuleIds":["casting-adds-mark"]}]}}""";
        var model = new FakeModel(invalid, corrected);

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("Property valueType requires a kind", model.Requests[1].Messages[^1].Content);
        Assert.Contains("materialCost", model.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public void Lifts_misplaced_definition_sources_before_reporting_actual_schema_errors()
    {
        var modelOutput = """{"action":"presentProposal","proposal":{"summary":"Incomplete.","schema":{"id":"armor","name":"Armor","version":"0.1.0","entityTypes":[{"id":"armorClass","name":"Armor Class","definitions":[{"id":"weight","name":"Weight","properties":[{"name":"category","metadata":{"id":"category","name":"Category"},"valueType":{"kind":"definedValueType","valueTypeId":"armor-weights"},"required":true,"nullable":false}]}]}],"valueTypes":[{"id":"armor-weights","name":"Armor Weights","kind":"enumeration","entries":[]}],"definitionSources":[{"definitionId":"weight","workingRuleIds":["casting-adds-mark"]}]}}}""";

        var exception = Assert.Throws<SchemaTranslationFormatException>(() => new SchemaTranslationJsonParser().Parse(modelOutput, CreateWorkingRuleSet()));

        Assert.Contains("Enumeration value types require at least one entry", exception.Message);
        Assert.Contains("Definition source 'weight' does not exist", exception.Message);
        Assert.DoesNotContain("requires definition sources", exception.Message);
    }

    [Fact]
    public async Task Normalizes_enumeration_labels_and_retries_a_second_time_for_missing_definition_mappings()
    {
        var first = """{"action":"presentProposal","proposal":{"summary":"Incomplete.","schema":{"id":"armor","name":"Armor","version":"0.1.0","entityTypes":[{"id":"armorClass","name":"Armor Class"}],"valueTypes":[{"id":"armorWeight","name":"Armor Weight","kind":"enumeration","entries":["Light","Heavy"]}]},"definitionSources":[{"definitionId":"armorClass","workingRuleIds":["casting-adds-mark"]}]}}""";
        var second = """{"action":"presentProposal","proposal":{"summary":"Armor class.","schema":{"id":"armor","name":"Armor","version":"0.1.0","entityTypes":[{"metadata":{"id":"armorClass","name":"Armor Class"}}],"valueTypes":[{"metadata":{"id":"armorWeight","name":"Armor Weight"},"kind":"enumeration","entries":[{"id":"light","name":"Light"},{"id":"heavy","name":"Heavy"}]}]},"definitionSources":[{"definitionId":"armorClass","workingRuleIds":["casting-adds-mark"]},{"definitionId":"armorWeight","workingRuleIds":["casting-adds-mark"]}]}}""";
        var model = new FakeModel(first, first, second);

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal(3, model.Requests.Count);
        Assert.Contains("armorWeight", model.Requests[2].Messages[^1].Content);
    }

    private static WorkingRuleSet CreateWorkingRuleSet()
    {
        var now = DateTimeOffset.Parse("2026-10-14T00:00:00Z");
        var rule = new WorkingRule("casting-adds-mark", "Casting Adds a Mark", WorkingRuleKind.Rule, "Casting magic gives the caster a lasting mark.", [new WorkingRuleEvidence("source-001", "source-001:p001", "Magic leaves marks.")], [new DraftRuleReference("session-001", "draft-001")], [], [], null, [], []);
        return new WorkingRuleSet("working-rules-dangerous-magic", "project-dangerous-magic", "0.1", "Dangerous Magic", null, 3, now, now, [rule], [], [], [], []);
    }

    private sealed class FakeModel(params string[] contents) : ILanguageModelClient
    {
        private int _next;
        public LlmChatRequest? Request { get; private set; }
        public List<LlmChatRequest> Requests { get; } = [];
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            Requests.Add(request);
            var content = contents[Math.Min(_next++, contents.Length - 1)];
            return Task.FromResult(new LlmChatResponse(content, "test-model", null));
        }
    }
}
