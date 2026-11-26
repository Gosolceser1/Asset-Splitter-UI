using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AssetSplitterUI.Services;

public sealed partial class AnnoInstallationDetector
{
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

            string steamRoot = steamPath.Replace("/", "\\");
            var commonPaths = new List<string> { Path.Combine(steamRoot, "steamapps", "common") };
            // Additional Steam libraries (e.g. D:\Games\Steam) are only listed in libraryfolders.vdf.
            commonPaths.AddRange(ReadSteamLibraryCommonFolders(steamRoot));

            foreach (string commonPath in commonPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(commonPath))
                {
                    continue;
                }

                foreach (string directory in Directory.GetDirectories(commonPath, "Anno*"))
                {
                    TryAddInstallation(installations, directory);
                }
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
}
