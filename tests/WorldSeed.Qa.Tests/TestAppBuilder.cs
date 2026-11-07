using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(WorldSeed.Qa.Tests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WorldSeed.Qa.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORLDSEED_DATA_DIRECTORY")))
            Environment.SetEnvironmentVariable("WORLDSEED_DATA_DIRECTORY", Path.Combine(Path.GetTempPath(), "worldseed-qa", Guid.NewGuid().ToString("N")));

        return AppBuilder.Configure<WorldSeed.App.App>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
