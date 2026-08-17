using RDAExplorer.Misc;

namespace RDAExplorer;

/// <summary>
/// Holds decompressed data for an RDA "memory-resident" block. These blocks store directory data in a special
/// in-memory layout; this helper reads, decrypts/decompresses it, and exposes a MemoryStream for RDAFile to read from.
/// </summary>
public sealed class RDAMemoryResidentHelper : IDisposable
{
    /// <summary>Start offset of this block's data in the RDA.</summary>
    public ulong Offset { get; }
    /// <summary>Uncompressed data size.</summary>
    public ulong DataSize { get; }
    /// <summary>Compressed size in the archive.</summary>
    public ulong Compressed { get; }
    /// <summary>Decompressed block data; <see cref="RDAFile"/> reads from this stream.</summary>
    public MemoryStream Data { get; }
    /// <summary>Reader over <see cref="Data"/> shared by files in this memory-resident block.</summary>
    public BinaryReader Reader { get; }
    /// <summary>Block metadata.</summary>
    public BlockInfo Info { get; }

    /// <summary>Reads block from <paramref name="dataSource"/>, decrypts/decompresses if needed, and fills <see cref="Data"/>.</summary>
    public RDAMemoryResidentHelper(
      ulong offset,
      ulong uncompressedDataSize,
      ulong compressed,
      Stream dataSource,
      BlockInfo blockInfo,
      FileHeader.RdaVersion version)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        this.Offset = offset;
        this.DataSize = uncompressedDataSize;
        this.Compressed = compressed;
        this.Info = blockInfo;
        this.Data = new MemoryStream();
        this.Reader = new BinaryReader(this.Data);

        int compressedSize = RDAValidation.ToSupportedArrayLength(compressed, "memory-resident compressed size");
        RDAValidation.EnsureRangeWithinStream(dataSource, offset, compressed, "memory-resident data");

        byte[] buffer = new byte[compressedSize];
        dataSource.Position = RDAValidation.ToStreamOffset(offset, "memory-resident data offset");
        RDAValidation.ReadExact(dataSource, buffer, "memory-resident data");

        if (((int)blockInfo.Flags & 2) == 2)
        {
            buffer = BinaryExtension.Decrypt(buffer, BinaryExtension.GetDecryptionSeed(version));
        }

        if (((int)blockInfo.Flags & 1) == 1)
        {
            buffer = ZLib.ZLib.Uncompress(
              buffer,
              RDAValidation.ToSupportedArrayLength(uncompressedDataSize, "memory-resident uncompressed size"));
        }

        this.Data.Write(buffer, 0, buffer.Length);
        this.Data.Position = 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Reader.Dispose();
        this.Data.Dispose();
        GC.SuppressFinalize(this);
    }
}
