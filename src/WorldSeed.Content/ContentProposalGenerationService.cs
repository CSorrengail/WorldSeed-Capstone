using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.Content;

public sealed record ContentProposalGenerationRequest(
    string ProjectId,
    string EntityTypeId,
    JsonObject ActiveSchema,
    IReadOnlyList<WorkingRule> Rules,
    string DesignerRequest);

public sealed record ContentProposalGenerationResult(JsonObject Candidate, string RawModelOutput);
public sealed class ContentProposalGenerationFormatException(string message) : Exception(message);

/// <summary>Turns one model response into a reviewable entity candidate; it never persists or applies it.</summary>
public sealed class ContentProposalGenerationService(ILanguageModelClient client)
{
    private readonly ILanguageModelClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<ContentProposalGenerationResult> GenerateAsync(ContentProposalGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId) || string.IsNullOrWhiteSpace(request.EntityTypeId) || string.IsNullOrWhiteSpace(request.DesignerRequest)) throw new ArgumentException("Project, entity type, and designer request are required.");
        if (request.Rules.Count == 0) throw new ArgumentException("At least one selected Working Rule is required.", nameof(request));
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, "You propose one TTRPG content entry. Return JSON only: {\"candidate\":{\"id\":\"...\",\"entityTypeId\":\"...\",\"values\":{...}}}. Use only the supplied schema and rules. Do not invent properties, change the entity type, or claim approval."),
            new LlmMessage(LlmMessageRole.System, "Required entityTypeId: " + request.EntityTypeId + "\nSchema: " + request.ActiveSchema.ToJsonString()),
            new LlmMessage(LlmMessageRole.System, "Working Rules:\n" + string.Join("\n", request.Rules.Select(rule => $"[{rule.Id}] {rule.AuthoritativeText}"))),
            new LlmMessage(LlmMessageRole.User, request.DesignerRequest)
        };
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.2, MaxOutputTokens: 1800, RequireJsonObject: true), cancellationToken);
        return new ContentProposalGenerationResult(ParseCandidate(response.Content, request.EntityTypeId), response.Content);
    }

    public static JsonObject ParseCandidate(string content, string entityTypeId)
    {
        try
        {
            var root = JsonNode.Parse(content) as JsonObject ?? throw new ContentProposalGenerationFormatException("The model did not return a JSON object.");
            var candidate = root["candidate"] as JsonObject ?? throw new ContentProposalGenerationFormatException("The model response requires a candidate object.");
            if (string.IsNullOrWhiteSpace(candidate["id"]?.GetValue<string>()) || candidate["values"] is not JsonObject) throw new ContentProposalGenerationFormatException("A candidate requires an id and values object.");
            if (!string.Equals(candidate["entityTypeId"]?.GetValue<string>(), entityTypeId, StringComparison.Ordinal)) throw new ContentProposalGenerationFormatException("The candidate entityTypeId does not match the requested type.");
            return (JsonObject)candidate.DeepClone();
        }
        catch (JsonException) { throw new ContentProposalGenerationFormatException("The model did not return valid JSON."); }
    }
}
