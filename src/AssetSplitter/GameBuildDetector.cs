using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AssetProcessor;

public sealed record GameBuildInfo(
    string PatchVersion,
    string Branch,
    string Changelist,
    string BuildTime,
    string ExecutablePath)
{
    public string ToDisplayString()
    {
        string version = FormatPatchVersion(PatchVersion);
        string branch = string.IsNullOrWhiteSpace(Branch) ? "" : " " + Branch;
        string changelist = string.IsNullOrWhiteSpace(Changelist) ? "" : $" (CL {Changelist})";
        return (version + branch + changelist).Trim();
    }

    private static string FormatPatchVersion(string patchVersion)
    {
        if (string.IsNullOrWhiteSpace(patchVersion))
        {
            return "";
        }

        if (patchVersion.StartsWith('v') || patchVersion.StartsWith('V'))
        {
            return patchVersion;
        }

        // Only prefix digit versions (e.g. 1.6.1.1680013). Leave labels like "Steam buildid …" alone.
        return char.IsDigit(patchVersion[0]) ? "v" + patchVersion : patchVersion;
    }
}

public static class GameBuildDetector
{
    private const int ChunkSize = 4 * 1024 * 1024;
    private const int TailSize = 16 * 1024;

    private const string SteamAppIdAnno1800 = "916440";
    private const string SteamAppIdAnno117 = "3274580";

    private static readonly Regex BuildMetadataRegex = new(
        @"ChangeList:(?<cl>\d+);Version:(?<version>[^;\0]+);(?:User:[^;\0]*;)?Branch:(?<branch>[^;\0]+);Project Name:(?<project>[^;\0]+);Time:(?<time>[^;\0]+);",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VersionBranchRegex = new(
        // The trailing lookahead requires a terminator after the branch, so a match cut off at a
        // 4 MB scan-chunk boundary is not accepted half-read (the next chunk's overlap sees it whole).
        @"(?<![A-Za-z0-9])(?<version>\d+\.\d+\.\d+\.\d+(?:\.\d+)?)\s+(?<branch>//Anno\d/[A-Za-z0-9_/\.-]+)(?=[^A-Za-z0-9_/\.-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Anno 1800 embeds a Goldmaster build id as null-padded ASCII fragments near "Goldmaster" / "Anno7".
    /// </summary>
    private static readonly Regex GoldmasterVersionRegex = new(
        // Version must be followed by a non-digit (not ".<digit>"), see VersionBranchRegex.
        @"Goldmaster.{0,120}?(?<version>\d+\.\d+\.\d+\.\d+)(?!\d)(?!\.\d)(?=.)(?:\s+main)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex DocumentsBuildStringRegex = new(
        @"BuildString:.*?\[Build:\s*(?<time>[^\]]+)\].*?\bv?(?<version>\d+\.\d+\.\d+\.\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex SteamBuildIdRegex = new(
        @"""buildid""\s+""(?<id>\d+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static GameBuildInfo? TryDetect(string gamePath, string gameType = "")
    {
        string installRoot = NormalizeInstallRoot(gamePath);
        foreach (string executable in GetCandidateExecutables(installRoot, gameType))
        {
            GameBuildInfo? info = TryReadExecutableBuildInfo(executable);
            if (info is not null)
            {
                return info;
            }
        }

        GameBuildInfo? steamInfo = TryReadSteamBuildId(installRoot, gameType);
        if (steamInfo is not null)
        {
            return steamInfo;
        }

        return TryReadDocumentsLogBuild(installRoot, gameType);
    }

    private static IEnumerable<string> GetCandidateExecutables(string installRoot, string gameType)
    {
        if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
        {
            yield break;
        }

        bool preferAnno117 = GameTypeDetector.IsAnno117(gameType)
            || installRoot.Contains("Anno 117", StringComparison.OrdinalIgnoreCase)
            || installRoot.Contains("Pax Romana", StringComparison.OrdinalIgnoreCase);

        string binWin64 = Path.Combine(installRoot, "Bin", "Win64");
        string[] preferredNames = preferAnno117
            ? ["Anno117.exe", "Anno8.exe", "Anno1800.exe"]
            : ["Anno1800.exe", "Anno7.exe", "Anno117.exe", "Anno8.exe"];

        foreach (string name in preferredNames)
        {
            string path = Path.Combine(binWin64, name);
            if (File.Exists(path))
            {
                yield return path;
            }
        }

        if (!Directory.Exists(binWin64))
        {
            yield break;
        }

        IEnumerable<string> fallbackExecutables;
        try
        {
            fallbackExecutables = Directory.EnumerateFiles(binWin64, "Anno*.exe")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string path in fallbackExecutables)
        {
            if (!preferredNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static string NormalizeInstallRoot(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return "";
        }

        // Trim the trailing separator before walking up: Directory.GetParent("X\\maindata\\")
        // returns "X\\maindata", not "X".
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gamePath.Trim()));
        string leaf = Path.GetFileName(path);
        if (leaf.Equals("maindata", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(path)?.FullName ?? path;
        }

        if (leaf.Equals("Win64", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(Directory.GetParent(path)?.FullName ?? path)?.FullName ?? path;
        }

        return path;
    }

    private static GameBuildInfo? TryReadExecutableBuildInfo(string executablePath)
    {
        try
        {
            using FileStream stream = File.OpenRead(executablePath);
            byte[] buffer = new byte[ChunkSize];
            byte[] tail = [];
            GameBuildInfo? fallbackInfo = null;

            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                byte[] scanBytes = new byte[tail.Length + read];
                Buffer.BlockCopy(tail, 0, scanBytes, 0, tail.Length);
                Buffer.BlockCopy(buffer, 0, scanBytes, tail.Length, read);

                string scanText = Encoding.ASCII.GetString(scanBytes);
                if (TryParseBuildMetadata(scanText, executablePath, out GameBuildInfo? metadataInfo))
                {
                    return metadataInfo;
                }

                if (fallbackInfo is null && TryParseVersionBranch(scanText, executablePath, out GameBuildInfo? versionInfo))
                {
                    fallbackInfo = versionInfo;
                }

                if (fallbackInfo is null)
                {
                    string normalized = NormalizeBinaryScanText(scanText);
                    if (TryParseGoldmasterVersion(normalized, executablePath, out GameBuildInfo? goldmasterInfo))
                    {
                        fallbackInfo = goldmasterInfo;
                    }
                }

                int tailLength = Math.Min(TailSize, scanBytes.Length);
                tail = new byte[tailLength];
                Buffer.BlockCopy(scanBytes, scanBytes.Length - tailLength, tail, 0, tailLength);
            }

            if (fallbackInfo is not null)
            {
                return fallbackInfo;
            }

            return TryReadPeFileVersion(executablePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static string NormalizeBinaryScanText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        bool lastWasSpace = false;
        foreach (char c in text)
        {
            bool isPrintableAscii = c >= 0x20 && c <= 0x7E;
            if (!isPrintableAscii)
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            builder.Append(c);
            lastWasSpace = false;
        }

        return builder.ToString();
    }

    private static bool TryParseBuildMetadata(string text, string executablePath, out GameBuildInfo? info)
    {
        Match match = BuildMetadataRegex.Match(text);
        if (!match.Success)
        {
            info = null;
            return false;
        }

        string rawVersion = match.Groups["version"].Value.Trim();
        string branch = match.Groups["branch"].Value.Trim();
        string patchVersion = rawVersion;
        int branchSeparator = rawVersion.IndexOf("_//", StringComparison.Ordinal);
        if (branchSeparator >= 0)
        {
            patchVersion = rawVersion[..branchSeparator];
        }

        info = new GameBuildInfo(
            patchVersion,
            branch,
            match.Groups["cl"].Value.Trim(),
            match.Groups["time"].Value.Trim(),
            executablePath);
        return true;
    }

    private static bool TryParseVersionBranch(string text, string executablePath, out GameBuildInfo? info)
    {
        Match match = VersionBranchRegex.Match(text);
        if (!match.Success)
        {
            info = null;
            return false;
        }

        info = new GameBuildInfo(
            match.Groups["version"].Value.Trim(),
            match.Groups["branch"].Value.Trim(),
            "",
            "",
            executablePath);
        return true;
    }

    internal static bool TryParseGoldmasterVersion(string normalizedText, string executablePath, out GameBuildInfo? info)
    {
        Match match = GoldmasterVersionRegex.Match(normalizedText);
        if (!match.Success)
        {
            info = null;
            return false;
        }

        info = new GameBuildInfo(
            match.Groups["version"].Value.Trim(),
            "Goldmaster",
            "",
            "",
            executablePath);
        return true;
    }

    private static GameBuildInfo? TryReadPeFileVersion(string executablePath)
    {
        try
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            bool hasNumeric =
                versionInfo.FileMajorPart != 0
                || versionInfo.FileMinorPart != 0
                || versionInfo.FileBuildPart != 0
                || versionInfo.FilePrivatePart != 0;
            if (!hasNumeric)
            {
                return null;
            }

            string patchVersion =
                $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}";
            return new GameBuildInfo(patchVersion, "", "", "", executablePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static GameBuildInfo? TryReadSteamBuildId(string installRoot, string gameType = "")
    {
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            return null;
        }

        string? steamApps = FindSteamAppsDirectory(installRoot);
        if (steamApps is null)
        {
            return null;
        }

        foreach (string appId in GetSteamAppIds(installRoot, gameType))
        {
            string manifestPath = Path.Combine(steamApps, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                string text = File.ReadAllText(manifestPath);
                Match match = SteamBuildIdRegex.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                string buildId = match.Groups["id"].Value.Trim();
                if (buildId.Length == 0)
                {
                    continue;
                }

                return new GameBuildInfo(
                    "",
                    $"Steam buildid {buildId}",
                    "",
                    "",
                    manifestPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // try next app id
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSteamAppIds(string installRoot, string gameType)
    {
        bool preferAnno117 = GameTypeDetector.IsAnno117(gameType)
            || installRoot.Contains("Anno 117", StringComparison.OrdinalIgnoreCase)
            || installRoot.Contains("Pax Romana", StringComparison.OrdinalIgnoreCase);

        if (preferAnno117)
        {
            yield return SteamAppIdAnno117;
            yield return SteamAppIdAnno1800;
        }
        else
        {
            yield return SteamAppIdAnno1800;
            yield return SteamAppIdAnno117;
        }
    }

    private static string? FindSteamAppsDirectory(string installRoot)
    {
        try
        {
            DirectoryInfo? current = new(installRoot);
            while (current is not null)
            {
                if (current.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                    && current.Parent is { Name: var parentName }
                    && parentName.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                {
                    return current.Parent.FullName;
                }

                if (current.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        return null;
    }

    internal static GameBuildInfo? TryReadDocumentsLogBuild(string installRoot, string gameType = "")
    {
        bool preferAnno117 = GameTypeDetector.IsAnno117(gameType)
            || (!string.IsNullOrWhiteSpace(installRoot)
                && (installRoot.Contains("Anno 117", StringComparison.OrdinalIgnoreCase)
                    || installRoot.Contains("Pax Romana", StringComparison.OrdinalIgnoreCase)));

        if (preferAnno117)
        {
            // Anno 117 already exposes ChangeList metadata in the EXE; Documents logs are not the primary source.
            return null;
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            return null;
        }

        string logDirectory = Path.Combine(documents, "Anno 1800", "log");
        if (!Directory.Exists(logDirectory))
        {
            return null;
        }

        string? newestLog;
        try
        {
            newestLog = Directory.EnumerateFiles(logDirectory, "Anno1800_LOGFILE_*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (newestLog is null)
        {
            return null;
        }

        try
        {
            // BuildString is on the first line; cap read size so huge logs stay cheap.
            string head = ReadFileHead(newestLog, maxBytes: 4096);
            return TryParseDocumentsBuildString(head, newestLog);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static GameBuildInfo? TryParseDocumentsBuildString(string logText, string logPath)
    {
        Match match = DocumentsBuildStringRegex.Match(logText);
        if (!match.Success)
        {
            return null;
        }

        return new GameBuildInfo(
            match.Groups["version"].Value.Trim(),
            "Goldmaster",
            "",
            match.Groups["time"].Value.Trim(),
            logPath);
    }

    private static string ReadFileHead(string path, int maxBytes)
    {
        using FileStream stream = File.OpenRead(path);
        int toRead = (int)Math.Min(maxBytes, stream.Length);
        byte[] buffer = new byte[toRead];
        int read = stream.Read(buffer, 0, toRead);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}
