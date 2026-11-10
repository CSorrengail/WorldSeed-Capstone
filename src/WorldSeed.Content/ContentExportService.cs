using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSeed.GameData;

namespace WorldSeed.Content;

/// <summary>Produces the MVP's portable JSON artifact only for an explicitly approved proposal.</summary>
public static class ContentExportService
{
    public static string ExportApproved(ContentProposal proposal, ContentProposalDecision decision, ActiveGameSchema schema)
    {
        ArgumentNullException.ThrowIfNull(proposal); ArgumentNullException.ThrowIfNull(decision); ArgumentNullException.ThrowIfNull(schema);
        if (decision.Kind != ContentProposalDecisionKind.Approved || decision.ProposalId != proposal.Id || decision.ProposalVersion != proposal.Version)
            throw new ArgumentException("Only the matching approved proposal version can be exported.");
        if (proposal.ActiveGameSchemaId != schema.Id || proposal.ActiveGameSchemaRevision != schema.Revision)
            throw new ArgumentException("The approved proposal does not match this active schema revision.");
        var gameSchema = JsonNode.Parse(schema.SchemaJson) as JsonObject ?? throw new GameDataFormatException("The active schema is not valid JSON.");
        return new JsonObject
        {
            ["content"] = proposal.Candidate.DeepClone(), ["gameSchemaId"] = gameSchema["id"]?.DeepClone(), ["gameSchemaVersion"] = gameSchema["version"]?.DeepClone(),
            ["workingRuleSetId"] = proposal.WorkingRuleSetId, ["workingRuleSetRevision"] = proposal.WorkingRuleSetRevision,
            ["proposalId"] = proposal.Id, ["proposalVersion"] = proposal.Version,
            ["designerRequest"] = proposal.DesignerRequest,
            ["schemaGapAnalysis"] = new JsonObject
            {
                ["reported"] = proposal.SchemaGaps is not null,
                ["gaps"] = proposal.SchemaGaps is null
                    ? new JsonArray()
                    : new JsonArray(proposal.SchemaGaps.Select(gap => (JsonNode)new JsonObject
                    {
                        ["requestedConcept"] = gap.RequestedConcept,
                        ["reason"] = gap.Reason,
                        ["suggestedSchemaChange"] = gap.SuggestedSchemaChange
                    }).ToArray())
            },
            ["designerDecision"] = new JsonObject { ["id"] = decision.Id, ["kind"] = decision.Kind.ToString(), ["createdAt"] = decision.CreatedAt }
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
