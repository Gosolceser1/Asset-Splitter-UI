namespace RDAExplorer;

/// <summary>
/// Represents a region of RDA data that was skipped during read (e.g. deleted or unsupported block).
/// Used for reporting or when rewriting archives.
/// </summary>
public class RDASkippedDataSection
{
    /// <summary>Block metadata for this section.</summary>
    public BlockInfo BlockInfo { get; set; }
    /// <summary>Start offset in the RDA file.</summary>
    public ulong Offset { get; set; }
    /// <summary>Length in bytes.</summary>
    public ulong Size { get; set; }
}
