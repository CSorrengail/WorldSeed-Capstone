using System.Text.Json;

namespace WorldSeed.App;

/// <summary>Machine-local navigation preferences; never stored in a project file.</summary>
internal sealed record AppPreferences(string? LastProjectDirectory);

internal static class AppPreferencesStore
{
    private static readonly string PathValue = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed", "preferences.json");

    public static AppPreferences Load()
    {
        try { return File.Exists(PathValue) ? JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(PathValue)) ?? new(null) : new(null); }
        catch { return new(null); }
    }

    public static void Save(AppPreferences preferences)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(PathValue)!); File.WriteAllText(PathValue, JsonSerializer.Serialize(preferences)); }
        catch { /* Preferences must never block work. */ }
    }
}
