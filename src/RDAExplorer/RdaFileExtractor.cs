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
            int matches = 1;
            foreach (string criterion in parts)
            {
                if (criterion.Length == 0)
                {
                    continue;
                }

                if (criterion[0] == '!')
                {
                    if (file.FileName.Contains(criterion[1..], StringComparison.Ordinal))
                    {
                        matches = 0;
                        break;
                    }
                }
                else if (!file.FileName.Contains(criterion, StringComparison.Ordinal))
                {
                    matches = 0;
                    break;
                }
            }

            if (matches > 0)
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
                    if (skipExistingBareOutput && File.Exists(destinationPath))
                    {
                        onSkipped?.Invoke(file.FileName, "newer patch already extracted (skip overwrite)");
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

    public static long Extract(RDAFile file, string destinationPath)
    {
        byte[] fileBytes = file.GetData();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        fileStream.Write(fileBytes, 0, fileBytes.Length);
        new FileInfo(destinationPath).LastWriteTime = file.TimeStamp;
        return fileBytes.LongLength;
    }
}
