using System.Globalization;
using System.Text.Json.Nodes;
using WorldSeed.GameData;

namespace WorldSeed.Content;

/// <summary>
/// Evaluates one game-owned profile. It deliberately knows only declared property
/// paths and two basic measurements; it never assumes dice, hit points, or CR.
/// </summary>
public sealed class ContentEvaluationService
{
    public ContentEvaluationResult Evaluate(ContentEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var structural = GameDataService.Validate(request.ActiveSchema, request.GameData);
        var violations = structural.Issues.Select(issue => new MechanicalViolation(issue.Path, issue.Message)).ToArray();
        if (!structural.IsValid) return new ContentEvaluationResult(violations, []);

        var root = JsonNode.Parse(request.GameData.DataJson) as JsonObject
            ?? throw new GameDataFormatException("Game data is not a JSON object.");
        var concerns = new List<BalanceConcern>();
        foreach (var instance in root["entityInstances"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            if (!string.Equals(instance["entityTypeId"]?.GetValue<string>(), request.BalanceProfile.EntityTypeId, StringComparison.Ordinal)) continue;
            var instanceId = instance["id"]?.GetValue<string>() ?? "<unknown>";
            var values = instance["values"] as JsonObject;
            if (values is null) continue;
            foreach (var signal in request.BalanceProfile.Signals)
            {
                if (!values.TryGetPropertyValue(signal.PropertyId, out var value) || value is null) continue;
                if (!TryMeasure(value, signal.Measure, out var observed)) continue;
                var below = signal.MinimumReferenceValue is { } minimum && observed < minimum;
                var above = signal.MaximumReferenceValue is { } maximum && observed > maximum;
                if (below || above)
                    concerns.Add(new BalanceConcern(signal.Id, instanceId, signal.PropertyId, observed, signal.Message, signal.References));
            }
        }
        return new ContentEvaluationResult(violations, concerns);
    }

    private static bool TryMeasure(JsonNode value, BalanceMeasure measure, out decimal observed)
    {
        observed = 0;
        if (measure == BalanceMeasure.CollectionCount && value is JsonArray array) { observed = array.Count; return true; }
        if (measure == BalanceMeasure.NumericValue)
            return decimal.TryParse(value.ToJsonString(), NumberStyles.Number, CultureInfo.InvariantCulture, out observed);
        return false;
    }
}
