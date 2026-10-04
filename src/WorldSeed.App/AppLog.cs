namespace WorldSeed.App;

/// <summary>Small local diagnostic log. Deliberately excludes source text, prompts, and model responses.</summary>
internal static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed", "logs");

    public static string CurrentPath => Path.Combine(DirectoryPath, "worldseed-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
    public static void Info(string message) => Write("INFO", message);
    public static void Error(string operation, Exception exception) => Write("ERROR", $"{operation}: {exception.GetType().Name}: {exception.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(CurrentPath, $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch { /* Logging must never crash the application. */ }
    }
}
