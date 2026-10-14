using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.DesignSessions;

namespace WorldSeed.App;

/// <summary>Desktop client for the local WorldSeed API. The API, rather than the UI, owns database access.</summary>
internal sealed class HttpDesignProjectStore(HttpClient http) : IDesignProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task SaveAsync(DesignProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(project.Id)}", project, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<DesignProject?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DesignProject>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<DesignProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _http.GetFromJsonAsync<List<DesignProjectSummary>>("api/projects/", JsonOptions, cancellationToken);
        return projects ?? [];
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync($"api/projects/{Uri.EscapeDataString(projectId)}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
