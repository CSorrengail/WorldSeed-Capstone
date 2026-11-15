using System.Text.Json.Nodes;
using WorldSeed.GameData;

namespace WorldSeed.Content;

public sealed record ContentExpansionStage(
    string Id,
    string EntityTypeId,
    int Quantity,
    string DesignerInstruction,
    IReadOnlyList<string> DependsOnStageIds);

/// <summary>
/// A durable designer-approved plan for generating several kinds of game-owned content.
/// It creates proposal work only; it never approves or applies generated content.
/// </summary>
public sealed record ContentExpansionRun(
    string Id,
    string ProjectId,
    string FormatVersion,
    string Name,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    IReadOnlyList<ContentExpansionStage> Stages,
    DateTimeOffset CreatedAt);

/// <summary>Immutable linkage between one planned slot and one independently reviewable proposal.</summary>
public sealed record ContentExpansionProposalLink(
    string Id,
    string ProjectId,
    string RunId,
    string StageId,
    int Ordinal,
    string ProposalId,
    int ProposalVersion,
    DateTimeOffset CreatedAt);

public enum ContentExpansionStageState
{
    BlockedByDependencies,
    ReadyToGenerate,
    AwaitingDesignerReview,
    Complete
}

public sealed record ContentExpansionStageProgress(
    string StageId,
    string EntityTypeId,
    int Quantity,
    int GeneratedCount,
    int AppliedCount,
    ContentExpansionStageState State,
    IReadOnlyList<string> BlockingStageIds);

public sealed record ContentExpansionRunProgress(
    string RunId,
    IReadOnlyList<ContentExpansionStageProgress> Stages,
    string? NextStageId)
{
    public bool IsComplete => Stages.Count > 0 && Stages.All(stage => stage.State == ContentExpansionStageState.Complete);
}

public sealed record ContentExpansionRunCreationRequest(
    string Name,
    IReadOnlyList<ContentExpansionStage> Stages);

public sealed record ContentExpansionRunWorkspaceItem(
    ContentExpansionRun Run,
    IReadOnlyList<ContentExpansionProposalLink> ProposalLinks,
    ContentExpansionRunProgress Progress);

public interface IContentExpansionRunStore
{
    Task SaveRunAsync(ContentExpansionRun run, CancellationToken cancellationToken = default);
    Task<ContentExpansionRun?> LoadRunAsync(string projectId, string runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentExpansionRun>> ListRunsAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveProposalLinkAsync(ContentExpansionProposalLink link, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentExpansionProposalLink>> ListProposalLinksAsync(string projectId, string runId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class ContentExpansionRunService
{
    private const string FormatVersion = "0.1";

    public static ContentExpansionRun Create(
        string projectId,
        string name,
        ActiveGameSchema activeSchema,
        string workingRuleSetId,
        int workingRuleSetRevision,
        IReadOnlyList<ContentExpansionStage> stages,
        DateTimeOffset now)
    {
        Require(projectId, "A project is required.");
        Require(name, "A run name is required.");
        ArgumentNullException.ThrowIfNull(activeSchema);
        ArgumentNullException.ThrowIfNull(stages);
        if (!string.Equals(projectId, activeSchema.ProjectId, StringComparison.Ordinal))
            throw new ArgumentException("The active schema belongs to another project.", nameof(activeSchema));
        Require(workingRuleSetId, "A Working Rule Set is required.");
        if (workingRuleSetRevision < 1) throw new ArgumentException("The Working Rule Set revision must be positive.", nameof(workingRuleSetRevision));
        if (!string.Equals(activeSchema.WorkingRuleSetId, workingRuleSetId, StringComparison.Ordinal) || activeSchema.WorkingRuleSetRevision != workingRuleSetRevision)
            throw new ArgumentException("The run must use the Working Rule Set revision bound to the active schema.", nameof(workingRuleSetRevision));

        ValidateStages(activeSchema, stages);
        return new ContentExpansionRun(
            "content-expansion-" + Guid.NewGuid().ToString("N"),
            projectId,
            FormatVersion,
            name.Trim(),
            activeSchema.Id,
            activeSchema.Revision,
            workingRuleSetId,
            workingRuleSetRevision,
            stages.Select(stage => stage with
            {
                Id = stage.Id.Trim(),
                EntityTypeId = stage.EntityTypeId.Trim(),
                DesignerInstruction = stage.DesignerInstruction.Trim(),
                DependsOnStageIds = stage.DependsOnStageIds.ToArray()
            }).ToArray(),
            now);
    }

    public static ContentExpansionRunProgress Evaluate(
        ContentExpansionRun run,
        IReadOnlyList<ContentExpansionProposalLink> links,
        IReadOnlyList<ContentProposal> proposals,
        IReadOnlyList<ContentApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(run);
        links ??= [];
        proposals ??= [];
        applications ??= [];
        var proposalById = proposals.Where(item => item.ProjectId == run.ProjectId)
            .GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var applied = applications.Where(item => item.ProjectId == run.ProjectId && item.Status == ContentApplicationStatus.Applied)
            .Select(item => (item.ProposalId, item.ProposalVersion)).ToHashSet();
        var progress = new List<ContentExpansionStageProgress>();

        foreach (var stage in run.Stages)
        {
            var stageLinks = links.Where(link => LinkBelongsTo(link, run, stage))
                .GroupBy(link => link.Ordinal).Select(group => group.OrderByDescending(link => link.CreatedAt).First()).ToArray();
            var validLinks = stageLinks.Where(link => proposalById.TryGetValue(link.ProposalId, out var proposal)
                && proposal.Version == link.ProposalVersion
                && string.Equals(proposal.EntityTypeId, stage.EntityTypeId, StringComparison.Ordinal)
                && string.Equals(proposal.ActiveGameSchemaId, run.ActiveGameSchemaId, StringComparison.Ordinal)
                && proposal.ActiveGameSchemaRevision == run.ActiveGameSchemaRevision
                && string.Equals(proposal.WorkingRuleSetId, run.WorkingRuleSetId, StringComparison.Ordinal)
                && proposal.WorkingRuleSetRevision == run.WorkingRuleSetRevision).ToArray();
            var generated = Math.Min(stage.Quantity, validLinks.Length);
            var appliedCount = validLinks.Count(link => applied.Contains((link.ProposalId, link.ProposalVersion)));
            var blockers = stage.DependsOnStageIds.Where(id => progress.First(item => item.StageId == id).State != ContentExpansionStageState.Complete).ToArray();
            var state = blockers.Length > 0
                ? ContentExpansionStageState.BlockedByDependencies
                : appliedCount >= stage.Quantity
                    ? ContentExpansionStageState.Complete
                    : generated < stage.Quantity
                        ? ContentExpansionStageState.ReadyToGenerate
                        : ContentExpansionStageState.AwaitingDesignerReview;
            progress.Add(new ContentExpansionStageProgress(stage.Id, stage.EntityTypeId, stage.Quantity, generated, appliedCount, state, blockers));
        }

        return new ContentExpansionRunProgress(run.Id, progress, progress.FirstOrDefault(item => item.State == ContentExpansionStageState.ReadyToGenerate)?.StageId);
    }

    public static ContentExpansionProposalLink LinkProposal(
        ContentExpansionRun run,
        string stageId,
        ContentProposal proposal,
        IReadOnlyList<ContentExpansionProposalLink> existingLinks,
        IReadOnlyList<ContentProposal> proposals,
        IReadOnlyList<ContentApplication> applications,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(proposal);
        var stage = run.Stages.SingleOrDefault(item => item.Id == stageId)
            ?? throw new ArgumentException("The expansion stage does not exist in this run.", nameof(stageId));
        if (!string.Equals(proposal.ProjectId, run.ProjectId, StringComparison.Ordinal)
            || !string.Equals(proposal.EntityTypeId, stage.EntityTypeId, StringComparison.Ordinal)
            || !string.Equals(proposal.ActiveGameSchemaId, run.ActiveGameSchemaId, StringComparison.Ordinal)
            || proposal.ActiveGameSchemaRevision != run.ActiveGameSchemaRevision
            || !string.Equals(proposal.WorkingRuleSetId, run.WorkingRuleSetId, StringComparison.Ordinal)
            || proposal.WorkingRuleSetRevision != run.WorkingRuleSetRevision)
            throw new ArgumentException("The proposal does not match this run stage and its exact schema/rule revisions.", nameof(proposal));
        if (existingLinks.Any(link => link.ProjectId == run.ProjectId && link.RunId == run.Id && link.ProposalId == proposal.Id && link.ProposalVersion == proposal.Version))
            throw new InvalidOperationException("This proposal version is already linked to the expansion run.");

        var progress = Evaluate(run, existingLinks, proposals.Append(proposal).DistinctBy(item => item.Id).ToArray(), applications);
        var stageProgress = progress.Stages.Single(item => item.StageId == stage.Id);
        if (stageProgress.State == ContentExpansionStageState.BlockedByDependencies)
            throw new InvalidOperationException("All dependency stages must be approved and applied before this stage can generate content.");
        if (stageProgress.GeneratedCount >= stage.Quantity)
            throw new InvalidOperationException("This stage already has a proposal for every planned slot.");

        var ordinal = existingLinks.Where(link => LinkBelongsTo(link, run, stage)).Select(link => link.Ordinal).DefaultIfEmpty(0).Max() + 1;
        return new ContentExpansionProposalLink(
            $"content-expansion-link-{run.Id}-{stage.Id}-{ordinal}", run.ProjectId, run.Id, stage.Id, ordinal,
            proposal.Id, proposal.Version, now);
    }

    public static void ValidateStoredRun(ContentExpansionRun run, ActiveGameSchema activeSchema, string workingRuleSetId, int workingRuleSetRevision)
    {
        ArgumentNullException.ThrowIfNull(run);
        var validated = Create(run.ProjectId, run.Name, activeSchema, workingRuleSetId, workingRuleSetRevision, run.Stages, run.CreatedAt);
        if (!string.Equals(run.FormatVersion, FormatVersion, StringComparison.Ordinal)
            || !string.Equals(run.ActiveGameSchemaId, validated.ActiveGameSchemaId, StringComparison.Ordinal)
            || run.ActiveGameSchemaRevision != validated.ActiveGameSchemaRevision
            || !string.Equals(run.WorkingRuleSetId, validated.WorkingRuleSetId, StringComparison.Ordinal)
            || run.WorkingRuleSetRevision != validated.WorkingRuleSetRevision)
            throw new ArgumentException("The expansion run does not match the current schema and Working Rule Set revisions.", nameof(run));
    }

    private static void ValidateStages(ActiveGameSchema schema, IReadOnlyList<ContentExpansionStage> stages)
    {
        if (stages.Count == 0) throw new ArgumentException("An expansion run needs at least one stage.", nameof(stages));
        if (stages.Count > 25) throw new ArgumentException("An expansion run can contain at most 25 stages.", nameof(stages));
        var schemaRoot = JsonNode.Parse(schema.SchemaJson) as JsonObject ?? throw new ArgumentException("The active schema is not a JSON object.", nameof(schema));
        var entityTypeIds = (schemaRoot["entityTypes"] as JsonArray)?.OfType<JsonObject>()
            .Select(item => item["id"]?.GetValue<string>() ?? item["metadata"]?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal) ?? [];
        var knownStages = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var stage in stages)
        {
            Require(stage.Id, "Every expansion stage needs an id.");
            Require(stage.EntityTypeId, "Every expansion stage needs a content type.");
            Require(stage.DesignerInstruction, "Every expansion stage needs a designer instruction.");
            if (!knownStages.Add(stage.Id.Trim())) throw new ArgumentException($"Expansion stage id '{stage.Id}' is duplicated.", nameof(stages));
            if (!entityTypeIds.Contains(stage.EntityTypeId.Trim())) throw new ArgumentException($"Content type '{stage.EntityTypeId}' does not exist in the active schema.", nameof(stages));
            if (stage.Quantity is < 1 or > 20) throw new ArgumentException("Each expansion stage quantity must be between 1 and 20.", nameof(stages));
            total += stage.Quantity;
            foreach (var dependency in stage.DependsOnStageIds ?? [])
                if (!knownStages.Contains(dependency)) throw new ArgumentException($"Stage '{stage.Id}' depends on '{dependency}', which must appear earlier in the run.", nameof(stages));
        }
        if (total > 100) throw new ArgumentException("An expansion run can contain at most 100 planned entries.", nameof(stages));
    }

    private static bool LinkBelongsTo(ContentExpansionProposalLink link, ContentExpansionRun run, ContentExpansionStage stage) =>
        link.ProjectId == run.ProjectId && link.RunId == run.Id && link.StageId == stage.Id && link.Ordinal is > 0;

    private static void Require(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message);
    }
}
