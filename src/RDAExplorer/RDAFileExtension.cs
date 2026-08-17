namespace RDAExplorer;

/// <summary>
/// High-level API for listing and extracting RDA archives. Each call creates, reads,
/// and disposes its own <see cref="RDAReader"/> — no static reader state is retained.
/// </summary>
public static class RDAFileExtension
{
    /// <summary>Reads the RDA and writes a text file listing all contained file paths to <paramref name="folder"/> (filename: {rdaName}_dir.txt).</summary>
    public static void ListAll(string rdaFile, string folder)
    {
        using RDAReader reader = new();
        reader.FileName = rdaFile;
        reader.ReadRDAFile();

        List<RDAFile> allFiles = reader.RdaFolder is not null
            ? reader.RdaFolder.GetAllFiles()
            : throw new InvalidOperationException("RDA folder not initialized");

        List<string> fileNames = allFiles.Select(f => f.FileName).ToList();
        string listingName = Path.GetFileName(rdaFile);
        File.WriteAllLines(Path.Combine(folder, $"{listingName}_dir.txt"), fileNames);
    }

    /// <summary>Reads the RDA and extracts all files matching <paramref name="filterCriteria"/> into <paramref name="folder"/>.</summary>
    public static RdaExtractStatistics ExtractAll(
        string rdaFile,
        string folder,
        string filterCriteria,
        bool bare = false,
        bool skipExistingBareOutput = false,
        RdaFileExtractor.RdaExtractedHandler? onExtracted = null,
        RdaFileExtractor.RdaSkippedHandler? onSkipped = null,
        Action<RdaArchiveDiagnostics>? onArchiveAnalyzed = null)
    {
        using RDAReader reader = new();
        reader.FileName = rdaFile;
        reader.ReadRDAFile();

        List<RDAFile> allFiles = reader.RdaFolder is not null
            ? reader.RdaFolder.GetAllFiles()
            : throw new InvalidOperationException("RDA folder not initialized");

        onArchiveAnalyzed?.Invoke(new RdaArchiveDiagnostics
        {
            ArchiveFormat = reader.RdaFolder?.Version switch
            {
                FileHeader.RdaVersion.Version20 => "RDA V2.0",
                FileHeader.RdaVersion.Version22 => "RDA V2.2",
                _ => "RDA (unknown version)"
            },
            FileSizeBytes = reader.FileSizeBytes,
            FirstBlockOffset = reader.FirstBlockOffset,
            BlocksRead = reader.BlocksRead,
            TotalEntries = allFiles.Count,
            CompressedEntries = allFiles.Count(f => (f.Flags & RDAFile.RDAFileAttributes.Compressed) == RDAFile.RDAFileAttributes.Compressed),
            EncryptedEntries = allFiles.Count(f => (f.Flags & RDAFile.RDAFileAttributes.Encrypted) == RDAFile.RDAFileAttributes.Encrypted),
            MemoryResidentEntries = allFiles.Count(f => (f.Flags & RDAFile.RDAFileAttributes.MemoryResident) == RDAFile.RDAFileAttributes.MemoryResident),
            DeletedEntries = allFiles.Count(f => (f.Flags & RDAFile.RDAFileAttributes.Deleted) == RDAFile.RDAFileAttributes.Deleted),
            Blocks = reader.BlockDiagnostics.ToArray()
        });

        var stats = new RdaExtractStatistics { TotalEntries = allFiles.Count };
        foreach (RDAFile file in allFiles)
        {
            if (ExtractEntry(file, folder, filterCriteria, bare, skipExistingBareOutput, stats, onExtracted, onSkipped))
            {
                stats.Extracted++;
            }
        }

        return stats;
    }

    private static bool ExtractEntry(
        RDAFile file,
        string folder,
        string filterCriteria,
        bool bare,
        bool skipExistingBareOutput,
        RdaExtractStatistics stats,
        RdaFileExtractor.RdaExtractedHandler? onExtracted,
        RdaFileExtractor.RdaSkippedHandler? onSkipped)
    {
        return RdaFileExtractor.ExtractToRoot(
            file,
            folder,
            filterCriteria,
            bare,
            skipExistingBareOutput,
            onExtracted,
            (archivePath, reason) =>
            {
                TallySkip(stats, reason);
                onSkipped?.Invoke(archivePath, reason);
            });
    }

    private static void TallySkip(RdaExtractStatistics stats, string reason)
    {
        if (reason.Contains("newer patch already", StringComparison.OrdinalIgnoreCase))
        {
            stats.SkippedExistingBare++;
        }
        else if (reason.Contains("checksum", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("metadata", StringComparison.OrdinalIgnoreCase))
        {
            stats.SkippedChecksumOrMetadata++;
        }
        else if (reason.Contains("filter", StringComparison.OrdinalIgnoreCase))
        {
            stats.SkippedFilterMismatch++;
        }
        else
        {
            stats.SkippedInvalidPath++;
        }
    }
}
