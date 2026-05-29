namespace RDAExplorer;

/// <summary>
/// Metadata for one block in an RDA archive. Blocks contain a directory (file list) and optional compressed/encrypted data.
/// </summary>
public record struct BlockInfo
{
    /// <summary>Block flags: 1=Compressed, 2=Encrypted, 4=MemoryResident, 8=Deleted.</summary>
    public uint Flags { get; set; }
    /// <summary>Number of file entries in this block's directory.</summary>
    public uint FileCount { get; set; }
    /// <summary>Size in bytes of the directory data (may be compressed/encrypted).</summary>
    public ulong DirectorySize { get; set; }
    /// <summary>Decompressed size of the directory data.</summary>
    public ulong DecompressedSize { get; set; }
    /// <summary>File offset of the next block, or 0 if this is the last block.</summary>
    public ulong NextBlock { get; set; }
}
