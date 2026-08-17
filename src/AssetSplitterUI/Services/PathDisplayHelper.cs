using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace AssetSplitterUI.Services;

internal static class PathDisplayHelper
{
    private const int MaxRecentPathCount = 10;

    public static string GetPathWithActualCasing(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return NormalizeDriveLetter(path);
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return NormalizeDriveLetter(path);
        }

        // UNC paths (\\server\share) and paths containing wildcard characters (*, ?)
        // cannot be safely resolved via filesystem enumeration; skip normalization.
        if (IsUncPath(path) || ContainsWildcard(path))
        {
            return NormalizeDriveLetter(path);
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            if (IsUncPath(fullPath))
            {
                return NormalizeDriveLetter(path);
            }

            char separator = Path.DirectorySeparatorChar;
            string[] parts = fullPath.Split(separator, Path.AltDirectorySeparatorChar);
            if (parts.Length == 0)
            {
                return path;
            }

            string current = ResolveDriveRoot(parts[0], separator);
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string[] entries = Directory.GetFileSystemEntries(current, parts[i]);
                if (entries.Length == 0)
                {
                    return NormalizeDriveLetter(path);
                }

                current = entries[0];
            }

            return current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return NormalizeDriveLetter(path);
        }
    }

    public static void AddToRecentPaths(ObservableCollection<string> collection, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string? existing = collection.FirstOrDefault(candidate => PathsEqual(candidate, path));
        if (existing is not null)
        {
            collection.Remove(existing);
        }

        collection.Insert(0, path);

        while (collection.Count > MaxRecentPathCount)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            NormalizeForComparison(left),
            NormalizeForComparison(right),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static string NormalizeDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 2)
        {
            return path ?? "";
        }

        return path[1] == ':' && char.IsLetter(path[0])
          ? char.ToUpperInvariant(path[0]) + path[1..]
          : path;
    }

    private static string NormalizeForComparison(string path)
    {
        try
        {
            path = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            path = path.Trim();
        }

        return Path.TrimEndingDirectorySeparator(NormalizeDriveLetter(path));
    }

    private static string ResolveDriveRoot(string firstPart, char separator)
    {
        if (firstPart.Length != 2 || firstPart[1] != ':')
        {
            return firstPart + separator;
        }

        return DriveInfo.GetDrives()
          .FirstOrDefault(drive => drive.Name.Equals(firstPart + separator, StringComparison.OrdinalIgnoreCase))
          ?.Name ?? firstPart + separator;
    }

    private static bool IsUncPath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    private static bool ContainsWildcard(string path) =>
        path.Contains('*') || path.Contains('?');
}
