using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AssetSplitterUI.Services;

public sealed partial class AnnoInstallationDetector
{
    private static readonly string[] AnnoGames = ["Anno 1800", "Anno 117 - Pax Romana", "Anno 117 - Pax Romana - Demo"];

    private static string[] WindowsDriveLetters => System.IO.DriveInfo.GetDrives().Where(d => d.DriveType == System.IO.DriveType.Fixed).Select(d => d.Name.TrimEnd('\\')).ToArray();

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

            IEnumerable<string> candidatePaths =
                from pattern in windowsPathPatterns
                from drive in WindowsDriveLetters
                let baseDir = drive + "\\" + string.Join("\\", pattern)
                from game in AnnoGames
                select Path.Combine(baseDir, game);

            foreach (string candidatePath in candidatePaths)
            {
                TryAddInstallation(installations, candidatePath);
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
            // ~/.steam/steam is usually a symlink; the native install lives in ~/.local/share/Steam.
            string[] steamRoots = [Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam")];
            foreach (string steamRoot in steamRoots)
            {
                var commonPaths = new List<string> { Path.Combine(steamRoot, "steamapps", "common") };
                commonPaths.AddRange(ReadSteamLibraryCommonFolders(steamRoot));
                foreach (string commonPath in commonPaths)
                {
                    foreach (string game in AnnoGames.Take(2))
                    {
                        TryAddInstallation(installations, Path.Combine(commonPath, game));
                    }
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
}
