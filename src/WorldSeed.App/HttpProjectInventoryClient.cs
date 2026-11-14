using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.ProjectPlanning;

namespace WorldSeed.App;

internal sealed class HttpProjectInventoryClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<IReadOnlyList<ProjectInventory>> ListAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/inventories", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProjectInventory>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ProjectInventory> CreateAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"api/projects/{Uri.EscapeDataString(projectId)}/inventories", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectInventory>(JsonOptions, cancellationToken)
            ?? throw new HttpRequestException("WorldSeed local service did not return the created inventory.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
