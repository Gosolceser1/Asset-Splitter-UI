using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AssetSplitterUI.Services;

/// <summary>Detects installed Anno games from registry entries, common paths, and library folders.</summary>
public sealed partial class AnnoInstallationDetector
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
}
