using System.Text.Json.Nodes;

namespace WorldSeed.SchemaValidation;

/// <summary>Validates provenance, revision lineage, and decisions in a change-management ledger.</summary>
public sealed class ChangeLedgerValidator
{
    public SchemaValidationResult Validate(JsonNode? ledgerNode)
    {
        var result = new SchemaValidationResult();
        if (ledgerNode is not JsonObject ledger) { result.Add("$", "Change ledger must be a JSON object."); return result; }
        RequireString(ledger, "id", "$", result); RequireString(ledger, "projectId", "$", result);
        var sources = RequireArray(ledger, "sourceMaterials", result);
        var proposals = RequireArray(ledger, "interpretationProposals", result);
        var revisions = RequireArray(ledger, "revisions", result);
        var changeSets = RequireArray(ledger, "changeSets", result);
        var decisions = RequireArray(ledger, "decisions", result);

        var sourceById = Index(sources, "$.sourceMaterials", result);
        var proposalById = Index(proposals, "$.interpretationProposals", result);
        var revisionById = Index(revisions, "$.revisions", result);
        var changeSetById = Index(changeSets, "$.changeSets", result);
        var decisionById = Index(decisions, "$.decisions", result);

        foreach (var source in sourceById.Values) ValidateSource(source, result);
        foreach (var proposal in proposalById.Values) ValidateProposal(proposal, sourceById, result);
        foreach (var revision in revisionById.Values) ValidateRevision(revision, revisionById, result);
        foreach (var changeSet in changeSetById.Values) ValidateChangeSet(changeSet, sourceById, proposalById, revisionById, result);
        ValidateDecisions(decisionById.Values, changeSetById, result);
        ValidateAppliedChangeSets(changeSetById, decisionById.Values, result);
        return result;
    }

    private static void ValidateSource(JsonObject source, SchemaValidationResult result)
    {
        var path = "$.sourceMaterials[" + String(source["id"]) + "]";
        var kind = String(source["kind"]);
        if (kind is not ("designerInput" or "externalReference" or "import")) result.Add(path, "Source material has an unknown kind.");
        if (kind == "designerInput" && String(source["content"]) is null) result.Add(path, "Designer input must preserve its original content.");
        if (String(source["content"]) is null && String(source["location"]) is null) result.Add(path, "Source material requires content or a location.");
        ValidateTimestamp(source["capturedAt"], path, result); ValidateActor(source["capturedBy"] as JsonObject, path + ".capturedBy", result, requireHuman: false);
    }

    private static void ValidateProposal(JsonObject proposal, IReadOnlyDictionary<string, JsonObject> sources, SchemaValidationResult result)
    {
        var path = "$.interpretationProposals[" + String(proposal["id"]) + "]";
        ValidateReferences(proposal, "sourceMaterialIds", sources, path, result);
        if (proposal["sourceMaterialIds"] is not JsonArray { Count: > 0 }) result.Add(path, "Interpretation proposals require at least one source material reference.");
        if (String(proposal["summary"]) is null) result.Add(path, "Interpretation proposals require a summary.");
        if (String(proposal["proposedText"]) is null) result.Add(path, "Interpretation proposals require human-readable proposed text.");
        if (proposal["proposedChanges"] is not JsonArray { Count: > 0 }) result.Add(path, "Interpretation proposals require at least one proposed change.");
        foreach (var change in proposal["proposedChanges"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            if (String(change["operation"]) is not ("create" or "replace" or "remove" or "retire")) result.Add(path, "A proposed change has an unknown operation.");
            if (String(change["explanation"]) is null) result.Add(path, "Every proposed change requires an explanation.");
        }
        if (String(proposal["state"]) is not ("proposed" or "accepted" or "rejected" or "superseded")) result.Add(path, "Interpretation proposal has an unknown state.");
        ValidateTimestamp(proposal["proposedAt"], path, result); ValidateActor(proposal["proposedBy"] as JsonObject, path + ".proposedBy", result, requireHuman: false);
    }

    private static void ValidateRevision(JsonObject revision, IReadOnlyDictionary<string, JsonObject> revisions, SchemaValidationResult result)
    {
        var path = "$.revisions[" + String(revision["id"]) + "]";
        RequireString(revision, "artifactKind", path, result); RequireString(revision, "artifactId", path, result);
        if (!revision.ContainsKey("snapshot") || revision["snapshot"] is null) result.Add(path, "Revisions require an immutable snapshot.");
        var previousId = String(revision["previousRevisionId"]);
        if (previousId is not null)
        {
            if (!revisions.TryGetValue(previousId, out var previous)) result.Add(path, $"Unknown previous revision '{previousId}'.");
            else if (String(previous["artifactKind"]) != String(revision["artifactKind"]) || String(previous["artifactId"]) != String(revision["artifactId"])) result.Add(path, "A previous revision must belong to the same artifact.");
        }
        ValidateTimestamp(revision["createdAt"], path, result); ValidateActor(revision["createdBy"] as JsonObject, path + ".createdBy", result, requireHuman: false);
    }

    private static void ValidateChangeSet(JsonObject changeSet, IReadOnlyDictionary<string, JsonObject> sources, IReadOnlyDictionary<string, JsonObject> proposals, IReadOnlyDictionary<string, JsonObject> revisions, SchemaValidationResult result)
    {
        var path = "$.changeSets[" + String(changeSet["id"]) + "]";
        var operation = String(changeSet["operation"]); var state = String(changeSet["state"]);
        if (operation is not ("create" or "update" or "retire")) result.Add(path, "Change set has an unknown operation.");
        if (state is not ("draft" or "underReview" or "applied" or "rejected" or "withdrawn" or "superseded")) result.Add(path, "Change set has an unknown state.");
        RequireString(changeSet, "artifactKind", path, result); RequireString(changeSet, "artifactId", path, result);
        ValidateReferences(changeSet, "sourceMaterialIds", sources, path, result); ValidateReferences(changeSet, "proposalIds", proposals, path, result);
        var baseRevision = ResolveRevision(changeSet, "baseRevisionId", revisions, path, result);
        var resultRevision = ResolveRevision(changeSet, "resultRevisionId", revisions, path, result);
        if (state == "applied" && resultRevision is null) result.Add(path, "An applied change set requires a resulting revision.");
        if (operation == "update" && baseRevision is null) result.Add(path, "An update change set requires a base revision.");
        foreach (var revision in new[] { baseRevision, resultRevision }.Where(revision => revision is not null).Cast<JsonObject>())
            if (String(revision["artifactKind"]) != String(changeSet["artifactKind"]) || String(revision["artifactId"]) != String(changeSet["artifactId"])) result.Add(path, "Change-set revisions must belong to the named artifact.");
        if (operation == "update" && baseRevision is not null && resultRevision is not null && String(resultRevision["previousRevisionId"]) != String(baseRevision["id"])) result.Add(path, "An update result revision must directly follow its base revision.");
        ValidateTimestamp(changeSet["createdAt"], path, result); ValidateActor(changeSet["createdBy"] as JsonObject, path + ".createdBy", result, requireHuman: false);
    }

    private static void ValidateDecisions(IEnumerable<JsonObject> decisions, IReadOnlyDictionary<string, JsonObject> changeSets, SchemaValidationResult result)
    {
        foreach (var decision in decisions)
        {
            var path = "$.decisions[" + String(decision["id"]) + "]";
            var changeSetId = String(decision["changeSetId"]);
            if (changeSetId is null || !changeSets.ContainsKey(changeSetId)) result.Add(path, $"Unknown change set '{changeSetId}'.");
            if (String(decision["outcome"]) is not ("apply" or "reject" or "requestRevision" or "withdraw")) result.Add(path, "Decision has an unknown outcome.");
            ValidateTimestamp(decision["decidedAt"], path, result); ValidateActor(decision["decidedBy"] as JsonObject, path + ".decidedBy", result, requireHuman: true);
        }
    }

    private static void ValidateAppliedChangeSets(IReadOnlyDictionary<string, JsonObject> changeSets, IEnumerable<JsonObject> decisions, SchemaValidationResult result)
    {
        var applied = decisions.Where(decision => String(decision["outcome"]) == "apply").Select(decision => String(decision["changeSetId"])).ToHashSet();
        foreach (var (id, changeSet) in changeSets) if (String(changeSet["state"]) == "applied" && !applied.Contains(id)) result.Add("$.changeSets[" + id + "]", "An applied change set requires a human apply decision.");
    }

    private static JsonObject? ResolveRevision(JsonObject owner, string name, IReadOnlyDictionary<string, JsonObject> revisions, string path, SchemaValidationResult result)
    {
        var id = String(owner[name]); if (id is null) return null;
        if (revisions.TryGetValue(id, out var revision)) return revision;
        result.Add(path, $"Unknown revision '{id}'."); return null;
    }
    private static void ValidateReferences(JsonObject owner, string name, IReadOnlyDictionary<string, JsonObject> targets, string path, SchemaValidationResult result)
    {
        foreach (var id in Strings(owner, name)) if (!targets.ContainsKey(id)) result.Add(path, $"Unknown {name} reference '{id}'.");
    }
    private static void ValidateTimestamp(JsonNode? node, string path, SchemaValidationResult result) { if (String(node) is not { } value || !DateTimeOffset.TryParse(value, out _)) result.Add(path, "A valid timestamp is required."); }
    private static void ValidateActor(JsonObject? actor, string path, SchemaValidationResult result, bool requireHuman) { if (actor is null || String(actor["id"]) is null || String(actor["kind"]) is not ("human" or "model" or "service")) result.Add(path, "A valid actor is required."); else if (requireHuman && String(actor["kind"]) != "human") result.Add(path, "A human actor is required for a decision."); }
    private static Dictionary<string, JsonObject> Index(JsonArray? entries, string path, SchemaValidationResult result) { var index = new Dictionary<string, JsonObject>(); foreach (var entry in entries?.OfType<JsonObject>() ?? []) { var id = String(entry["id"]); if (id is null || !index.TryAdd(id, entry)) result.Add(path, "Every entry requires a unique id."); } return index; }
    private static JsonArray? RequireArray(JsonObject owner, string name, SchemaValidationResult result) => owner[name] as JsonArray ?? AddMissing(name, result);
    private static JsonArray? AddMissing(string name, SchemaValidationResult result) { result.Add("$", $"'{name}' must be an array."); return null; }
    private static string? String(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static IEnumerable<string> Strings(JsonObject owner, string name) => (owner[name] as JsonArray)?.Select(String).Where(value => value is not null).Cast<string>() ?? [];
    private static void RequireString(JsonObject owner, string name, string path, SchemaValidationResult result) { if (String(owner[name]) is null) result.Add(path, $"'{name}' must be a non-empty string."); }
}
