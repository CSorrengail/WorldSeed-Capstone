using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;

namespace WorldSeed.Publication;

public sealed record PublicationEditorialGenerationRequest(
    PublicationSnapshot Snapshot,
    string TargetSectionId,
    string? DesignerGuidance = null,
    PublicationEditorialProposal? Prior = null);

public sealed record PublicationEditorialGenerationResult(
    PublicationEditorialProposal Proposal,
    string RawModelOutput);

public sealed class PublicationEditorialGenerationFormatException(string message) : Exception(message);

public sealed class PublicationEditorialGenerationService(ILanguageModelClient client)
{
    public const int MaximumSourceUnits = 24;
    public const int MaximumMarkdownCharacters = 16000;
    private readonly ILanguageModelClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<PublicationEditorialGenerationResult> GenerateAsync(PublicationEditorialGenerationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = PublicationEditorialProposalService.DescribeTargets(request.Snapshot)
            .SingleOrDefault(candidate => string.Equals(candidate.Id, request.TargetSectionId, StringComparison.Ordinal))
            ?? throw new ArgumentException("The editorial target does not exist in this immutable publication snapshot.", nameof(request));
        if (target.SourceUnitIds.Count > MaximumSourceUnits)
            throw new InvalidOperationException($"This section has {target.SourceUnitIds.Count} source units. Split it before editorial generation; the bounded limit is {MaximumSourceUnits}.");
        if (request.Prior is not null && string.IsNullOrWhiteSpace(request.DesignerGuidance))
            throw new ArgumentException("A requested editorial revision needs designer guidance.", nameof(request));
        if (request.Prior is not null)
        {
            PublicationEditorialProposalService.Validate(request.Snapshot, request.Prior);
            if (!string.Equals(request.Prior.TargetSectionId, target.Id, StringComparison.Ordinal))
                throw new ArgumentException("The prior editorial proposal belongs to a different target section.", nameof(request));
        }

        var units = request.Snapshot.Units!.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
        var source = new StringBuilder();
        foreach (var id in target.SourceUnitIds)
        {
            var unit = units[id];
            source.AppendLine($"SOURCE UNIT {unit.Id} ({unit.Kind})").AppendLine($"TITLE: {unit.Title}").AppendLine(unit.MarkdownBody).AppendLine("END SOURCE UNIT").AppendLine();
        }
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, "You are proposing reader-facing Markdown for exactly one TTRPG publication section. Return one JSON object only: {\"proposedMarkdown\":\"...\"}. Faithfully reorganize and clarify only the supplied source units. Preserve all game terms, numbers, conditions, exceptions, and distinctions. Do not add mechanics, examples, lore, advice, IDs, provenance, or facts that are absent from the sources. Do not omit mechanics. This is an editorial proposal requiring designer approval, not canonical game content."),
            new LlmMessage(LlmMessageRole.System, $"Immutable snapshot: {request.Snapshot.Id}\nTarget section: {target.Id} ({target.Title})\nExact source units: {string.Join(", ", target.SourceUnitIds)}\n\n{source}"),
            new LlmMessage(LlmMessageRole.System, request.Prior is null ? "No prior editorial proposal exists." : $"Prior proposal revision {request.Prior.Revision}:\n{request.Prior.ProposedMarkdown}"),
            new LlmMessage(LlmMessageRole.User, string.IsNullOrWhiteSpace(request.DesignerGuidance) ? "Draft this section for a non-technical reader." : request.DesignerGuidance.Trim())
        };
        var response = await _client.CompleteAsync(new LlmChatRequest(messages, Temperature: 0.1, MaxOutputTokens: 2400, RequireJsonObject: true), cancellationToken);
        var markdown = Parse(response.Content);
        var proposal = PublicationEditorialProposalService.Create(request.Snapshot, target.Id, target.SourceUnitIds, markdown, DateTimeOffset.UtcNow, request.Prior);
        return new PublicationEditorialGenerationResult(proposal, response.Content);
    }

    public static string Parse(string content)
    {
        try
        {
            var root = JsonNode.Parse(content) as JsonObject ?? throw new PublicationEditorialGenerationFormatException("The model did not return a JSON object.");
            if (root.Count != 1 || root["proposedMarkdown"] is not JsonValue value || !value.TryGetValue<string>(out var markdown) || string.IsNullOrWhiteSpace(markdown))
                throw new PublicationEditorialGenerationFormatException("The model response must contain only a non-empty proposedMarkdown string.");
            markdown = markdown.Trim();
            if (markdown.Length > MaximumMarkdownCharacters) throw new PublicationEditorialGenerationFormatException($"The editorial proposal exceeds {MaximumMarkdownCharacters} characters.");
            return markdown;
        }
        catch (JsonException) { throw new PublicationEditorialGenerationFormatException("The model did not return valid JSON."); }
    }
}
