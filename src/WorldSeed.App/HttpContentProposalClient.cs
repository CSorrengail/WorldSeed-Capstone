using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.Content;

namespace WorldSeed.App;

/// <summary>Desktop access to proposal artifacts that remain outside canonical game data.</summary>
internal sealed class HttpContentProposalClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<ContentProposalWorkspace> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/content-proposals", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContentProposalWorkspace>(JsonOptions, cancellationToken)
            ?? new ContentProposalWorkspace([], []);
    }

    public async Task SaveProposalAsync(ContentProposal proposal, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(proposal.ProjectId)}/content-proposals/{Uri.EscapeDataString(proposal.Id)}", proposal, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SaveDecisionAsync(ContentProposalDecision decision, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"api/projects/{Uri.EscapeDataString(decision.ProjectId)}/content-proposals/{Uri.EscapeDataString(decision.ProposalId)}/decisions", decision, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ContentApplication> ApplyApprovedAsync(string projectId, string proposalId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"api/projects/{Uri.EscapeDataString(projectId)}/content-proposals/{Uri.EscapeDataString(proposalId)}/apply", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContentApplication>(JsonOptions, cancellationToken)
            ?? throw new HttpRequestException("WorldSeed local service did not return the content application record.");
    }

    public async Task<IReadOnlyList<ProjectContentBalanceProfile>> LoadBalanceProfilesAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/content-balance-profiles", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProjectContentBalanceProfile>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task SaveBalanceProfileAsync(ProjectContentBalanceProfile profile, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(profile.ProjectId)}/content-balance-profiles/{Uri.EscapeDataString(profile.Id)}", profile, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}

internal sealed record ContentProposalWorkspace(
    IReadOnlyList<ContentProposal> Proposals,
    IReadOnlyList<ContentProposalDecision> Decisions,
    IReadOnlyList<ContentApplication>? Applications = null);
