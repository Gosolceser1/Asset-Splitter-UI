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

    private const int LoadAttempts = 3;
    private const int LoadRetryDelayMs = 150;

    /// <summary>
    /// True when settings.json exists but could not be read (locked by antivirus/backup/sync, access denied).
    /// While set, <see cref="Save"/> refuses to overwrite the file so a transient read failure cannot wipe
    /// the user's saved paths with first-run defaults.
    /// </summary>
    private bool _existingFileUnreadable;

    /// <summary>Loads settings from disk, returning <see langword="null"/> when the file is missing or unreadable.</summary>
    public PersistedAppSettings? Load()
    {
        _existingFileUnreadable = false;
        for (int attempt = 1; ; attempt++)
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < LoadAttempts)
                {
                    Thread.Sleep(LoadRetryDelayMs * attempt);
                    continue;
                }

                // File is present but locked/inaccessible: keep it intact for the next launch.
                _existingFileUnreadable = File.Exists(SettingsFilePath);
                UILogger.Warning(nameof(AppSettingsStore), "Failed to load settings");
                UILogger.Debug(nameof(AppSettingsStore), ex);
                return null;
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
            {
                // Corrupt content: keep a copy before defaults are written over it.
                TryBackupCorruptFile();
                UILogger.Warning(nameof(AppSettingsStore), "Failed to load settings");
                UILogger.Debug(nameof(AppSettingsStore), ex);
                return null;
            }
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            File.Copy(SettingsFilePath, SettingsFilePath + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            UILogger.Debug(nameof(AppSettingsStore), ex);
        }
    }

    /// <summary>Saves settings to disk atomically (write to temp file, then replace).</summary>
    public void Save(PersistedAppSettings settings)
    {
        if (_existingFileUnreadable)
        {
            UILogger.Warning(nameof(AppSettingsStore), "Skipping settings save: existing settings file could not be read this session");
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath) ?? ".");
            string tempPath = SettingsFilePath + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(json);
                writer.Flush();
                // Make sure the bytes are on disk before the rename so a power cut cannot leave an empty file.
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, SettingsFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            UILogger.Warning(nameof(AppSettingsStore), "Failed to save settings");
            UILogger.Debug(nameof(AppSettingsStore), ex);
        }
    }
}
