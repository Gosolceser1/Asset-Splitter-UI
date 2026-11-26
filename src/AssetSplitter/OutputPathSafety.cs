namespace AssetProcessor;

/// <summary>Containment and segment checks for pipeline output paths (deletes, clears, writes).</summary>
public static class OutputPathSafety
{
    public static bool IsPathUnderDirectory(string filePath, string directoryPath)
    {
        string fullFile = Path.GetFullPath(filePath);
        string fullDir = Path.GetFullPath(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        return fullFile.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSafeSinglePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        if (segment is "." or "..")
        {
            return false;
        }

        if (segment.Contains('/', StringComparison.Ordinal)
            || segment.Contains('\\', StringComparison.Ordinal)
            || segment.Contains(Path.DirectorySeparatorChar)
            || segment.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        return segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    public static string SanitizeSinglePathSegment(string segment, string fallback)
    {
        if (string.IsNullOrWhiteSpace(fallback))
        {
            throw new ArgumentException("Fallback path segment must be non-empty.", nameof(fallback));
        }

        if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
        {
            return fallback;
        }

        if (IsSafeSinglePathSegment(segment))
        {
            return segment;
        }

        // Path separators or ".." mean this is not a folder name — never invent a "safe" traversal string.
        if (segment.Contains('/', StringComparison.Ordinal)
            || segment.Contains('\\', StringComparison.Ordinal)
            || segment.Contains("..", StringComparison.Ordinal)
            || segment.Contains(Path.DirectorySeparatorChar)
            || segment.Contains(Path.AltDirectorySeparatorChar))
        {
            return fallback;
        }

        string sanitized = AssetTextSanitizer.SanitizeFileNamePart(segment.Trim());
        if (sanitized is "." or ".." || !IsSafeSinglePathSegment(sanitized))
        {
            return fallback;
        }

        return sanitized;
    }

    public static bool TryGetContainedPath(string rootDirectory, string relativeOrCombined, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativeOrCombined))
        {
            return false;
        }

        try
        {
            string fullRoot = Path.GetFullPath(rootDirectory);
            string candidate = Path.IsPathRooted(relativeOrCombined)
                ? Path.GetFullPath(relativeOrCombined)
                : Path.GetFullPath(Path.Combine(fullRoot, relativeOrCombined));

            if (!IsPathUnderDirectory(candidate, fullRoot))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// True if <paramref name="directoryPath"/> or any ancestor up to the volume root is a reparse point
    /// (junction/symlink). Uses the same segment walk as <see cref="RDAExplorer.ArchivePathSanitizer.PathContainsReparsePoint"/>.
    /// </summary>
    public static bool DirectoryOrAncestorsHaveReparsePoint(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(directoryPath);
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            // Probe as if fullPath were a file under the directory so the walk includes the directory itself.
            string probePath = Path.Combine(fullPath, "_");
            return RDAExplorer.ArchivePathSanitizer.PathContainsReparsePoint(root, probePath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// True if <paramref name="directoryPath"/> or any directory between <paramref name="requiredRoot"/>
    /// (exclusive) and it is a junction/symlink. Ancestors <em>above</em> the output root (a junctioned
    /// Documents folder, OneDrive placeholders, macOS /var) are the user's choice of location and must
    /// not block every cleanup.
    /// </summary>
    private static bool HasReparsePointBelowRoot(string directoryPath, string requiredRoot)
    {
        try
        {
            string fullPath = Path.GetFullPath(directoryPath);
            string probePath = Path.Combine(fullPath, "_");
            return RDAExplorer.ArchivePathSanitizer.PathContainsReparsePoint(Path.GetFullPath(requiredRoot), probePath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // Fail closed: cannot prove the path is safe to delete.
            return true;
        }
    }

    private static void LogReparseRefusal(PipelineContext? context, string directoryPath) =>
        context?.Log.WriteLocalized(
            ConsoleMessageKind.Error,
            "outputFolderClearFailed",
            always: true,
            $"refusing to delete '{directoryPath}': it contains a junction or symbolic link");

    public static bool TryDeleteDirectoryRecursive(PipelineContext? context, string directoryPath, string requiredRoot)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(requiredRoot))
        {
            return false;
        }

        if (!IsPathUnderDirectory(directoryPath, requiredRoot))
        {
            return false;
        }

        if (HasReparsePointBelowRoot(directoryPath, requiredRoot))
        {
            LogReparseRefusal(context, directoryPath);
            return false;
        }

        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            context?.Log.WriteLocalized(ConsoleMessageKind.Error, "outputFolderClearFailed", always: true, ex.Message);
            return false;
        }
    }

    public static bool TryClearDirectory(PipelineContext context, string directoryPath, string requiredRoot, string clearedMessage)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(requiredRoot))
        {
            return false;
        }

        if (!IsPathUnderDirectory(directoryPath, requiredRoot))
        {
            return false;
        }

        if (HasReparsePointBelowRoot(directoryPath, requiredRoot))
        {
            LogReparseRefusal(context, directoryPath);
            return false;
        }

        try
        {
            if (!Directory.Exists(directoryPath) || AssetProcessorFileSystem.IsDirectoryEmpty(directoryPath))
            {
                _ = Directory.CreateDirectory(directoryPath);
                return true;
            }

            if (!TryDeleteDirectoryRecursive(context, directoryPath, requiredRoot))
            {
                return false;
            }

            _ = Directory.CreateDirectory(directoryPath);

            if (context.DebugMode)
            {
                context.Log.Write(ConsoleMessageKind.Info, clearedMessage);
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "outputFolderClearFailed", always: true, ex.Message);
            return false;
        }
    }
}
