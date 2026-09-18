using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using WorldSeed.LlmIntegration;

namespace WorldSeed.LlmIntegration.Tests;

public class OllamaIntegrationTests
{
    [Fact]
    public async Task Sends_a_native_local_ollama_chat_request_and_reads_response()
    {
        var handler = new RecordingHandler("""{"model":"qwen3:8b","message":{"role":"assistant","content":"A structured answer."},"prompt_eval_count":9,"eval_count":5}""");
        using var httpClient = new HttpClient(handler);
        var client = new OllamaChatClient(httpClient, OllamaDefaults.CreateProfile("local", "My local model", "qwen3:8b"));

        var response = await client.CompleteAsync(new LlmChatRequest([new(LlmMessageRole.System, "Use JSON."), new(LlmMessageRole.User, "An idea")], Temperature: 0.1, MaxOutputTokens: 400));

        Assert.Equal("A structured answer.", response.Content);
        Assert.Equal("qwen3:8b", response.Model);
        Assert.Equal(9, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal("http://127.0.0.1:11434/api/chat", handler.Request!.RequestUri!.ToString());
        Assert.Null(handler.Request.Headers.Authorization);
        var body = JsonNode.Parse(handler.Body!)!.AsObject();
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.Equal("user", body["messages"]![1]!["role"]!.GetValue<string>());
        Assert.Equal(0.1, body["options"]!["temperature"]!.GetValue<double>());
        Assert.Equal(400, body["options"]!["num_predict"]!.GetValue<int>());
    }

    [Fact]
    public async Task Lists_installed_models_without_mutating_the_local_library()
    {
        var handler = new RecordingHandler("""{"models":[{"name":"qwen3:8b","size":5200000000,"modified_at":"2026-08-12T10:00:00Z"},{"name":"llama3.2:3b","size":2000000000}]}""");
        using var httpClient = new HttpClient(handler);
        var catalog = new OllamaModelCatalog(httpClient);

        var models = await catalog.GetInstalledModelsAsync();

        Assert.Collection(models,
            model => { Assert.Equal("llama3.2:3b", model.Name); Assert.Equal(2000000000, model.SizeBytes); },
            model => { Assert.Equal("qwen3:8b", model.Name); Assert.Equal(5200000000, model.SizeBytes); Assert.NotNull(model.ModifiedAt); });
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("http://127.0.0.1:11434/api/tags", handler.Request.RequestUri!.ToString());
    }

    private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
