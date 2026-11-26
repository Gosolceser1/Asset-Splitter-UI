using System.Text;
using AssetProcessor;
using RDAExplorer;

namespace AssetSplitter.Tests;

/// <summary>
/// Wave 1 resource-safety gauntlet: proves handle / lock behavior with real I/O.
/// Does not modify product code — failures document leaks or soft-lock gaps.
/// </summary>
public sealed class GauntletResourceTests
{
    [Fact]
    public void RDAReader_FailedRead_ReleasesSourceFileHandleWithoutCallerDispose()
    {
        string path = CreateTempFile(Encoding.ASCII.GetBytes("XX-not-an-rda"));
        RDAReader? reader = null;
        try
        {
            reader = new RDAReader { FileName = path };
            Assert.ThrowsAny<Exception>(() => reader.ReadRDAFile());

            Assert.True(
                TryOpenExclusive(path),
                "Failed ReadRDAFile must release the source FileStream even if the caller never Dispose()s.");
            Assert.True(TryDelete(path), "Source must be deletable after a failed ReadRDAFile.");
        }
        finally
        {
            reader?.Dispose();
            TryDelete(path);
        }
    }

    [Fact]
    public void RDAReader_Dispose_ReleasesSourceFileHandle()
    {
        string path = CreateTempFile(Encoding.ASCII.GetBytes("XX-not-an-rda"));
        try
        {
            var reader = new RDAReader { FileName = path };
            Assert.ThrowsAny<Exception>(() => reader.ReadRDAFile());
            Assert.True(TryOpenExclusive(path), "Failed read should already have released the handle.");

            reader.Dispose();

            Assert.True(
                TryOpenExclusive(path),
                "After Dispose, exclusive open of the RDA source should succeed.");
            Assert.True(TryDelete(path), "After Dispose, deleting the RDA source should succeed.");
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void RDAReader_UsingBlock_ReleasesSourceEvenWhenReadThrows()
    {
        string path = CreateTempFile(Encoding.ASCII.GetBytes("XX-not-an-rda"));
        try
        {
            try
            {
                using var reader = new RDAReader { FileName = path };
                reader.ReadRDAFile();
            }
            catch (Exception)
            {
                // expected — invalid magic
            }

            Assert.True(
                TryOpenExclusive(path),
                "using/Dispose path should release the RDA FileStream even when ReadRDAFile throws.");
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void RdaFileExtractor_Extract_ReleasesDestinationHandle()
    {
        byte[] payload = Encoding.UTF8.GetBytes("gauntlet-extract-payload");
        string destination = Path.Combine(Path.GetTempPath(), "assetsplitter-extract-" + Guid.NewGuid().ToString("N") + ".bin");
        BinaryReader? ownedReader = null;
        try
        {
            ownedReader = new BinaryReader(new MemoryStream(payload));
            var file = new RDAFile
            {
                FileName = "data/test.bin",
                BinaryFile = ownedReader,
                Offset = 0,
                CompressedSize = (ulong)payload.Length,
                UncompressedSize = (ulong)payload.Length,
                TimeStamp = DateTime.UtcNow,
            };

            long written = RdaFileExtractor.Extract(file, destination);
            Assert.Equal(payload.LongLength, written);

            Assert.True(
                TryOpenExclusive(destination),
                "Destination FileStream should be disposed before Extract returns.");
            Assert.True(TryDelete(destination));
        }
        finally
        {
            ownedReader?.Dispose();
            TryDelete(destination);
        }
    }

    [Fact]
    public void RdaFileExtractor_GetData_MaterializesEntireEntryInMemory()
    {
        // Documents large-buffer pattern: Extract always calls GetData() which allocates full entry size.
        const int size = 256 * 1024;
        byte[] payload = new byte[size];
        Random.Shared.NextBytes(payload);

        using var ownedReader = new BinaryReader(new MemoryStream(payload));
        var file = new RDAFile
        {
            FileName = "data/large.bin",
            BinaryFile = ownedReader,
            Offset = 0,
            CompressedSize = (ulong)size,
            UncompressedSize = (ulong)size,
            TimeStamp = DateTime.UtcNow,
        };

        byte[] copy = file.GetData();
        Assert.Equal(size, copy.Length);
        Assert.False(ReferenceEquals(payload, copy));
    }

    [Fact]
    public void OutputRunLock_WithoutDispose_LeavesLockFileOnDisk()
    {
        string root = CreateTempDirectory();
        try
        {
            var context = new PipelineContext
            {
                AnnoAssetsRoot = root,
                BaseOutputDir = root,
            };

            IDisposable? held = OutputRunLock.TryAcquire(context);
            Assert.NotNull(held);

            string lockPath = Path.Combine(root, OutputRunLock.LockFileName);
            Assert.True(File.Exists(lockPath), "Acquire should create the lock file.");

            // Deliberately leak HeldLock — mimics forgotten Dispose / killed process before release.
            // Do not call held.Dispose().

            Assert.True(
                File.Exists(lockPath),
                "Lock file remains after TryAcquire returns when Dispose is never called.");

            // The exclusive OS handle is held for the whole run (released by the OS on crash).
            Assert.False(
                TryOpenExclusive(lockPath),
                "Lock file must stay OS-locked while the run lock is held.");

            held!.Dispose();
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void OutputRunLock_Dispose_RemovesLockFile()
    {
        string root = CreateTempDirectory();
        try
        {
            var context = new PipelineContext
            {
                AnnoAssetsRoot = root,
                BaseOutputDir = root,
            };

            string lockPath = Path.Combine(root, OutputRunLock.LockFileName);
            using (IDisposable? held = OutputRunLock.TryAcquire(context))
            {
                Assert.NotNull(held);
                Assert.True(File.Exists(lockPath));
            }

            Assert.False(
                File.Exists(lockPath),
                "HeldLock.Dispose should delete the lock file when PID still matches.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void OutputRunLock_SecondAcquireWhileFirstHeld_ReturnsNull()
    {
        string root = CreateTempDirectory();
        try
        {
            var context = new PipelineContext
            {
                AnnoAssetsRoot = root,
                BaseOutputDir = root,
            };

            using IDisposable? first = OutputRunLock.TryAcquire(context);
            Assert.NotNull(first);

            IDisposable? second = OutputRunLock.TryAcquire(context);
            Assert.Null(second);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void OutputDirectoryManager_StagingFolder_PersistsUntilExplicitCleanup()
    {
        string root = CreateTempDirectory();
        try
        {
            var context = new PipelineContext
            {
                AssetOutputDir = Path.Combine(root, "output_xml_Anno117"),
            };
            Directory.CreateDirectory(context.AssetOutputDir);

            string staging = OutputDirectoryManager.EnsureBaseAssetGuidStagingDirectory(context);
            Assert.True(Directory.Exists(staging));

            string leftover = Path.Combine(staging, "leftover-asset.xml");
            File.WriteAllText(leftover, "<Asset />");

            // Empty-dir cleanup must not remove a non-empty staging tree (failed merge leftover).
            OutputDirectoryManager.TryRemoveEmptyStagingDirectory(context);
            Assert.True(Directory.Exists(staging));
            Assert.True(File.Exists(leftover));

            OutputDirectoryManager.TryRemoveMergedStagingFile(context, leftover);
            Assert.False(File.Exists(leftover));

            OutputDirectoryManager.TryRemoveEmptyStagingDirectory(context);
            Assert.False(Directory.Exists(staging));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string CreateTempFile(byte[] contents)
    {
        string path = Path.Combine(Path.GetTempPath(), "assetsplitter-rda-" + Guid.NewGuid().ToString("N") + ".rda");
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "assetsplitter-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool TryOpenExclusive(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            File.Delete(path);
            return !File.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // best effort cleanup
        }
    }
}
