using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.App;

/// <summary>Desktop client for durable, project-scoped schema-evolution requests.</summary>
internal sealed class HttpProjectSchemaChangeRequestStore(HttpClient http) : IProjectSchemaChangeRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task SaveAsync(SchemaChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(request.ProjectId)}/schema-change-requests/{Uri.EscapeDataString(request.Id)}", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<SchemaChangeRequest>> ListAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/schema-change-requests", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<SchemaChangeRequest>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync($"api/projects/{Uri.EscapeDataString(projectId)}/schema-change-requests", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
