using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WorldSeed.App;

namespace WorldSeed.Qa.Tests;

public sealed class ContentReviewWorkspaceTests
{
    [AvaloniaFact]
    public void Content_review_workspace_is_present_and_refuses_generation_without_project_context()
    {
        using var model = new HttpClient(new OfflineHandler());
        using var api = new HttpClient(new OfflineHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var window = new TestMainWindow(model, api);
        try
        {
            Assert.NotNull(window.FindControl<ComboBox>("ContentProposalEntityTypeList"));
            Assert.NotNull(window.FindControl<TextBox>("ContentProposalRequestText"));
            Assert.NotNull(window.FindControl<TextBox>("ContentProposalEditor"));
            Assert.NotNull(window.FindControl<TextBox>("ContentBalanceProfileEditor"));
            Assert.NotNull(window.FindControl<ListBox>("ContentProposalList"));
            Assert.False(window.FindControl<Button>("GenerateContentProposalButton")!.IsEnabled);
            Assert.False(window.FindControl<Button>("SaveContentBalanceProfileButton")!.IsEnabled);
            Assert.False(window.FindControl<Button>("ApproveContentProposalButton")!.IsEnabled);
            Assert.Contains("Activate a schema", window.FindControl<TextBlock>("ContentReviewSchemaText")!.Text!, StringComparison.OrdinalIgnoreCase);
        }
        finally { window.Close(); }
    }

    private sealed class TestMainWindow(HttpClient model, HttpClient api) : MainWindow(model, api) { }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The content-review shell test must not contact a service: " + request.RequestUri);
    }
}
