namespace WorldSeed.Publication;

public enum PublicationEditorialDecisionKind { Approved, Rejected, RevisionRequested }

public sealed record PublicationEditorialTarget(
    string Id,
    string Title,
    IReadOnlyList<string> SourceUnitIds);

public sealed record PublicationEditorialProposal(
    string Id,
    string SeriesId,
    string ProjectId,
    string SnapshotId,
    string TargetSectionId,
    int Revision,
    IReadOnlyList<string> SourceUnitIds,
    string ProposedMarkdown,
    DateTimeOffset CreatedAt,
    string? ReplacesProposalId);

public sealed record PublicationEditorialDecision(
    string Id,
    string ProjectId,
    string ProposalId,
    int ProposalRevision,
    PublicationEditorialDecisionKind Kind,
    string? Note,
    string? ReplacementProposalId,
    DateTimeOffset CreatedAt);

public interface IPublicationEditorialStore
{
    Task SaveProposalAsync(PublicationEditorialProposal proposal, CancellationToken cancellationToken = default);
    Task<PublicationEditorialProposal?> LoadProposalAsync(string projectId, string proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicationEditorialProposal>> ListProposalsAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveDecisionAsync(PublicationEditorialDecision decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicationEditorialDecision>> ListDecisionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class PublicationEditorialProposalService
{
    public const string RulesTargetId = "rules";
    public const string ValueTypesTargetId = "categories-and-value-types";
    public const string RelationshipsTargetId = "relationships";

    public static IReadOnlyList<PublicationEditorialTarget> DescribeTargets(PublicationSnapshot snapshot)
    {
        var units = RequireUnits(snapshot);
        var targets = new List<PublicationEditorialTarget>();
        AddTarget(targets, RulesTargetId, "Rules", units.Where(unit => unit.Kind == PublicationUnitKind.Rule));
        AddTarget(targets, ValueTypesTargetId, "Categories And Value Types", units.Where(unit => unit.Kind == PublicationUnitKind.ValueType));
        foreach (var contentType in units.Where(unit => unit.Kind == PublicationUnitKind.ContentType))
        {
            var sourceIds = units.Where(unit => unit.Id == contentType.Id || unit.ParentId == contentType.Id).Select(unit => unit.Id).ToArray();
            targets.Add(new PublicationEditorialTarget(contentType.Id, contentType.Title, sourceIds));
        }
        AddTarget(targets, RelationshipsTargetId, "Relationships", units.Where(unit => unit.Kind == PublicationUnitKind.Relationship));
        return targets;
    }

    public static PublicationEditorialProposal Create(
        PublicationSnapshot snapshot,
        string targetSectionId,
        IReadOnlyList<string> sourceUnitIds,
        string proposedMarkdown,
        DateTimeOffset now,
        PublicationEditorialProposal? prior = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sourceUnitIds);
        if (string.IsNullOrWhiteSpace(targetSectionId)) throw new ArgumentException("An editorial target section is required.", nameof(targetSectionId));
        if (string.IsNullOrWhiteSpace(proposedMarkdown)) throw new ArgumentException("An editorial proposal needs Markdown text.", nameof(proposedMarkdown));

        var target = DescribeTargets(snapshot).SingleOrDefault(candidate => string.Equals(candidate.Id, targetSectionId, StringComparison.Ordinal))
            ?? throw new ArgumentException("The editorial target does not exist in this immutable publication snapshot.", nameof(targetSectionId));
        ValidateExactSources(sourceUnitIds, target.SourceUnitIds);

        if (prior is not null &&
            (!string.Equals(prior.ProjectId, snapshot.ProjectId, StringComparison.Ordinal) ||
             !string.Equals(prior.SnapshotId, snapshot.Id, StringComparison.Ordinal) ||
             !string.Equals(prior.TargetSectionId, target.Id, StringComparison.Ordinal)))
            throw new ArgumentException("An editorial revision must retain its project, immutable snapshot, and target section.", nameof(prior));

        var seriesId = prior?.SeriesId ?? "publication-editorial-series-" + Guid.NewGuid().ToString("N");
        var revision = (prior?.Revision ?? 0) + 1;
        return new PublicationEditorialProposal(
            $"{seriesId}-r{revision}", seriesId, snapshot.ProjectId, snapshot.Id, target.Id, revision,
            sourceUnitIds.ToArray(), proposedMarkdown.Trim(), now, prior?.Id);
    }

    public static PublicationEditorialDecision Decide(
        PublicationEditorialProposal proposal,
        PublicationEditorialDecisionKind kind,
        string? note,
        DateTimeOffset now,
        PublicationEditorialProposal? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (kind == PublicationEditorialDecisionKind.RevisionRequested && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("An editorial revision request needs designer guidance.", nameof(note));
        if (replacement is not null &&
            (!string.Equals(replacement.ProjectId, proposal.ProjectId, StringComparison.Ordinal) ||
             !string.Equals(replacement.SeriesId, proposal.SeriesId, StringComparison.Ordinal) ||
             !string.Equals(replacement.ReplacesProposalId, proposal.Id, StringComparison.Ordinal)))
            throw new ArgumentException("An editorial replacement must be the next revision of the reviewed proposal series.", nameof(replacement));
        return new PublicationEditorialDecision(
            "publication-editorial-decision-" + Guid.NewGuid().ToString("N"), proposal.ProjectId, proposal.Id,
            proposal.Revision, kind, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), replacement?.Id, now);
    }

    public static void Validate(PublicationSnapshot snapshot, PublicationEditorialProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(proposal);
        if (!string.Equals(snapshot.ProjectId, proposal.ProjectId, StringComparison.Ordinal) ||
            !string.Equals(snapshot.Id, proposal.SnapshotId, StringComparison.Ordinal))
            throw new ArgumentException("The editorial proposal must belong to its immutable publication snapshot.", nameof(proposal));
        var target = DescribeTargets(snapshot).SingleOrDefault(candidate => string.Equals(candidate.Id, proposal.TargetSectionId, StringComparison.Ordinal))
            ?? throw new ArgumentException("The editorial target does not exist in this immutable publication snapshot.", nameof(proposal));
        ValidateExactSources(proposal.SourceUnitIds, target.SourceUnitIds);
        if (proposal.Revision < 1 || string.IsNullOrWhiteSpace(proposal.Id) || string.IsNullOrWhiteSpace(proposal.SeriesId) || string.IsNullOrWhiteSpace(proposal.ProposedMarkdown))
            throw new ArgumentException("The editorial proposal is incomplete.", nameof(proposal));
    }

    public static void ValidateDecision(PublicationEditorialProposal proposal, PublicationEditorialDecision decision, PublicationEditorialProposal? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(decision.Id) ||
            !string.Equals(decision.ProjectId, proposal.ProjectId, StringComparison.Ordinal) ||
            !string.Equals(decision.ProposalId, proposal.Id, StringComparison.Ordinal) ||
            decision.ProposalRevision != proposal.Revision)
            throw new ArgumentException("The editorial decision must identify the exact proposal revision.", nameof(decision));
        if (decision.Kind == PublicationEditorialDecisionKind.RevisionRequested && string.IsNullOrWhiteSpace(decision.Note))
            throw new ArgumentException("An editorial revision request needs designer guidance.", nameof(decision));
        if (decision.ReplacementProposalId is null && replacement is not null ||
            decision.ReplacementProposalId is not null && (replacement is null || !string.Equals(decision.ReplacementProposalId, replacement.Id, StringComparison.Ordinal)))
            throw new ArgumentException("The editorial decision replacement does not match a persisted proposal.", nameof(decision));
        if (replacement is not null &&
            (!string.Equals(replacement.ProjectId, proposal.ProjectId, StringComparison.Ordinal) ||
             !string.Equals(replacement.SeriesId, proposal.SeriesId, StringComparison.Ordinal) ||
             !string.Equals(replacement.ReplacesProposalId, proposal.Id, StringComparison.Ordinal)))
            throw new ArgumentException("The editorial replacement must be the next revision of the reviewed proposal series.", nameof(replacement));
    }

    private static IReadOnlyList<PublicationUnit> RequireUnits(PublicationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Units is { Count: > 0 }
            ? snapshot.Units
            : throw new InvalidOperationException("Section-scoped editorial proposals require a publication snapshot with structured source units.");
    }

    private static void AddTarget(List<PublicationEditorialTarget> targets, string id, string title, IEnumerable<PublicationUnit> units)
    {
        var sourceIds = units.Select(unit => unit.Id).ToArray();
        if (sourceIds.Length > 0) targets.Add(new PublicationEditorialTarget(id, title, sourceIds));
    }

    private static void ValidateExactSources(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
    {
        if (actual.Count != expected.Count || actual.Distinct(StringComparer.Ordinal).Count() != actual.Count || expected.Except(actual, StringComparer.Ordinal).Any())
            throw new ArgumentException("An editorial proposal must cite every source unit for its target section exactly once.", nameof(actual));
    }
}
