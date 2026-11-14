using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;
using WorldSeed.RuleDrafting;

namespace WorldSeed.ProjectPlanning;

public static class ProjectInventoryService
{
    private const string FormatVersion = "0.1";
    private static readonly string[] DefinitionCollections = ["schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations"];

    public static ProjectInventory Create(ProjectInventoryInput input, ProjectInventory? prior, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Project);
        EnsureProjectOwnership(input);

        var schema = ParseObject(input.ActiveSchema?.SchemaJson);
        var data = ParseObject(input.GameData?.DataJson);
        var proposalSchema = ParseObject(input.SchemaProposal?.SchemaJson);
        var latestPublication = input.PublicationSnapshots.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        var citedSourceIds = input.WorkingRules?.Rules.SelectMany(rule => rule.Evidence).Select(evidence => evidence.SourceNoteId).ToHashSet(StringComparer.Ordinal) ?? [];
        var completedDraftCount = input.Project.Sessions.Count(session => session.LatestTurn is { Action: RuleDraftAction.PresentDraft, Draft: not null });

        var sources = new InventorySourceCoverage(
            input.Project.SourceNotes.Count,
            input.Project.SourceNotes.Sum(source => source.OriginalText.Length),
            completedDraftCount,
            input.Project.SourceNotes.Select(source => source.Id).Where(id => !citedSourceIds.Contains(id)).Order(StringComparer.Ordinal).ToArray());

        var rules = input.WorkingRules?.Rules ?? [];
        var ruleCoverage = new InventoryRuleCoverage(
            rules.Count,
            rules.GroupBy(rule => rule.Kind.ToString()).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            rules.Count(rule => rule.Evidence.Count > 0),
            rules.Where(rule => rule.Evidence.Count == 0).Select(rule => rule.Id).Order(StringComparer.Ordinal).ToArray(),
            input.WorkingRules?.OpenQuestions.ToArray() ?? [],
            input.WorkingRules?.Conflicts.ToArray() ?? [],
            input.WorkingRules?.Exclusions.ToArray() ?? []);

        var definitionCounts = DefinitionCollections.ToDictionary(name => name, name => (schema?[name] as JsonArray)?.Count ?? 0, StringComparer.Ordinal);
        var instances = (data?["entityInstances"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var currentProfiles = input.ActiveSchema is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : input.BalanceProfiles.Where(profile => profile.ActiveGameSchemaId == input.ActiveSchema.Id && profile.ActiveGameSchemaRevision == input.ActiveSchema.Revision)
                .Select(profile => profile.Profile.EntityTypeId).ToHashSet(StringComparer.Ordinal);
        var latestDecisions = input.ContentDecisions.GroupBy(decision => decision.ProposalId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(decision => decision.CreatedAt).First(), StringComparer.Ordinal);
        var appliedIds = input.ContentApplications.Where(application => application.Status == ContentApplicationStatus.Applied)
            .Select(application => application.ProposalId).ToHashSet(StringComparer.Ordinal);

        var contentTypes = (schema?["entityTypes"] as JsonArray)?.OfType<JsonObject>().Select(entity =>
        {
            var id = DefinitionId(entity);
            var name = entity["metadata"]?["name"]?.GetValue<string>() ?? entity["name"]?.GetValue<string>() ?? id;
            var properties = (entity["properties"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
            var proposals = input.ContentProposals.Where(proposal => proposal.EntityTypeId == id).ToArray();
            return new InventoryContentTypeCoverage(
                id, name, properties.Length,
                properties.Count(property => property["required"]?.GetValue<bool>() == true),
                instances.Count(instance => instance["entityTypeId"]?.GetValue<string>() == id),
                proposals.Length,
                proposals.Count(proposal => latestDecisions.TryGetValue(proposal.Id, out var decision) && decision.Kind == ContentProposalDecisionKind.Approved),
                proposals.Count(proposal => appliedIds.Contains(proposal.Id)),
                currentProfiles.Contains(id));
        }).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        var schemaCoverage = new InventorySchemaCoverage(
            definitionCounts,
            contentTypes,
            definitionCounts["validations"],
            input.SchemaProposal is not null && input.ActiveSchema is not null && proposalSchema is not null && !JsonNode.DeepEquals(schema, proposalSchema));
        var publication = new InventoryPublicationCoverage(
            input.PublicationSnapshots.Count,
            latestPublication?.Id,
            latestPublication?.CreatedAt,
            latestPublication?.IncludedEntityIds.Count ?? 0);

        var observations = BuildObservations(input, sources, ruleCoverage, schemaCoverage, publication);
        var binding = new ProjectInventoryBinding(
            input.Project.UpdatedAt, input.WorkingRules?.Id, input.WorkingRules?.Revision,
            input.SchemaProposal?.Id, input.SchemaProposal?.UpdatedAt,
            input.ActiveSchema?.Id, input.ActiveSchema?.Revision,
            input.GameData?.Id, input.GameData?.UpdatedAt,
            latestPublication?.Id, latestPublication?.GameDataFingerprint);
        var revision = (prior?.Revision ?? 0) + 1;
        return new ProjectInventory($"project-inventory-{input.Project.Id}-r{revision}", input.Project.Id, FormatVersion, revision, now,
            binding, sources, ruleCoverage, schemaCoverage, publication, observations);
    }

    private static IReadOnlyList<InventoryObservation> BuildObservations(ProjectInventoryInput input, InventorySourceCoverage sources, InventoryRuleCoverage rules, InventorySchemaCoverage schema, InventoryPublicationCoverage publication)
    {
        var items = new List<InventoryObservation>();
        if (input.WorkingRules is null) Add(InventoryObservationKind.MissingFoundation, "working-rules", "No project-wide Working Rule Set exists.");
        if (input.ActiveSchema is null) Add(InventoryObservationKind.MissingFoundation, "active-schema", "No active game schema exists.");
        if (input.GameData is null) Add(InventoryObservationKind.MissingFoundation, "game-data", "No canonical Game Data document exists.");
        foreach (var id in sources.UnreferencedSourceIds) Add(InventoryObservationKind.MissingSourceTrace, id, "This source is not cited by the current Working Rule Set.", id);
        foreach (var id in rules.RulesWithoutEvidence) Add(InventoryObservationKind.MissingSourceTrace, id, "This Working Rule has no source evidence.", id);
        for (var index = 0; index < rules.OpenQuestions.Count; index++) Add(InventoryObservationKind.UnresolvedQuestion, $"question-{index + 1}", rules.OpenQuestions[index]);
        for (var index = 0; index < rules.Conflicts.Count; index++) Add(InventoryObservationKind.Conflict, $"conflict-{index + 1}", rules.Conflicts[index]);
        foreach (var type in schema.ContentTypes.Where(type => type.CanonicalEntryCount == 0)) Add(InventoryObservationKind.EmptyContentType, type.Id, $"Content type '{type.Name}' has no canonical entries.", type.Id);
        foreach (var type in schema.ContentTypes.Where(type => !type.HasCurrentBalanceProfile)) Add(InventoryObservationKind.MissingEvaluationCoverage, type.Id, $"Content type '{type.Name}' has no balance profile for the active schema revision.", type.Id);
        if (schema.ProposalDiffersFromActive) Add(InventoryObservationKind.UnpublishedChanges, "schema-proposal", "The saved schema proposal differs from the active schema.", input.SchemaProposal!.Id);
        if (input.GameData is not null && (publication.LatestSnapshotAt is null || publication.LatestSnapshotAt < input.GameData.UpdatedAt)) Add(InventoryObservationKind.UnpublishedChanges, "publication", "Canonical Game Data is newer than the latest publication snapshot.", input.GameData.Id);
        return items;

        void Add(InventoryObservationKind kind, string subjectId, string description, params string[] evidenceIds) =>
            items.Add(new InventoryObservation($"inventory-observation-{items.Count + 1:D3}", kind, subjectId, description, evidenceIds));
    }

    private static void EnsureProjectOwnership(ProjectInventoryInput input)
    {
        var projectId = input.Project.Id;
        if (input.WorkingRules is not null && input.WorkingRules.ProjectId != projectId ||
            input.SchemaProposal is not null && input.SchemaProposal.ProjectId != projectId ||
            input.ActiveSchema is not null && input.ActiveSchema.ProjectId != projectId ||
            input.GameData is not null && input.GameData.ProjectId != projectId ||
            input.ContentProposals.Any(item => item.ProjectId != projectId) ||
            input.ContentDecisions.Any(item => item.ProjectId != projectId) ||
            input.ContentApplications.Any(item => item.ProjectId != projectId) ||
            input.BalanceProfiles.Any(item => item.ProjectId != projectId) ||
            input.PublicationSnapshots.Any(item => item.ProjectId != projectId))
            throw new ArgumentException("Every inventory input must belong to the same project.", nameof(input));
    }

    private static JsonObject? ParseObject(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;
    private static string DefinitionId(JsonObject definition) => definition["metadata"]?["id"]?.GetValue<string>() ?? definition["id"]?.GetValue<string>() ?? throw new ArgumentException("A schema definition is missing an id.");
}
