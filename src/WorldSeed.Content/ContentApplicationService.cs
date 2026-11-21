using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using WorldSeed.GameData;

namespace WorldSeed.Content;

public enum ContentApplicationStatus { Pending, Applied }

/// <summary>An immutable audit record linking one approved proposal version to canonical Game Data.</summary>
public sealed record ContentApplication(
    string Id,
    string ProjectId,
    string ProposalId,
    int ProposalVersion,
    string ApprovalDecisionId,
    string EntityInstanceId,
    string EntityTypeId,
    string CandidateJson,
    string CandidateFingerprint,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string TargetGameDataId,
    ContentApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt = null,
    DateTimeOffset? ResultingGameDataUpdatedAt = null,
    string? RelationshipInstancesJson = null);

public interface IContentApplicationStore
{
    Task SaveAsync(ContentApplication application, CancellationToken cancellationToken = default);
    Task<ContentApplication?> LoadAsync(string projectId, string proposalId, int proposalVersion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentApplication>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public sealed record ContentApplicationPreparation(ContentApplication Application, ProjectGameData UpdatedGameData);

public static class ContentApplicationService
{
    public static ContentApplicationPreparation Prepare(
        ContentProposal proposal,
        ContentProposalDecision approval,
        ActiveGameSchema activeSchema,
        ProjectGameData gameData,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(activeSchema);
        ArgumentNullException.ThrowIfNull(gameData);
        if (approval.Kind != ContentProposalDecisionKind.Approved || approval.ProposalId != proposal.Id || approval.ProposalVersion != proposal.Version)
            throw new InvalidOperationException("The exact proposal version must have a current approval decision before it can be applied.");
        if (!string.Equals(proposal.ActiveGameSchemaId, activeSchema.Id, StringComparison.Ordinal) || proposal.ActiveGameSchemaRevision != activeSchema.Revision)
            throw new InvalidOperationException("The proposal must be re-evaluated against the current active schema before it can be applied.");
        if (!string.Equals(gameData.ActiveGameSchemaId, activeSchema.Id, StringComparison.Ordinal) || gameData.ActiveGameSchemaRevision != activeSchema.Revision)
            throw new InvalidOperationException("Canonical Game Data does not match the current active schema revision.");

        var candidateId = proposal.Candidate["id"]?.GetValue<string>();
        var entityTypeId = proposal.Candidate["entityTypeId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(entityTypeId) || proposal.Candidate["values"] is not JsonObject)
            throw new InvalidOperationException("The proposal candidate requires an id, entityTypeId, and values object.");
        var root = JsonNode.Parse(gameData.DataJson) as JsonObject ?? throw new InvalidOperationException("Canonical Game Data is not a JSON object.");
        var entities = root["entityInstances"] as JsonArray ?? throw new InvalidOperationException("Canonical Game Data has no entityInstances array.");
        if (entities.OfType<JsonObject>().Any(entity => string.Equals(entity["id"]?.GetValue<string>(), candidateId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Canonical Game Data already contains entity id '{candidateId}'. WorldSeed will not overwrite or merge it automatically.");

        foreach (var link in proposal.ProposedRelationships ?? [])
            if (link["fromEntityInstanceId"]?.GetValue<string>() != candidateId && link["toEntityInstanceId"]?.GetValue<string>() != candidateId)
                throw new InvalidOperationException("Proposed relationships must involve this candidate.");
        var updated = Preview(proposal, gameData, now);
        var validation = GameDataService.Validate(activeSchema, updated);
        if (!validation.IsValid)
            throw new InvalidOperationException("The approved proposal is not mechanically valid against the current schema: " + string.Join(" ", validation.Issues.Select(issue => $"{issue.Path}: {issue.Message}")));

        var snapshot = proposal.Candidate.ToJsonString();
        var linksJson = proposal.ProposedRelationships is { Count: > 0 }
            ? new JsonArray(proposal.ProposedRelationships.Select(link => link.DeepClone()).ToArray()).ToJsonString() : null;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot + (linksJson is null ? "" : "\n" + linksJson)))).ToLowerInvariant();
        var application = new ContentApplication(
            $"content-application-{proposal.Id}-v{proposal.Version}",
            proposal.ProjectId,
            proposal.Id,
            proposal.Version,
            approval.Id,
            candidateId,
            entityTypeId,
            snapshot,
            fingerprint,
            activeSchema.Id,
            activeSchema.Revision,
            gameData.Id,
            ContentApplicationStatus.Pending,
            now, RelationshipInstancesJson: linksJson);
        return new ContentApplicationPreparation(application, updated);
    }

    public static ContentApplication Complete(ContentApplication application, ProjectGameData resultingGameData, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(resultingGameData);
        return application with
        {
            Status = ContentApplicationStatus.Applied,
            AppliedAt = application.AppliedAt ?? now,
            ResultingGameDataUpdatedAt = resultingGameData.UpdatedAt
        };
    }

    public static bool CanonicalEntryMatches(ContentApplication application, ProjectGameData gameData)
    {
        var root = JsonNode.Parse(gameData.DataJson) as JsonObject;
        var existing = (root?["entityInstances"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(entity => string.Equals(entity["id"]?.GetValue<string>(), application.EntityInstanceId, StringComparison.OrdinalIgnoreCase));
        var snapshot = JsonNode.Parse(application.CandidateJson);
        var expectedLinks = application.RelationshipInstancesJson is null ? null : JsonNode.Parse(application.RelationshipInstancesJson) as JsonArray;
        var actualLinks = root?["relationshipInstances"] as JsonArray;
        return existing is not null && snapshot is not null && JsonNode.DeepEquals(existing, snapshot) &&
            (expectedLinks?.All(expected => actualLinks?.Any(actual => JsonNode.DeepEquals(actual, expected)) == true) ?? true);
    }

    /// <summary>Review in canonical context without changing its original document.</summary>
    public static ProjectGameData Preview(ContentProposal proposal, ProjectGameData gameData, DateTimeOffset now)
    {
        if (proposal.ProjectId != gameData.ProjectId || proposal.ActiveGameSchemaId != gameData.ActiveGameSchemaId ||
            proposal.ActiveGameSchemaRevision != gameData.ActiveGameSchemaRevision)
            throw new InvalidOperationException("The proposal and canonical context belong to different schema revisions or projects.");
        var root = JsonNode.Parse(gameData.DataJson)!.AsObject();
        var entities = root["entityInstances"]!.AsArray();
        if (!entities.Any(entity => JsonNode.DeepEquals(entity, proposal.Candidate))) entities.Add(proposal.Candidate.DeepClone());
        if (root["relationshipInstances"] is not JsonArray) root["relationshipInstances"] = new JsonArray();
        var links = root["relationshipInstances"]!.AsArray();
        foreach (var link in proposal.ProposedRelationships ?? [])
            if (!links.Any(existing => JsonNode.DeepEquals(existing, link))) links.Add(link.DeepClone());
        return gameData with { DataJson = root.ToJsonString(), UpdatedAt = now };
    }
}
