using System.IO.Compression;
using System.Text;
using RDAExplorer;
using RDAExplorer.Misc;

namespace AssetSplitter.Tests;

/// <summary>
/// Wave-1 gauntlet: reproduce RDAExplorer / RDAExtract defects with synthetic archives only.
/// </summary>
public sealed class GauntletRdaExplorerTests
{
    private const uint FlagMemoryResident = 4;

    // -------------------------------------------------------------------------
    // DEFECT: memory-resident directory offset ignores mem-res info size
    // -------------------------------------------------------------------------

    [Fact]
    public void MemoryResident_V22_CorrectLayout_FailsOrCorrupts_ExpectedReadable()
    {
        // Spec / wiki / scripts/agent-tools/Read-RDA.ps1 layout:
        //   [file data][dir entries][mem-res info 16][block header]
        // directoryStart = blockOffset - memResInfoSize - DirectorySize
        //
        // RDAReader.ReadBlock uses:
        //   directoryStart = blockOffset - DirectorySize
        // then places mem-res info *before* the directory — inverted vs format.

        byte[] payload = "HELLO"u8.ToArray();
        string archivePath = WriteTempRda(BuildV22MemoryResidentArchive(
            archivePath: "payload.bin",
            fileBytes: payload,
            useCorrectMemResLayout: true));

        try
        {
            using var reader = new RDAReader { FileName = archivePath };

            Exception? caught = Record.Exception(reader.ReadRDAFile);

            if (caught is not null)
            {
                // Defect path A: valid mem-res archive throws.
                Assert.True(
                    caught is EndOfStreamException or InvalidDataException or InvalidOperationException,
                    $"Unexpected exception type: {caught.GetType().FullName}: {caught.Message}");
                return;
            }

            // Defect path B (observed): ReadRDAFile "succeeds" but the mem-res block is
            // silently skipped after InvalidDataException inside ReadMemoryResidentInfo /
            // CreateMemoryResidentHelper (caught in ReadBlock) → 0 files, no error.
            List<RDAFile> files = reader.RdaFolder!.GetAllFiles();
            bool correctlyRoundTripped = false;
            foreach (RDAFile file in files)
            {
                try
                {
                    byte[] data = file.GetData();
                    if (file.FileName == "payload.bin" && data.AsSpan().SequenceEqual(payload))
                    {
                        correctlyRoundTripped = true;
                    }
                }
                catch
                {
                    // Corrupt extract still counts as defect (not a successful round-trip).
                }
            }

            Assert.True(
                correctlyRoundTripped,
                "Correct [data][dir][meminfo][block] layout must round-trip after the mem-res offset fix.");
            Assert.Single(files);
        }
        finally
        {
            TryDelete(archivePath);
        }
    }

    [Fact]
    public void MemoryResident_V22_WrongLayoutMatchingReader_CanBeParsed()
    {
        // Control: layout that matches RDAReader's inverted assumption should parse.
        byte[] payload = "WORLD"u8.ToArray();
        string archivePath = WriteTempRda(BuildV22MemoryResidentArchive(
            archivePath: "payload.bin",
            fileBytes: payload,
            useCorrectMemResLayout: false));

        try
        {
            using var reader = new RDAReader { FileName = archivePath };
            Exception? caught = Record.Exception(reader.ReadRDAFile);
            if (caught is not null)
            {
                Assert.True(caught is EndOfStreamException or InvalidDataException or InvalidOperationException);
                return;
            }

            bool anyCorrect = reader.RdaFolder!.GetAllFiles().Any(f =>
            {
                try
                {
                    return f.FileName == "payload.bin" && f.GetData().AsSpan().SequenceEqual(payload);
                }
                catch
                {
                    return false;
                }
            });
            Assert.False(anyCorrect, "Inverted mem-res layout must not yield the original payload.");
        }
        finally
        {
            TryDelete(archivePath);
        }
    }

    [Fact]
    public void NonMemoryResident_V22_RoundTrips()
    {
        byte[] payload = "PLAIN"u8.ToArray();
        string archivePath = WriteTempRda(BuildV22PlainArchive("data/ok.txt", payload));

        try
        {
            using var reader = new RDAReader { FileName = archivePath };
            reader.ReadRDAFile();
            RDAFile file = Assert.Single(reader.RdaFolder!.GetAllFiles());
            Assert.Equal("data/ok.txt", file.FileName);
            Assert.Equal(payload, file.GetData());
        }
        finally
        {
            TryDelete(archivePath);
        }
    }

    // -------------------------------------------------------------------------
    // DEFECT: filter "/" does not match backslash archive paths (CLI "extract all")
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractFilter_SlashExtractsBackslashAndSlashlessPaths()
    {
        var file = new RDAFile
        {
            FileName = @"data\config\asset.xml",
            UncompressedSize = 4,
            CompressedSize = 4,
            BinaryFile = new BinaryReader(new MemoryStream([1, 2, 3, 4]))
        };

        string outDir = NewTempDir();
        try
        {
            Assert.True(RdaFileExtractor.ExtractToRoot(file, outDir, filterCriteria: "/"));
            Assert.True(File.Exists(Path.Combine(outDir, "data", "config", "asset.xml")));
        }
        finally
        {
            TryDeleteDir(outDir);
            file.BinaryFile.Dispose();
        }
    }

    // -------------------------------------------------------------------------
    // DEFECT: PathContainsReparsePoint never inspects the final file (symlink write-out)
    // -------------------------------------------------------------------------

    [Fact]
    public void PathContainsReparsePoint_DetectsDirectoryJunction_ButNotFileComponent()
    {
        string root = NewTempDir();
        string outside = NewTempDir();
        string junction = Path.Combine(root, "junc");
        string outsideFile = Path.Combine(outside, "secret.txt");
        File.WriteAllText(outsideFile, "secret");

        try
        {
            Assert.True(TryCreateDirectoryJunction(junction, outside), "Setup requires directory junction (mklink /J).");

            // Control: intermediate directory reparse points are detected.
            Assert.True(
                ArchivePathSanitizer.PathContainsReparsePoint(root, Path.Combine(junction, "secret.txt")),
                "Directory junctions under the output root must be rejected.");

            Assert.False(
                ArchivePathSanitizer.TryGetOutputPath(@"junc\secret.txt", root, out _),
                "TryGetOutputPath must block paths that traverse a directory junction.");

            // Defect (privilege-free proof): when the output file sits directly under fullRoot,
            // Path.GetRelativePath(root, GetDirectoryName(fullPath)) == "." and the method
            // returns false WITHOUT ever calling File.GetAttributes on the file. A root-level
            // file symlink therefore cannot be detected.
            string rootLevelFile = Path.Combine(root, "root-level.txt");
            File.WriteAllText(rootLevelFile, "x");
            Assert.False(
                ArchivePathSanitizer.PathContainsReparsePoint(root, rootLevelFile),
                "Directory walk does not treat a normal file as a reparse point.");
            Assert.True(ArchivePathSanitizer.TryGetOutputPath("root-level.txt", root, out _));

            string linkPath = Path.Combine(root, "linked.txt");
            if (TryCreateFileSymlink(linkPath, outsideFile))
            {
                Assert.True((File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0);
                Assert.True(ArchivePathSanitizer.ExistingPathIsReparsePoint(linkPath));
                Assert.False(
                    ArchivePathSanitizer.TryGetOutputPath("linked.txt", root, out _),
                    "Must not authorize writing through an existing file symlink.");
            }
        }
        finally
        {
            TryDelete(Path.Combine(root, "linked.txt"));
            TryDeleteDir(junction);
            TryDeleteDir(root);
            TryDeleteDir(outside);
        }
    }

    [Fact]
    public void TryGetOutputPath_RejectsDotDotSegments()
    {
        string root = NewTempDir();
        try
        {
            Assert.False(ArchivePathSanitizer.TryGetOutputPath(@"..\escape.txt", root, out _));
            Assert.False(ArchivePathSanitizer.TryGetOutputPath(@"foo\..\..\escape.txt", root, out _));
            Assert.True(ArchivePathSanitizer.TryGetOutputPath(@"foo\bar.txt", root, out _));
        }
        finally
        {
            TryDeleteDir(root);
        }
    }

    // -------------------------------------------------------------------------
    // DEFECT: RDAExtract data*.rda branch silently succeeds when no dataN parses
    // -------------------------------------------------------------------------

    [Fact]
    public void RdaExtract_DataGlobWithUnparseableNames_ReturnsZeroWithoutExtracting()
    {
        string work = NewTempDir();
        string outDir = Path.Combine(work, "out");
        Directory.CreateDirectory(outDir);

        // Matches GetFiles("data*.rda") but ParseIndex("database") fails → filtered out.
        string decoy = Path.Combine(work, "database.rda");
        File.WriteAllBytes(decoy, BuildV22PlainArchive("x.txt", "x"u8.ToArray()));

        try
        {
            int exit = RunRdaExtract(work, "/", outDir);
            Assert.Equal(0, exit);

            bool anyExtracted = Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories)
                .Any(p => !Path.GetFileName(p).Equals(".watch.rda", StringComparison.OrdinalIgnoreCase));

            Assert.True(
                anyExtracted,
                "Unparseable data*.rda names must fall through to extracting all *.rda files.");
        }
        finally
        {
            TryDeleteDir(work);
        }
    }

    // -------------------------------------------------------------------------
    // Supporting: encryption / zlib / malformed bounds (behavioral evidence)
    // -------------------------------------------------------------------------

    [Fact]
    public void Decrypt_RoundTripsXorSymmetric()
    {
        byte[] plain = Encoding.ASCII.GetBytes("abcdEFGH!");
        int seed = BinaryExtension.GetDecryptionSeed(FileHeader.RdaVersion.Version22);
        byte[] once = BinaryExtension.Decrypt(plain, seed);
        byte[] twice = BinaryExtension.Decrypt(once, seed);
        Assert.Equal(plain, twice);
        Assert.Equal(plain[^1], once[^1]); // odd trailing byte copied unchanged
    }

    [Fact]
    public void ZLib_RejectsTruncatedOutput()
    {
        byte[] compressed = CompressZlib(Encoding.ASCII.GetBytes("0123456789"));
        Assert.Throws<InvalidDataException>(() => RDAExplorer.ZLib.ZLib.Uncompress(compressed, uncompressedSize: 4));
    }

    [Fact]
    public void Reader_RejectsDirectoryExtendingPastEof()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteV22Header(bw, firstBlockOffset: 792);
        bw.Flush();
        byte[] bytes = ms.ToArray();
        string path = WriteTempRda(bytes[..100]); // truncated header
        try
        {
            using var reader = new RDAReader { FileName = path };
            Assert.ThrowsAny<Exception>(reader.ReadRDAFile);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void Reader_RejectsInvalidBlockOffsetChain()
    {
        byte[] payload = "A"u8.ToArray();
        string path = WriteTempRda(BuildV22TwoBlockBackwardChain(payload));
        try
        {
            using var reader = new RDAReader { FileName = path };
            var ex = Assert.Throws<InvalidDataException>(reader.ReadRDAFile);
            Assert.Contains("block offset chain", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(path);
        }
    }

    // -------------------------------------------------------------------------
    // Synthetic archive builders
    // -------------------------------------------------------------------------

    private static byte[] BuildV22PlainArchive(string archiveFileName, byte[] fileBytes)
    {
        const int headerSize = 792;
        const int dirEntrySize = 560;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        long dataOffset = headerSize;
        WriteV22Header(bw, firstBlockOffset: 0);

        bw.Write(fileBytes);
        WriteDirEntry(bw, archiveFileName, (ulong)dataOffset, (ulong)fileBytes.Length, (ulong)fileBytes.Length, timestamp: 0);
        long blockOffset = ms.Position;
        WriteBlockHeader(
            bw,
            flags: 0,
            fileCount: 1,
            directorySize: dirEntrySize,
            decompressedSize: dirEntrySize,
            nextBlock: 0);

        long fileLen = ms.Position;
        ms.Position = 784;
        bw.Write((ulong)blockOffset);
        ms.Position = blockOffset + 24;
        bw.Write((ulong)fileLen);
        return ms.ToArray();
    }

    /// <param name="useCorrectMemResLayout">
    /// true = wiki/PowerShell order [data][dir][meminfo][block];
    /// false = RDAReader order [data][meminfo][dir][block].
    /// </param>
    private static byte[] BuildV22MemoryResidentArchive(
        string archivePath,
        byte[] fileBytes,
        bool useCorrectMemResLayout)
    {
        const int dirEntrySize = 560;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteV22Header(bw, firstBlockOffset: 0);

        if (useCorrectMemResLayout)
        {
            // [data][dir][meminfo][block]
            bw.Write(fileBytes);
            WriteDirEntry(bw, archivePath, offset: 0, compressed: (ulong)fileBytes.Length,
                fileSize: (ulong)fileBytes.Length, timestamp: 0);
            bw.Write((ulong)fileBytes.Length);
            bw.Write((ulong)fileBytes.Length);
        }
        else
        {
            // [data][meminfo][dir][block] — matches RDAReader.ReadMemoryResidentInfo assumptions
            bw.Write(fileBytes);
            bw.Write((ulong)fileBytes.Length);
            bw.Write((ulong)fileBytes.Length);
            WriteDirEntry(bw, archivePath, offset: 0, compressed: (ulong)fileBytes.Length,
                fileSize: (ulong)fileBytes.Length, timestamp: 0);
        }

        long blockOffset = ms.Position;
        WriteBlockHeader(
            bw,
            flags: FlagMemoryResident,
            fileCount: 1,
            directorySize: dirEntrySize,
            decompressedSize: dirEntrySize,
            nextBlock: 0);

        long fileLen = ms.Position;
        ms.Position = 784;
        bw.Write((ulong)blockOffset);
        ms.Position = blockOffset + 24;
        bw.Write((ulong)fileLen);
        return ms.ToArray();
    }

    private static byte[] BuildV22TwoBlockBackwardChain(byte[] fileBytes)
    {
        const int headerSize = 792;
        const int dirEntrySize = 560;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteV22Header(bw, firstBlockOffset: 0);

        long dataOffset = headerSize;
        bw.Write(fileBytes);
        WriteDirEntry(bw, "a.txt", (ulong)dataOffset, (ulong)fileBytes.Length, (ulong)fileBytes.Length, 0);
        long blockOffset = ms.Position;
        WriteBlockHeader(bw, 0, 1, dirEntrySize, dirEntrySize, nextBlock: (ulong)headerSize);

        ms.Position = 784;
        bw.Write((ulong)blockOffset);
        return ms.ToArray();
    }

    private static void WriteV22Header(BinaryWriter bw, ulong firstBlockOffset)
    {
        byte[] magic = Encoding.UTF8.GetBytes("Resource File V2.2");
        if (magic.Length != 18)
        {
            throw new InvalidOperationException("V2.2 magic must be 18 bytes.");
        }

        bw.Write(magic);
        bw.Write(new byte[766]);
        bw.Write(firstBlockOffset);
    }

    private static void WriteBlockHeader(
        BinaryWriter bw,
        uint flags,
        uint fileCount,
        ulong directorySize,
        ulong decompressedSize,
        ulong nextBlock)
    {
        bw.Write(flags);
        bw.Write(fileCount);
        bw.Write(directorySize);
        bw.Write(decompressedSize);
        bw.Write(nextBlock);
    }

    private static void WriteDirEntry(
        BinaryWriter bw,
        string fileName,
        ulong offset,
        ulong compressed,
        ulong fileSize,
        ulong timestamp)
    {
        byte[] nameBytes = new byte[520];
        byte[] encoded = Encoding.Unicode.GetBytes(fileName);
        if (encoded.Length >= 520)
        {
            throw new InvalidOperationException("File name too long for RDA dir entry.");
        }

        Buffer.BlockCopy(encoded, 0, nameBytes, 0, encoded.Length);
        bw.Write(nameBytes);
        bw.Write(offset);
        bw.Write(compressed);
        bw.Write(fileSize);
        bw.Write(timestamp);
        bw.Write(0UL);
    }

    private static byte[] CompressZlib(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var zs = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zs.Write(raw, 0, raw.Length);
        }

        return ms.ToArray();
    }

    private static string WriteTempRda(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "gauntlet-rda-" + Guid.NewGuid().ToString("N") + ".rda");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string NewTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "gauntlet-rda-dir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static bool TryCreateFileSymlink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return File.Exists(linkPath)
                && (File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateDirectoryJunction(string junctionPath, string targetPath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                ArgumentList = { "/c", "mklink", "/J", junctionPath, targetPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            _ = proc.StandardOutput.ReadToEnd();
            _ = proc.StandardError.ReadToEnd();
            proc.WaitForExit(30_000);
            return proc.ExitCode == 0
                && Directory.Exists(junctionPath)
                && (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static int RunRdaExtract(string source, string match, string outDir)
    {
        string repoRoot = FindRepoRoot();
        string project = Path.Combine(repoRoot, "src", "RDAExtract", "RDAExtract.csproj");

        var build = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "build", project, "-v", "q" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot,
        })!;
        _ = build.StandardOutput.ReadToEnd();
        _ = build.StandardError.ReadToEnd();
        build.WaitForExit(120_000);
        Assert.Equal(0, build.ExitCode);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", project, "--no-build", "--", source, match, outDir },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot,
        };

        using var proc = System.Diagnostics.Process.Start(psi)!;
        _ = proc.StandardOutput.ReadToEnd();
        _ = proc.StandardError.ReadToEnd();
        Assert.True(proc.WaitForExit(120_000));
        return proc.ExitCode;
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "AssetSplitter.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate AssetSplitter.sln from test base directory.");
    }
}
