namespace WorldSeed.SchemaTranslation;

/// <summary>
/// A durable, designer-approved request to evolve a project's still-editable schema proposal.
/// It is intentionally separate from source-backed Working Rules: a request records the
/// author's design decision while the normal schema translator turns that decision into a
/// bounded, validated schema change.
/// </summary>
public enum SchemaChangeRequestStatus { Pending, Applying, Applied, Failed }
public enum SchemaChangeRequestOrigin { Designer, ContentSuggestion, SystemSuggestion }

public sealed record SchemaChangeRequest(
    string Id,
    string ProjectId,
    string Title,
    string ProposedRule,
    SchemaChangeRequestOrigin Origin,
    SchemaChangeRequestStatus Status,
    string? FailureDetail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? AppliedAt);

public interface IProjectSchemaChangeRequestStore
{
    Task SaveAsync(SchemaChangeRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemaChangeRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class SchemaChangeRequestService
{
    public static SchemaChangeRequest Create(string projectId, string title, string proposedRule, SchemaChangeRequestOrigin origin, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("A project is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A change title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(proposedRule)) throw new ArgumentException("Describe the rule or schema change before adding it.", nameof(proposedRule));
        return new SchemaChangeRequest(
            "schema-change-" + Guid.NewGuid().ToString("N"), projectId, title.Trim(), proposedRule.Trim(), origin,
            SchemaChangeRequestStatus.Pending, null, now, now, null);
    }

    public static SchemaChangeRequest MarkApplying(SchemaChangeRequest request, DateTimeOffset now) =>
        request with { Status = SchemaChangeRequestStatus.Applying, FailureDetail = null, UpdatedAt = now };

    public static SchemaChangeRequest MarkApplied(SchemaChangeRequest request, DateTimeOffset now) =>
        request with { Status = SchemaChangeRequestStatus.Applied, FailureDetail = null, UpdatedAt = now, AppliedAt = now };

    public static SchemaChangeRequest MarkFailed(SchemaChangeRequest request, string detail, DateTimeOffset now) =>
        request with { Status = SchemaChangeRequestStatus.Failed, FailureDetail = string.IsNullOrWhiteSpace(detail) ? "Schema translation failed." : detail.Trim(), UpdatedAt = now };
}
