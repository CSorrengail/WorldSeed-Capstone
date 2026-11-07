using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using WorldSeed.App;

namespace WorldSeed.Qa.Tests;

public sealed class DesktopShutdownTests
{
    [AvaloniaFact]
    public async Task Closing_returns_immediately_then_exits_after_async_housekeeping()
    {
        var handler = new EmptyProjectsHandler();
        using var model = new HttpClient(new OfflineHandler());
        using var api = new HttpClient(handler) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new MainWindow(model, api, initializeOnOpen: false, cleanUpOnClose: true);
        window.Show();

        var elapsed = Stopwatch.StartNew();
        window.Close();
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), "The first close request must not synchronously wait on HTTP housekeeping.");
        await WaitUntilAsync(() => !window.IsVisible);
        Assert.Equal(1, handler.ProjectListRequests);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.True(condition(), "The application window did not complete graceful shutdown.");
    }

    private sealed class EmptyProjectsHandler : HttpMessageHandler
    {
        public int ProjectListRequests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("http://qa.invalid/api/projects/", request.RequestUri?.ToString());
            ProjectListRequests++;
            await Task.Delay(75, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
        }
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Shutdown QA must not contact a model service.");
    }
}
