using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.WorkingRules;

namespace WorldSeed.App;

/// <summary>Desktop client for the one active project-level Working Rule Set.</summary>
internal sealed class HttpWorkingRuleSetStore(HttpClient http) : IWorkingRuleSetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task SaveAsync(WorkingRuleSet ruleSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(ruleSet.ProjectId)}/working-rules", ruleSet, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<WorkingRuleSet?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/working-rules", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkingRuleSet>(JsonOptions, cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync($"api/projects/{Uri.EscapeDataString(projectId)}/working-rules", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
