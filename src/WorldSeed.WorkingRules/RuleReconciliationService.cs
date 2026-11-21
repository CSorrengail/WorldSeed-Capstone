using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;

namespace WorldSeed.WorkingRules;

public enum RuleReconciliationState { Pending, NeedsClarification, Applied, Rejected, Superseded }

/// <summary>Originals and designer decisions survive replacement of the active rule list.</summary>
public sealed record RuleReconciliation(
    string Id, string ProjectId, string WorkingRuleSetId, int BaseRevision, IReadOnlyList<WorkingRule> Originals,
    string Guidance, string Name, WorkingRuleKind Kind, string? Text, string Rationale,
    string? Question, RuleReconciliationState State, DateTimeOffset CreatedAt,
    string? OriginalSchemaProposalJson = null, int? AppliedRevision = null, DateTimeOffset? DecidedAt = null,
    IReadOnlyList<string>? ResolvedConflicts = null, IReadOnlyList<string>? ResolvedQuestions = null);

public sealed class RuleReconciliationService(ILanguageModelClient client)
{
    public async Task<RuleReconciliation> ProposeAsync(WorkingRuleSet rules, IReadOnlyList<string> selectedIds,
        string? guidance, string? originalSchemaProposalJson, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var ids = selectedIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length is < 1 or > 8) throw new ArgumentException("Select one to eight existing rules to revise or reconcile.");
        var originals = ids.Select(id => rules.Rules.SingleOrDefault(rule => rule.Id == id)
            ?? throw new ArgumentException("A selected rule no longer exists.")).ToArray();
        var response = await client.CompleteAsync(new LlmChatRequest([
            new(LlmMessageRole.System, "Propose one cohesive replacement for the selected TTRPG rules. Preserve all compatible requirements and exceptions. Never resolve a contradiction by guessing: ask a specific question unless designer guidance supplies the decision. Designer guidance may explicitly change mechanics. Do not invent source quotations or claim approval. Return JSON only: {\"name\":\"...\",\"kind\":\"definition|rule|procedure|constraint\",\"text\":\"replacement wording or null\",\"rationale\":\"what changes and why\",\"question\":\"clarification or null\",\"resolvedConflicts\":[],\"resolvedQuestions\":[]}. Resolved lists may contain only exact supplied conflict/question strings actually resolved by this proposal. If clarification is needed, text must be null. This is a reviewable proposal, not an applied rule."),
            new(LlmMessageRole.User, JsonSerializer.Serialize(new { rules = originals.Select(rule => new { rule.Id, rule.Name, rule.Kind, rule.AuthoritativeText, rule.OpenQuestions, rule.Exclusions }), conflicts = rules.Conflicts, questions = rules.OpenQuestions, guidance = guidance?.Trim() ?? "" }))
        ], Temperature: 0.1, MaxOutputTokens: 1600, RequireJsonObject: true), cancellationToken);
        try
        {
            var root = JsonNode.Parse(response.Content) as JsonObject ?? throw new JsonException();
            var name = root["name"]?.GetValue<string>()?.Trim();
            var text = root["text"]?.GetValue<string>()?.Trim();
            var rationale = root["rationale"]?.GetValue<string>()?.Trim();
            var question = root["question"]?.GetValue<string>()?.Trim();
            var conflicts = ReadResolved(root["resolvedConflicts"], rules.Conflicts);
            var questions = ReadResolved(root["resolvedQuestions"], rules.OpenQuestions.Concat(originals.SelectMany(rule => rule.OpenQuestions)).ToArray());
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rationale) ||
                !Enum.TryParse<WorkingRuleKind>(root["kind"]?.GetValue<string>(), true, out var kind) || !Enum.IsDefined(kind) ||
                (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(question)))
                throw new JsonException();
            return new("reconciliation-" + Guid.NewGuid().ToString("N"), rules.ProjectId, rules.Id, rules.Revision,
                JsonSerializer.Deserialize<WorkingRule[]>(JsonSerializer.Serialize(originals))!, guidance?.Trim() ?? "", name, kind,
                string.IsNullOrWhiteSpace(question) ? text : null, rationale, question,
                string.IsNullOrWhiteSpace(question) ? RuleReconciliationState.Pending : RuleReconciliationState.NeedsClarification,
                now, originalSchemaProposalJson, ResolvedConflicts: conflicts, ResolvedQuestions: questions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new WorkingRuleSetFormatException("The model did not return a usable reconciliation proposal. No rules were changed.");
        }
    }

    public static WorkingRuleSet AddProposal(WorkingRuleSet rules, RuleReconciliation proposal, DateTimeOffset now)
    {
        EnsureCurrent(rules, proposal);
        var ids = proposal.Originals.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        return rules with { Reconciliations = (rules.Reconciliations ?? []).Select(old =>
            old.State is RuleReconciliationState.Pending or RuleReconciliationState.NeedsClarification &&
            ids.SetEquals(old.Originals.Select(rule => rule.Id))
                ? old with { State = RuleReconciliationState.Superseded, DecidedAt = now } : old).Append(proposal).ToArray(), UpdatedAt = now };
    }

    public static WorkingRuleSet Reject(WorkingRuleSet rules, string proposalId, DateTimeOffset now)
    {
        var proposal = Find(rules, proposalId);
        if (proposal.State == RuleReconciliationState.Applied) throw new InvalidOperationException("An applied update cannot be rejected retroactively. Propose another rule update.");
        return ReplaceRecord(rules, proposal with { State = RuleReconciliationState.Rejected, DecidedAt = now }, now);
    }

    public static WorkingRuleSet Apply(WorkingRuleSet rules, string proposalId, DateTimeOffset now)
    {
        var proposal = Find(rules, proposalId);
        if (proposal.State == RuleReconciliationState.Applied) return rules;
        if (proposal.State != RuleReconciliationState.Pending || string.IsNullOrWhiteSpace(proposal.Text))
            throw new InvalidOperationException("Only a pending, clarified proposal can be accepted and applied.");
        EnsureCurrent(rules, proposal);
        var selected = proposal.Originals.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var retainedId = proposal.Originals[0].Id;
        var replacement = proposal.Originals[0] with
        {
            Name = proposal.Name, Kind = proposal.Kind, AuthoritativeText = proposal.Text,
            Evidence = proposal.Originals.SelectMany(rule => rule.Evidence).Distinct().ToArray(),
            SourceDraftRules = proposal.Originals.SelectMany(rule => rule.SourceDraftRules).Distinct().ToArray(),
            ConceptIds = proposal.Originals.SelectMany(rule => rule.ConceptIds).Distinct().ToArray(),
            SchemaHints = [], StructuredSupport = null,
            OpenQuestions = proposal.Originals.SelectMany(rule => rule.OpenQuestions).Except(proposal.ResolvedQuestions ?? []).Distinct().ToArray(),
            Exclusions = proposal.Originals.SelectMany(rule => rule.Exclusions).Distinct().ToArray()
        };
        var updated = rules.Rules.Where(rule => !selected.Contains(rule.Id)).Append(replacement)
            .Select(rule => rule with { SchemaHints = rule.SchemaHints.Select(hint => hint with
                { SourceWorkingRuleIds = hint.SourceWorkingRuleIds.Select(id => selected.Contains(id) ? retainedId : id).Distinct().ToArray() }).ToArray() }).ToArray();
        return ReplaceRecord(rules with { Rules = updated, Revision = checked(rules.Revision + 1),
            Conflicts = rules.Conflicts.Except(proposal.ResolvedConflicts ?? []).ToArray(),
            OpenQuestions = rules.OpenQuestions.Except(proposal.ResolvedQuestions ?? []).ToArray() },
            proposal with { State = RuleReconciliationState.Applied, AppliedRevision = rules.Revision + 1, DecidedAt = now }, now);
    }

    private static void EnsureCurrent(WorkingRuleSet rules, RuleReconciliation proposal)
    {
        if (rules.ProjectId != proposal.ProjectId || rules.Id != proposal.WorkingRuleSetId || rules.Revision != proposal.BaseRevision || proposal.Originals.Count is < 1 or > 8 ||
            proposal.Originals.Any(original => !rules.Rules.Any(current => current.Id == original.Id && JsonSerializer.Serialize(current) == JsonSerializer.Serialize(original))))
            throw new InvalidOperationException("The selected rules changed. Generate a fresh proposal against the current Working Rules.");
    }

    private static RuleReconciliation Find(WorkingRuleSet rules, string id) =>
        (rules.Reconciliations ?? []).SingleOrDefault(item => item.Id == id) ?? throw new ArgumentException("The reconciliation proposal does not belong to these Working Rules.");

    private static string[] ReadResolved(JsonNode? node, IReadOnlyList<string> allowed)
    {
        if (node is null) return [];
        if (node is not JsonArray array) throw new JsonException();
        var values = array.Select(value => value?.GetValue<string>() ?? throw new JsonException()).Distinct().ToArray();
        if (values.Any(value => !allowed.Contains(value, StringComparer.Ordinal))) throw new JsonException();
        return values;
    }

    private static WorkingRuleSet ReplaceRecord(WorkingRuleSet rules, RuleReconciliation proposal, DateTimeOffset now) =>
        rules with { Reconciliations = (rules.Reconciliations ?? []).Select(item => item.Id == proposal.Id ? proposal : item).ToArray(), UpdatedAt = now };
}
