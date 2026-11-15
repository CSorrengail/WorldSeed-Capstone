using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.Content;

namespace WorldSeed.App;

internal sealed class HttpContentExpansionRunClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ContentExpansionRunWorkspaceItem>> ListAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/content-expansion-runs", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ContentExpansionRunWorkspaceItem>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ContentExpansionRun> CreateAsync(string projectId, ContentExpansionRunCreationRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/projects/{Uri.EscapeDataString(projectId)}/content-expansion-runs", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContentExpansionRun>(JsonOptions, cancellationToken)
            ?? throw new HttpRequestException("WorldSeed local service did not return the saved expansion run.");
    }

    public async Task<ContentExpansionProposalLink> LinkProposalAsync(string projectId, string runId, string stageId, string proposalId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/projects/{Uri.EscapeDataString(projectId)}/content-expansion-runs/{Uri.EscapeDataString(runId)}/stages/{Uri.EscapeDataString(stageId)}/proposals/{Uri.EscapeDataString(proposalId)}", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContentExpansionProposalLink>(JsonOptions, cancellationToken)
            ?? throw new HttpRequestException("WorldSeed local service did not return the expansion proposal link.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
