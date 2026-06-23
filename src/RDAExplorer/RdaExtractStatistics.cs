namespace RDAExplorer;

public sealed class RdaExtractStatistics
{
    public int TotalEntries { get; set; }
    public int Extracted { get; set; }
    public int SkippedChecksumOrMetadata { get; set; }
    public int SkippedInvalidPath { get; set; }
    public int SkippedFilterMismatch { get; set; }
    public int SkippedExistingBare { get; set; }
}
