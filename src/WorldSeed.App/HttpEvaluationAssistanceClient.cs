using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.Content;

namespace WorldSeed.App;

internal sealed class HttpEvaluationAssistanceClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EvaluationAssistanceWorkspace> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/evaluation-proposals", cancellationToken);
        await EnsureAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EvaluationAssistanceWorkspace>(JsonOptions, cancellationToken) ?? new([], []);
    }

    public async Task SaveProposalAsync(EvaluationAssistanceProposal proposal, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(proposal.ProjectId)}/evaluation-proposals/{Uri.EscapeDataString(proposal.Id)}", proposal, JsonOptions, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public async Task<EvaluationProposalDecision> DecideAsync(string projectId, string proposalId, EvaluationProposalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/projects/{Uri.EscapeDataString(projectId)}/evaluation-proposals/{Uri.EscapeDataString(proposalId)}/decisions", request, JsonOptions, cancellationToken);
        await EnsureAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EvaluationProposalDecision>(JsonOptions, cancellationToken)
            ?? throw new HttpRequestException("WorldSeed local service did not return the evaluation decision.");
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode);
    }
}
