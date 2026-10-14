using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.WorkingRules;

namespace WorldSeed.WorkingRules.Tests;

public class WorkingRuleSetValidatorTests
{
    [Fact]
    public void Accepts_a_traceable_project_level_working_rule_set()
    {
        var project = CreateProject();
        var ruleSet = CreateValidRuleSet(project.Id);

        var result = new WorkingRuleSetValidator().Validate(ruleSet, project);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public void Rejects_invented_evidence_and_unknown_draft_references()
    {
        var project = CreateProject();
        var valid = CreateValidRuleSet(project.Id);
        var invalidRule = valid.Rules.Single() with
        {
            Evidence = [new WorkingRuleEvidence("source-initiative", "source-initiative:p001", "Invented source wording.")],
            SourceDraftRules = [new DraftRuleReference("missing-session", "missing-rule")]
        };
        var invalid = valid with { Rules = [invalidRule] };

        var result = new WorkingRuleSetValidator().Validate(invalid, project);

        Assert.Contains(result.Issues, issue => issue.Message.Contains("exactly match"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("does not exist in a completed project draft"));
    }

    [Fact]
    public void Rejects_executable_support_and_schema_hints_that_cite_unknown_rules()
    {
        var project = CreateProject();
        var valid = CreateValidRuleSet(project.Id);
        var invalidRule = valid.Rules.Single() with
        {
            StructuredSupport = new WorkingRuleStructuredSupport(WorkingRuleAutomationStatus.Executable, "example", "1", "{}", []),
            SchemaHints = [new SchemaHint(SchemaHintKind.Property, "combatant.initiative", "Initiative", "combatant", "Suggested by the rule.", ["missing-working-rule"])]
        };

        var result = new WorkingRuleSetValidator().Validate(valid with { Rules = [invalidRule] }, project);

        Assert.Contains(result.Issues, issue => issue.Message.Contains("Executable structured support"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("unknown working rule"));
    }

    [Fact]
    public void Reports_missing_collections_without_throwing()
    {
        var project = CreateProject();
        var invalid = CreateValidRuleSet(project.Id) with { Rules = null!, Concepts = null!, OpenQuestions = null!, Exclusions = null!, Conflicts = null! };

        var result = new WorkingRuleSetValidator().Validate(invalid, project);

        Assert.Contains(result.Issues, issue => issue.Message == "rules is required.");
        Assert.Contains(result.Issues, issue => issue.Message == "concepts is required.");
    }

    private static DesignProject CreateProject()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new DesignSourceNote("source-initiative", "At the beginning of combat, the combatant with the lowest initiative takes a turn.", now, new SourceNoteOrigin("initiative.txt"));
        var project = DesignProjectService.AddSources(DesignProjectService.Create("project-initiative", "Initiative Project"), [source]);
        var draftRule = new RuleStatement("draft-turn-order", "Turn Order", RuleStatementKind.Procedure, "The lowest initiative acts first.", [source.Id], [new RuleSourceSupport(source.Id, source.OriginalText, "source-initiative:p001")]);
        var draft = new StructuredRuleDraft("Initiative", "Define turn order.", [draftRule], [], [], [], []);
        var turn = new RuleDraftTurn(RuleDraftAction.PresentDraft, null, [], draft);
        var session = new DesignSession("session-initiative", project.Id, "local", now, now, [source], [new LlmMessage(LlmMessageRole.User, source.OriginalText)], turn);
        return DesignProjectService.SaveSession(project, session);
    }

    private static WorkingRuleSet CreateValidRuleSet(string projectId)
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new WorkingRuleEvidence("source-initiative", "source-initiative:p001", "At the beginning of combat, the combatant with the lowest initiative takes a turn.");
        var concept = new WorkingRuleConcept("initiative", "Initiative", "A value used to determine combat turn order.", [evidence]);
        var rule = new WorkingRule("combat-turn-order", "Combat Turn Order", WorkingRuleKind.Procedure, "At the beginning of combat, the combatant with the lowest initiative takes a turn.", [evidence], [new DraftRuleReference("session-initiative", "draft-turn-order")], [concept.Id], [], null, [], []);
        return new WorkingRuleSet("working-rules-project-initiative", projectId, "0.1", "Initiative Rules", null, 1, now, now, [rule], [concept], [], [], []);
    }
}
