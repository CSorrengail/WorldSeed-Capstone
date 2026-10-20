using System.Net.Http.Json;
using System.Text.Json;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.App;

/// <summary>Desktop client for the one active, non-canonical schema proposal per project.</summary>
internal sealed class HttpProjectSchemaProposalStore(HttpClient http) : IProjectSchemaProposalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task SaveAsync(ProjectSchemaProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        using var response = await _http.PutAsJsonAsync($"api/projects/{Uri.EscapeDataString(proposal.ProjectId)}/schema-proposal", proposal, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ProjectSchemaProposal?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"api/projects/{Uri.EscapeDataString(projectId)}/schema-proposal", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectSchemaProposal>(JsonOptions, cancellationToken);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync($"api/projects/{Uri.EscapeDataString(projectId)}/schema-proposal", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"WorldSeed local service returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
