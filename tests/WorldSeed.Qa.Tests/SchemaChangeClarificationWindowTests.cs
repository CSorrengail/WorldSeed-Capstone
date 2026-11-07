using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Net;
using System.Reflection;
using WorldSeed.App;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.Qa.Tests;

public sealed class SchemaChangeClarificationWindowTests
{
    [AvaloniaFact]
    public void Dialog_displays_its_request_refuses_blank_answers_and_accepts_trimmed_answers()
    {
        var request = WaitingRequest();
        var dialog = new SchemaChangeClarificationWindow(request);
        try
        {
            dialog.Show();
            Assert.Equal(request.Title, Find<TextBlock>(dialog, "RequestTitleText").Text);
            Assert.Equal(request.PendingClarificationQuestion, Find<TextBlock>(dialog, "QuestionText").Text);
            Capture(dialog, "clarification-dialog");

            Click(dialog, FindButton(dialog, "Continue"));
            Assert.True(dialog.IsVisible);
            Assert.Null(dialog.Answer);

            Find<TextBox>(dialog, "AnswerInput").Text = "  Reach Is Optional.  ";
            Click(dialog, FindButton(dialog, "Continue"));
            Assert.False(dialog.IsVisible);
            Assert.Equal("Reach Is Optional.", dialog.Answer);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public async Task Resume_button_reopens_the_same_pending_question_after_cancel()
    {
        var request = WaitingRequest();
        var api = new HttpClient(new OfflineHandler()) { BaseAddress = new Uri("http://qa.invalid/") };
        var model = new HttpClient(new OfflineHandler());
        var window = new TestMainWindow(model, api);
        try
        {
            window.Show();
            var requests = (List<SchemaChangeRequest>)typeof(MainWindow)
                .GetField("_schemaChangeRequests", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window)!;
            requests.Add(request);
            typeof(MainWindow).GetMethod("RefreshSchemaChangeRequests", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);

            var selector = Find<ComboBox>(window, "ModelSelector");
            selector.ItemsSource = new[] { "qa-model" };
            selector.SelectedIndex = 0;
            var resume = Find<Button>(window, "ResumeSchemaChangeClarificationButton");
            Assert.True(resume.IsVisible);

            Click(window, resume);
            var first = await WaitForDialogAsync(window, 1);
            Assert.True(first.IsVisible);
            Assert.Equal(request.PendingClarificationQuestion, Find<TextBlock>(first, "QuestionText").Text);
            Click(first, FindButton(first, "Cancel"));
            await WaitUntilAsync(() => !first.IsVisible);
            Assert.Equal(SchemaChangeRequestStatus.NeedsClarification, request.Status);
            Assert.Equal("Is Reach optional?", request.PendingClarificationQuestion);

            Click(window, resume);
            var resumed = await WaitForDialogAsync(window, 2);
            Assert.Equal(request.Id, window.RequestIds[1]);
            Assert.Equal(request.Title, Find<TextBlock>(resumed, "RequestTitleText").Text);
            Assert.Equal(request.PendingClarificationQuestion, Find<TextBlock>(resumed, "QuestionText").Text);
            Click(resumed, FindButton(resumed, "Cancel"));
            await WaitUntilAsync(() => !resumed.IsVisible);
        }
        finally { window.Close(); }
    }

    private static SchemaChangeRequest WaitingRequest()
    {
        var created = SchemaChangeRequestService.Create("qa-project", "Add Weapon Reach", "Weapons may have reach.", SchemaChangeRequestOrigin.Designer, DateTimeOffset.UnixEpoch);
        return SchemaChangeRequestService.MarkNeedsClarification(created, "Is Reach optional?", DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    private static T Find<T>(Window window, string name) where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException($"Missing control: {name}");

    private static Button FindButton(Window window, string content) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => string.Equals(button.Content?.ToString(), content, StringComparison.Ordinal));

    private static void Click(Window window, Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<SchemaChangeClarificationWindow> WaitForDialogAsync(TestMainWindow window, int expectedCount)
    {
        await WaitUntilAsync(() => window.Dialogs.Count >= expectedCount && window.Dialogs[^1].IsVisible);
        return window.Dialogs[^1];
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.True(condition(), "Timed out while waiting for the dialog state.");
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("WORLDSEED_QA_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var image = window.CaptureRenderedFrame();
        Assert.NotNull(image);
        Assert.True(image!.PixelSize.Width > 100 && image.PixelSize.Height > 100);
        image.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    private sealed class TestMainWindow : MainWindow
    {
        public List<SchemaChangeClarificationWindow> Dialogs { get; } = [];
        public List<string> RequestIds { get; } = [];

        public TestMainWindow(HttpClient model, HttpClient api) : base(model, api) { }

        internal override SchemaChangeClarificationWindow CreateSchemaChangeClarificationWindow(SchemaChangeRequest request)
        {
            RequestIds.Add(request.Id);
            var dialog = new SchemaChangeClarificationWindow(request);
            Dialogs.Add(dialog);
            return dialog;
        }
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Headless dialog testing must not access a service: " + request.RequestUri);
    }
}
