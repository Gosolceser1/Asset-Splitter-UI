namespace RDAExplorer;

/// <summary>
/// A single directory entry inside an RDA block. Describes one file: path, offsets, sizes, and timestamp.
/// </summary>
public record struct DirEntry
{
    /// <summary>Path of the file inside the archive (forward slashes, e.g. "data/config/asset.xml").</summary>
    public string Filename { get; set; }
    /// <summary>Byte offset of the file data within the RDA.</summary>
    public ulong Offset { get; set; }
    /// <summary>Compressed size in bytes (or same as filesize if not compressed).</summary>
    public ulong Compressed { get; set; }
    /// <summary>Uncompressed size in bytes.</summary>
    public ulong FileSize { get; set; }
    /// <summary>File timestamp (format-specific encoding).</summary>
    public ulong Timestamp { get; set; }
    /// <summary>Reserved field in directory entry (format-defined).</summary>
    public ulong Reserved { get; set; }
}
