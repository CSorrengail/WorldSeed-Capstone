using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;

namespace WorldSeed.LlmIntegration.Tests;

public class OpenAiCompatibleChatClientTests
{
    [Fact]
    public async Task Sends_a_safe_openai_compatible_request_and_reads_a_response()
    {
        var handler = new RecordingHandler("""{"model":"user-selected-model","choices":[{"message":{"content":"Proposed rule text."}}],"usage":{"prompt_tokens":12,"completion_tokens":7}}""");
        using var httpClient = new HttpClient(handler);
        var profile = new LlmModelProfile("primary", "Primary", new Uri("https://provider.example/v1"), "user-selected-model", LlmProviderKind.OpenAiCompatible, UseBearerAuthentication: true);
        var client = new OpenAiCompatibleChatClient(httpClient, profile, "secret-value");

        var response = await client.CompleteAsync(new LlmChatRequest([new(LlmMessageRole.System, "Be concise."), new(LlmMessageRole.User, "My idea")], Temperature: 0.2, MaxOutputTokens: 500));

        Assert.Equal("Proposed rule text.", response.Content);
        Assert.Equal("user-selected-model", response.Model);
        Assert.Equal(12, response.Usage!.InputTokens);
        Assert.Equal("https://provider.example/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-value", handler.Request.Headers.Authorization.Parameter);
        var body = JsonNode.Parse(handler.Body!)!.AsObject();
        Assert.Equal("user-selected-model", body["model"]!.GetValue<string>());
        Assert.Equal("user", body["messages"]![1]! ["role"]!.GetValue<string>());
    }

    [Fact]
    public async Task Supports_local_models_without_a_bearer_secret()
    {
        var handler = new RecordingHandler("""{"choices":[{"message":{"content":"Local response"}}]}""");
        using var httpClient = new HttpClient(handler);
        var profile = new LlmModelProfile("local", "Local", new Uri("http://localhost:1234/v1/"), "local-model", LlmProviderKind.OpenAiCompatible, UseBearerAuthentication: false);
        var client = new OpenAiCompatibleChatClient(httpClient, profile, null);

        var response = await client.CompleteAsync(new LlmChatRequest([new(LlmMessageRole.User, "Hello")]));

        Assert.Equal("Local response", response.Content);
        Assert.Null(handler.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task Does_not_include_provider_error_bodies_in_exceptions()
    {
        var handler = new RecordingHandler("sensitive provider detail", HttpStatusCode.Unauthorized);
        using var httpClient = new HttpClient(handler);
        var profile = new LlmModelProfile("primary", "Primary", new Uri("https://provider.example/v1/"), "model", LlmProviderKind.OpenAiCompatible, UseBearerAuthentication: true);
        var client = new OpenAiCompatibleChatClient(httpClient, profile, "secret-value");

        var exception = await Assert.ThrowsAsync<LlmClientException>(() => client.CompleteAsync(new LlmChatRequest([new(LlmMessageRole.User, "Hello")])));

        Assert.Equal(401, exception.StatusCode);
        Assert.DoesNotContain("sensitive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request; Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
