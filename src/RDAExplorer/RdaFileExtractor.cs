namespace RDAExplorer;

public static class RdaFileExtractor
{
    public delegate void RdaExtractedHandler(string archivePath, string outputPath, long bytes);
    public delegate void RdaSkippedHandler(string archivePath, string reason);

    public static bool ExtractToRoot(
        RDAFile file,
        string folder,
        string filterCriteria,
        bool bare = false,
        bool skipExistingBareOutput = false,
        RdaExtractedHandler? onExtracted = null,
        RdaSkippedHandler? onSkipped = null)
    {
        ArgumentNullException.ThrowIfNull(filterCriteria);

        if (IsExcludedArchiveEntry(file.FileName))
        {
            onSkipped?.Invoke(file.FileName, "checksum or metadata entry (excluded by policy)");
            return false;
        }

        if (!ArchivePathSanitizer.TryGetOutputPath(file.FileName, folder, out string outputPath))
        {
            onSkipped?.Invoke(file.FileName, "invalid or blocked output path");
            return false;
        }

        string? dir = Path.GetDirectoryName(outputPath);
        foreach (string criteriaGroup in filterCriteria.Split(';'))
        {
            string[] parts = criteriaGroup.Split('+');
            bool groupMatches = true;
            foreach (string criterion in parts)
            {
                if (criterion.Length == 0)
                {
                    continue;
                }

                if (criterion[0] == '!')
                {
                    if (ArchivePathMatchesCriterion(file.FileName, criterion[1..]))
                    {
                        groupMatches = false;
                        break;
                    }
                }
                else if (!ArchivePathMatchesCriterion(file.FileName, criterion))
                {
                    groupMatches = false;
                    break;
                }
            }

            if (groupMatches)
            {
                string destinationPath;
                if (bare)
                {
                    Directory.CreateDirectory(folder);
                    string? fileName = Path.GetFileName(outputPath);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        onSkipped?.Invoke(file.FileName, "bare output has no file name");
                        return false;
                    }

                    destinationPath = Path.Combine(folder, fileName);
                    if (File.Exists(destinationPath))
                    {
                        onSkipped?.Invoke(
                            file.FileName,
                            skipExistingBareOutput
                                ? "newer patch already extracted (skip overwrite)"
                                : "bare basename already extracted (skip overwrite)");
                        return false;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    destinationPath = outputPath;
                }

                long bytes = Extract(file, destinationPath);
                onExtracted?.Invoke(file.FileName, destinationPath, bytes);
                return true;
            }
        }

        onSkipped?.Invoke(file.FileName, "does not match filter criteria");
        return false;
    }

    /// <summary>
    /// Skips RDA checksum sidecars and internal metadata exports.
    /// Anno 117 <c>texts_metadata.xml</c> (LineId dev names) must not be excluded here.
    /// </summary>
    private static bool IsExcludedArchiveEntry(string fileName) =>
        fileName.EndsWith(".checksum", StringComparison.OrdinalIgnoreCase)
        || (fileName.Contains("_metadata.xml", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith("texts_metadata.xml", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Help documents <c>/</c> as extract-all. Also treat <c>\</c> as extract-all and
    /// compare paths with normalized separators so Windows-style archive names match.
    /// </summary>
    internal static bool ArchivePathMatchesCriterion(string fileName, string criterion)
    {
        if (criterion.Length == 0 || criterion is "/" or "\\")
        {
            return true;
        }

        string normalizedName = fileName.Replace('\\', '/');
        string normalizedCriterion = criterion.Replace('\\', '/');
        return normalizedName.Contains(normalizedCriterion, StringComparison.Ordinal);
    }

    public static long Extract(RDAFile file, string destinationPath)
    {
        byte[] fileBytes = file.GetData();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        fileStream.Write(fileBytes, 0, fileBytes.Length);
        TrySetLastWriteTime(destinationPath, file.TimeStamp);
        return fileBytes.LongLength;
    }

    private static void TrySetLastWriteTime(string destinationPath, DateTime timeStamp)
    {
        if (timeStamp.Year < 1601)
        {
            return;
        }

        try
        {
            new FileInfo(destinationPath).LastWriteTime = timeStamp;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Timestamp is metadata only; extraction already wrote the bytes.
        }
    }
}
