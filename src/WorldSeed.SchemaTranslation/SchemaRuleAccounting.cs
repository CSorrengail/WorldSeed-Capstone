using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.WorkingRules;

namespace WorldSeed.SchemaTranslation;

public sealed record SchemaRuleOutcome(string WorkingRuleId, string Disposition, string Reason, IReadOnlyList<string> DefinitionIds);
public sealed record SchemaRuleAssessment(SchemaRuleOutcome Outcome, string WorkingRuleSetId, int WorkingRuleSetRevision,
    string RuleFingerprint, string SchemaFingerprint, IReadOnlyDictionary<string, string> DefinitionFingerprints, DateTimeOffset RecordedAt);

/// <summary>Explicit accounting is separate from structural validation and never deletes a Working Rule.</summary>
public static class SchemaRuleAccounting
{
    public static readonly string[] Dispositions = ["create", "extend", "alreadyRepresented", "retainAsText", "excludeFromMechanics", "needsClarification"];
    private static readonly string[] Collections = ["schemaFragments", "entityTypes", "valueTypes", "relationships", "rules", "procedures", "validations"];

    public static Dictionary<string, (string Collection, JsonObject Definition)> Definitions(JsonObject? schema) => Collections
        .SelectMany(collection => (schema?[collection] as JsonArray)?.OfType<JsonObject>()
            .Select(definition => (Collection: collection, Definition: definition)) ?? [])
        .ToDictionary(item => (item.Definition["metadata"]?["id"] ?? item.Definition["id"])!.GetValue<string>(), StringComparer.Ordinal);

    public static IReadOnlyList<SchemaRuleOutcome> Parse(JsonNode? node, WorkingRuleSet rules)
    {
        if (node is not JsonArray array || array.Count != rules.Rules.Count)
            throw new SchemaTranslationFormatException("Return exactly one outcome for every selected Working Rule.");
        var allowed = rules.Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var result = new List<SchemaRuleOutcome>();
        foreach (var item in array)
        {
            if (item is not JsonObject) throw new SchemaTranslationFormatException("Each rule outcome must be an object.");
            var id = Text(item?["workingRuleId"]); var disposition = Text(item?["disposition"]); var reason = Text(item?["reason"]);
            if (!allowed.Remove(id) || !Dispositions.Contains(disposition, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(reason))
                throw new SchemaTranslationFormatException("Outcomes require unique selected workingRuleIds, a supported disposition, and a specific reason.");
            if (item?["definitionIds"] is not JsonArray ids || ids.Any(value => string.IsNullOrWhiteSpace(Text(value))))
                throw new SchemaTranslationFormatException("Every outcome requires definitionIds (an empty array for text, metadata, or clarification).");
            var references = ids.Select(Text).ToArray();
            if (references.Distinct(StringComparer.Ordinal).Count() != references.Length ||
                ((disposition is "create" or "extend" or "alreadyRepresented") != (references.Length > 0)))
                throw new SchemaTranslationFormatException("Create, extend, and alreadyRepresented outcomes require definition IDs; other outcomes must not claim schema definitions.");
            result.Add(new(id, disposition, reason, references));
        }
        return result;
    }

    public static void Validate(IReadOnlyList<SchemaRuleOutcome> outcomes, JsonObject? baseline, JsonObject? addition, IReadOnlyList<DefinitionSource> sources)
    {
        var saved = Definitions(baseline); var proposed = Definitions(addition);
        foreach (var outcome in outcomes)
        foreach (var id in outcome.DefinitionIds)
        {
            if (outcome.Disposition == "alreadyRepresented")
            {
                if (!saved.ContainsKey(id) || proposed.ContainsKey(id)) throw new SchemaTranslationFormatException($"Already represented definition '{id}' must reference an unchanged saved definition.");
                continue;
            }
            if (!proposed.TryGetValue(id, out var candidate) || !sources.Any(source => source.DefinitionId == id && source.WorkingRuleIds.Contains(outcome.WorkingRuleId, StringComparer.Ordinal)))
                throw new SchemaTranslationFormatException($"Outcome '{id}' must identify a proposed definition citing that Working Rule.");
            if (outcome.Disposition == "create" && saved.ContainsKey(id))
                throw new SchemaTranslationFormatException($"Definition '{id}' already exists. Reference it or explicitly extend it; never rename it to bypass a conflict.");
            if (outcome.Disposition == "extend")
            {
                if (!saved.TryGetValue(id, out var existing) || existing.Collection != candidate.Collection || !IsAdditive(existing.Definition, candidate.Definition, definitionRoot: true))
                    throw new SchemaTranslationFormatException($"Extension '{id}' must preserve its collection, kind, and existing members. Conflicting changes require a separate designer schema-change request.");
                var merged = SchemaProposalMerger.MergeSchemas(baseline, addition!, true);
                if (JsonNode.DeepEquals(Definitions(merged)[id].Definition, existing.Definition))
                    throw new SchemaTranslationFormatException($"Extension '{id}' adds nothing. Use alreadyRepresented instead.");
            }
        }
        foreach (var source in sources)
            foreach (var ruleId in source.WorkingRuleIds)
                if (!outcomes.Any(outcome => outcome.WorkingRuleId == ruleId && outcome.Disposition is "create" or "extend" && outcome.DefinitionIds.Contains(source.DefinitionId, StringComparer.Ordinal)))
                    throw new SchemaTranslationFormatException("Every proposed source mapping requires a matching create or extend outcome.");
        foreach (var id in proposed.Keys)
            if (!outcomes.Any(outcome => outcome.Disposition is "create" or "extend" && outcome.DefinitionIds.Contains(id, StringComparer.Ordinal)))
                throw new SchemaTranslationFormatException($"Proposed definition '{id}' has no structural outcome.");
    }

    // Ordinary extensions may add members but cannot silently change an existing type, requirement, or value.
    private static bool IsAdditive(JsonNode? existing, JsonNode? patch, bool definitionRoot = false)
    {
        if (existing is JsonObject oldObject && patch is JsonObject patchObject)
            return patchObject.All(pair => oldObject.ContainsKey(pair.Key) ? IsAdditive(oldObject[pair.Key], pair.Value)
                : definitionRoot && pair.Key is "properties" or "entries" && pair.Value is JsonArray);
        if (existing is JsonArray oldArray && patch is JsonArray patchArray)
        {
            string? Id(JsonNode? member) => Text(member?["metadata"]?["id"] ?? member?["id"]);
            if (oldArray.Concat(patchArray).All(member => member is JsonObject && !string.IsNullOrEmpty(Id(member))))
                return patchArray.All(member => oldArray.FirstOrDefault(old => Id(old) == Id(member)) is not { } old || IsAdditive(old, member));
        }
        return JsonNode.DeepEquals(existing, patch);
    }

    public static SchemaRuleAssessment Bind(SchemaRuleOutcome outcome, WorkingRuleSet rules, JsonObject schema, DateTimeOffset now)
    {
        var definitions = Definitions(schema);
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (!dependencies.Add(id)) return;
            foreach (var reference in SchemaPromptContext.References(definitions[id].Definition).Where(definitions.ContainsKey)) Visit(reference);
        }
        foreach (var id in outcome.DefinitionIds) Visit(id);
        return new(outcome, rules.Id, rules.Revision, RuleHash(rules.Rules.Single(rule => rule.Id == outcome.WorkingRuleId)), Fingerprint(schema),
            dependencies.ToDictionary(id => id, id => Fingerprint(definitions[id].Definition), StringComparer.Ordinal), now);
    }

    public static SchemaRuleAssessment? Current(ProjectSchemaProposal? proposal, WorkingRuleSet? rules, WorkingRule rule)
        => CurrentAssessments(proposal, rules).GetValueOrDefault(rule.Id);

    public static IReadOnlyDictionary<string, SchemaRuleAssessment> CurrentAssessments(ProjectSchemaProposal? proposal, WorkingRuleSet? rules)
    {
        var result = new Dictionary<string, SchemaRuleAssessment>(StringComparer.Ordinal);
        if (proposal is null || rules is null || proposal.RuleAssessments is not { Count: > 0 }) return result;
        var definitions = Definitions(JsonNode.Parse(proposal.SchemaJson)!.AsObject());
        var hashes = definitions.ToDictionary(pair => pair.Key, pair => Fingerprint(pair.Value.Definition), StringComparer.Ordinal);
        var byRule = rules.Rules.ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        foreach (var latest in proposal.RuleAssessments.GroupBy(item => item.Outcome.WorkingRuleId, StringComparer.Ordinal).Select(group => group.Last()))
            if (latest.WorkingRuleSetId == rules.Id && latest.WorkingRuleSetRevision == rules.Revision &&
                byRule.TryGetValue(latest.Outcome.WorkingRuleId, out var rule) && latest.RuleFingerprint == RuleHash(rule) &&
                latest.DefinitionFingerprints.All(pair => hashes.TryGetValue(pair.Key, out var hash) && hash == pair.Value))
                result[rule.Id] = latest;
        return result;
    }

    public static bool IsComplete(SchemaRuleAssessment? assessment) => assessment is not null && assessment.Outcome.Disposition != "needsClarification";

    /// <summary>The API preserves assessment history and verifies new entries against the actual saved rules/schema.</summary>
    public static void ValidateHistory(ProjectSchemaProposal proposal, WorkingRuleSet rules, ProjectSchemaProposal? existing)
    {
        var history = proposal.RuleAssessments ?? [];
        var previous = existing?.RuleAssessments ?? [];
        if (history.Count < previous.Count || previous.Where((item, index) => Fingerprint(JsonSerializer.SerializeToNode(item)!) != Fingerprint(JsonSerializer.SerializeToNode(history[index])!)).Any())
            throw new SchemaTranslationFormatException("Saved rule assessment history must be preserved.");
        if (history.Count == previous.Count) return;
        var schema = JsonNode.Parse(proposal.SchemaJson)!.AsObject();
        foreach (var assessment in history.Skip(previous.Count))
        {
            if (assessment is null || assessment.Outcome is null || !rules.Rules.Any(rule => rule.Id == assessment.Outcome.WorkingRuleId))
                throw new SchemaTranslationFormatException("An assessment must cite a current Working Rule.");
            var selected = rules with { Rules = rules.Rules.Where(rule => rule.Id == assessment.Outcome.WorkingRuleId).ToArray() };
            Parse(JsonSerializer.SerializeToNode(new[] { new { workingRuleId = assessment.Outcome.WorkingRuleId, disposition = assessment.Outcome.Disposition, reason = assessment.Outcome.Reason, definitionIds = assessment.Outcome.DefinitionIds } }), selected);
            var expected = Bind(assessment.Outcome, rules, schema, assessment.RecordedAt);
            if (Fingerprint(JsonSerializer.SerializeToNode(expected)!) != Fingerprint(JsonSerializer.SerializeToNode(assessment)!))
                throw new SchemaTranslationFormatException("A new assessment must match the current Working Rule revision and saved schema fingerprints.");
        }
    }

    public static string RuleHash(WorkingRule rule) => Fingerprint(JsonSerializer.SerializeToNode(rule)!);
    public static string Fingerprint(JsonNode node)
    {
        JsonNode? Canonical(JsonNode? value) => value switch
        {
            JsonObject obj => new JsonObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => KeyValuePair.Create(pair.Key, Canonical(pair.Value)))),
            JsonArray array => new JsonArray(array.Select(Canonical).ToArray()),
            _ => value?.DeepClone()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(node)!.ToJsonString())));
    }
    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}
