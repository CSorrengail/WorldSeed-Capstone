using System.Text.Json;

namespace WorldSeed.App;

/// <summary>Small, local-only desktop preferences. These settings never enter a game project or the Docker database.</summary>
internal sealed record WorldSeedSettings(int IdleTimeoutMinutes)
{
    public const int DefaultIdleTimeoutMinutes = 15;
    public const int MaximumIdleTimeoutMinutes = 720;
    public static WorldSeedSettings Default { get; } = new(DefaultIdleTimeoutMinutes);
}

internal sealed class WorldSeedSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldSeed", "settings.json");

    public WorldSeedSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return WorldSeedSettings.Default;
            var settings = JsonSerializer.Deserialize<WorldSeedSettings>(File.ReadAllText(SettingsPath));
            return settings is { IdleTimeoutMinutes: >= 0 and <= WorldSeedSettings.MaximumIdleTimeoutMinutes }
                ? settings
                : WorldSeedSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            AppLog.Error("Load desktop settings", ex);
            return WorldSeedSettings.Default;
        }
    }

    public void Save(WorldSeedSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid(settings)) throw new ArgumentOutOfRangeException(nameof(settings), $"Idle timeout must be between 0 and {WorldSeedSettings.MaximumIdleTimeoutMinutes} minutes.");
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool IsValid(WorldSeedSettings? settings) => settings is { IdleTimeoutMinutes: >= 0 and <= WorldSeedSettings.MaximumIdleTimeoutMinutes };
}
