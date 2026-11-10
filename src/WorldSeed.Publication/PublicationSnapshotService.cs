using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.GameData;
using WorldSeed.WorkingRules;

namespace WorldSeed.Publication;

public sealed record PublicationSnapshot(
    string Id,
    string ProjectId,
    string ProjectName,
    string WorkingRuleSetId,
    int WorkingRuleSetRevision,
    string ActiveGameSchemaId,
    int ActiveGameSchemaRevision,
    string GameDataId,
    string GameDataFingerprint,
    IReadOnlyList<string> IncludedEntityIds,
    DateTimeOffset CreatedAt,
    string Markdown,
    string ManifestJson);

public interface IPublicationSnapshotStore
{
    Task SaveAsync(PublicationSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicationSnapshot>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

public static class PublicationSnapshotService
{
    public static PublicationSnapshot Create(string projectName, WorkingRuleSet ruleSet, ActiveGameSchema activeSchema, ProjectGameData gameData, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(projectName)) throw new ArgumentException("A project name is required.", nameof(projectName));
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(activeSchema);
        ArgumentNullException.ThrowIfNull(gameData);
        if (!string.Equals(ruleSet.ProjectId, activeSchema.ProjectId, StringComparison.Ordinal) || !string.Equals(gameData.ProjectId, activeSchema.ProjectId, StringComparison.Ordinal))
            throw new InvalidOperationException("The rule snapshot, active schema, and Game Data must belong to the same project.");
        if (!string.Equals(activeSchema.WorkingRuleSetId, ruleSet.Id, StringComparison.Ordinal) || activeSchema.WorkingRuleSetRevision != ruleSet.Revision)
            throw new InvalidOperationException("Publication requires the exact Working Rule Set revision bound to the active schema.");
        var validation = GameDataService.Validate(activeSchema, gameData);
        if (!validation.IsValid)
            throw new InvalidOperationException("Publication requires valid canonical Game Data: " + string.Join(" ", validation.Issues.Select(issue => $"{issue.Path}: {issue.Message}")));

        var schema = JsonNode.Parse(activeSchema.SchemaJson) as JsonObject ?? throw new InvalidOperationException("The active schema is not a JSON object.");
        var data = JsonNode.Parse(gameData.DataJson) as JsonObject ?? throw new InvalidOperationException("Canonical Game Data is not a JSON object.");
        var entries = (data["entityInstances"] as JsonArray)?.OfType<JsonObject>()
            .OrderBy(entry => entry["entityTypeId"]?.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(entry => entry["id"]?.GetValue<string>(), StringComparer.Ordinal)
            .ToArray() ?? [];
        var includedIds = entries.Select(entry => entry["id"]?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToArray();
        var fingerprint = Fingerprint(gameData.DataJson);
        var snapshotId = "publication-" + Guid.NewGuid().ToString("N");
        var manifest = BuildManifest(snapshotId, projectName.Trim(), ruleSet, activeSchema, gameData, fingerprint, includedIds, now);
        var markdown = RenderMarkdown(projectName.Trim(), ruleSet, activeSchema, schema, data, entries, fingerprint, now);
        return new PublicationSnapshot(snapshotId, activeSchema.ProjectId, projectName.Trim(), ruleSet.Id, ruleSet.Revision, activeSchema.Id, activeSchema.Revision, gameData.Id, fingerprint, includedIds, now, markdown, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string RenderMarkdown(string projectName, WorkingRuleSet ruleSet, ActiveGameSchema activeSchema, JsonObject schema, JsonObject data, IReadOnlyList<JsonObject> entries, string fingerprint, DateTimeOffset now)
    {
        var builder = new StringBuilder()
            .AppendLine($"# {Escape(projectName)}")
            .AppendLine()
            .AppendLine("> Generated deterministically from a WorldSeed project snapshot. This document does not add or alter game mechanics.")
            .AppendLine()
            .AppendLine("## Publication Snapshot")
            .AppendLine()
            .AppendLine($"- Created: {now:O}")
            .AppendLine($"- Working Rule Set: `{Escape(ruleSet.Id)}` revision {ruleSet.Revision}")
            .AppendLine($"- Active Schema: `{Escape(activeSchema.Id)}` revision {activeSchema.Revision}")
            .AppendLine($"- Game Data Fingerprint: `{fingerprint}`")
            .AppendLine()
            .AppendLine("## Rules")
            .AppendLine();

        foreach (var rule in ruleSet.Rules.OrderBy(rule => rule.Name, StringComparer.Ordinal).ThenBy(rule => rule.Id, StringComparer.Ordinal))
        {
            builder.AppendLine($"### {Escape(rule.Name)}").AppendLine().AppendLine(rule.AuthoritativeText.Trim()).AppendLine().AppendLine($"Rule ID: `{Escape(rule.Id)}`").AppendLine();
        }
        if (ruleSet.Rules.Count == 0) builder.AppendLine("_No rules are present in this selected snapshot._").AppendLine();

        RenderValueTypes(builder, schema);
        RenderEntities(builder, schema, entries);
        RenderRelationships(builder, data);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void RenderValueTypes(StringBuilder builder, JsonObject schema)
    {
        builder.AppendLine("## Categories And Value Types").AppendLine();
        var definitions = (schema["valueTypes"] as JsonArray)?.OfType<JsonObject>()
            .OrderBy(definition => DisplayName(definition), StringComparer.Ordinal).ToArray() ?? [];
        if (definitions.Length == 0) { builder.AppendLine("_No named categories or value types are defined._").AppendLine(); return; }
        foreach (var definition in definitions)
        {
            builder.AppendLine($"### {Escape(DisplayName(definition))}").AppendLine();
            var kind = definition["kind"]?.GetValue<string>() ?? "unspecified";
            builder.AppendLine($"Type: {Escape(kind)}").AppendLine();
            var entries = (definition["entries"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
            foreach (var entry in entries.OrderBy(DisplayName, StringComparer.Ordinal)) builder.AppendLine($"- {Escape(DisplayName(entry))} (`{Escape(DefinitionId(entry) ?? "unknown")}`)");
            if (entries.Length > 0) builder.AppendLine();
        }
    }

    private static void RenderEntities(StringBuilder builder, JsonObject schema, IReadOnlyList<JsonObject> entries)
    {
        builder.AppendLine("## Game Content").AppendLine();
        var entityTypes = (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>()
            .Where(entity => DefinitionId(entity) is not null)
            .OrderBy(entity => DisplayName(entity), StringComparer.Ordinal).ToArray() ?? [];
        if (entityTypes.Length == 0) { builder.AppendLine("_No content types are defined._").AppendLine(); return; }
        foreach (var entityType in entityTypes)
        {
            var entityTypeId = DefinitionId(entityType)!;
            var typeEntries = entries.Where(entry => string.Equals(entry["entityTypeId"]?.GetValue<string>(), entityTypeId, StringComparison.Ordinal)).ToArray();
            builder.AppendLine($"### {Escape(DisplayName(entityType))}").AppendLine();
            if (typeEntries.Length == 0) { builder.AppendLine("_No canonical entries._").AppendLine(); continue; }
            var propertyNames = EffectiveProperties(schema, entityType).ToDictionary(property => DefinitionId(property)!, DisplayName, StringComparer.Ordinal);
            foreach (var entry in typeEntries)
            {
                var entryId = entry["id"]?.GetValue<string>() ?? "unknown";
                var values = entry["values"] as JsonObject ?? new JsonObject();
                var heading = values.FirstOrDefault(property => IsLabelProperty(property.Key) && property.Value is JsonValue).Value is { } labelNode && StringValue(labelNode) is { Length: > 0 } label ? label : entryId;
                builder.AppendLine($"#### {Escape(heading)}").AppendLine().AppendLine($"Entry ID: `{Escape(entryId)}`").AppendLine();
                foreach (var property in values.OrderBy(property => propertyNames.GetValueOrDefault(property.Key, property.Key), StringComparer.Ordinal))
                    builder.AppendLine($"- **{Escape(propertyNames.GetValueOrDefault(property.Key, property.Key))}:** {RenderValue(property.Value)}");
                builder.AppendLine();
            }
        }
    }

    private static void RenderRelationships(StringBuilder builder, JsonObject data)
    {
        builder.AppendLine("## Relationships").AppendLine();
        var relationships = (data["relationshipInstances"] as JsonArray)?.OfType<JsonObject>().OrderBy(item => item["id"]?.GetValue<string>(), StringComparer.Ordinal).ToArray() ?? [];
        if (relationships.Length == 0) { builder.AppendLine("_No canonical relationships._").AppendLine(); return; }
        foreach (var relationship in relationships) builder.AppendLine($"- `{Escape(relationship["id"]?.GetValue<string>() ?? "unknown")}`: {RenderValue(relationship)}");
        builder.AppendLine();
    }

    private static JsonObject BuildManifest(string snapshotId, string projectName, WorkingRuleSet ruleSet, ActiveGameSchema activeSchema, ProjectGameData gameData, string fingerprint, IReadOnlyList<string> includedIds, DateTimeOffset now) => new()
    {
        ["formatVersion"] = "0.1",
        ["snapshotId"] = snapshotId,
        ["projectId"] = activeSchema.ProjectId,
        ["projectName"] = projectName,
        ["createdAt"] = now,
        ["workingRuleSetId"] = ruleSet.Id,
        ["workingRuleSetRevision"] = ruleSet.Revision,
        ["activeGameSchemaId"] = activeSchema.Id,
        ["activeGameSchemaRevision"] = activeSchema.Revision,
        ["gameDataId"] = gameData.Id,
        ["gameDataFingerprint"] = fingerprint,
        ["includedEntityIds"] = new JsonArray(includedIds.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray())
    };

    private static IEnumerable<JsonObject> EffectiveProperties(JsonObject schema, JsonObject entity)
    {
        var fragments = (schema["schemaFragments"] as JsonArray)?.OfType<JsonObject>().Where(fragment => DefinitionId(fragment) is not null).ToDictionary(fragment => DefinitionId(fragment)!, StringComparer.Ordinal) ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fragmentId in entity["fragmentIds"]?.AsArray().OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var id) ? id : null).Where(id => id is not null).Cast<string>() ?? [])
            if (fragments.TryGetValue(fragmentId, out var fragment))
                foreach (var property in fragment["properties"]?.AsArray().OfType<JsonObject>() ?? [])
                    if (DefinitionId(property) is { } id && seen.Add(id)) yield return property;
        foreach (var property in entity["properties"]?.AsArray().OfType<JsonObject>() ?? [])
            if (DefinitionId(property) is { } id && seen.Add(id)) yield return property;
    }

    private static string RenderValue(JsonNode? value) => value switch
    {
        null => "null",
        JsonValue json when json.TryGetValue<string>(out var text) => Escape(text),
        JsonValue json when json.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
        JsonValue json when json.TryGetValue<decimal>(out var number) => number.ToString(CultureInfo.InvariantCulture),
        JsonArray array => string.Join(", ", array.Select(RenderValue)),
        _ => $"`{value.ToJsonString()}`"
    };

    private static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string DisplayName(JsonObject definition) => (definition["metadata"] as JsonObject)?["name"]?.GetValue<string>() ?? definition["name"]?.GetValue<string>() ?? DefinitionId(definition) ?? "Unnamed";
    private static string? DefinitionId(JsonObject definition) => (definition["metadata"] as JsonObject)?["id"]?.GetValue<string>() ?? definition["id"]?.GetValue<string>();
    private static bool IsLabelProperty(string id) => id.Equals("name", StringComparison.OrdinalIgnoreCase) || id.EndsWith("Name", StringComparison.OrdinalIgnoreCase) || id.Equals("title", StringComparison.OrdinalIgnoreCase) || id.EndsWith("Title", StringComparison.OrdinalIgnoreCase) || id.Equals("label", StringComparison.OrdinalIgnoreCase) || id.EndsWith("Label", StringComparison.OrdinalIgnoreCase);
    private static string? StringValue(JsonNode node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
}
