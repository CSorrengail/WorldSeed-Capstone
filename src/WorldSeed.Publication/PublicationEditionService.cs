using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.Publication;

public enum PublicationEditionState { Draft, Ready }

public sealed record PublicationEditionSection(string Id, string Title, bool IsEntityType);

public sealed record PublicationEditionEditorialSelection(
    string TargetSectionId,
    string ProposalId,
    int ProposalRevision,
    string ApprovalDecisionId,
    IReadOnlyList<string> SourceUnitIds);

public sealed record PublicationEdition(
    string Id,
    string SeriesId,
    string ProjectId,
    string SnapshotId,
    int Revision,
    string Title,
    string? Subtitle,
    IReadOnlyList<string> SectionOrder,
    IReadOnlyList<string> IncludedSectionIds,
    IReadOnlyList<string> EntitySectionOrder,
    PublicationEditionState State,
    DateTimeOffset CreatedAt,
    string Markdown,
    string ManifestJson,
    IReadOnlyList<PublicationEditionEditorialSelection>? EditorialSelections = null);

public sealed record PublicationEditionRevisionRequest(
    string Title,
    string? Subtitle,
    IReadOnlyList<string> SectionOrder,
    IReadOnlyList<string> IncludedSectionIds,
    IReadOnlyList<string> EntitySectionOrder,
    bool MarkReady,
    string? BasedOnEditionId = null,
    int? BasedOnRevision = null,
    IReadOnlyList<PublicationEditionEditorialSelection>? EditorialSelections = null);

public interface IPublicationEditionStore
{
    Task SaveAsync(PublicationEdition edition, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicationEdition>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task<PublicationEdition?> LoadLatestAsync(string projectId, string seriesId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class PublicationEditionService
{
    private const string GameContentSectionId = "game-content";

    public static PublicationEdition CreateInitial(PublicationSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var source = Parse(snapshot);
        var request = new PublicationEditionRevisionRequest(
            snapshot.ProjectName,
            null,
            source.TopLevel.Select(section => section.Id).ToArray(),
            source.TopLevel.Select(section => section.Id).ToArray(),
            source.Entities.Select(section => section.Id).ToArray(),
            false);
        return CreateRevision(snapshot, request, null, now);
    }

    public static PublicationEdition CreateRevision(PublicationSnapshot snapshot, PublicationEditionRevisionRequest request, PublicationEdition? prior, DateTimeOffset now, IReadOnlyList<PublicationEditorialProposal>? editorialProposals = null, IReadOnlyList<PublicationEditorialDecision>? editorialDecisions = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("A publication-edition title is required.", nameof(request));
        if (prior is not null && (!string.Equals(prior.ProjectId, snapshot.ProjectId, StringComparison.Ordinal) || !string.Equals(prior.SnapshotId, snapshot.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException("A publication edition can only be revised against its original immutable snapshot.");
        if (prior is null && (request.BasedOnEditionId is not null || request.BasedOnRevision is not null))
            throw new InvalidOperationException("The requested base publication edition does not exist.");
        if (prior is not null && (!string.Equals(request.BasedOnEditionId, prior.Id, StringComparison.Ordinal) || request.BasedOnRevision != prior.Revision))
            throw new InvalidOperationException("The publication edition changed. Reload its latest revision before saving another revision.");

        var source = Parse(snapshot);
        ValidatePermutation("top-level section", request.SectionOrder, source.TopLevel.Select(section => section.Id));
        ValidateSubset("included section", request.IncludedSectionIds, source.TopLevel.Select(section => section.Id));
        ValidatePermutation("entity section", request.EntitySectionOrder, source.Entities.Select(section => section.Id));
        var editorial = ResolveEditorialSelections(snapshot, request, source, editorialProposals ?? [], editorialDecisions ?? []);

        var seriesId = prior?.SeriesId ?? "publication-edition-" + Guid.NewGuid().ToString("N");
        var revision = (prior?.Revision ?? 0) + 1;
        var id = $"{seriesId}-r{revision}";
        var state = request.MarkReady ? PublicationEditionState.Ready : PublicationEditionState.Draft;
        var markdown = Render(snapshot, id, revision, request, source, editorial);
        var manifest = BuildManifest(snapshot, id, seriesId, revision, state, request, now);
        return new PublicationEdition(id, seriesId, snapshot.ProjectId, snapshot.Id, revision, request.Title.Trim(), Normalize(request.Subtitle), request.SectionOrder.ToArray(), request.IncludedSectionIds.ToArray(), request.EntitySectionOrder.ToArray(), state, now, markdown, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), request.EditorialSelections?.ToArray() ?? []);
    }

    public static IReadOnlyList<PublicationEditionSection> DescribeSections(PublicationSnapshot snapshot)
    {
        var source = Parse(snapshot);
        return source.TopLevel.Concat(source.Entities).Select(section => new PublicationEditionSection(section.Id, section.Title, section.IsEntityType)).ToArray();
    }

    private static string Render(PublicationSnapshot snapshot, string editionId, int revision, PublicationEditionRevisionRequest request, ParsedSnapshot source, IReadOnlyDictionary<string, PublicationEditorialProposal> editorial)
    {
        var top = source.TopLevel.ToDictionary(section => section.Id, StringComparer.Ordinal);
        var entities = source.Entities.ToDictionary(section => section.Id, StringComparer.Ordinal);
        var included = request.IncludedSectionIds.ToHashSet(StringComparer.Ordinal);
        var builder = new StringBuilder().AppendLine($"# {Escape(request.Title.Trim())}").AppendLine();
        if (Normalize(request.Subtitle) is { } subtitle) builder.AppendLine($"_{Escape(subtitle)}_").AppendLine();
        builder.AppendLine("> Designer-reviewed WorldSeed publication edition.").AppendLine();

        foreach (var sectionId in request.SectionOrder.Where(included.Contains))
        {
            var section = top[sectionId];
            builder.AppendLine($"## {section.Title}").AppendLine();
            if (sectionId == GameContentSectionId && source.Entities.Count > 0)
            {
                foreach (var entityId in request.EntitySectionOrder)
                {
                    var entity = entities[entityId];
                    builder.AppendLine($"### {entity.Title}").AppendLine();
                    var body = editorial.TryGetValue(entity.EditorialTargetId, out var proposal) ? NormalizeEditorialBody(proposal.ProposedMarkdown) : entity.Body;
                    if (!string.IsNullOrWhiteSpace(body)) builder.AppendLine(body.Trim()).AppendLine();
                }
            }
            else
            {
                var body = editorial.TryGetValue(section.EditorialTargetId, out var proposal) ? NormalizeEditorialBody(proposal.ProposedMarkdown) : section.Body;
                if (!string.IsNullOrWhiteSpace(body)) builder.AppendLine(body.Trim()).AppendLine();
            }
        }
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static JsonObject BuildManifest(PublicationSnapshot snapshot, string id, string seriesId, int revision, PublicationEditionState state, PublicationEditionRevisionRequest request, DateTimeOffset now) => new()
    {
        ["formatVersion"] = "0.1",
        ["editionId"] = id,
        ["editionSeriesId"] = seriesId,
        ["editionRevision"] = revision,
        ["state"] = state.ToString(),
        ["createdAt"] = now,
        ["snapshotId"] = snapshot.Id,
        ["workingRuleSetId"] = snapshot.WorkingRuleSetId,
        ["workingRuleSetRevision"] = snapshot.WorkingRuleSetRevision,
        ["activeGameSchemaId"] = snapshot.ActiveGameSchemaId,
        ["activeGameSchemaRevision"] = snapshot.ActiveGameSchemaRevision,
        ["gameDataFingerprint"] = snapshot.GameDataFingerprint,
        ["sectionOrder"] = Strings(request.SectionOrder),
        ["includedSectionIds"] = Strings(request.IncludedSectionIds),
        ["entitySectionOrder"] = Strings(request.EntitySectionOrder),
        ["editorialSelections"] = new JsonArray((request.EditorialSelections ?? []).Select(selection => (JsonNode?)new JsonObject
        {
            ["targetSectionId"] = selection.TargetSectionId,
            ["proposalId"] = selection.ProposalId,
            ["proposalRevision"] = selection.ProposalRevision,
            ["approvalDecisionId"] = selection.ApprovalDecisionId,
            ["sourceUnitIds"] = Strings(selection.SourceUnitIds)
        }).ToArray()),
        ["sourceManifest"] = JsonNode.Parse(snapshot.ManifestJson)
    };

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private static ParsedSnapshot Parse(PublicationSnapshot snapshot)
    {
        if (snapshot.Units is { Count: > 0 }) return ParseUnits(snapshot.Units);

        var lines = snapshot.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var headings = Enumerable.Range(0, lines.Length).Where(index => lines[index].StartsWith("## ", StringComparison.Ordinal)).ToArray();
        if (headings.Length == 0) throw new InvalidOperationException("The publication snapshot does not contain edition-ready sections.");
        var top = new List<SourceSection>();
        var entities = new List<SourceSection>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var headingIndex = 0; headingIndex < headings.Length; headingIndex++)
        {
            var start = headings[headingIndex];
            var end = headingIndex + 1 < headings.Length ? headings[headingIndex + 1] : lines.Length;
            var title = lines[start][3..].Trim();
            if (title.Equals("Publication Snapshot", StringComparison.Ordinal)) continue;
            var id = UniqueSlug(title, usedIds);
            var bodyLines = lines[(start + 1)..end];
            if (title.Equals("Game Content", StringComparison.Ordinal))
            {
                id = GameContentSectionId;
                usedIds.Add(id);
                var entityHeadings = Enumerable.Range(start + 1, end - start - 1).Where(index => lines[index].StartsWith("### ", StringComparison.Ordinal)).ToArray();
                for (var entityIndex = 0; entityIndex < entityHeadings.Length; entityIndex++)
                {
                    var entityStart = entityHeadings[entityIndex];
                    var entityEnd = entityIndex + 1 < entityHeadings.Length ? entityHeadings[entityIndex + 1] : end;
                    var entityTitle = lines[entityStart][4..].Trim();
                    entities.Add(new SourceSection("entity-" + UniqueSlug(entityTitle, usedIds), entityTitle, string.Join('\n', lines[(entityStart + 1)..entityEnd]).Trim(), true, string.Empty));
                }
                var introEnd = entityHeadings.FirstOrDefault(end);
                top.Add(new SourceSection(id, title, string.Join('\n', lines[(start + 1)..introEnd]).Trim(), false, id));
            }
            else top.Add(new SourceSection(id, title, string.Join('\n', bodyLines).Trim(), false, id));
        }
        return new ParsedSnapshot(top, entities);
    }

    private static ParsedSnapshot ParseUnits(IReadOnlyList<PublicationUnit> units)
    {
        var top = new List<SourceSection>
        {
            new("rules", "Rules", RenderUnits(units.Where(unit => unit.Kind == PublicationUnitKind.Rule), "###", readerFacing: true), false, "rules"),
            new("categories-and-value-types", "Categories And Value Types", RenderUnits(units.Where(unit => unit.Kind == PublicationUnitKind.ValueType), "###", readerFacing: true), false, "categories-and-value-types"),
            new(GameContentSectionId, "Game Content", string.Empty, false, GameContentSectionId),
            new("relationships", "Relationships", RenderRelationships(units.Where(unit => unit.Kind == PublicationUnitKind.Relationship), readerFacing: true), false, "relationships")
        };
        var usedIds = top.Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
        var entities = units.Where(unit => unit.Kind == PublicationUnitKind.ContentType).Select(contentType =>
        {
            var entries = units.Where(unit => unit.Kind == PublicationUnitKind.ContentEntry && unit.ParentId == contentType.Id);
            var body = RenderUnits(entries, "####", readerFacing: true);
            if (string.IsNullOrWhiteSpace(body)) body = "_No canonical entries._";
            return new SourceSection("entity-" + UniqueSlug(contentType.Title, usedIds), contentType.Title, body, true, contentType.Id);
        }).ToArray();
        return new ParsedSnapshot(top, entities);
    }

    private static string RenderUnits(IEnumerable<PublicationUnit> units, string heading, bool readerFacing = false)
    {
        var builder = new StringBuilder();
        foreach (var unit in units)
            builder.AppendLine($"{heading} {Escape(unit.Title)}").AppendLine().AppendLine((readerFacing ? unit.ReaderMarkdownBody ?? unit.MarkdownBody : unit.MarkdownBody).Trim()).AppendLine();
        return builder.ToString().Trim();
    }

    private static string RenderRelationships(IEnumerable<PublicationUnit> units, bool readerFacing = false)
    {
        var bodies = units.Select(unit => (readerFacing ? unit.ReaderMarkdownBody ?? unit.MarkdownBody : unit.MarkdownBody).Trim()).Where(body => body.Length > 0).ToArray();
        return bodies.Length == 0 ? "_No canonical relationships._" : string.Join(Environment.NewLine, bodies);
    }

    private static IReadOnlyDictionary<string, PublicationEditorialProposal> ResolveEditorialSelections(PublicationSnapshot snapshot, PublicationEditionRevisionRequest request, ParsedSnapshot source, IReadOnlyList<PublicationEditorialProposal> proposals, IReadOnlyList<PublicationEditorialDecision> decisions)
    {
        var selections = request.EditorialSelections ?? [];
        if (selections.Count == 0) return new Dictionary<string, PublicationEditorialProposal>();
        if (snapshot.Units is not { Count: > 0 }) throw new InvalidOperationException("Editorial sections require a structured publication snapshot.");
        var includedSections = request.IncludedSectionIds.ToHashSet(StringComparer.Ordinal);
        var availableTargets = source.TopLevel.Where(section => includedSections.Contains(section.Id) && section.Id != GameContentSectionId).Select(section => section.EditorialTargetId).ToHashSet(StringComparer.Ordinal);
        if (includedSections.Contains(GameContentSectionId)) availableTargets.UnionWith(source.Entities.Select(section => section.EditorialTargetId));
        var result = new Dictionary<string, PublicationEditorialProposal>(StringComparer.Ordinal);
        foreach (var selection in selections)
        {
            if (!availableTargets.Contains(selection.TargetSectionId) || !result.TryAdd(selection.TargetSectionId, null!))
                throw new InvalidOperationException("An editorial edition selection targets an unavailable or duplicate section.");
            var proposal = proposals.SingleOrDefault(value => value.Id == selection.ProposalId && value.Revision == selection.ProposalRevision)
                ?? throw new InvalidOperationException("The selected editorial proposal revision does not exist.");
            var decision = decisions.SingleOrDefault(value => value.Id == selection.ApprovalDecisionId)
                ?? throw new InvalidOperationException("The selected editorial approval decision does not exist.");
            PublicationEditorialProposalService.Validate(snapshot, proposal);
            PublicationEditorialProposalService.ValidateDecision(proposal, decision);
            if (decision.Kind != PublicationEditorialDecisionKind.Approved ||
                !string.Equals(proposal.TargetSectionId, selection.TargetSectionId, StringComparison.Ordinal) ||
                !proposal.SourceUnitIds.SequenceEqual(selection.SourceUnitIds, StringComparer.Ordinal))
                throw new InvalidOperationException("An edition can use only an approved editorial proposal with its exact target and source units.");
            result[selection.TargetSectionId] = proposal;
        }
        return result;
    }

    private static string NormalizeEditorialBody(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
        if (lines.Count > 0 && lines[0].TrimStart().StartsWith('#')) lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string UniqueSlug(string title, ISet<string> used)
    {
        var baseId = new string(title.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (baseId.Contains("--", StringComparison.Ordinal)) baseId = baseId.Replace("--", "-", StringComparison.Ordinal);
        baseId = baseId.Trim('-');
        if (string.IsNullOrWhiteSpace(baseId)) baseId = "section";
        var id = baseId;
        for (var suffix = 2; !used.Add(id); suffix++) id = $"{baseId}-{suffix}";
        return id;
    }

    private static void ValidatePermutation(string label, IReadOnlyList<string> actual, IEnumerable<string> expectedValues)
    {
        var expected = expectedValues.ToArray();
        if (actual.Count != expected.Length || actual.Distinct(StringComparer.Ordinal).Count() != actual.Count || expected.Except(actual, StringComparer.Ordinal).Any())
            throw new InvalidOperationException($"The {label} order must contain every available {label} exactly once.");
    }

    private static void ValidateSubset(string label, IReadOnlyList<string> actual, IEnumerable<string> availableValues)
    {
        var available = availableValues.ToHashSet(StringComparer.Ordinal);
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Count || actual.Any(id => !available.Contains(id)))
            throw new InvalidOperationException($"The {label} list contains an unavailable or duplicate section.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    private sealed record SourceSection(string Id, string Title, string Body, bool IsEntityType, string EditorialTargetId);
    private sealed record ParsedSnapshot(IReadOnlyList<SourceSection> TopLevel, IReadOnlyList<SourceSection> Entities);
}
