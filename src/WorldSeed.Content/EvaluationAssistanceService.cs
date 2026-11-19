using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WorldSeed.GameData;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.Content;

public enum EvaluationProposalKind { MechanicalValidation, AdvisoryBalanceProfile }
public enum EvaluationProposalDecisionKind { Approved, Rejected, RevisionRequested }

public sealed record EvaluationAssistanceProposal(
    string Id,
    string ProjectId,
    string EntityTypeId,
    EvaluationProposalKind Kind,
    string Summary,
    string CandidateJson,
    IReadOnlyList<string> SourceWorkingRuleIds,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    int Version,
    DateTimeOffset CreatedAt,
    string? ReplacesProposalId = null);

public sealed record EvaluationProposalDecision(
    string Id,
    string ProjectId,
    string ProposalId,
    int ProposalVersion,
    EvaluationProposalDecisionKind Kind,
    string? Note,
    string? ReplacementProposalId,
    DateTimeOffset CreatedAt);

public sealed record EvaluationSchemaChangeDraft(string Title, string RuleText);
public sealed record EvaluationAssistanceWorkspace(IReadOnlyList<EvaluationAssistanceProposal> Proposals, IReadOnlyList<EvaluationProposalDecision> Decisions);
public sealed record EvaluationProposalDecisionRequest(EvaluationProposalDecisionKind Kind, string? Note);

public interface IEvaluationAssistanceStore
{
    Task SaveProposalAsync(EvaluationAssistanceProposal proposal, CancellationToken cancellationToken = default);
    Task<EvaluationAssistanceProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationAssistanceProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveDecisionAsync(EvaluationProposalDecision decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationProposalDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class EvaluationAssistanceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static EvaluationAssistanceProposal Create(
        string projectId,
        string entityTypeId,
        EvaluationProposalKind kind,
        string summary,
        JsonObject candidate,
        IReadOnlyList<string> sourceWorkingRuleIds,
        ActiveGameSchema activeSchema,
        WorkingRuleSet workingRules,
        DateTimeOffset now,
        EvaluationAssistanceProposal? replaces = null)
    {
        Require(projectId, "A project is required.");
        Require(entityTypeId, "A content type is required.");
        Require(summary, "A proposal summary is required.");
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(sourceWorkingRuleIds);
        ArgumentNullException.ThrowIfNull(activeSchema);
        ArgumentNullException.ThrowIfNull(workingRules);
        if (activeSchema.ProjectId != projectId || workingRules.ProjectId != projectId)
            throw new ArgumentException("The schema and Working Rule Set must belong to this project.");
        if (activeSchema.WorkingRuleSetId != workingRules.Id || activeSchema.WorkingRuleSetRevision != workingRules.Revision)
            throw new ArgumentException("Evaluation proposals must use the Working Rule Set revision bound to the active schema.");
        var schema = JsonNode.Parse(activeSchema.SchemaJson) as JsonObject ?? throw new ArgumentException("The active schema is not a JSON object.");
        var properties = EntityPropertyIds(schema, entityTypeId);
        if (properties is null) throw new ArgumentException($"Content type '{entityTypeId}' does not exist in the active schema.", nameof(entityTypeId));
        var ruleIds = sourceWorkingRuleIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ruleIds.Length == 0 || ruleIds.Any(id => workingRules.Rules.All(rule => rule.Id != id)))
            throw new ArgumentException("Every evaluation proposal must cite existing Working Rules.", nameof(sourceWorkingRuleIds));
        if (replaces is not null && (replaces.ProjectId != projectId || replaces.EntityTypeId != entityTypeId || replaces.Kind != kind))
            throw new ArgumentException("A replacement must belong to the same project, content type, and evaluation category.", nameof(replaces));

        ValidateCandidate(kind, candidate, entityTypeId, properties, ruleIds);
        return new EvaluationAssistanceProposal(
            "evaluation-proposal-" + Guid.NewGuid().ToString("N"), projectId, entityTypeId, kind, summary.Trim(),
            candidate.ToJsonString(), ruleIds, activeSchema.Id, activeSchema.Revision, workingRules.Id, workingRules.Revision,
            (replaces?.Version ?? 0) + 1, now, replaces?.Id);
    }

    public static EvaluationProposalDecision Decide(EvaluationAssistanceProposal proposal, EvaluationProposalDecisionKind kind, string? note, DateTimeOffset now, EvaluationAssistanceProposal? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (kind == EvaluationProposalDecisionKind.RevisionRequested && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A revision request needs designer guidance.", nameof(note));
        if (replacement is not null && (replacement.ProjectId != proposal.ProjectId || replacement.ReplacesProposalId != proposal.Id))
            throw new ArgumentException("A replacement must identify this proposal as its base.", nameof(replacement));
        return new EvaluationProposalDecision("evaluation-decision-" + Guid.NewGuid().ToString("N"), proposal.ProjectId, proposal.Id,
            proposal.Version, kind, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), replacement?.Id, now);
    }

    public static ProjectContentBalanceProfile BuildApprovedBalanceProfile(
        EvaluationAssistanceProposal proposal,
        EvaluationProposalDecision approval,
        DateTimeOffset now,
        ProjectContentBalanceProfile? replaces = null)
    {
        RequireApproval(proposal, approval, EvaluationProposalKind.AdvisoryBalanceProfile);
        var profile = JsonSerializer.Deserialize<ContentBalanceProfile>(proposal.CandidateJson, JsonOptions)
            ?? throw new ArgumentException("The approved balance proposal is not a balance profile.", nameof(proposal));
        return ContentBalanceProfileService.Create(proposal.ProjectId, proposal.ActiveGameSchemaId, proposal.ActiveGameSchemaRevision, profile, now, replaces);
    }

    public static EvaluationSchemaChangeDraft BuildApprovedSchemaChangeDraft(EvaluationAssistanceProposal proposal, EvaluationProposalDecision approval)
    {
        RequireApproval(proposal, approval, EvaluationProposalKind.MechanicalValidation);
        var candidate = JsonNode.Parse(proposal.CandidateJson) as JsonObject ?? throw new ArgumentException("The approved validation proposal is not a JSON object.", nameof(proposal));
        return new EvaluationSchemaChangeDraft(candidate["title"]!.GetValue<string>(), candidate["ruleText"]!.GetValue<string>());
    }

    private static void RequireApproval(EvaluationAssistanceProposal proposal, EvaluationProposalDecision approval, EvaluationProposalKind kind)
    {
        if (proposal.Kind != kind || approval.Kind != EvaluationProposalDecisionKind.Approved || approval.ProposalId != proposal.Id || approval.ProposalVersion != proposal.Version)
            throw new InvalidOperationException("The exact evaluation proposal version must be approved before it can be used.");
    }

    private static void ValidateCandidate(EvaluationProposalKind kind, JsonObject candidate, string entityTypeId, HashSet<string> properties, IReadOnlyList<string> sourceRuleIds)
    {
        if (kind == EvaluationProposalKind.MechanicalValidation)
        {
            Require(candidate["title"]?.GetValue<string>(), "A mechanical validation proposal needs a title.");
            Require(candidate["ruleText"]?.GetValue<string>(), "A mechanical validation proposal needs authoritative rule text.");
            var targets = candidate["targetPropertyIds"] as JsonArray ?? throw new ArgumentException("A mechanical validation proposal needs targetPropertyIds.");
            var targetIds = targets.Select(node => node?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToArray();
            if (targetIds.Length == 0 || targetIds.Any(id => !properties.Contains(id)))
                throw new ArgumentException("Mechanical validation targets must be properties on the selected content type.");
            var constraints = candidate["suggestedConstraints"] as JsonObject ?? throw new ArgumentException("A mechanical validation proposal needs suggestedConstraints.");
            if (constraints.Count == 0 || constraints.Any(pair => !properties.Contains(pair.Key) || pair.Value is not JsonObject))
                throw new ArgumentException("Suggested constraints must be keyed by selected content-type properties.");
            return;
        }

        var profile = JsonSerializer.Deserialize<ContentBalanceProfile>(candidate.ToJsonString(), JsonOptions)
            ?? throw new ArgumentException("An advisory proposal must contain a balance profile.");
        if (profile.EntityTypeId != entityTypeId || !profile.SourceWorkingRuleIds.Order().SequenceEqual(sourceRuleIds.Order()))
            throw new ArgumentException("The balance profile must use the selected content type and cited Working Rules.");
        if (profile.Signals.Any(signal => !properties.Contains(signal.PropertyId)))
            throw new ArgumentException("Every balance signal must target a property on the selected content type.");
        _ = ContentBalanceProfileService.Create("validation", "schema", 1, profile, DateTimeOffset.UnixEpoch);
    }

    private static HashSet<string>? EntityPropertyIds(JsonObject schema, string entityTypeId)
    {
        var entity = (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item => DefinitionId(item) == entityTypeId);
        if (entity is null) return null;
        var ids = (entity["properties"] as JsonArray)?.OfType<JsonObject>().Select(DefinitionId).Where(id => id is not null).Cast<string>().ToHashSet(StringComparer.Ordinal) ?? [];
        var fragments = (schema["schemaFragments"] as JsonArray)?.OfType<JsonObject>().Where(item => DefinitionId(item) is not null).ToDictionary(item => DefinitionId(item)!, StringComparer.Ordinal) ?? [];
        foreach (var fragmentId in (entity["fragmentIds"] as JsonArray)?.Select(item => item?.GetValue<string>()).Where(id => id is not null).Cast<string>() ?? [])
            if (fragments.TryGetValue(fragmentId, out var fragment))
                foreach (var id in (fragment["properties"] as JsonArray)?.OfType<JsonObject>().Select(DefinitionId).Where(id => id is not null).Cast<string>() ?? []) ids.Add(id);
        return ids;
    }

    private static string? DefinitionId(JsonObject item) => (item["metadata"] as JsonObject)?["id"]?.GetValue<string>() ?? item["id"]?.GetValue<string>();
    private static void Require(string? value, string message) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message); }
}

public sealed record EvaluationAssistanceGenerationRequest(
    string ProjectId,
    string EntityTypeId,
    EvaluationProposalKind Kind,
    JsonObject ActiveSchema,
    IReadOnlyList<WorkingRule> Rules,
    string DesignerGoal);

public sealed record EvaluationAssistanceGenerationResult(string Summary, JsonObject Candidate, IReadOnlyList<string> SourceWorkingRuleIds, string RawModelOutput);
public sealed class EvaluationAssistanceFormatException(string message) : Exception(message);

public sealed class EvaluationAssistanceGenerationService(ILanguageModelClient client)
{
    public const int MaximumContextRules = 30;
    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "or", "of", "to", "for", "in", "on", "with", "is", "are", "be",
        "must", "should", "this", "that", "these", "those", "from", "by", "as", "it", "its",
        "can", "may", "only", "one", "use", "using"
    };

    public async Task<EvaluationAssistanceGenerationResult> GenerateAsync(EvaluationAssistanceGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rules.Count == 0 || string.IsNullOrWhiteSpace(request.DesignerGoal)) throw new ArgumentException("Working Rules and a designer goal are required.");
        var contextRules = SelectContextRules(request);
        var contract = request.Kind == EvaluationProposalKind.MechanicalValidation
            ? "candidate must be {\"title\":\"...\",\"ruleText\":\"...\",\"targetPropertyIds\":[\"property-id\"],\"suggestedConstraints\":{\"property-id\":{\"minimum\":0,\"maximum\":10}}}. Use only applicable minimum, maximum, minimumLength, maximumLength, pattern, or distinctItems constraints."
            : "candidate must be a ContentBalanceProfile: {\"id\":\"...\",\"entityTypeId\":\"...\",\"sourceWorkingRuleIds\":[\"rule-id\"],\"signals\":[{\"id\":\"...\",\"propertyId\":\"...\",\"measure\":\"numericValue\",\"minimumReferenceValue\":0,\"maximumReferenceValue\":10,\"message\":\"...\",\"references\":[{\"id\":\"...\",\"label\":\"...\",\"value\":5,\"rationale\":\"...\"}]}]}. measure is numericValue or collectionCount.";
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, $"You propose one reviewable, game-specific evaluation addition. Never invent universal balance formulas, approve your output, or change project data. Return JSON only: {{\"summary\":\"...\",\"sourceWorkingRuleIds\":[\"...\"],\"candidate\":{{...}}}}. {contract}"),
            new LlmMessage(LlmMessageRole.System, $"Selected content type: {request.EntityTypeId}\nActive game-owned schema:\n{request.ActiveSchema.ToJsonString()}"),
            new LlmMessage(LlmMessageRole.System, $"Available Working Rules (bounded selection of {contextRules.Count} from {request.Rules.Count}; cite only these IDs):\n" + string.Join("\n", contextRules.Select(rule => $"[{rule.Id}] {rule.AuthoritativeText}"))),
            new LlmMessage(LlmMessageRole.User, request.DesignerGoal)
        };
        var response = await client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.15, MaxOutputTokens: 1600, RequireJsonObject: true), cancellationToken);
        return ParseResponse(response.Content, contextRules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal));
    }

    private static IReadOnlyList<WorkingRule> SelectContextRules(EvaluationAssistanceGenerationRequest request)
    {
        var entity = (request.ActiveSchema["entityTypes"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(type => (type["metadata"]?["id"] ?? type["id"])?.GetValue<string>() == request.EntityTypeId);
        var typeTerms = Terms(request.EntityTypeId + " " + (entity?["metadata"]?["name"] ?? entity?["name"])?.GetValue<string>());
        var fieldTerms = Terms(string.Join(" ", (entity?["properties"] as JsonArray)?.OfType<JsonObject>()
            .Select(property => (property["metadata"]?["id"] ?? property["name"])?.GetValue<string>()) ?? []));
        var goalTerms = Terms(request.DesignerGoal);
        var ranked = request.Rules.Select((rule, index) =>
        {
            var name = Terms(rule.Name);
            var text = Terms(rule.AuthoritativeText);
            var score = name.Count(typeTerms.Contains) * 8 + text.Count(typeTerms.Contains) * 4
                + name.Count(goalTerms.Contains) * 3 + text.Count(goalTerms.Contains)
                + text.Count(fieldTerms.Contains) * 2;
            return (Rule: rule, Index: index, Score: score);
        }).OrderByDescending(item => item.Score).ThenBy(item => item.Index).ToArray();
        var relevant = ranked.Where(item => item.Score > 0).ToArray();
        return (relevant.Length > 0 ? relevant : ranked).Take(MaximumContextRules).Select(item => item.Rule).ToArray();
    }

    private static HashSet<string> Terms(string? text)
    {
        var split = Regex.Replace(text ?? string.Empty, @"(?<=[\p{Ll}\p{N}])(?=\p{Lu})", " ");
        return Regex.Matches(split, @"[\p{L}\p{N}]+")
            .Select(match => match.Value.ToLowerInvariant()).Where(term => !CommonWords.Contains(term)).ToHashSet(StringComparer.Ordinal);
    }

    public static EvaluationAssistanceGenerationResult ParseResponse(string content, IReadOnlySet<string> availableRuleIds)
    {
        try
        {
            var root = JsonNode.Parse(content) as JsonObject ?? throw new EvaluationAssistanceFormatException("The model did not return a JSON object.");
            var summary = root["summary"]?.GetValue<string>()?.Trim();
            var candidate = root["candidate"] as JsonObject;
            var sourceIds = (root["sourceWorkingRuleIds"] as JsonArray)?.Select(item => item?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray() ?? [];
            if (string.IsNullOrWhiteSpace(summary) || candidate is null || sourceIds.Length == 0)
                throw new EvaluationAssistanceFormatException("The model response requires summary, candidate, and sourceWorkingRuleIds.");
            if (sourceIds.Any(id => !availableRuleIds.Contains(id))) throw new EvaluationAssistanceFormatException("The model cited a Working Rule that was not supplied.");
            return new EvaluationAssistanceGenerationResult(summary, (JsonObject)candidate.DeepClone(), sourceIds, content);
        }
        catch (JsonException) { throw new EvaluationAssistanceFormatException("The model did not return valid JSON."); }
    }
}
