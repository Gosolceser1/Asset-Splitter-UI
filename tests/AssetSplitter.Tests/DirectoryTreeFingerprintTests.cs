using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class DirectoryTreeFingerprintTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(50, 100, false)]
    [InlineData(100, 250, true)]
    [InlineData(500, 10_000, true)]
    [InlineData(250, 10_000, false)]
    [InlineData(3, 4, true)]
    public void ShouldReportHashProgress_UsesFirstLastAndInterval(int processed, int total, bool expected)
    {
        Assert.Equal(expected, DirectoryTreeFingerprint.ShouldReportHashProgress(processed, total));
    }

    [Fact]
    public void TryComputeFileHashes_ReportsProgress_AndDoesNotHang_OnSmallFixture()
    {
        string root = CreateTempDir();
        try
        {
            for (int i = 0; i < 12; i++)
            {
                File.WriteAllText(Path.Combine(root, $"f{i}.txt"), $"content-{i}");
            }

            var ticks = new List<(int Processed, int Total)>();
            Dictionary<string, string>? map = DirectoryTreeFingerprint.TryComputeFileHashes(
                root,
                (processed, total) => ticks.Add((processed, total)));

            Assert.NotNull(map);
            Assert.Equal(12, map!.Count);
            Assert.Contains(ticks, t => t.Processed == 0 && t.Total == 12);
            Assert.Contains(ticks, t => t.Processed == 12 && t.Total == 12);
            Assert.True(ticks.Count >= 2, "expected start and end progress ticks");

            string digest = DirectoryTreeFingerprint.DigestFromFileHashes(map);
            Assert.Equal(64, digest.Length);
            Assert.Equal(digest, DirectoryTreeFingerprint.TryCompute(root));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void TryDigestSubtree_MatchesDirectModPackageFingerprint()
    {
        string modsRoot = CreateTempDir();
        try
        {
            string modA = Path.Combine(modsRoot, "1001 - Alpha");
            string modB = Path.Combine(modsRoot, "1002 - Beta");
            Directory.CreateDirectory(Path.Combine(modA, "data"));
            Directory.CreateDirectory(Path.Combine(modB, "data"));
            File.WriteAllText(Path.Combine(modA, "modinfo.json"), "{\"id\":1}");
            File.WriteAllText(Path.Combine(modA, "data", "assets.xml"), "<A/>");
            File.WriteAllText(Path.Combine(modB, "modinfo.json"), "{\"id\":2}");
            File.WriteAllText(Path.Combine(modB, "data", "assets.xml"), "<B/>");

            Dictionary<string, string>? tree = DirectoryTreeFingerprint.TryComputeFileHashes(modsRoot);
            Assert.NotNull(tree);

            string? fromTree = DirectoryTreeFingerprint.TryDigestSubtree(tree!, "1001 - Alpha");
            string? direct = DirectoryTreeFingerprint.TryCompute(modA);
            Assert.False(string.IsNullOrWhiteSpace(fromTree));
            Assert.Equal(direct, fromTree);
            Assert.NotEqual(
                fromTree,
                DirectoryTreeFingerprint.TryDigestSubtree(tree!, "1002 - Beta"));
        }
        finally
        {
            TryDelete(modsRoot);
        }
    }

    [Fact]
    public void ListRelativePaths_DoesNotRequireHashing()
    {
        string root = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "a.xml"), "<a/>");
            File.WriteAllText(Path.Combine(root, "skip.stamp"), "x");
            List<string> paths = DirectoryTreeFingerprint.ListRelativePaths(root, "skip.stamp");
            Assert.Equal(["a.xml"], paths);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void BuildInventoryFromDisk_ReusesPrecomputedTreeHashes()
    {
        string root = Path.Combine(Path.GetTempPath(), "as-inv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var context = new PipelineContext
            {
                GameOutputRoot = root,
                AssetOutputDir = Path.Combine(root, "output_xml_anno1800"),
                AssetModOutputRoot = Path.Combine(root, "output_xml_anno1800_mods"),
                CreateAssetMods = true,
                DetectedGameType = "anno1800",
            };
            context.ProgressReporter.Initialize(context);
            Directory.CreateDirectory(context.AssetOutputDir);
            Directory.CreateDirectory(context.AssetModOutputRoot);

            string xmlName = "1001 - [ Test ].xml";
            string xmlFull = Path.Combine(context.AssetOutputDir, xmlName);
            File.WriteAllText(xmlFull, "<ModOps><ModOp GUID=\"1001\"/></ModOps>");

            string modDir = Path.Combine(context.AssetModOutputRoot, "1001 - Test");
            Directory.CreateDirectory(Path.Combine(modDir, "data", "config", "export", "main", "asset"));
            File.WriteAllText(Path.Combine(modDir, "modinfo.json"), "{}");
            File.WriteAllText(
                Path.Combine(modDir, "data", "config", "export", "main", "asset", "assets.xml"),
                "<Asset/>");

            var stamp = new OutputRunCache.StampDocument();
            OutputRunCache.StampTreeHashMaps maps = OutputRunCache.PopulateStampTreeFingerprints(
                context,
                "anno1800",
                stamp);

            Assert.False(string.IsNullOrWhiteSpace(stamp.OutputXmlTreeHash));
            Assert.False(string.IsNullOrWhiteSpace(stamp.OutputModsTreeHash));

            List<OutputRunCache.InventoryEntry> withReuse = OutputRunCache.BuildInventoryFromDisk(
                context,
                sourceHashes: null,
                gameTypeOverride: null,
                maps);
            List<OutputRunCache.InventoryEntry> fresh = OutputRunCache.BuildInventoryFromDisk(context);

            OutputRunCache.InventoryEntry reused = Assert.Single(withReuse);
            OutputRunCache.InventoryEntry direct = Assert.Single(fresh);
            Assert.Equal(direct.OutputHash, reused.OutputHash);
            Assert.Equal(direct.ModOutputHash, reused.ModOutputHash);
            Assert.Equal(64, reused.OutputHash.Length);
            Assert.Equal(64, reused.ModOutputHash.Length);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "as-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
