using RDAExplorer.Misc;

namespace RDAExplorer;

/// <summary>
/// Represents one file inside an RDA archive. Holds metadata (path, size, flags) and can read/decompress/decrypt
/// the file content and extract it to disk. Used by RDAReader and RDAFileExtension.
/// </summary>
public class RDAFile
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Stream, Lock> StreamLocks = new();

    /// <summary>Path inside the archive (e.g. "data/config/export/assets.xml").</summary>
    public string FileName { get; set; } = "";
    /// <summary>RDA format version (affects decryption seed and offset size).</summary>
    public FileHeader.RdaVersion Version { get; set; }
    /// <summary>Compressed, Encrypted, MemoryResident, or Deleted.</summary>
    public RDAFileAttributes Flags { get; set; }
    /// <summary>Byte offset of this file's data in the RDA.</summary>
    public ulong Offset { get; set; }
    /// <summary>Uncompressed size in bytes.</summary>
    public ulong UncompressedSize { get; set; }
    /// <summary>Compressed size in bytes (in the archive).</summary>
    public ulong CompressedSize { get; set; }
    /// <summary>File modification time.</summary>
    public DateTime TimeStamp { get; set; }
    /// <summary>Reader over the RDA stream (or memory-resident data). Set by RDAReader.</summary>
    public BinaryReader? BinaryFile { get; set; }

    /// <summary>Reads and returns file bytes (decrypts and decompresses if needed).</summary>
    /// <returns>Raw file content.</returns>
    public byte[] GetData()
    {
        if (this.BinaryFile is null)
        {
            throw new InvalidOperationException("BinaryFile is not initialized");
        }

        byte[] buffer;
        Stream stream = this.BinaryFile.BaseStream;
        int compressedSize = RDAValidation.ToSupportedArrayLength(this.CompressedSize, "compressed file size");
        RDAValidation.EnsureRangeWithinStream(stream, this.Offset, this.CompressedSize, $"file data for '{this.FileName}'");

        lock (GetStreamLock(stream))
        {
            this.BinaryFile.BaseStream.Position = RDAValidation.ToStreamOffset(this.Offset, "file data offset");
            buffer = RDAValidation.ReadExact(this.BinaryFile, compressedSize, $"file data for '{this.FileName}'");
        }

        if ((this.Flags & RDAFileAttributes.Encrypted) == RDAFileAttributes.Encrypted)
        {
            buffer = BinaryExtension.Decrypt(buffer, BinaryExtension.GetDecryptionSeed(this.Version));
        }

        if ((this.Flags & RDAFileAttributes.Compressed) == RDAFileAttributes.Compressed)
        {
            buffer = ZLib.ZLib.Uncompress(
              buffer,
              RDAValidation.ToSupportedArrayLength(this.UncompressedSize, "uncompressed file size"));
        }
        return buffer;
    }

    private static Lock GetStreamLock(Stream stream) =>
      StreamLocks.GetValue(stream, static _ => new Lock());

    /// <summary>Creates an RDAFile from a directory entry and block metadata.</summary>
    public static RDAFile FromUnmanaged(
      FileHeader.RdaVersion version,
      DirEntry dir,
      BlockInfo block,
      BinaryReader reader,
      RDAMemoryResidentHelper? memoryResidentHelper)
    {
        var rdaFile = new RDAFile
        {
            FileName = dir.Filename,
            Version = version,
            Offset = dir.Offset,
            UncompressedSize = dir.FileSize,
            CompressedSize = dir.Compressed,
            TimeStamp = DateTimeExtension.FromTimeStamp(dir.Timestamp)
        };

        if (((int)block.Flags & 4) != 4)
        {
            if (((int)block.Flags & 1) == 1)
            {
                rdaFile.Flags |= RDAFileAttributes.Compressed;
            }

            if (((int)block.Flags & 2) == 2)
            {
                rdaFile.Flags |= RDAFileAttributes.Encrypted;
            }
        }

        if (((int)block.Flags & 4) == 4)
        {
            rdaFile.Flags |= RDAFileAttributes.MemoryResident;
        }

        if (((int)block.Flags & 8) == 8)
        {
            rdaFile.Flags |= RDAFileAttributes.Deleted;
        }

        rdaFile.BinaryFile = memoryResidentHelper?.Reader ?? reader;

        return rdaFile;
    }

    /// <summary>File flags in the RDA: compression, encryption, memory-resident, or deleted.</summary>
    [Flags]
    public enum RDAFileAttributes
    {
        /// <summary>No special flags.</summary>
        None = 0,
        /// <summary>File data is zlib-compressed.</summary>
        Compressed = 1,
        /// <summary>File data is LCG XOR encrypted.</summary>
        Encrypted = 2,
        /// <summary>Data stored in block directory; read via RDAMemoryResidentHelper.</summary>
        MemoryResident = 4,
        /// <summary>Entry marked deleted; data may be skipped.</summary>
        Deleted = 8,
    }
}
