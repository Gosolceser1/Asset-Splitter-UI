namespace RDAExplorer;

/// <summary>
/// Header of an RDA (Resource Data Archive) file. RDA is the archive format used by Anno 1800 / Anno 117.
/// Contains magic string, format version, reserved bytes, and offset to the first data block.
/// </summary>
public record struct FileHeader
{
    /// <summary>Magic identifier (e.g. "Resource File V2.0" or "Resource File V2.2").</summary>
    public string Magic { get; set; }
    /// <summary>RDA format version; determines 32- vs 64-bit offsets and encryption.</summary>
    public RdaVersion Version { get; set; }
    /// <summary>Reserved bytes in the RDA header (format padding).</summary>
    public byte[] Reserved { get; set; }
    /// <summary>File offset in bytes where the first block starts.</summary>
    public ulong FirstBlockOffset { get; set; }

    /// <summary>Supported RDA archive format versions.</summary>
    public enum RdaVersion
    {
        /// <summary>Unknown or unsupported version.</summary>
        Invalid,
        /// <summary>RDA V2.0 (32-bit offsets, seed 666666).</summary>
        Version20,
        /// <summary>RDA V2.2 (64-bit offsets, seed 1908874353).</summary>
        Version22,
    }
}
