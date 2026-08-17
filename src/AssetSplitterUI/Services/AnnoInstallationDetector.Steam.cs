using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AssetSplitterUI.Services;

public sealed partial class AnnoInstallationDetector
{
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
}
