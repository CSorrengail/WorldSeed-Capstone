using System.Text.Json.Nodes;

namespace WorldSeed.Content;

public static class ContentProposalLifecycleService
{
    public static ContentProposal Create(string projectId, string entityTypeId, JsonObject candidate, string activeSchemaId, int activeSchemaRevision, string workingRuleSetId, int workingRuleSetRevision, DateTimeOffset now, ContentProposal? replaces = null)
    {
        Require(projectId, "A project is required.");
        Require(entityTypeId, "An entity type is required.");
        Require(activeSchemaId, "An active schema is required.");
        Require(workingRuleSetId, "A Working Rule Set is required.");
        ArgumentNullException.ThrowIfNull(candidate);
        if (activeSchemaRevision < 1 || workingRuleSetRevision < 1) throw new ArgumentException("Schema and Working Rule Set revisions must be positive.");
        if (!string.Equals(candidate["entityTypeId"]?.GetValue<string>(), entityTypeId, StringComparison.Ordinal)) throw new ArgumentException("Candidate entityTypeId must match the proposal entity type.", nameof(candidate));
        if (candidate["values"] is not JsonObject) throw new ArgumentException("A candidate needs a values object.", nameof(candidate));

        return new ContentProposal(
            "content-proposal-" + Guid.NewGuid().ToString("N"), projectId, entityTypeId, (JsonObject)candidate.DeepClone(),
            activeSchemaId, activeSchemaRevision, workingRuleSetId, workingRuleSetRevision,
            (replaces?.Version ?? 0) + 1, now, replaces?.Id);
    }

    public static ContentProposalDecision Decide(ContentProposal proposal, ContentProposalDecisionKind kind, string? note, DateTimeOffset now, ContentProposal? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (kind == ContentProposalDecisionKind.RevisionRequested && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A revision request needs designer guidance.", nameof(note));
        if (replacement is not null && (!string.Equals(replacement.ProjectId, proposal.ProjectId, StringComparison.Ordinal) || !string.Equals(replacement.ReplacesProposalId, proposal.Id, StringComparison.Ordinal)))
            throw new ArgumentException("A replacement must belong to this project and identify the reviewed proposal as its base.", nameof(replacement));
        return new ContentProposalDecision("content-decision-" + Guid.NewGuid().ToString("N"), proposal.ProjectId, proposal.Id, proposal.Version, kind, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), replacement?.Id, now);
    }

    private static void Require(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message);
    }
}
