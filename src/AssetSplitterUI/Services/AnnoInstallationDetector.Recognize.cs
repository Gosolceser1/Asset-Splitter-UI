using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AssetSplitterUI.Services;

public sealed partial class AnnoInstallationDetector
{
    /// <summary>
    /// Returns a valid installation for <paramref name="path"/>, or scans immediate <c>Anno*</c> subfolders
    /// when the user picked a parent folder (e.g. Steam <c>steamapps\common</c>).
    /// </summary>
    public IReadOnlyList<GameInstallation> RecognizeFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        string trimmedPath = path.Trim();
        GameInstallation? direct = TryCreateInstallation(trimmedPath);
        if (direct is not null)
        {
            return [direct];
        }

        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(trimmedPath)).Equals("maindata", StringComparison.OrdinalIgnoreCase)
            && Directory.GetParent(trimmedPath)?.FullName is { } parentPath)
        {
            GameInstallation? parentInstall = TryCreateInstallation(parentPath);
            if (parentInstall is not null)
            {
                return [parentInstall];
            }
        }

        if (!Directory.Exists(path))
        {
            return [];
        }

        try
        {
            return
            [
                ..Directory.GetDirectories(path, "Anno*", SearchOption.TopDirectoryOnly)
                    .Select(TryCreateInstallation)
                    .Where(game => game is not null)
                    .Cast<GameInstallation>()
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UILogger.Debug(nameof(AnnoInstallationDetector), ex);
            return [];
        }
    }

    public static GameInstallation? TryCreateInstallation(string path)
    {
        // Registry InstallDir values often look like "C:/.../Anno 117 - Pax Romana - Demo/":
        // with the trailing separator Path.GetFileName returns "" and the Demo/117 name checks fail.
        path = NormalizeInstallationPath(path);
        if (!IsValidAnnoInstallation(path))
        {
            return null;
        }

        string gameType = DetectGameType(path);
        return new GameInstallation(path, gameType, GetDisplayName(path, gameType));
    }

    /// <summary>Adds a valid Anno installation to <paramref name="installations"/> when the path contains RDA files.</summary>
    private static void TryAddInstallation(List<GameInstallation> installations, string path)
    {
        GameInstallation? game = TryCreateInstallation(path);
        if (game is not null)
        {
            installations.Add(game);
        }
    }

    private static bool IsValidAnnoInstallation(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return false;
        }

        string maindataPath = Path.Combine(path, "maindata");
        if (!Directory.Exists(maindataPath))
        {
            return false;
        }

        try
        {
            return Directory.GetFiles(maindataPath, "*.rda").Length > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UILogger.Debug(nameof(AnnoInstallationDetector), ex);
            return false;
        }
    }

    private static string DetectGameType(string path)
    {
        string directoryName = Path.GetFileName(path) ?? "";

        if (directoryName.Contains("Anno 117", StringComparison.OrdinalIgnoreCase) ||
            directoryName.Contains("Pax Romana", StringComparison.OrdinalIgnoreCase))
        {
            return "anno117";
        }

        string maindataPath = Path.Combine(path, "maindata");
        if (Directory.Exists(maindataPath) &&
            (File.Exists(Path.Combine(maindataPath, "config.rda")) ||
             File.Exists(Path.Combine(maindataPath, "shared_configs.rda"))))
        {
            return "anno117";
        }

        return "anno1800";
    }

    /// <summary>Returns the localization key for the game display name (e.g. gameNames.anno1800).</summary>
    private static string GetDisplayName(string path, string gameType) =>
        (gameType, (Path.GetFileName(path) ?? "").Contains("Demo", StringComparison.OrdinalIgnoreCase)) switch
        {
            ("anno117", true) => "gameNames.anno117Demo",
            ("anno117", false) => "gameNames.anno117",
            _ => "gameNames.anno1800"
        };

    private static string NormalizeInstallationPath(string path)
    {
        try
        {
            return Path.GetFullPath(Path.TrimEndingDirectorySeparator(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}
