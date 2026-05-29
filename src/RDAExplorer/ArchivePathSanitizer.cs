namespace RDAExplorer;

public static class ArchivePathSanitizer
{
    private static readonly HashSet<string> ReservedWindowsFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool TryGetOutputPath(string fileName, string folder, out string outputPath)
    {
        outputPath = string.Empty;
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
            return false;

        if (!TryNormalizeRelativeArchivePath(fileName, out string relativePath))
            return false;

        try
        {
            string fullRoot = Path.GetFullPath(folder);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            string relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);

            if (relativeToRoot == "." ||
                relativeToRoot == ".." ||
                relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                relativeToRoot.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativeToRoot) ||
                PathContainsReparsePoint(fullRoot, fullPath))
            {
                return false;
            }

            outputPath = fullPath;
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (IOException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool TryNormalizeRelativeArchivePath(string fileName, out string relativePath)
    {
        relativePath = string.Empty;

        string normalized = fileName
          .Replace('/', Path.DirectorySeparatorChar)
          .Replace('\\', Path.DirectorySeparatorChar)
          .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string[] segments = normalized.Split(
          Path.DirectorySeparatorChar,
          StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return false;

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        foreach (string segment in segments)
        {
            if (segment is "." or ".." ||
                segment.IndexOfAny(invalidFileNameChars) >= 0 ||
                segment.Contains(':', StringComparison.Ordinal) ||
                segment.StartsWith(' ') ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                IsReservedWindowsFileName(segment))
            {
                return false;
            }
        }

        relativePath = Path.Combine(segments);
        return true;
    }

    public static bool PathContainsReparsePoint(string fullRoot, string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(fullRoot))
            return false;

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullRoot));
        string relativeDirectory = Path.GetRelativePath(root, directory);
        if (relativeDirectory == ".")
            return false;

        string current = root;
        string[] segments = relativeDirectory.Split(
          [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
          StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReservedWindowsFileName(string segment)
    {
        string baseName = segment.Split('.')[0];
        return ReservedWindowsFileNames.Contains(baseName);
    }
}
