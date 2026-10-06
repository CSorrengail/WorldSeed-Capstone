using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.RuleDrafting;

public sealed class RuleDraftJsonParser
{
    private readonly StructuredRuleDraftValidator _validator = new();

    public RuleDraftTurn Parse(string content, IReadOnlySet<string> allowedSourceNoteIds, IReadOnlyDictionary<string, string>? sourceTextById = null, IReadOnlyList<SourcePassage>? sourcePassages = null)
    {
        if (allowedSourceNoteIds is null || allowedSourceNoteIds.Count == 0) throw new ArgumentException("At least one allowed source note id is required.", nameof(allowedSourceNoteIds));
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new RuleDraftFormatException("The model did not return a JSON object."); }
        catch (JsonException)
        {
            var trimmed = content.Trim();
            var detail = trimmed.StartsWith('{') && !trimmed.EndsWith('}')
                ? " The response appears incomplete, usually because the model reached its output limit."
                : string.Empty;
            throw new RuleDraftFormatException("The model did not return valid JSON." + detail);
        }
        catch (InvalidOperationException) { throw new RuleDraftFormatException("The model did not return a JSON object."); }

        var action = String(root["action"]) switch
        {
            "askClarifyingQuestion" => RuleDraftAction.AskClarifyingQuestion,
            "presentDraft" => RuleDraftAction.PresentDraft,
            _ => throw new RuleDraftFormatException("The model response has an unknown action.")
        };
        var questions = Strings(root, "clarifyingQuestions");
        // Accept the v0.1 response shape while existing saved sessions and small local models catch up.
        var legacyQuestion = String(root["clarifyingQuestion"]);
        if (questions.Length == 0 && legacyQuestion is not null) questions = [legacyQuestion];
        if (action == RuleDraftAction.AskClarifyingQuestion && (questions.Length is < 1 or > 5)) throw new RuleDraftFormatException("A clarification response requires one to five questions.");
        if (questions.Distinct(StringComparer.Ordinal).Count() != questions.Length) throw new RuleDraftFormatException("Clarifying questions must not repeat.");
        var passagesById = (sourcePassages ?? []).ToDictionary(passage => passage.Id, StringComparer.Ordinal);
        var draft = root["draft"] is null ? null : ParseDraft(root["draft"] as JsonObject, passagesById);
        if (action == RuleDraftAction.PresentDraft && draft is null) throw new RuleDraftFormatException("A draft response requires a structured draft.");
        if (draft is not null)
        {
            draft = RepairNearExcerpts(draft, sourceTextById);
            var issues = _validator.Validate(draft, action == RuleDraftAction.PresentDraft, allowedSourceNoteIds, sourceTextById);
            if (issues.Count > 0) throw new RuleDraftFormatException(string.Join(" ", issues));
        }
        return new RuleDraftTurn(action, legacyQuestion ?? questions.FirstOrDefault(), questions, draft);
    }

    private static StructuredRuleDraft RepairNearExcerpts(StructuredRuleDraft draft, IReadOnlyDictionary<string, string>? sourceTextById)
    {
        if (sourceTextById is null || sourceTextById.Count == 0) return draft;
        var rules = draft.Rules.Select(rule => rule with
        {
            SourceSupport = rule.SourceSupport.Select(support =>
                sourceTextById.TryGetValue(support.SourceNoteId, out var sourceText)
                    ? support with { Excerpt = StructuredRuleDraftValidator.RepairNearExcerpt(sourceText, support.Excerpt) ?? support.Excerpt }
                    : support).ToArray()
        }).ToArray();
        return draft with { Rules = rules };
    }

    private static StructuredRuleDraft ParseDraft(JsonObject? draft, IReadOnlyDictionary<string, SourcePassage> passagesById)
    {
        if (draft is null) throw new RuleDraftFormatException("Draft must be a JSON object.");
        return new StructuredRuleDraft(
            Required(draft, "title"), Required(draft, "intent"),
            (draft["rules"] as JsonArray)?.OfType<JsonObject>().Select(rule => ParseRule(rule, passagesById)).ToArray() ?? [],
            (draft["concepts"] as JsonArray)?.OfType<JsonObject>().Select(ParseConcept).ToArray() ?? [],
            Strings(draft, "assumptions"), Strings(draft, "openQuestions"), Strings(draft, "exclusions"));
    }

    private static RuleStatement ParseRule(JsonObject rule, IReadOnlyDictionary<string, SourcePassage> passagesById)
    {
        var kind = String(rule["kind"]) switch
        {
            "definition" => RuleStatementKind.Definition, "rule" => RuleStatementKind.Rule,
            "procedure" => RuleStatementKind.Procedure, "constraint" => RuleStatementKind.Constraint,
            _ => throw new RuleDraftFormatException("A rule statement has an unknown kind.")
        };
        return new RuleStatement(Required(rule, "id"), Required(rule, "name"), kind, Required(rule, "text"), Strings(rule, "sourceNoteIds"), (rule["sourceSupport"] as JsonArray)?.OfType<JsonObject>().SelectMany(support => ParseSupport(support, passagesById)).ToArray() ?? []);
    }

    private static IEnumerable<RuleSourceSupport> ParseSupport(JsonObject support, IReadOnlyDictionary<string, SourcePassage> passagesById)
    {
        var sourceNoteId = Required(support, "sourceNoteId");
        var passageIds = Strings(support, "passageIds");
        if (passageIds.Length > 0)
        {
            foreach (var passageId in passageIds)
            {
                if (!passagesById.TryGetValue(passageId, out var passage)) throw new RuleDraftFormatException($"Source passage '{passageId}' is not available in this conversation.");
                if (!string.Equals(sourceNoteId, passage.SourceNoteId, StringComparison.Ordinal)) throw new RuleDraftFormatException($"Source passage '{passageId}' does not belong to source note '{sourceNoteId}'.");
                yield return new RuleSourceSupport(sourceNoteId, passage.Text, passageId);
            }
            yield break;
        }
        var excerpt = Required(support, "excerpt");
        // Models sometimes use an ellipsis to concatenate two quotations. Preserve the
        // evidence only as separate literal snippets, never as a fabricated continuous quote.
        foreach (var part in excerpt.Split(["...", "…"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            yield return new RuleSourceSupport(sourceNoteId, part);
    }

    private static RuleConcept ParseConcept(JsonObject concept) => new(Required(concept, "id"), Required(concept, "name"), Required(concept, "description"));
    private static string Required(JsonObject owner, string name) => String(owner[name]) ?? throw new RuleDraftFormatException($"'{name}' is required.");
    private static string? String(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static string[] Strings(JsonObject owner, string name) => owner[name] is JsonArray values ? values.Select(String).Where(value => value is not null).Cast<string>().ToArray() : [];
}
