using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.RuleDrafting;

public sealed class RuleDraftJsonParser
{
    private readonly StructuredRuleDraftValidator _validator = new();

    public RuleDraftTurn Parse(string content)
    {
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new RuleDraftFormatException("The model did not return a JSON object."); }
        catch (JsonException) { throw new RuleDraftFormatException("The model did not return valid JSON."); }
        catch (InvalidOperationException) { throw new RuleDraftFormatException("The model did not return a JSON object."); }

        var action = String(root["action"]) switch
        {
            "askClarifyingQuestion" => RuleDraftAction.AskClarifyingQuestion,
            "presentDraft" => RuleDraftAction.PresentDraft,
            _ => throw new RuleDraftFormatException("The model response has an unknown action.")
        };
        var question = String(root["clarifyingQuestion"]);
        if (action == RuleDraftAction.AskClarifyingQuestion && question is null) throw new RuleDraftFormatException("A clarification response requires a question.");
        var draft = root["draft"] is null ? null : ParseDraft(root["draft"] as JsonObject);
        if (action == RuleDraftAction.PresentDraft && draft is null) throw new RuleDraftFormatException("A draft response requires a structured draft.");
        if (draft is not null)
        {
            var issues = _validator.Validate(draft, action == RuleDraftAction.PresentDraft);
            if (issues.Count > 0) throw new RuleDraftFormatException(string.Join(" ", issues));
        }
        return new RuleDraftTurn(action, question, draft);
    }

    private static StructuredRuleDraft ParseDraft(JsonObject? draft)
    {
        if (draft is null) throw new RuleDraftFormatException("Draft must be a JSON object.");
        return new StructuredRuleDraft(
            Required(draft, "title"), Required(draft, "intent"),
            (draft["rules"] as JsonArray)?.OfType<JsonObject>().Select(ParseRule).ToArray() ?? [],
            (draft["concepts"] as JsonArray)?.OfType<JsonObject>().Select(ParseConcept).ToArray() ?? [],
            Strings(draft, "assumptions"), Strings(draft, "openQuestions"), Strings(draft, "exclusions"));
    }

    private static RuleStatement ParseRule(JsonObject rule)
    {
        var kind = String(rule["kind"]) switch
        {
            "definition" => RuleStatementKind.Definition, "rule" => RuleStatementKind.Rule,
            "procedure" => RuleStatementKind.Procedure, "constraint" => RuleStatementKind.Constraint,
            _ => throw new RuleDraftFormatException("A rule statement has an unknown kind.")
        };
        return new RuleStatement(Required(rule, "id"), Required(rule, "name"), kind, Required(rule, "text"), Strings(rule, "sourceNoteIds"));
    }

    private static RuleConcept ParseConcept(JsonObject concept) => new(Required(concept, "id"), Required(concept, "name"), Required(concept, "description"));
    private static string Required(JsonObject owner, string name) => String(owner[name]) ?? throw new RuleDraftFormatException($"'{name}' is required.");
    private static string? String(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static string[] Strings(JsonObject owner, string name) => owner[name] is JsonArray values ? values.Select(String).Where(value => value is not null).Cast<string>().ToArray() : [];
}
