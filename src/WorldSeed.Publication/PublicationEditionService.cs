using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSeed.Publication;

public enum PublicationEditionState { Draft, Ready }

public sealed record PublicationEditionSection(string Id, string Title, bool IsEntityType);

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
    string ManifestJson);

public sealed record PublicationEditionRevisionRequest(
    string Title,
    string? Subtitle,
    IReadOnlyList<string> SectionOrder,
    IReadOnlyList<string> IncludedSectionIds,
    IReadOnlyList<string> EntitySectionOrder,
    bool MarkReady,
    string? BasedOnEditionId = null,
    int? BasedOnRevision = null);

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

    public static PublicationEdition CreateRevision(PublicationSnapshot snapshot, PublicationEditionRevisionRequest request, PublicationEdition? prior, DateTimeOffset now)
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

        var seriesId = prior?.SeriesId ?? "publication-edition-" + Guid.NewGuid().ToString("N");
        var revision = (prior?.Revision ?? 0) + 1;
        var id = $"{seriesId}-r{revision}";
        var state = request.MarkReady ? PublicationEditionState.Ready : PublicationEditionState.Draft;
        var markdown = Render(snapshot, id, revision, request, source);
        var manifest = BuildManifest(snapshot, id, seriesId, revision, state, request, now);
        return new PublicationEdition(id, seriesId, snapshot.ProjectId, snapshot.Id, revision, request.Title.Trim(), Normalize(request.Subtitle), request.SectionOrder.ToArray(), request.IncludedSectionIds.ToArray(), request.EntitySectionOrder.ToArray(), state, now, markdown, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static IReadOnlyList<PublicationEditionSection> DescribeSections(PublicationSnapshot snapshot)
    {
        var source = Parse(snapshot);
        return source.TopLevel.Concat(source.Entities).Select(section => new PublicationEditionSection(section.Id, section.Title, section.IsEntityType)).ToArray();
    }

    private static string Render(PublicationSnapshot snapshot, string editionId, int revision, PublicationEditionRevisionRequest request, ParsedSnapshot source)
    {
        var top = source.TopLevel.ToDictionary(section => section.Id, StringComparer.Ordinal);
        var entities = source.Entities.ToDictionary(section => section.Id, StringComparer.Ordinal);
        var included = request.IncludedSectionIds.ToHashSet(StringComparer.Ordinal);
        var builder = new StringBuilder().AppendLine($"# {Escape(request.Title.Trim())}").AppendLine();
        if (Normalize(request.Subtitle) is { } subtitle) builder.AppendLine($"_{Escape(subtitle)}_").AppendLine();
        builder.AppendLine("> Designer-reviewed WorldSeed publication edition. Presentation choices do not add or alter game mechanics.")
            .AppendLine()
            .AppendLine("## Edition Provenance")
            .AppendLine()
            .AppendLine($"- Edition: `{Escape(editionId)}` revision {revision}")
            .AppendLine($"- Publication Snapshot: `{Escape(snapshot.Id)}`")
            .AppendLine($"- Working Rule Set: `{Escape(snapshot.WorkingRuleSetId)}` revision {snapshot.WorkingRuleSetRevision}")
            .AppendLine($"- Active Schema: `{Escape(snapshot.ActiveGameSchemaId)}` revision {snapshot.ActiveGameSchemaRevision}")
            .AppendLine($"- Game Data Fingerprint: `{snapshot.GameDataFingerprint}`")
            .AppendLine();

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
                    if (!string.IsNullOrWhiteSpace(entity.Body)) builder.AppendLine(entity.Body.Trim()).AppendLine();
                }
            }
            else if (!string.IsNullOrWhiteSpace(section.Body)) builder.AppendLine(section.Body.Trim()).AppendLine();
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
        ["sourceManifest"] = JsonNode.Parse(snapshot.ManifestJson)
    };

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private static ParsedSnapshot Parse(PublicationSnapshot snapshot)
    {
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
                    entities.Add(new SourceSection("entity-" + UniqueSlug(entityTitle, usedIds), entityTitle, string.Join('\n', lines[(entityStart + 1)..entityEnd]).Trim(), true));
                }
                var introEnd = entityHeadings.FirstOrDefault(end);
                top.Add(new SourceSection(id, title, string.Join('\n', lines[(start + 1)..introEnd]).Trim(), false));
            }
            else top.Add(new SourceSection(id, title, string.Join('\n', bodyLines).Trim(), false));
        }
        return new ParsedSnapshot(top, entities);
    }

    private static string UniqueSlug(string title, ISet<string> used)
    {
        var baseId = string.Join('-', title.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
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
    private sealed record SourceSection(string Id, string Title, string Body, bool IsEntityType);
    private sealed record ParsedSnapshot(IReadOnlyList<SourceSection> TopLevel, IReadOnlyList<SourceSection> Entities);
}
