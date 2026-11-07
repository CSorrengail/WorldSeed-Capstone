namespace WorldSeed.App;

/// <summary>Provides an opt-in, process-local data directory for isolated desktop runs and QA.</summary>
internal static class DesktopEnvironment
{
    public static string DataDirectory => Path.GetFullPath(
        Environment.GetEnvironmentVariable("WORLDSEED_DATA_DIRECTORY") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed"));
}
