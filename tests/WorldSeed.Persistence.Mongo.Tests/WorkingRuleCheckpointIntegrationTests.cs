using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using MongoDB.Driver;
using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.Persistence.Mongo;
using WorldSeed.RuleDrafting;
using WorldSeed.WorkingRules;

namespace WorldSeed.Persistence.Mongo.Tests;

public sealed class WorkingRuleCheckpointIntegrationTests
{
    private sealed class IsolatedCheckpointFactAttribute : FactAttribute
    {
        public IsolatedCheckpointFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("WORLDSEED_QA_MONGO_URI") is null || Environment.GetEnvironmentVariable("WORLDSEED_QA_API_DLL") is null)
                Skip = "Requires explicitly isolated Mongo URI and the built development API DLL.";
        }
    }

    [IsolatedCheckpointFact]
    public async Task Mongo_checkpoint_survives_interruption_and_API_blocks_partial_or_stale_promotion()
    {
        var uri = Environment.GetEnvironmentVariable("WORLDSEED_QA_MONGO_URI")!;
        var dll = Environment.GetEnvironmentVariable("WORLDSEED_QA_API_DLL")!;
        Assert.StartsWith("mongodb://127.0.0.1:", uri);
        Assert.Equal("WorldSeed.Api.dll", Path.GetFileName(dll));
        var client = new MongoClient(uri);
        var databaseName = "worldseed_qa_checkpoint_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);
        Process? api = null;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var source = new DesignSourceNote("source", "Promises connect participants. Record each promise.", now);
            var rules = new[]
            {
                new RuleStatement("draft1", "Promise", RuleStatementKind.Rule, "Promises connect participants.", [source.Id], [new(source.Id, "Promises connect participants.", "source:p001")]),
                new RuleStatement("draft2", "Record", RuleStatementKind.Rule, "Record each promise.", [source.Id], [new(source.Id, "Record each promise.", "source:p002")])
            };
            var draft = new StructuredRuleDraft("Promises", "Organize promises.", rules, [], [], [], []);
            var project = DesignProjectService.Create("qa-checkpoint", "Checkpoint") with
            { SourceNotes = [source], Sessions = [new("session", "qa-checkpoint", "local", now, now, [source], [], new(RuleDraftAction.PresentDraft, null, [], draft))] };
            var projects = new MongoDesignProjectStore(database);
            await projects.SaveAsync(project);
            var store = new MongoWorkingRulePreparationStore(database);
            await Assert.ThrowsAsync<OperationCanceledException>(() => new WorkingRuleSetFormalizationService(new Model("d001", interruptAfterFirst: true), preparations: store).CreateProposalAsync(project));
            var persisted = (await new MongoWorkingRulePreparationStore(database).LoadAsync(project.Id))!;
            Assert.Single(persisted.Batches);
            var restoredProject = (await projects.LoadAsync(project.Id))!;
            WorkingRulePreparationBinding.Validate(persisted, restoredProject, null, false);

            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            Assert.NotEqual(5080, port);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.GetFullPath(dll));
            start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
            start.Environment["ConnectionStrings__WorldSeed"] = uri;
            start.Environment["WorldSeed__DatabaseName"] = databaseName;
            api = Process.Start(start)!;
            var stdout = api.StandardOutput.ReadToEndAsync(); var stderr = api.StandardError.ReadToEndAsync();
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(3) };
            var watch = Stopwatch.StartNew(); var ready = false;
            while (watch.Elapsed < TimeSpan.FromSeconds(20) && !api.HasExited)
            {
                try { using var health = await http.GetAsync("health"); if (health.IsSuccessStatusCode) { ready = true; break; } }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(100);
            }
            Assert.True(ready, api.HasExited ? await stderr : "Isolated API startup timed out.");
            var path = $"api/projects/{project.Id}/working-rule-preparation";
            var loaded = await http.GetFromJsonAsync<WorkingRulePreparation>(path);
            Assert.Equal(persisted.InputFingerprint, loaded!.InputFingerprint);
            using (var put = await http.PutAsJsonAsync(path, loaded)) Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
            using (var partial = await http.PostAsJsonAsync(path + "/apply?fingerprint=" + loaded.InputFingerprint, loaded.Batches[0].RuleSet)) Assert.Equal(HttpStatusCode.BadRequest, partial.StatusCode);
            Assert.Null(await new MongoWorkingRuleSetStore(database).LoadAsync(project.Id));

            var model = new Model("d002");
            var result = await new WorkingRuleSetFormalizationService(model, preparations: new MongoWorkingRulePreparationStore(database)).CreateProposalAsync(restoredProject);
            Assert.Equal(1, model.Calls);
            Assert.Equal(2, result.RuleSet.Rules.Count);
            await projects.SaveAsync(restoredProject with { Name = "Changed" });
            using (var stale = await http.PostAsJsonAsync(path + "/apply?fingerprint=" + loaded.InputFingerprint, result.RuleSet)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Null(await new MongoWorkingRuleSetStore(database).LoadAsync(project.Id));
            await projects.SaveAsync(restoredProject);
            var altered = result.RuleSet with { Rules = result.RuleSet.Rules.Select(rule => rule with { AuthoritativeText = "Changed after checkpoint." }).ToArray() };
            using (var mismatch = await http.PostAsJsonAsync(path + "/apply?fingerprint=" + loaded.InputFingerprint, altered)) Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
            Assert.Null(await new MongoWorkingRuleSetStore(database).LoadAsync(project.Id));
            using (var apply = await http.PostAsJsonAsync(path + "/apply?fingerprint=" + loaded.InputFingerprint, result.RuleSet)) Assert.Equal(HttpStatusCode.NoContent, apply.StatusCode);
            Assert.Equal(2, (await new MongoWorkingRuleSetStore(database).LoadAsync(project.Id))!.Rules.Count);
            using (var delete = await http.DeleteAsync(path)) Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            using (var missing = await http.GetAsync(path)) Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(2, (await new MongoWorkingRuleSetStore(database).LoadAsync(project.Id))!.Rules.Count);
        }
        finally
        {
            if (api is { HasExited: false }) { api.Kill(entireProcessTree: true); await api.WaitForExitAsync(); }
            api?.Dispose();
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private sealed class Model(string citation, bool interruptAfterFirst = false) : ILanguageModelClient
    {
        public int Calls { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            if (++Calls > 1 && interruptAfterFirst) throw new OperationCanceledException("Simulated interruption.");
            var text = citation == "d001" ? "Promises connect participants." : "Record each promise.";
            var response = System.Text.Json.JsonSerializer.Serialize(new { name = "Rules", rules = new[] { new { id = "rule", name = "Promise", kind = "rule", authoritativeText = text, sourceDraftIds = new[] { citation }, conceptIds = Array.Empty<string>(), schemaHints = Array.Empty<string>(), openQuestions = Array.Empty<string>(), exclusions = Array.Empty<string>() } }, concepts = Array.Empty<string>(), openQuestions = Array.Empty<string>(), exclusions = Array.Empty<string>(), conflicts = Array.Empty<string>() });
            return Task.FromResult(new LlmChatResponse(response, "fake", null));
        }
    }
}
