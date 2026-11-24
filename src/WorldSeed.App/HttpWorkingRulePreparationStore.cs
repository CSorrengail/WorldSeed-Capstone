using System.Net;
using System.Net.Http.Json;
using WorldSeed.WorkingRules;

namespace WorldSeed.App;

internal sealed class HttpWorkingRulePreparationStore(HttpClient http) : IWorkingRulePreparationStore
{
    private static string Path(string projectId) => $"api/projects/{Uri.EscapeDataString(projectId)}/working-rule-preparation";
    public async Task<WorkingRulePreparation?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(Path(projectId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkingRulePreparation>(cancellationToken: cancellationToken);
    }
    public async Task SaveAsync(WorkingRulePreparation preparation, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(Path(preparation.ProjectId), preparation, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(Path(projectId), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
    public async Task ApplyAsync(WorkingRuleSet rules, string fingerprint, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(Path(rules.ProjectId) + "/apply?fingerprint=" + Uri.EscapeDataString(fingerprint), rules, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Checkpoint Service Returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode);
    }
}
