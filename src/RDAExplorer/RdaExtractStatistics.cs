namespace RDAExplorer;

public sealed class RdaExtractStatistics
{
    public int TotalEntries { get; set; }
    public int Extracted { get; set; }
    public int SkippedChecksumOrMetadata { get; set; }
    public int SkippedInvalidPath { get; set; }
    public int SkippedFilterMismatch { get; set; }
    public int SkippedExistingBare { get; set; }

    /// <summary>Archive blocks that could not be read (their files are missing from this archive).</summary>
    public IReadOnlyList<string> UnreadableBlocks { get; set; } = [];

    /// <summary>Matched entries that failed to decode or write ("name: reason").</summary>
    public IReadOnlyList<string> FailedEntries { get; set; } = [];
}
