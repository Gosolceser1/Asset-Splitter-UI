using System.Text.Json;

namespace AssetSplitterUI.Services;

/// <summary>Loads and saves UI settings JSON from the app data directory.</summary>
public sealed class AppSettingsStore(IPlatformServices platformServices)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private string SettingsFilePath => Path.Combine(platformServices.GetAppDataPath(), "settings.json");

    /// <summary>Loads settings from disk, returning <see langword="null"/> when the file is missing or unreadable.</summary>
    public PersistedAppSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return null;
            }

            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<PersistedAppSettings>(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            UILogger.Warning(nameof(AppSettingsStore), "Failed to load settings");
            UILogger.Debug(nameof(AppSettingsStore), ex);
            return null;
        }
    }

    /// <summary>Saves settings to disk atomically (write to temp file, then replace).</summary>
    public void Save(PersistedAppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath) ?? ".");
            string tempPath = SettingsFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SettingsFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            UILogger.Warning(nameof(AppSettingsStore), "Failed to save settings");
            UILogger.Debug(nameof(AppSettingsStore), ex);
        }
    }
}
