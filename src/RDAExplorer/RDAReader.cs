using System.ComponentModel;
using RDAExplorer.Misc;

namespace RDAExplorer;

/// <summary>
/// Reads an RDA (Resource Data Archive) file and builds an in-memory folder tree (<see cref="RdaFolder"/>) of all contained files.
/// Supports V2.0 and V2.2 formats, compressed/encrypted blocks, and optional progress reporting via <see cref="BackgroundWorker"/>.
/// Set <see cref="FileName"/> then call <see cref="ReadRDAFile"/>; use <see cref="RdaFolder"/> to browse or pass files to <see cref="RDAFileExtension.ExtractAll(string, string, string, bool)"/>.
/// </summary>
public sealed class RDAReader : IDisposable
{
    private readonly List<RDAFile> fileEntries = [];
    private readonly List<RdaBlockDiagnostics> blockDiagnostics = [];
    /// <summary>Root of the folder tree after <see cref="ReadRDAFile"/>. <see langword="null"/> until read completes.</summary>
    public RDAFolder? RdaFolder { get; private set; } = new(FileHeader.RdaVersion.Version22);
    /// <summary>Full path to the .rda file to read.</summary>
    public string FileName { get; set; } = "";
    private BinaryReader? read;
    private FileHeader fileHeader;
    private readonly List<RDAMemoryResidentHelper> memoryResidentHelpers = [];
    /// <summary>Number of RDA blocks read so far (for progress reporting).</summary>
    public uint BlocksRead { get; private set; }
    public ulong FileSizeBytes { get; private set; }
    public ulong FirstBlockOffset { get; private set; }
    public IReadOnlyList<RdaBlockDiagnostics> BlockDiagnostics => this.blockDiagnostics;
    private readonly List<RDASkippedDataSection> skippedDataSections = [];
    /// <summary>Optional: when set, progress is reported here during <see cref="ReadRDAFile"/>.</summary>
    public BackgroundWorker? BackgroundWorker { get; set; }

    private void UpdateOutput(string message)
    {
        if (this.BackgroundWorker is null || this.read is null)
        {
            return;
        }

        this.BackgroundWorker.ReportProgress(
          (int)(this.read.BaseStream.Position / (double)this.read.BaseStream.Length * 100.0));
    }

    /// <summary>Opens the RDA file, reads all blocks, and builds <see cref="RdaFolder"/>. Call after setting <see cref="FileName"/>.</summary>
    /// <exception cref="InvalidOperationException">When file format is invalid or stream read fails.</exception>
    public void ReadRDAFile()
    {
        this.DisposeMemoryResidentHelpers();
        this.read?.Dispose();
        this.read = new BinaryReader(new FileStream(this.FileName, FileMode.Open, FileAccess.Read, FileShare.Read));
        byte[] magicBytes = RDAValidation.ReadExact(this.read, 2, "RDA magic");
        this.read.BaseStream.Position = 0L;

        bool isV20 = magicBytes[0] == (byte)'R' && magicBytes[1] == 0;
        bool isV22 = magicBytes[0] == (byte)'R' && magicBytes[1] == (byte)'e';

        if (isV20)
        {
            this.fileHeader = ReadFileHeader(this.read, FileHeader.RdaVersion.Version20);
        }
        else if (isV22)
        {
            this.fileHeader = ReadFileHeader(this.read, FileHeader.RdaVersion.Version22);
        }
        else
        {
            throw new InvalidDataException("Invalid or unsupported RDA file!");
        }

        this.BlocksRead = 0U;
        this.fileEntries.Clear();
        this.blockDiagnostics.Clear();

        ulong beginningOfDataSection = (ulong)this.read.BaseStream.Position;
        ulong fileLength = (ulong)this.read.BaseStream.Length;
        this.FileSizeBytes = fileLength;
        this.FirstBlockOffset = this.fileHeader.FirstBlockOffset;

        if (this.fileHeader.FirstBlockOffset > fileLength)
        {
            throw new InvalidDataException("Invalid first block offset");
        }

        ulong nextBlockOffset = this.fileHeader.FirstBlockOffset;
        int blockIndex = 0;
        while (nextBlockOffset != 0 && nextBlockOffset < fileLength)
        {
            ulong current = nextBlockOffset;
            nextBlockOffset = this.ReadBlock(current, beginningOfDataSection, blockIndex++);

            if (nextBlockOffset == 0)
            {
                break;
            }

            if (nextBlockOffset <= current)
            {
                throw new InvalidDataException("Invalid block offset chain");
            }

            if (nextBlockOffset > fileLength)
            {
                throw new InvalidDataException("Next block offset extends beyond the end of the RDA file");
            }

            ulong headerSize = this.GetBlockHeaderSize();
            beginningOfDataSection = current + headerSize;
        }

        this.skippedDataSections.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        this.RdaFolder = RDAFolder.GenerateFrom(this.fileEntries, this.fileHeader.Version);
    }

    private static FileHeader ReadFileHeader(BinaryReader reader, FileHeader.RdaVersion expectedVersion)
    {
        string magicString;
        uint reservedBytes;
        if (expectedVersion == FileHeader.RdaVersion.Version20)
        {
            int byteCount = Encoding.Unicode.GetByteCount("Resource File V2.0");
            magicString = Encoding.Unicode.GetString(RDAValidation.ReadExact(reader, byteCount, "RDA V2.0 magic"));
            if (magicString != "Resource File V2.0")
            {
                throw new InvalidDataException("Invalid RDA V2.0 magic header.");
            }

            reservedBytes = 1008;
        }
        else
        {
            int byteCount = Encoding.UTF8.GetByteCount("Resource File V2.2");
            magicString = Encoding.UTF8.GetString(RDAValidation.ReadExact(reader, byteCount, "RDA V2.2 magic"));
            if (magicString != "Resource File V2.2")
            {
                throw new InvalidDataException("Invalid RDA V2.2 magic header.");
            }

            reservedBytes = 766;
        }
        return new FileHeader
        {
            Magic = magicString,
            Version = expectedVersion,
            Reserved = RDAValidation.ReadExact(reader, (int)reservedBytes, "RDA reserved header"),
            FirstBlockOffset = ReadUIntVersionAware(reader, expectedVersion)
        };
    }

    private static ulong ReadUIntVersionAware(BinaryReader reader, FileHeader.RdaVersion version) =>
      version == FileHeader.RdaVersion.Version20 ? reader.ReadUInt32() : reader.ReadUInt64();

    private ulong ReadUIntVersionAware(BinaryReader reader) =>
      ReadUIntVersionAware(reader, this.fileHeader.Version);

    private ulong GetBlockHeaderSize() =>
      this.fileHeader.Version == FileHeader.RdaVersion.Version20 ? 20UL : 32UL;

    private ulong ReadBlock(ulong offset, ulong beginningOfDataSection, int blockIndex)
    {
        BlockInfo blockInfo = this.ReadBlockInfo(offset);

        DecodeBlockFlags(blockInfo.Flags, out bool isMemoryResident, out bool isEncrypted, out bool isCompressed);
        bool isDeleted = IsSkippedBlock(blockInfo);
        this.blockDiagnostics.Add(new RdaBlockDiagnostics
        {
            Index = blockIndex,
            Offset = offset,
            RawFlags = blockInfo.Flags,
            IsCompressed = isCompressed,
            IsEncrypted = isEncrypted,
            IsMemoryResident = isMemoryResident,
            IsDeleted = isDeleted,
            FileCount = blockInfo.FileCount,
            DirectorySize = blockInfo.DirectorySize,
            DecompressedDirectorySize = blockInfo.DecompressedSize,
            NextBlockOffset = blockInfo.NextBlock
        });

        if (IsSkippedBlock(blockInfo))
            return blockInfo.NextBlock;

        this.ReportBlockFlags(isMemoryResident, isEncrypted, isCompressed, blockInfo.Flags);

        ulong directoryStart = RDAValidation.SubtractOffset(offset, blockInfo.DirectorySize, "block directory");
        RDAValidation.EnsureRangeWithinStream(this.read!.BaseStream, directoryStart, blockInfo.DirectorySize, "block directory");

        if (!this.TryGetEncryptionSeed(isEncrypted, blockInfo, beginningOfDataSection, offset, out int seed))
            return blockInfo.NextBlock;

        this.ReadMemoryResidentInfo(isMemoryResident, directoryStart,
            out int memResInfoSize, out ulong memResCompressed, out ulong memResDataSize);

        byte[] directoryBytes = this.ReadAndDecodeDirectory(directoryStart, blockInfo, isEncrypted, isCompressed, seed);

        RDAMemoryResidentHelper? memResHelper = null;
        if (isMemoryResident)
        {
            memResHelper = this.CreateMemoryResidentHelper(directoryStart, memResInfoSize, memResCompressed, memResDataSize, blockInfo);
            this.memoryResidentHelpers.Add(memResHelper);
        }

        this.ValidateDirectorySize(blockInfo, directoryBytes);

        ++this.BlocksRead;
        this.UpdateOutput("-- DirEntries:");
        this.ReadDirEntries(directoryBytes, blockInfo, memResHelper);

        return blockInfo.NextBlock;
    }

    private BlockInfo ReadBlockInfo(ulong offset)
    {
        if (this.read is null)
            throw new InvalidOperationException("BinaryReader is not initialized");

        RDAValidation.EnsureRangeWithinStream(this.read.BaseStream, offset, this.GetBlockHeaderSize(), "block header");
        this.read.BaseStream.Position = RDAValidation.ToStreamOffset(offset, "block header offset");
        return new BlockInfo
        {
            Flags = this.read.ReadUInt32(),
            FileCount = this.read.ReadUInt32(),
            DirectorySize = this.ReadUIntVersionAware(this.read),
            DecompressedSize = this.ReadUIntVersionAware(this.read),
            NextBlock = this.ReadUIntVersionAware(this.read)
        };
    }

    private static bool IsSkippedBlock(BlockInfo blockInfo) =>
        ((int)blockInfo.Flags & 8) == 8;

    private static void DecodeBlockFlags(uint flags, out bool isMemoryResident, out bool isEncrypted, out bool isCompressed)
    {
        isMemoryResident = ((int)flags & 4) == 4;
        isEncrypted = ((int)flags & 2) == 2;
        isCompressed = ((int)flags & 1) == 1;
    }

    private void ReportBlockFlags(bool isMemoryResident, bool isEncrypted, bool isCompressed, uint flags)
    {
        if (isMemoryResident) this.UpdateOutput("MemoryResident");
        if (isEncrypted) this.UpdateOutput("Encrypted");
        if (isCompressed) this.UpdateOutput("Compressed");
        if (flags == 0U) this.UpdateOutput("No Flags");
    }

    private bool TryGetEncryptionSeed(bool isEncrypted, BlockInfo blockInfo, ulong beginningOfDataSection, ulong offset, out int seed)
    {
        seed = 0;
        if (!isEncrypted)
            return true;

        try
        {
            seed = BinaryExtension.GetDecryptionSeed(this.fileHeader.Version);
            return true;
        }
        catch (ArgumentException ex)
        {
            this.UpdateOutput($"Skipping ({blockInfo.FileCount} files) -- {ex.Message}");
            this.skippedDataSections.Add(new RDASkippedDataSection
            {
                BlockInfo = blockInfo,
                Offset = beginningOfDataSection,
                Size = offset - beginningOfDataSection
            });
            return false;
        }
    }

    private void ReadMemoryResidentInfo(bool isMemoryResident, ulong directoryStart,
        out int memResInfoSize, out ulong memResCompressed, out ulong memResDataSize)
    {
        memResInfoSize = 0;
        memResCompressed = 0;
        memResDataSize = 0;

        if (!isMemoryResident)
            return;

        memResInfoSize = this.fileHeader.Version == FileHeader.RdaVersion.Version20 ? 8 : 16;
        ulong memResInfoStart = RDAValidation.SubtractOffset(directoryStart, (ulong)memResInfoSize, "memory-resident metadata");
        this.read!.BaseStream.Position = RDAValidation.ToStreamOffset(memResInfoStart, "memory-resident metadata offset");
        memResCompressed = this.ReadUIntVersionAware(this.read);
        memResDataSize = this.ReadUIntVersionAware(this.read);
        ulong dataStart = RDAValidation.SubtractOffset(memResInfoStart, memResCompressed, "memory-resident data");
        RDAValidation.EnsureRangeWithinStream(this.read.BaseStream, dataStart, memResCompressed, "memory-resident data");
    }

    private byte[] ReadAndDecodeDirectory(ulong directoryStart, BlockInfo blockInfo, bool isEncrypted, bool isCompressed, int seed)
    {
        this.read!.BaseStream.Position = RDAValidation.ToStreamOffset(directoryStart, "block directory offset");
        byte[] directoryBytes = RDAValidation.ReadExact(
            this.read,
            RDAValidation.ToSupportedArrayLength(blockInfo.DirectorySize, "block directory size"),
            "block directory");

        if (isEncrypted)
            directoryBytes = BinaryExtension.Decrypt(directoryBytes, seed);

        if (isCompressed)
            directoryBytes = ZLib.ZLib.Uncompress(
                directoryBytes,
                RDAValidation.ToSupportedArrayLength(blockInfo.DecompressedSize, "decompressed block directory size"));

        return directoryBytes;
    }

    private RDAMemoryResidentHelper CreateMemoryResidentHelper(ulong directoryStart, int memResInfoSize,
        ulong memResCompressed, ulong memResDataSize, BlockInfo blockInfo)
    {
        ulong memResInfoStart = RDAValidation.SubtractOffset(directoryStart, (ulong)memResInfoSize, "memory-resident metadata");
        ulong dataStart = RDAValidation.SubtractOffset(memResInfoStart, memResCompressed, "memory-resident data");
        return new RDAMemoryResidentHelper(
            dataStart, memResDataSize, memResCompressed, this.read!.BaseStream, blockInfo, this.fileHeader.Version);
    }

    private void ValidateDirectorySize(BlockInfo blockInfo, byte[] directoryBytes)
    {
        uint entrySize = this.fileHeader.Version == FileHeader.RdaVersion.Version20 ? 540u : 560u;
        ulong expectedDirSize = blockInfo.FileCount * (ulong)entrySize;
        if (expectedDirSize > (ulong)directoryBytes.Length)
        {
            throw new InvalidDataException(
                $"Invalid directory: expected at least {expectedDirSize:N0} bytes but got {directoryBytes.Length:N0}.");
        }
    }

    private void ReadDirEntries(byte[] buffer, BlockInfo block, RDAMemoryResidentHelper? memResHelper)
    {
        using var dirReader = new BinaryReader(new MemoryStream(buffer));
        for (uint i = 0; i < block.FileCount; ++i)
        {
            string filename = Encoding.Unicode.GetString(
              RDAValidation.ReadExact(dirReader, 520, "directory entry filename")).Replace("\0", "");
            var dir = new DirEntry
            {
                Filename = filename,
                Offset = this.ReadUIntVersionAware(dirReader),
                Compressed = this.ReadUIntVersionAware(dirReader),
                FileSize = this.ReadUIntVersionAware(dirReader),
                Timestamp = this.ReadUIntVersionAware(dirReader),
                Reserved = this.ReadUIntVersionAware(dirReader)
            };
            if (this.read is null)
            {
                throw new InvalidOperationException("BinaryReader is not initialized");
            }

            this.fileEntries.Add(RDAFile.FromUnmanaged(this.fileHeader.Version, dir, block, this.read, memResHelper));
        }
    }

    private void DisposeMemoryResidentHelpers()
    {
        foreach (RDAMemoryResidentHelper helper in this.memoryResidentHelpers)
        {
            helper.Dispose();
        }

        this.memoryResidentHelpers.Clear();
    }

    /// <summary>Closes the underlying stream and clears the file list.</summary>
    public void Dispose()
    {
        this.DisposeMemoryResidentHelpers();
        this.read?.Dispose();
        this.read = null;
        this.fileEntries.Clear();
        this.RdaFolder = null;
        GC.SuppressFinalize(this);
    }
}
