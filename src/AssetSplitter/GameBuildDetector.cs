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
        string version = string.IsNullOrWhiteSpace(PatchVersion)
            ? ""
            : PatchVersion.StartsWith('v') ? PatchVersion : "v" + PatchVersion;
        string branch = string.IsNullOrWhiteSpace(Branch) ? "" : " " + Branch;
        string changelist = string.IsNullOrWhiteSpace(Changelist) ? "" : $" (CL {Changelist})";
        return (version + branch + changelist).Trim();
    }
}

public static class GameBuildDetector
{
    private const int ChunkSize = 4 * 1024 * 1024;
    private const int TailSize = 16 * 1024;

    private static readonly Regex BuildMetadataRegex = new(
        @"ChangeList:(?<cl>\d+);Version:(?<version>[^;\0]+);(?:User:[^;\0]*;)?Branch:(?<branch>[^;\0]+);Project Name:(?<project>[^;\0]+);Time:(?<time>[^;\0]+);",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VersionBranchRegex = new(
        @"(?<![A-Za-z0-9])(?<version>\d+\.\d+\.\d+\.\d+(?:\.\d+)?)\s+(?<branch>//Anno\d/[A-Za-z0-9_/\.-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static GameBuildInfo? TryDetect(string gamePath, string gameType = "")
    {
        foreach (string executable in GetCandidateExecutables(gamePath, gameType))
        {
            GameBuildInfo? info = TryReadExecutableBuildInfo(executable);
            if (info is not null)
            {
                return info;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateExecutables(string gamePath, string gameType)
    {
        string installRoot = NormalizeInstallRoot(gamePath);
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

        string path = Path.GetFullPath(gamePath.Trim());
        string leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
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
                    return fallbackInfo;
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

                int tailLength = Math.Min(TailSize, scanBytes.Length);
                tail = new byte[tailLength];
                Buffer.BlockCopy(scanBytes, scanBytes.Length - tailLength, tail, 0, tailLength);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
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
}
