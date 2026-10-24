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
