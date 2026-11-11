using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.GameData;
using WorldSeed.WorkingRules;

namespace WorldSeed.Publication;

public enum PublicationUnitKind { Rule, ValueType, ContentType, ContentEntry, Relationship }

public sealed record PublicationUnitSource(string SourceType, string SourceId);

public sealed record PublicationUnit(
    string Id,
    PublicationUnitKind Kind,
    string Title,
    string? ParentId,
    IReadOnlyList<PublicationUnitSource> Sources,
    string MarkdownBody);

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
    string ManifestJson,
    IReadOnlyList<PublicationUnit>? Units = null);

public interface IPublicationSnapshotStore
{
    Task SaveAsync(PublicationSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicationSnapshot>> ListAsync(string projectId, CancellationToken cancellationToken = default);
    Task<PublicationSnapshot?> LoadAsync(string projectId, string snapshotId, CancellationToken cancellationToken = default);
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
        var units = BuildUnits(ruleSet, schema, entries, data);
        var manifest = BuildManifest(snapshotId, projectName.Trim(), ruleSet, activeSchema, gameData, fingerprint, includedIds, units, now);
        var markdown = RenderMarkdown(projectName.Trim(), ruleSet, activeSchema, units, fingerprint, now);
        return new PublicationSnapshot(snapshotId, activeSchema.ProjectId, projectName.Trim(), ruleSet.Id, ruleSet.Revision, activeSchema.Id, activeSchema.Revision, gameData.Id, fingerprint, includedIds, now, markdown, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), units);
    }

    private static string RenderMarkdown(string projectName, WorkingRuleSet ruleSet, ActiveGameSchema activeSchema, IReadOnlyList<PublicationUnit> units, string fingerprint, DateTimeOffset now)
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

        var rules = units.Where(unit => unit.Kind == PublicationUnitKind.Rule).ToArray();
        foreach (var rule in rules) RenderUnit(builder, "###", rule);
        if (rules.Length == 0) builder.AppendLine("_No rules are present in this selected snapshot._").AppendLine();

        builder.AppendLine("## Categories And Value Types").AppendLine();
        var valueTypes = units.Where(unit => unit.Kind == PublicationUnitKind.ValueType).ToArray();
        foreach (var valueType in valueTypes) RenderUnit(builder, "###", valueType);
        if (valueTypes.Length == 0) builder.AppendLine("_No named categories or value types are defined._").AppendLine();

        builder.AppendLine("## Game Content").AppendLine();
        var contentTypes = units.Where(unit => unit.Kind == PublicationUnitKind.ContentType).ToArray();
        foreach (var contentType in contentTypes)
        {
            builder.AppendLine($"### {Escape(contentType.Title)}").AppendLine();
            var contentEntries = units.Where(unit => unit.Kind == PublicationUnitKind.ContentEntry && unit.ParentId == contentType.Id).ToArray();
            foreach (var contentEntry in contentEntries) RenderUnit(builder, "####", contentEntry);
            if (contentEntries.Length == 0) builder.AppendLine("_No canonical entries._").AppendLine();
        }
        if (contentTypes.Length == 0) builder.AppendLine("_No content types are defined._").AppendLine();

        builder.AppendLine("## Relationships").AppendLine();
        var relationships = units.Where(unit => unit.Kind == PublicationUnitKind.Relationship).ToArray();
        foreach (var relationship in relationships) builder.AppendLine(relationship.MarkdownBody.Trim()).AppendLine();
        if (relationships.Length == 0) builder.AppendLine("_No canonical relationships._").AppendLine();
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static IReadOnlyList<PublicationUnit> BuildUnits(WorkingRuleSet ruleSet, JsonObject schema, IReadOnlyList<JsonObject> entries, JsonObject data)
    {
        var units = new List<PublicationUnit>();
        foreach (var rule in ruleSet.Rules.OrderBy(rule => rule.Name, StringComparer.Ordinal).ThenBy(rule => rule.Id, StringComparer.Ordinal))
            units.Add(new PublicationUnit($"rule:{rule.Id}", PublicationUnitKind.Rule, rule.Name, null, [new("working-rule", rule.Id)], rule.AuthoritativeText.Trim()));

        foreach (var definition in (schema["valueTypes"] as JsonArray)?.OfType<JsonObject>().OrderBy(DisplayName, StringComparer.Ordinal).ToArray() ?? [])
        {
            var definitionId = DefinitionId(definition) ?? throw new InvalidOperationException("A publication value type requires an identifier.");
            var kind = definition["kind"]?.GetValue<string>() ?? "unspecified";
            var body = new StringBuilder().AppendLine($"Type: {Escape(kind)}");
            var categoryEntries = (definition["entries"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
            foreach (var entry in categoryEntries.OrderBy(DisplayName, StringComparer.Ordinal)) body.AppendLine($"- {Escape(DisplayName(entry))} (`{Escape(DefinitionId(entry) ?? "unknown")}`)");
            units.Add(new PublicationUnit($"value-type:{definitionId}", PublicationUnitKind.ValueType, DisplayName(definition), null, [new("schema-value-type", definitionId)], body.ToString().TrimEnd()));
        }

        var entityTypes = (schema["entityTypes"] as JsonArray)?.OfType<JsonObject>()
            .Where(entity => DefinitionId(entity) is not null)
            .OrderBy(entity => DisplayName(entity), StringComparer.Ordinal).ToArray() ?? [];
        foreach (var entityType in entityTypes)
        {
            var entityTypeId = DefinitionId(entityType)!;
            var contentTypeUnitId = $"content-type:{entityTypeId}";
            units.Add(new PublicationUnit(contentTypeUnitId, PublicationUnitKind.ContentType, DisplayName(entityType), null, [new("schema-entity-type", entityTypeId)], string.Empty));
            var typeEntries = entries.Where(entry => string.Equals(entry["entityTypeId"]?.GetValue<string>(), entityTypeId, StringComparison.Ordinal)).ToArray();
            var propertyNames = EffectiveProperties(schema, entityType).ToDictionary(property => DefinitionId(property)!, DisplayName, StringComparer.Ordinal);
            foreach (var entry in typeEntries)
            {
                var entryId = entry["id"]?.GetValue<string>() ?? "unknown";
                var values = entry["values"] as JsonObject ?? new JsonObject();
                var heading = values.FirstOrDefault(property => IsLabelProperty(property.Key) && property.Value is JsonValue).Value is { } labelNode && StringValue(labelNode) is { Length: > 0 } label ? label : entryId;
                var body = new StringBuilder().AppendLine($"Entry ID: `{Escape(entryId)}`").AppendLine();
                foreach (var property in values.OrderBy(property => propertyNames.GetValueOrDefault(property.Key, property.Key), StringComparer.Ordinal))
                    body.AppendLine($"- **{Escape(propertyNames.GetValueOrDefault(property.Key, property.Key))}:** {RenderValue(property.Value)}");
                units.Add(new PublicationUnit($"content-entry:{entityTypeId}:{entryId}", PublicationUnitKind.ContentEntry, heading, contentTypeUnitId, [new("game-data-entry", entryId), new("schema-entity-type", entityTypeId)], body.ToString().TrimEnd()));
            }
        }

        var relationships = (data["relationshipInstances"] as JsonArray)?.OfType<JsonObject>().OrderBy(item => item["id"]?.GetValue<string>(), StringComparer.Ordinal).ToArray() ?? [];
        foreach (var relationship in relationships)
        {
            var relationshipId = relationship["id"]?.GetValue<string>() ?? "unknown";
            units.Add(new PublicationUnit($"relationship:{relationshipId}", PublicationUnitKind.Relationship, relationshipId, null, [new("game-data-relationship", relationshipId)], $"- `{Escape(relationshipId)}`: {RenderValue(relationship)}"));
        }
        return units;
    }

    private static void RenderUnit(StringBuilder builder, string heading, PublicationUnit unit) => builder.AppendLine($"{heading} {Escape(unit.Title)}").AppendLine().AppendLine(unit.MarkdownBody.Trim()).AppendLine();

    private static JsonObject BuildManifest(string snapshotId, string projectName, WorkingRuleSet ruleSet, ActiveGameSchema activeSchema, ProjectGameData gameData, string fingerprint, IReadOnlyList<string> includedIds, IReadOnlyList<PublicationUnit> units, DateTimeOffset now) => new()
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
        ["includedEntityIds"] = new JsonArray(includedIds.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
        ["publicationUnits"] = new JsonArray(units.Select(unit => (JsonNode?)new JsonObject
        {
            ["id"] = unit.Id,
            ["kind"] = unit.Kind.ToString(),
            ["parentId"] = unit.ParentId,
            ["sources"] = new JsonArray(unit.Sources.Select(source => (JsonNode?)new JsonObject { ["sourceType"] = source.SourceType, ["sourceId"] = source.SourceId }).ToArray())
        }).ToArray())
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
