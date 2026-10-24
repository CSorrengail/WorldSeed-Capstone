using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.GameData;

namespace WorldSeed.App;

/// <summary>Desktop access to the API-owned active schema and game-data workspace.</summary>
internal sealed class HttpGameDataWorkspaceClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<ActiveGameSchema?> LoadActiveSchemaAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/active-schema", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ActiveGameSchema>(JsonOptions, cancellationToken);
    }

    public async Task<ActiveGameSchema> ActivateAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"api/projects/{Uri.EscapeDataString(projectId)}/active-schema", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ActiveGameSchema>(JsonOptions, cancellationToken))
            ?? throw new HttpRequestException("WorldSeed local service did not return the activated schema.");
    }

    public async Task<ProjectGameData?> LoadGameDataAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/game-data", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectGameData>(JsonOptions, cancellationToken);
    }

    public async Task SaveGameDataAsync(ProjectGameData gameData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(gameData.ProjectId)}/game-data", gameData, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
