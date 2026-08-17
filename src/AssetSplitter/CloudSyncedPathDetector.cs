namespace AssetProcessor;

/// <summary>
/// Detects output paths under common cloud-sync roots (OneDrive, Dropbox, Google Drive).
/// Parallel XML load/save on those paths triggers file locks and retry backoff.
/// </summary>
public static class CloudSyncedPathDetector
{
    private static readonly string[] FolderNameMarkers =
    [
        "OneDrive",
        "Dropbox",
        "Google Drive",
        "GoogleDrive",
        "iCloudDrive",
        "iCloud Drive"
    ];

    private static readonly string[] EnvironmentPathVariables =
    [
        "OneDrive",
        "OneDriveConsumer",
        "OneDriveCommercial",
        "DROPBOX",
        "GOOGLE_DRIVE"
    ];

    public static bool IsLikelyCloudSynced(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (ContainsCloudFolderMarker(fullPath))
        {
            return true;
        }

        foreach (string variableName in EnvironmentPathVariables)
        {
            string? root = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            try
            {
                string fullRoot = Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string prefix = fullRoot + Path.DirectorySeparatorChar;
                if (fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ignore invalid environment paths.
            }
        }

        return false;
    }

    private static bool ContainsCloudFolderMarker(string fullPath)
    {
        string[] segments = fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            foreach (string marker in FolderNameMarkers)
            {
                if (segment.Equals(marker, StringComparison.OrdinalIgnoreCase)
                    || segment.StartsWith(marker + " ", StringComparison.OrdinalIgnoreCase)
                    || segment.StartsWith(marker + "-", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
