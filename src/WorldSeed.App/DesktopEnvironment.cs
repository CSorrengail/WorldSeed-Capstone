namespace WorldSeed.App;

/// <summary>Provides an opt-in, process-local data directory for isolated desktop runs and QA.</summary>
internal static class DesktopEnvironment
{
    public static string DataDirectory => Path.GetFullPath(
        Environment.GetEnvironmentVariable("WORLDSEED_DATA_DIRECTORY") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed"));

    public static Uri ApiBaseUri
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("WORLDSEED_API_BASE_URI");
            if (string.IsNullOrWhiteSpace(configured)) return new Uri("http://localhost:5080/");
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("WORLDSEED_API_BASE_URI must be an absolute HTTP or HTTPS URI.");
            return uri;
        }
    }
}
