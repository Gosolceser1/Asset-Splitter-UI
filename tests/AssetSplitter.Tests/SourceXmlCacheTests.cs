using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class SourceXmlCacheTests
{
    [Fact]
    public void BuildFingerprint_IsStable_AndIgnoresExecutablePath()
    {
        var a = new GameBuildInfo("1.6.1.1680013", "//Anno8/main", "1680013", "Wed Jun 24 19:41:11 2026", @"C:\Games\Anno\Anno.exe");
        var b = new GameBuildInfo("1.6.1.1680013", "//Anno8/main", "1680013", "Wed Jun 24 19:41:11 2026", @"D:\Other\Anno.exe");

        Assert.Equal(SourceXmlCache.BuildFingerprint(a), SourceXmlCache.BuildFingerprint(b));
        Assert.DoesNotContain("Anno.exe", SourceXmlCache.BuildFingerprint(a), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanReuse_True_WhenStampAndRequiredFilesMatch()
    {
        string root = CreateTempSourceRoot();
        try
        {
            var build = SampleBuild();
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            SourceXmlCache.WriteStamp(root, "anno1800", build);

            Assert.True(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", build, out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.None, reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenStampMissing()
    {
        string root = CreateTempSourceRoot();
        try
        {
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", SampleBuild(), out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.StampMissing, reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenFingerprintMismatch()
    {
        string root = CreateTempSourceRoot();
        try
        {
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            SourceXmlCache.WriteStamp(root, "anno1800", SampleBuild());
            var newer = new GameBuildInfo("9.9.9.9999999", "//Anno8/main", "9999999", "Thu Jan 1 00:00:00 2099", @"C:\Games\Anno\Anno.exe");

            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", newer, out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.BuildChanged, reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenAssetsXmlMissing()
    {
        string root = CreateTempSourceRoot();
        try
        {
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            File.Delete(Path.Combine(root, "assets.xml"));
            SourceXmlCache.WriteStamp(root, "anno1800", SampleBuild());

            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", SampleBuild(), out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.FilesIncomplete, reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenBuildUnknown()
    {
        string root = CreateTempSourceRoot();
        try
        {
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", null, out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.BuildUnknown, reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenSourceFileContentChanged()
    {
        string root = CreateTempSourceRoot();
        try
        {
            var build = SampleBuild();
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            SourceXmlCache.WriteStamp(root, "anno1800", build);

            Assert.True(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", build, out _));

            File.WriteAllText(Path.Combine(root, "assets.xml"), "<root><!-- edited --></root>");
            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", build, out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.ContentChanged, reason);
            Assert.Contains("assets.xml", SourceXmlCache.ListContentChangedPaths(root), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenStampSchemaIsOlder()
    {
        string root = CreateTempSourceRoot();
        try
        {
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            // Schema 1 stamp without contentTreeHash.
            var stamp = new
            {
                schema = 1,
                gameType = "anno1800",
                fingerprint = SourceXmlCache.BuildFingerprint(SampleBuild()),
                display = SampleBuild().ToDisplayString(),
                extractedUtc = "2026-01-01T00:00:00Z",
            };
            File.WriteAllText(
                Path.Combine(root, SourceXmlCache.StampFileName),
                System.Text.Json.JsonSerializer.Serialize(stamp, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", SampleBuild(), out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.StampInvalid, reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void CanReuse_False_WhenRequiredFileCorruptEmpty()
    {
        string root = CreateTempSourceRoot();
        try
        {
            var build = SampleBuild();
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            SourceXmlCache.WriteStamp(root, "anno1800", build);
            File.WriteAllText(Path.Combine(root, "assets.xml"), "");

            Assert.False(SourceXmlCache.CanReuse(root, "anno1800", "texts_english.xml", build, out SourceXmlCache.MissReason reason));
            Assert.Equal(SourceXmlCache.MissReason.FilesCorrupt, reason);
            Assert.False(SourceXmlCache.AreRequiredFilesIntact(root, "anno1800", "texts_english.xml", out string? failed));
            Assert.Equal("assets.xml", failed);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void AreRequiredFilesWellFormed_False_WhenMarkupBroken()
    {
        string root = CreateTempSourceRoot();
        try
        {
            WriteRequiredFiles(root, "anno1800", "texts_english.xml");
            File.WriteAllText(Path.Combine(root, "assets.xml"), "<Groups><Assets></Groups>");

            Assert.True(SourceXmlCache.AreRequiredFilesIntact(root, "anno1800", "texts_english.xml", out _));
            Assert.False(SourceXmlCache.AreRequiredFilesWellFormed(root, "anno1800", "texts_english.xml", out string? failed));
            Assert.Equal("assets.xml", failed);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Phase1_ContentChanged_KeepsOnDiskSource_WithoutRdaWipe()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "as-src-keep-" + Guid.NewGuid().ToString("N"));
        string gameRoot = Path.Combine(sandbox, "AnnoAssets", "Anno1800");
        string sourceXml = Path.Combine(gameRoot, "source_xml_anno1800");
        string fakeGame = Path.Combine(sandbox, "game");
        try
        {
            Directory.CreateDirectory(Path.Combine(fakeGame, "maindata"));
            WriteRequiredFiles(sourceXml, "anno1800", "texts_english.xml");
            File.WriteAllText(Path.Combine(sourceXml, "marker.txt"), "keep-me");
            var build = SampleBuild();
            SourceXmlCache.WriteStamp(sourceXml, "anno1800", build);
            File.WriteAllText(Path.Combine(sourceXml, "assets.xml"), "<Groups><!-- edited --></Groups>");

            var context = new PipelineContext
            {
                AssetRoot = fakeGame,
                BaseOutputDir = Path.Combine(sandbox, "AnnoAssets"),
                GameOutputRoot = gameRoot,
                AnnoAssetsRoot = Path.Combine(sandbox, "AnnoAssets"),
                AssetLanguage = "texts_english.xml",
                DetectedGameBuild = build,
                DetectedGameType = "anno1800",
                NoDefaultProperties = false,
                AddComments = true,
            };

            Assert.Equal(0, Phase1SourceAcquisition.EstimatePhase1PlanUnits(context, "anno1800", forceRefresh: false));
            int exit = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(context, "anno1800", gameRoot, forceRefresh: false);
            Assert.Equal(0, exit);
            Assert.True(File.Exists(Path.Combine(sourceXml, "marker.txt")));
            Assert.Contains("<!-- edited -->", File.ReadAllText(Path.Combine(sourceXml, "assets.xml")), StringComparison.Ordinal);
            Assert.True(SourceXmlCache.CanReuse(sourceXml, "anno1800", "texts_english.xml", build, out _));
        }
        finally
        {
            DeleteTemp(sandbox);
        }
    }

    [Fact]
    public void Phase1_CorruptRequiredFile_DoesNotKeepOnDisk_ForcesRefreshPath()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "as-src-corrupt-" + Guid.NewGuid().ToString("N"));
        string gameRoot = Path.Combine(sandbox, "AnnoAssets", "Anno1800");
        string sourceXml = Path.Combine(gameRoot, "source_xml_anno1800");
        string fakeGame = Path.Combine(sandbox, "game");
        try
        {
            Directory.CreateDirectory(Path.Combine(fakeGame, "maindata"));
            WriteRequiredFiles(sourceXml, "anno1800", "texts_english.xml");
            File.WriteAllText(Path.Combine(sourceXml, "marker.txt"), "keep-me");
            var build = SampleBuild();
            SourceXmlCache.WriteStamp(sourceXml, "anno1800", build);
            // Still starts with '<' so ContentChanged (hash moved) but not well-formed.
            File.WriteAllText(Path.Combine(sourceXml, "assets.xml"), "<Groups><Assets></Groups>");

            var context = new PipelineContext
            {
                AssetRoot = fakeGame,
                BaseOutputDir = Path.Combine(sandbox, "AnnoAssets"),
                GameOutputRoot = gameRoot,
                AnnoAssetsRoot = Path.Combine(sandbox, "AnnoAssets"),
                AssetLanguage = "texts_english.xml",
                DetectedGameBuild = build,
                DetectedGameType = "anno1800",
                NoDefaultProperties = false,
                AddComments = true,
            };

            // Corrupt keep path must not short-circuit to keep-on-disk (estimate would be 0).
            Assert.False(
                SourceXmlCache.AreRequiredFilesWellFormed(sourceXml, "anno1800", "texts_english.xml", out _));
            // No real RDAs under fakeGame — extract/validate fails, but must wipe corrupt source first.
            int exit = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(context, "anno1800", gameRoot, forceRefresh: false);
            Assert.NotEqual(0, exit);
            Assert.False(File.Exists(Path.Combine(sourceXml, "marker.txt")), "Corrupt keep path must wipe/prepare source_xml");
        }
        finally
        {
            DeleteTemp(sandbox);
        }
    }

    [Fact]
    public void Phase1_FullRun_ReusesSourceXml_WithoutDeletingMarker()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "as-src-cache-" + Guid.NewGuid().ToString("N"));
        string gameRoot = Path.Combine(sandbox, "AnnoAssets", "Anno1800");
        string sourceXml = Path.Combine(gameRoot, "source_xml_anno1800");
        string fakeGame = Path.Combine(sandbox, "game");
        try
        {
            Directory.CreateDirectory(fakeGame);
            Directory.CreateDirectory(Path.Combine(fakeGame, "maindata"));
            // Empty data*.rda list path exists so Estimate does not return 0 for missing maindata;
            // reuse path should skip extract entirely.
            WriteRequiredFiles(sourceXml, "anno1800", "texts_english.xml");
            File.WriteAllText(Path.Combine(sourceXml, "marker.txt"), "keep-me");
            var build = SampleBuild();
            SourceXmlCache.WriteStamp(sourceXml, "anno1800", build);

            var context = new PipelineContext
            {
                AssetRoot = fakeGame,
                BaseOutputDir = Path.Combine(sandbox, "AnnoAssets"),
                GameOutputRoot = gameRoot,
                AnnoAssetsRoot = Path.Combine(sandbox, "AnnoAssets"),
                AssetLanguage = "texts_english.xml",
                DetectedGameBuild = build,
                DetectedGameType = "anno1800",
                NoDefaultProperties = false,
                AddComments = true,
            };

            Assert.Equal(0, Phase1SourceAcquisition.EstimatePhase1PlanUnits(context, "anno1800", forceRefresh: false));
            int exit = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(context, "anno1800", gameRoot, forceRefresh: false);
            Assert.Equal(0, exit);
            Assert.True(File.Exists(Path.Combine(sourceXml, "marker.txt")));
            Assert.Contains("source_xml_anno1800", context.SourceXmlFolder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTemp(sandbox);
        }
    }

    [Fact]
    public void Phase1_SourceOnly_ForceRefresh_WipesMarker()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "as-src-force-" + Guid.NewGuid().ToString("N"));
        string gameRoot = Path.Combine(sandbox, "AnnoAssets", "Anno117");
        string sourceXml = Path.Combine(gameRoot, "source_xml_anno117");
        string fakeGame = Path.Combine(sandbox, "game");
        try
        {
            Directory.CreateDirectory(Path.Combine(fakeGame, "maindata"));
            // Minimal config.rda / shared_configs won't exist — extract will fail.
            // So for force wipe test we only assert TryPrepare would wipe: call with force when
            // direct sources unavailable and prepare wipes. Instead simulate: forceExtract path
            // that has no AssetRoot maindata archives — extract throws/fails.
            // Better: use HasDirectSourceFiles false, force true, and ensure prepare deletes marker
            // before extract fails — marker should be gone after prepare even if extract fails.

            WriteRequiredFiles(sourceXml, "anno117", "none");
            File.WriteAllText(Path.Combine(sourceXml, "marker.txt"), "wipe-me");
            var build = SampleBuild();
            SourceXmlCache.WriteStamp(sourceXml, "anno117", build);

            var context = new PipelineContext
            {
                AssetRoot = fakeGame,
                BaseOutputDir = Path.Combine(sandbox, "AnnoAssets"),
                GameOutputRoot = gameRoot,
                AnnoAssetsRoot = Path.Combine(sandbox, "AnnoAssets"),
                AssetLanguage = "none",
                DetectedGameBuild = build,
                DetectedGameType = "anno117",
                // Source-only flags
                NoDefaultProperties = true,
                AddComments = false,
                FixDependencies = false,
                CreateTemplateFolders = false,
                SplitTemplates = false,
                CreateAssetMods = false,
            };

            Assert.True(PipelineFeatureGates.IsSourceExtractionOnlyRun(context));
            int exit = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(context, "anno117", gameRoot, forceRefresh: true);
            // Extract fails without real RDAs, but wipe already happened.
            Assert.False(File.Exists(Path.Combine(sourceXml, "marker.txt")));
            Assert.NotEqual(0, exit);
        }
        finally
        {
            DeleteTemp(sandbox);
        }
    }

    private static GameBuildInfo SampleBuild() =>
        new("1.6.1.1680013", "//Anno8/main", "1680013", "Wed Jun 24 19:41:11 2026", @"C:\Games\Anno\Anno.exe");

    private static string CreateTempSourceRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "as-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteRequiredFiles(string root, string gameType, string language)
    {
        Directory.CreateDirectory(root);
        foreach (string name in SourceXmlCatalog.GetExpectedFileNames(gameType, language))
        {
            File.WriteAllText(Path.Combine(root, name), "<root/>");
        }
    }

    private static void DeleteTemp(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
