namespace RDAExplorer;

/// <summary>
/// Per-archive read diagnostics emitted before extraction starts.
/// Helps explain how RDA data is stored and decoded.
/// </summary>
public sealed class RdaArchiveDiagnostics
{
    public string ArchiveFormat { get; init; } = "";
    public ulong FileSizeBytes { get; init; }
    public ulong FirstBlockOffset { get; init; }
    public uint BlocksRead { get; init; }
    public int TotalEntries { get; init; }
    public int CompressedEntries { get; init; }
    public int EncryptedEntries { get; init; }
    public int MemoryResidentEntries { get; init; }
    public int DeletedEntries { get; init; }
    public IReadOnlyList<RdaBlockDiagnostics> Blocks { get; init; } = [];
}

/// <summary>
/// Low-level diagnostics for one parsed block header in an RDA archive.
/// </summary>
public sealed class RdaBlockDiagnostics
{
    public int Index { get; init; }
    public ulong Offset { get; init; }
    public uint RawFlags { get; init; }
    public bool IsCompressed { get; init; }
    public bool IsEncrypted { get; init; }
    public bool IsMemoryResident { get; init; }
    public bool IsDeleted { get; init; }
    public uint FileCount { get; init; }
    public ulong DirectorySize { get; init; }
    public ulong DecompressedDirectorySize { get; init; }
    public ulong NextBlockOffset { get; init; }
}
