using System.Text.Json.Nodes;
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
    public void Groups_schema_candidates_and_preserves_a_duplicate_free_queue_order()
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

        Assert.Equal(new[] { WorkingRuleKind.Definition, WorkingRuleKind.Rule, WorkingRuleKind.Procedure }, groups.Select(group => group.Kind));
        Assert.Equal("Definition - Weapon", Assert.Single(groups[0].Rules).Name);
        Assert.Equal("Weapon", SchemaRulePicker.DisplayName(groups[0].Rules[0]));
        Assert.False(SchemaRulePicker.TrySetSelected(selected, "too-many", true, SchemaTranslationService.MaxWorkingRulesPerProposal));
        Assert.Equal(SchemaTranslationService.MaxWorkingRulesPerProposal, selected.Count);

        var queue = new List<string>();
        Assert.True(SchemaRulePicker.TryEnqueue(queue, "procedure"));
        Assert.True(SchemaRulePicker.TryEnqueue(queue, "definition"));
        Assert.False(SchemaRulePicker.TryEnqueue(queue, "procedure"));
        Assert.Equal(new[] { "procedure", "definition" }, queue);
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
        var original = Assert.Single(CreateWorkingRuleSet().Rules);
        var multiRuleSet = CreateWorkingRuleSet() with { Rules = [original, original with { Id = "another-selected-rule" }] };

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(multiRuleSet));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal(3, model.Requests.Count);
        Assert.Contains("armorWeight", model.Requests[2].Messages[^1].Content);
    }

    [Fact]
    public void Normalizes_a_membered_primitive_value_type_to_a_record()
    {
        var modelOutput = """{"action":"presentProposal","proposal":{"summary":"A cooldown duration.","schema":{"id":"combat","name":"Combat","version":"0.1.0","entityTypes":[],"valueTypes":[{"id":"duration","name":"Duration","kind":"primitive","properties":[{"name":"value","valueType":{"kind":"primitive","name":"integer"},"required":true,"nullable":false}]}]},"definitionSources":[{"definitionId":"duration","workingRuleIds":["casting-adds-mark"]}]}}""";

        var turn = new SchemaTranslationJsonParser().Parse(modelOutput, CreateWorkingRuleSet());

        var proposal = Assert.IsType<SchemaTranslationProposal>(turn.Proposal);
        var values = Assert.IsType<JsonArray>(proposal.Schema["valueTypes"]);
        var duration = Assert.IsType<JsonObject>(Assert.Single(values));
        var properties = Assert.IsType<JsonArray>(duration["properties"]);
        var value = Assert.IsType<JsonObject>(Assert.Single(properties));
        Assert.Equal("record", duration["kind"]!.GetValue<string>());
        Assert.Equal("integer", value["valueType"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Retries_when_model_json_contains_a_duplicate_property_name()
    {
        var duplicate = """{"action":"presentProposal","proposal":{"summary":"First","summary":"Second","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["casting-adds-mark"]}]}}""";
        var valid = """{"action":"presentProposal","proposal":{"summary":"A caster schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"caster","name":"Caster"}}]},"definitionSources":[{"definitionId":"caster","workingRuleIds":["casting-adds-mark"]}]}}""";
        var model = new FakeModel(duplicate, valid);

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet()));

        Assert.Equal(SchemaTranslationAction.PresentProposal, turn.Action);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("same JSON property", model.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public void Repairs_a_named_primitive_value_type_with_a_repeated_name_key()
    {
        var output = """{"action":"presentProposal","proposal":{"summary":"A combat item cost.","schema":{"id":"combat","name":"Combat","version":"0.5","entityTypes":[],"valueTypes":[{"id":"combatItemCost","name":"Combat Item Cost","kind":"primitive","name":"integer"}]},"definitionSources":[{"definitionId":"combatItemCost","workingRuleIds":["casting-adds-mark"]}]}}""";

        var turn = new SchemaTranslationJsonParser().Parse(output, CreateWorkingRuleSet());

        var proposal = Assert.IsType<SchemaTranslationProposal>(turn.Proposal);
        var valueTypes = Assert.IsType<JsonArray>(proposal.Schema["valueTypes"]);
        var cost = Assert.IsType<JsonObject>(Assert.Single(valueTypes));
        Assert.Equal("alias", cost["kind"]!.GetValue<string>());
        Assert.Equal("integer", cost["targetType"]!["name"]!.GetValue<string>());
        Assert.Equal("Combat Item Cost", cost["metadata"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Validates_new_schema_additions_against_the_saved_base_and_merges_them()
    {
        var baseSchema = JsonNode.Parse("""{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[],"valueTypes":[{"metadata":{"id":"cooldownDuration","name":"Cooldown Duration"},"kind":"record","properties":[]}]}""")!.AsObject();
        var addition = """{"action":"presentProposal","proposal":{"summary":"An ability schema.","schema":{"id":"dangerous-magic","name":"Dangerous Magic","version":"0.1.0","entityTypes":[{"metadata":{"id":"ability","name":"Ability"},"properties":[{"metadata":{"id":"cooldown","name":"Cooldown"},"valueType":{"kind":"definedValueType","valueTypeId":"cooldownDuration"},"required":false,"nullable":true}]}]},"definitionSources":[{"definitionId":"ability","workingRuleIds":["casting-adds-mark"]}]}}""";
        var model = new FakeModel(addition);

        var turn = await new SchemaTranslationService(model).TranslateAsync(new SchemaTranslationRequest(CreateWorkingRuleSet(), baseSchema));
        var merged = SchemaProposalMerger.MergeSchemas(baseSchema, turn.Proposal!.Schema);
        var sources = SchemaProposalMerger.MergeDefinitionSources([new DefinitionSource("cooldownDuration", ["previous-rule"])], turn.Proposal.DefinitionSources);

        Assert.Contains("baseSchema", model.Request!.Messages[1].Content);
        Assert.Equal(new[] { "ability" }, merged["entityTypes"]!.AsArray().OfType<JsonObject>().Select(node => node["metadata"]!["id"]!.GetValue<string>()));
        Assert.Equal(new[] { "cooldownDuration" }, merged["valueTypes"]!.AsArray().OfType<JsonObject>().Select(node => node["metadata"]!["id"]!.GetValue<string>()));
        Assert.Equal(new[] { "cooldownDuration", "ability" }, sources.Select(source => source.DefinitionId));
    }

    [Fact]
    public void Normalizes_misplaced_schema_arrays_and_unambiguous_single_rule_mappings()
    {
        var ruleSet = CreateWorkingRuleSet() with { Rules = [Assert.Single(CreateWorkingRuleSet().Rules) with { Id = "b001-r004" }] };
        var baseSchema = JsonNode.Parse("""{"id":"project","name":"Project","version":"0.5","entityTypes":[]}""")!.AsObject();
        var modelOutput = """{"action":"presentProposal","proposal":{"summary":"Armor class weight.","schema":{"entityTypes":[{"id":"armorClass","name":"Armor Class","properties":[{"name":"weight","valueType":{"kind":"definedValueType","valueTypeId":"armorClassWeight"},"metadata":{"id":"weight","name":"Weight"},"required":true,"nullable":false}]}]},"valueTypes":[{"id":"armorClassWeight","name":"Armor Class Weight","kind":"enumeration","entries":[{"id":"light","name":"Light"}]}],"definitionSources":[{"definitionId":"armorClassWeight","workingRuleIds":["b001-r004"]}]}}""";

        var turn = new SchemaTranslationJsonParser().Parse(modelOutput, ruleSet, baseSchema);

        Assert.Single(turn.Proposal!.Schema["valueTypes"]!.AsArray());
        Assert.Equal(new[] { "armorClass", "armorClassWeight" }, turn.Proposal.DefinitionSources.Select(source => source.DefinitionId).Order());
        Assert.All(turn.Proposal.DefinitionSources, source => Assert.Equal(new[] { "b001-r004" }, source.WorkingRuleIds));
    }

    [Fact]
    public void Normalizes_enumeration_labels_mistakenly_placed_in_properties()
    {
        var ruleSet = CreateWorkingRuleSet() with { Rules = [Assert.Single(CreateWorkingRuleSet().Rules) with { Id = "b001-r004" }] };
        var baseSchema = JsonNode.Parse("""{"id":"project","name":"Project","version":"0.5","entityTypes":[]}""")!.AsObject();
        var modelOutput = """{"action":"presentProposal","proposal":{"summary":"Armor class weight.","schema":{"entityTypes":[],"valueTypes":[{"id":"armorClassWeight","name":"Armor Class Weight","kind":"enumeration","properties":[{"id":"light","name":"Light"},{"id":"medium","name":"Medium"},{"id":"heavy","name":"Heavy"}],"metadata":{"id":"armorClassWeight","name":"Armor Class Weight"}}]},"definitionSources":[{"definitionId":"armorClassWeight","workingRuleIds":["b001-r004"]}]}}""";

        var turn = new SchemaTranslationJsonParser().Parse(modelOutput, ruleSet, baseSchema);

        var proposal = Assert.IsType<SchemaTranslationProposal>(turn.Proposal);
        var valueTypes = Assert.IsType<JsonArray>(proposal.Schema["valueTypes"]);
        var valueType = Assert.IsType<JsonObject>(Assert.Single(valueTypes));
        Assert.Null(valueType["properties"]);
        Assert.Equal(new[] { "light", "medium", "heavy" }, valueType["entries"]!.AsArray().OfType<JsonObject>().Select(entry => entry["id"]!.GetValue<string>()));
    }

    [Fact]
    public void Discards_an_identical_saved_definition_echo_and_its_untrusted_source_link()
    {
        var cooldown = """{"id":"cooldownDuration","name":"Cooldown Duration","kind":"record","properties":[{"name":"duration","valueType":{"kind":"primitive","name":"integer"},"metadata":{"id":"duration","name":"Duration"},"required":true,"nullable":false}],"metadata":{"id":"cooldownDuration","name":"Cooldown Duration"}}""";
        var baseSchema = JsonNode.Parse("""{"id":"project","name":"Project","version":"0.5","entityTypes":[],"valueTypes":[]}""")!.AsObject();
        baseSchema["valueTypes"]!.AsArray().Add(JsonNode.Parse(cooldown));
        var output = """{"action":"presentProposal","proposal":{"summary":"Adds a weapon property.","schema":{"entityTypes":[],"valueTypes":[__COOLDOWN__,{"id":"weaponProperty","name":"Weapon Property","kind":"enumeration","entries":[{"id":"sharp","name":"Sharp"}],"metadata":{"id":"weaponProperty","name":"Weapon Property"}}]},"definitionSources":[{"definitionId":"cooldownDuration","workingRuleIds":["casting-adds-mark"]},{"definitionId":"weaponProperty","workingRuleIds":["casting-adds-mark"]}]}}""".Replace("__COOLDOWN__", cooldown, StringComparison.Ordinal);

        var turn = new SchemaTranslationJsonParser().Parse(output, CreateWorkingRuleSet(), baseSchema);
        var merged = SchemaProposalMerger.MergeSchemas(baseSchema, turn.Proposal!.Schema);
        var sources = SchemaProposalMerger.MergeDefinitionSources([new DefinitionSource("cooldownDuration", ["previous-rule"])], turn.Proposal.DefinitionSources);

        Assert.Equal(new[] { "cooldownDuration", "weaponProperty" }, merged["valueTypes"]!.AsArray().OfType<JsonObject>().Select(value => value["metadata"]!["id"]!.GetValue<string>()));
        Assert.Equal(new[] { "previous-rule" }, Assert.Single(sources, source => source.DefinitionId == "cooldownDuration").WorkingRuleIds);
        Assert.Equal(new[] { "weaponProperty" }, turn.Proposal.DefinitionSources.Select(source => source.DefinitionId));
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
