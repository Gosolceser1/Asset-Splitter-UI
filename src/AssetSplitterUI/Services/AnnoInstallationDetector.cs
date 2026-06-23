using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AssetSplitterUI.Services;

/// <summary>Detects installed Anno games from registry entries, common paths, and library folders.</summary>
public sealed class AnnoInstallationDetector
{
    /// <summary>Scans supported installation sources and returns de-duplicated game installations.</summary>
    public List<GameInstallation> Detect()
    {
        List<GameInstallation> installations = [];

        if (OperatingSystem.IsWindows())
        {
            DetectFromWindowsRegistry(installations);
        }

        DetectFromCommonPaths(installations);
        DetectFromSteamLibrary(installations);

        return
        [
            ..installations
                .GroupBy(game => NormalizeInstallationPath(game.Path), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
        ];
    }

    [SupportedOSPlatform("windows")]
    private static void DetectFromWindowsRegistry(List<GameInstallation> installations)
    {
        foreach (RegistryView registryView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            TryRegistryBlock(() =>
            {
                using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
                using RegistryKey? ubisoftKey = localMachine.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");
                if (ubisoftKey is null)
                {
                    return;
                }

                foreach (string subKeyName in ubisoftKey.GetSubKeyNames())
                {
                    using RegistryKey? gameKey = ubisoftKey.OpenSubKey(subKeyName);
                    string? installDir = gameKey?.GetValue("InstallDir")?.ToString();
                    if (!string.IsNullOrEmpty(installDir))
                    {
                        TryAddInstallation(installations, installDir);
                    }
                }
            });
        }

        TryRegistryBlock(() =>
        {
            using RegistryKey? steamKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            string? steamPath = steamKey?.GetValue("SteamPath")?.ToString();
            if (string.IsNullOrEmpty(steamPath))
            {
                return;
            }

            string commonPath = Path.Combine(steamPath.Replace("/", "\\"), "steamapps", "common");
            if (!Directory.Exists(commonPath))
            {
                return;
            }

            foreach (string directory in Directory.GetDirectories(commonPath, "Anno*"))
            {
                TryAddInstallation(installations, directory);
            }
        });

        TryRegistryBlock(() =>
        {
            string manifestsPath = Path.Combine(
                Environment.ExpandEnvironmentVariables("%ProgramData%"),
                "Epic",
                "EpicGamesLauncher",
                "Data",
                "Manifests");

            if (!Directory.Exists(manifestsPath))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(manifestsPath, "*.item"))
            {
                TryAddEpicManifestInstallation(installations, file);
            }
        });
    }

    private static string[] WindowsDriveLetters => System.IO.DriveInfo.GetDrives().Where(d => d.DriveType == System.IO.DriveType.Fixed).Select(d => d.Name.TrimEnd('\\')).ToArray();
    private static readonly string[] AnnoGames = ["Anno 1800", "Anno 117 - Pax Romana", "Anno 117 - Pax Romana - Demo"];

    private static void DetectFromCommonPaths(List<GameInstallation> installations)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (IsWindows)
        {
            string[][] windowsPathPatterns =
            [
                [@"Program Files", "Ubisoft", "Ubisoft Game Launcher", "games"],
                [@"Program Files (x86)", "Ubisoft", "Ubisoft Game Launcher", "games"],
                [@"Program Files (x86)", "Steam", "steamapps", "common"],
                [@"Program Files", "Steam", "steamapps", "common"],
                ["Games"],
                ["SteamLibrary", "steamapps", "common"]
            ];

            foreach (string[] pattern in windowsPathPatterns)
            {
                foreach (string drive in WindowsDriveLetters)
                {
                    string baseDir = drive + "\\" + string.Join("\\", pattern);
                    foreach (string game in AnnoGames)
                    {
                        TryAddInstallation(installations, Path.Combine(baseDir, game));
                    }
                }
            }

            ScanDirectoriesForAnno(installations, [..WindowsDriveLetters.Select(d => d + @"\Program Files\Epic Games"),
                ..WindowsDriveLetters.Select(d => d + @"\Program Files (x86)\Epic Games"),
                ..WindowsDriveLetters.Skip(1).Select(d => d + @"\Epic Games"),
                ..WindowsDriveLetters.Skip(1).Select(d => d + @"\Program Files\Epic Games"),
                ..WindowsDriveLetters.Skip(1).Select(d => d + @"\Games\Epic")]);

            ScanDirectoriesForAnno(installations, [..WindowsDriveLetters.Select(d => d + @"\Program Files\Ubisoft\Ubisoft Game Launcher\games"),
                ..WindowsDriveLetters.Select(d => d + @"\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games")]);
        }
        else if (IsLinux)
        {
            string[][] linuxPaths = [["steam", "steamapps", "common"], ["local", "share", "Steam", "steamapps", "common"]];
            foreach (string[] sub in linuxPaths)
            {
                foreach (string game in AnnoGames.Take(2))
                {
                    TryAddInstallation(installations, Path.Combine(home, ".steam", Path.Combine(sub), game));
                }
            }
        }
        else if (IsMacOS)
        {
            foreach (string game in AnnoGames.Take(2))
            {
                TryAddInstallation(installations, Path.Combine(home, "Library/Application Support/Steam/steamapps/common", game));
            }
        }
    }

    private static void DetectFromSteamLibrary(List<GameInstallation> installations)
    {
        if (!IsWindows)
        {
            return;
        }

        ScanDirectoriesForAnno(installations, [..WindowsDriveLetters.Select(d => d + @"\Program Files (x86)\Steam\steamapps\common"),
            ..WindowsDriveLetters.Select(d => d + @"\Program Files\Steam\steamapps\common"),
            ..WindowsDriveLetters.Skip(1).Select(d => d + @"\SteamLibrary\steamapps\common")]);
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
        if (!IsValidAnnoInstallation(path))
        {
            return null;
        }

        string gameType = DetectGameType(path);
        return new GameInstallation(path, gameType, GetDisplayName(path, gameType));
    }

    /// <summary>Scans each directory root for <c>Anno*</c> subfolders and adds valid installations.</summary>
    private static void ScanDirectoriesForAnno(List<GameInstallation> installations, string[] roots)
    {
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (string directory in Directory.GetDirectories(root, "Anno*", SearchOption.TopDirectoryOnly))
                {
                    TryAddInstallation(installations, directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UILogger.Warning(nameof(AnnoInstallationDetector), "Failed to scan some directories during game detection");
                UILogger.Debug(nameof(AnnoInstallationDetector), ex);
            }
        }
    }

    private static void TryRegistryBlock(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            UILogger.Warning(nameof(AnnoInstallationDetector), "Failed to read registry during game detection");
            UILogger.Debug(nameof(AnnoInstallationDetector), ex);
        }
    }

    private static void TryAddEpicManifestInstallation(List<GameInstallation> installations, string manifestPath)
    {
        try
        {
            string content = File.ReadAllText(manifestPath);
            if (!content.Contains("Anno", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string? installPath = ParseEpicInstallLocation(content);
            if (!string.IsNullOrEmpty(installPath))
            {
                TryAddInstallation(installations, installPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UILogger.Warning(nameof(AnnoInstallationDetector), "Failed to read Epic manifest");
            UILogger.Debug(nameof(AnnoInstallationDetector), ex);
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

    /// <summary>Parses the <c>InstallLocation</c> value from an Epic Games <c>.item</c> manifest.</summary>
    private static string? ParseEpicInstallLocation(string content)
    {
        int startIndex = content.IndexOf("\"InstallLocation\"", StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return null;
        }

        int colonIndex = content.IndexOf(':', startIndex);
        int firstQuote = content.IndexOf('"', colonIndex + 1);
        int secondQuote = content.IndexOf('"', firstQuote + 1);

        if (firstQuote < 0 || secondQuote <= firstQuote)
        {
            return null;
        }

        return content[(firstQuote + 1)..secondQuote].Replace("\\\\", "\\");
    }

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
