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

    private static readonly System.Text.RegularExpressions.Regex SteamLibraryPathRegex = new(
        "\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns <c>&lt;library&gt;\steamapps\common</c> for every library listed in
    /// <c>&lt;steamRoot&gt;\steamapps\libraryfolders.vdf</c> (lines like <c>"path"  "D:\\Games\\Steam"</c>).
    /// </summary>
    internal static IReadOnlyList<string> ReadSteamLibraryCommonFolders(string steamRoot)
    {
        string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        try
        {
            if (!File.Exists(vdfPath))
            {
                return [];
            }

            return ParseSteamLibraryCommonFolders(File.ReadAllText(vdfPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            UILogger.Debug(nameof(AnnoInstallationDetector), ex);
            return [];
        }
    }

    internal static IReadOnlyList<string> ParseSteamLibraryCommonFolders(string vdfText)
    {
        var result = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in SteamLibraryPathRegex.Matches(vdfText))
        {
            string libraryPath = match.Groups[1].Value.Replace("\\\\", "\\", StringComparison.Ordinal).Trim();
            if (libraryPath.Length > 0)
            {
                result.Add(Path.Combine(libraryPath, "steamapps", "common"));
            }
        }

        return result;
    }
}
