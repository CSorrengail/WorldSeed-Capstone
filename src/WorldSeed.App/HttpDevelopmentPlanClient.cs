using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.App;

internal sealed class HttpDevelopmentPlanClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DevelopmentPlanWorkspace> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/development-plans", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DevelopmentPlanWorkspace>(JsonOptions, cancellationToken) ?? new([], [], [], [], []);
    }

    public async Task SavePlanAsync(DevelopmentPlanProposal plan, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(plan.ProjectId)}/development-plans/{Uri.EscapeDataString(plan.Id)}", plan, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<DevelopmentPlanDecision> DecidePlanAsync(string projectId, string planId, DevelopmentPlanDecisionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"api/projects/{Uri.EscapeDataString(projectId)}/development-plans/{Uri.EscapeDataString(planId)}/decisions", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DevelopmentPlanDecision>(JsonOptions, cancellationToken))!;
    }

    public async Task<DevelopmentRecommendationDecision> DecideRecommendationAsync(string projectId, string planId, string recommendationId, DevelopmentRecommendationDecisionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"api/projects/{Uri.EscapeDataString(projectId)}/development-plans/{Uri.EscapeDataString(planId)}/recommendations/{Uri.EscapeDataString(recommendationId)}/decisions", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DevelopmentRecommendationDecision>(JsonOptions, cancellationToken))!;
    }

    public async Task<ExpansionRequest> StartExpansionAsync(string projectId, string planId, string recommendationId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"api/projects/{Uri.EscapeDataString(projectId)}/development-plans/{Uri.EscapeDataString(planId)}/recommendations/{Uri.EscapeDataString(recommendationId)}/expansion-request", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ExpansionRequest>(JsonOptions, cancellationToken))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode);
    }
}

internal sealed record DevelopmentPlanWorkspace(
    IReadOnlyList<DevelopmentPlanProposal> Plans,
    IReadOnlyList<DevelopmentPlanDecision> PlanDecisions,
    IReadOnlyList<DevelopmentRecommendationDecision> RecommendationDecisions,
    IReadOnlyList<ExpansionRequest> ExpansionRequests,
    IReadOnlyList<ExpansionRequestProgress> ExpansionProgress);
