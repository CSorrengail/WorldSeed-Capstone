using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.SchemaTranslation;
using WorldSeed.SchemaValidation;

namespace WorldSeed.GameData;

/// <summary>
/// The current schema snapshot used to validate a project's game data.  It is deliberately
/// separate from the editable schema proposal so later proposal work cannot silently change
/// already-authored game data.
/// </summary>
public sealed record ActiveGameSchema(
    string Id,
    string ProjectId,
    string FormatVersion,
    string Name,
    string SchemaJson,
    string SourceSchemaProposalId,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IActiveGameSchemaStore
{
    Task SaveAsync(ActiveGameSchema schema, CancellationToken cancellationToken = default);
    Task<ActiveGameSchema?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

/// <summary>One project's canonical game data, bound to the exact active-schema revision that accepted it.</summary>
public sealed record ProjectGameData(
    string Id,
    string ProjectId,
    string FormatVersion,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string DataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IProjectGameDataStore
{
    Task SaveAsync(ProjectGameData gameData, CancellationToken cancellationToken = default);
    Task<ProjectGameData?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public sealed class GameDataFormatException(string message) : Exception(message);

public enum SchemaTransitionKind
{
    InitialActivation,
    NoChanges,
    Compatible,
    RequiresDataWork,
    Unsupported
}

public sealed record SchemaTransitionReview(
    string ProposalId,
    DateTimeOffset ProposalUpdatedAt,
    int TargetRevision,
    SchemaTransitionKind Kind,
    bool CanActivate,
    string Summary,
    IReadOnlyList<string> Changes,
    IReadOnlyList<SchemaValidationIssue> Issues);

public sealed record SchemaTransitionResult(ActiveGameSchema ActiveSchema, ProjectGameData GameData);

public sealed record SchemaTransitionRecord(
    string Id,
    string ProjectId,
    string FormatVersion,
    string ProposalId,
    int FromRevision,
    int ToRevision,
    SchemaTransitionKind Kind,
    string? PreviousSchemaJson,
    string? PreviousGameDataJson,
    string ResultSchemaJson,
    string ResultGameDataJson,
    DateTimeOffset CreatedAt);

public interface ISchemaTransitionStore
{
    Task SaveAsync(SchemaTransitionRecord transition, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemaTransitionRecord>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class GameDataService
{
    private const string FormatVersion = "0.1";
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    /// <summary>
    /// Makes a validated proposal the current schema snapshot. A different snapshot cannot
    /// replace one that already has game data; schema migration is an explicit later workflow.
    /// </summary>
    public static ActiveGameSchema Activate(
        ProjectSchemaProposal proposal,
        ActiveGameSchema? current,
        bool hasGameData,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var schema = ParseObject(proposal.SchemaJson, "The saved schema proposal is not a JSON object.");
        var validation = new GameSchemaValidator().Validate(schema);
        if (!validation.IsValid)
            throw new GameDataFormatException("The saved schema proposal is not a valid game schema: " + Describe(validation));

        if (current is not null && JsonNode.DeepEquals(ParseObject(current.SchemaJson, "The active schema snapshot is not a JSON object."), schema))
            return current;

        if (current is not null && hasGameData)
            throw new GameDataFormatException("The active schema cannot be replaced while game data exists. Create a migration before changing the active schema.");

        var schemaName = schema["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new GameDataFormatException("The schema snapshot requires a name.");

        return new ActiveGameSchema(
            current?.Id ?? $"active-schema-{proposal.ProjectId}",
            proposal.ProjectId,
            FormatVersion,
            schemaName,
            schema.ToJsonString(PrettyJson),
            proposal.Id,
            proposal.WorkingRuleSetId,
            proposal.WorkingRuleSetRevision,
            (current?.Revision ?? 0) + 1,
            current?.CreatedAt ?? now,
            now);
    }

    /// <summary>
    /// Reviews a saved proposal against the current immutable snapshot without changing either
    /// schema or canonical data. Existing data is rebound only in memory so compatibility means
    /// it validates unchanged under the proposed revision.
    /// </summary>
    public static SchemaTransitionReview ReviewTransition(
        ProjectSchemaProposal proposal,
        ActiveGameSchema? current,
        ProjectGameData? gameData,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var targetRevision = (current?.Revision ?? 0) + 1;
        JsonObject proposed;
        try
        {
            proposed = ParseObject(proposal.SchemaJson, "The saved schema proposal is not a JSON object.");
            var structural = new GameSchemaValidator().Validate(proposed);
            if (!structural.IsValid)
                return Review(SchemaTransitionKind.Unsupported, false, "The saved proposal is not a valid game schema.", structural.Issues);
        }
        catch (GameDataFormatException exception)
        {
            return Review(SchemaTransitionKind.Unsupported, false, exception.Message,
                [new SchemaValidationIssue("$", exception.Message)]);
        }

        if (current is null)
            return new SchemaTransitionReview(proposal.Id, proposal.UpdatedAt, targetRevision,
                SchemaTransitionKind.InitialActivation, true,
                "No active schema exists. This proposal can become revision 1.",
                DescribeChanges(null, proposed), []);

        JsonObject active;
        try
        {
            active = ParseObject(current.SchemaJson, "The active schema snapshot is not a JSON object.");
        }
        catch (GameDataFormatException exception)
        {
            return Review(SchemaTransitionKind.Unsupported, false, exception.Message,
                [new SchemaValidationIssue("$", exception.Message)]);
        }

        if (JsonNode.DeepEquals(active, proposed))
            return new SchemaTransitionReview(proposal.Id, proposal.UpdatedAt, current.Revision,
                SchemaTransitionKind.NoChanges, false,
                "The saved proposal is identical to the active schema.", [], []);

        var changes = DescribeChanges(active, proposed);
        if (!HasAuthoredContent(gameData))
            return new SchemaTransitionReview(proposal.Id, proposal.UpdatedAt, targetRevision,
                SchemaTransitionKind.Compatible, true,
                "The proposal is structurally valid and the project has no authored Game Data to migrate.", changes, []);

        if (gameData is null)
            return Review(SchemaTransitionKind.Unsupported, false, "Canonical Game Data could not be loaded.",
                [new SchemaValidationIssue("$", "Canonical Game Data could not be loaded.")]);

        var candidate = CreateSnapshot(proposal, current, now);
        var rebound = RebindGameData(candidate, gameData, now);
        var validation = Validate(candidate, rebound);
        return validation.IsValid
            ? new SchemaTransitionReview(proposal.Id, proposal.UpdatedAt, targetRevision,
                SchemaTransitionKind.Compatible, true,
                "Existing canonical Game Data remains valid without transforming any entries.", changes, [])
            : new SchemaTransitionReview(proposal.Id, proposal.UpdatedAt, targetRevision,
                SchemaTransitionKind.RequiresDataWork, false,
                "Existing canonical Game Data does not conform to the proposed schema. Nothing can be activated until the listed data issues are resolved.",
                changes, validation.Issues);

        SchemaTransitionReview Review(SchemaTransitionKind kind, bool canActivate, string summary, IReadOnlyList<SchemaValidationIssue> issues) =>
            new(proposal.Id, proposal.UpdatedAt, targetRevision, kind, canActivate, summary, [], issues);
    }

    public static ProjectGameData RebindGameData(ActiveGameSchema target, ProjectGameData existing, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(existing);
        var data = ParseObject(existing.DataJson, "Game data is not a JSON object.");
        var schema = ParseObject(target.SchemaJson, "The active schema snapshot is not a JSON object.");
        data["gameSchemaId"] = schema["id"]?.DeepClone();
        data["gameSchemaVersion"] = schema["version"]?.DeepClone();
        return existing with
        {
            ActiveGameSchemaId = target.Id,
            ActiveGameSchemaRevision = target.Revision,
            DataJson = data.ToJsonString(PrettyJson),
            UpdatedAt = now
        };
    }

    public static SchemaTransitionResult ApplyReviewedTransition(
        ProjectSchemaProposal proposal,
        ActiveGameSchema? current,
        ProjectGameData? gameData,
        DateTimeOffset now)
    {
        var review = ReviewTransition(proposal, current, gameData, now);
        if (!review.CanActivate)
            throw new GameDataFormatException(review.Summary + (review.Issues.Count == 0 ? string.Empty : " " + string.Join(" ", review.Issues.Select(issue => $"{issue.Path}: {issue.Message}"))));

        var active = current is null
            ? Activate(proposal, null, false, now)
            : CreateSnapshot(proposal, current, now);
        var rebound = gameData is null
            ? CreateEmpty(active, now)
            : RebindGameData(active, gameData, now);
        EnsureValid(active, rebound);
        return new SchemaTransitionResult(active, rebound);
    }

    public static SchemaTransitionRecord CreateTransitionRecord(
        ProjectSchemaProposal proposal,
        ActiveGameSchema? previousSchema,
        ProjectGameData? previousData,
        SchemaTransitionReview review,
        SchemaTransitionResult result,
        DateTimeOffset now)
    {
        if (!review.CanActivate || result.ActiveSchema.Revision != review.TargetRevision)
            throw new GameDataFormatException("Only the exact reviewed schema transition can be recorded.");
        return new SchemaTransitionRecord(
            $"schema-transition-{proposal.ProjectId}-r{result.ActiveSchema.Revision}", proposal.ProjectId, FormatVersion,
            proposal.Id, previousSchema?.Revision ?? 0, result.ActiveSchema.Revision, review.Kind,
            previousSchema?.SchemaJson, previousData?.DataJson, result.ActiveSchema.SchemaJson,
            result.GameData.DataJson, now);
    }

    private static ActiveGameSchema CreateSnapshot(ProjectSchemaProposal proposal, ActiveGameSchema current, DateTimeOffset now)
    {
        var schema = ParseObject(proposal.SchemaJson, "The saved schema proposal is not a JSON object.");
        return new ActiveGameSchema(current.Id, proposal.ProjectId, FormatVersion,
            schema["name"]?.GetValue<string>() ?? proposal.Summary,
            schema.ToJsonString(PrettyJson), proposal.Id, proposal.WorkingRuleSetId,
            proposal.WorkingRuleSetRevision, current.Revision + 1, current.CreatedAt, now);
    }

    private static IReadOnlyList<string> DescribeChanges(JsonObject? active, JsonObject proposed)
    {
        var changes = new List<string>();
        foreach (var collectionName in new[] { "schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations" })
        {
            var before = Definitions(active?[collectionName] as JsonArray);
            var after = Definitions(proposed[collectionName] as JsonArray);
            foreach (var id in after.Keys.Except(before.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
                changes.Add($"Added {Label(collectionName)} '{id}'.");
            foreach (var id in before.Keys.Except(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
                changes.Add($"Removed {Label(collectionName)} '{id}'.");
            foreach (var id in before.Keys.Intersect(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
                if (!JsonNode.DeepEquals(before[id], after[id])) changes.Add($"Changed {Label(collectionName)} '{id}'.");
        }
        return changes;

        static Dictionary<string, JsonNode> Definitions(JsonArray? definitions) =>
            definitions?.OfType<JsonObject>()
                .Select(definition => (Id: definition["metadata"]?["id"]?.GetValue<string>() ?? definition["id"]?.GetValue<string>(), Node: (JsonNode)definition))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Node, StringComparer.Ordinal)
            ?? new Dictionary<string, JsonNode>(StringComparer.Ordinal);

        static string Label(string collectionName) => collectionName switch
        {
            "schemaFragments" => "schema fragment",
            "entityTypes" => "content type",
            "valueTypes" => "value type",
            "relationships" => "relationship",
            "rules" => "schema rule",
            "procedures" => "procedure",
            "validations" => "validation",
            _ => "definition"
        };
    }

    /// <summary>Creates a valid empty data document for a newly activated schema snapshot.</summary>
    public static ProjectGameData CreateEmpty(ActiveGameSchema activeSchema, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(activeSchema);
        var schema = ParseObject(activeSchema.SchemaJson, "The active schema snapshot is not a JSON object.");
        var data = new JsonObject
        {
            ["id"] = $"game-data-{activeSchema.ProjectId}",
            ["gameSchemaId"] = schema["id"]?.GetValue<string>(),
            ["gameSchemaVersion"] = schema["version"]?.GetValue<string>(),
            ["entityInstances"] = new JsonArray(),
            ["relationshipInstances"] = new JsonArray()
        };
        var gameData = new ProjectGameData(
            $"game-data-{activeSchema.ProjectId}",
            activeSchema.ProjectId,
            FormatVersion,
            activeSchema.Id,
            activeSchema.Revision,
            data.ToJsonString(PrettyJson),
            now,
            now);
        EnsureValid(activeSchema, gameData);
        return gameData;
    }

    /// <summary>
    /// An automatically-created empty document does not count as authored data and may be
    /// safely replaced when its active schema changes. Relationships count as authored too.
    /// </summary>
    public static bool HasAuthoredContent(ProjectGameData? gameData)
    {
        if (gameData is null) return false;
        try
        {
            var data = ParseObject(gameData.DataJson, "Game data is not a JSON object.");
            return (data["entityInstances"] as JsonArray)?.Count > 0 || (data["relationshipInstances"] as JsonArray)?.Count > 0;
        }
        catch (GameDataFormatException)
        {
            // Retain malformed documents rather than overwriting something that may need recovery.
            return true;
        }
    }

    /// <summary>Returns every schema or active-snapshot issue without altering the submitted data.</summary>
    public static SchemaValidationResult Validate(ActiveGameSchema activeSchema, ProjectGameData gameData)
    {
        var result = new SchemaValidationResult();
        if (!string.Equals(activeSchema.ProjectId, gameData.ProjectId, StringComparison.Ordinal))
            result.Add("$", "Game data belongs to a different project than the active schema.");
        if (!string.Equals(activeSchema.Id, gameData.ActiveGameSchemaId, StringComparison.Ordinal))
            result.Add("$", "Game data is bound to a different active schema.");
        if (activeSchema.Revision != gameData.ActiveGameSchemaRevision)
            result.Add("$", "Game data is bound to a different active-schema revision.");

        try
        {
            var schema = ParseObject(activeSchema.SchemaJson, "The active schema snapshot is not a JSON object.");
            var data = ParseObject(gameData.DataJson, "Game data is not a JSON object.");
            foreach (var issue in new GameDataValidator().Validate(schema, data).Issues)
                result.Add(issue.Path, issue.Message);
        }
        catch (GameDataFormatException exception)
        {
            result.Add("$", exception.Message);
        }
        return result;
    }

    public static void EnsureValid(ActiveGameSchema activeSchema, ProjectGameData gameData)
    {
        var validation = Validate(activeSchema, gameData);
        if (!validation.IsValid)
            throw new GameDataFormatException("Game data is invalid: " + Describe(validation));
    }

    private static JsonObject ParseObject(string json, string message)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? throw new GameDataFormatException(message);
        }
        catch (JsonException)
        {
            throw new GameDataFormatException(message);
        }
    }

    private static string Describe(SchemaValidationResult validation) => string.Join(" ", validation.Issues.Select(issue => issue.Message));
}
