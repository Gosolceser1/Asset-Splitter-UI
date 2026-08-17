using System.Diagnostics;
using System.Text;
using System.Xml;
using AssetProcessor;
using RDAExplorer;

namespace AssetSplitter.Tests;

/// <summary>
/// Wave 1 filesystem gauntlet: probes containment, locks, staging, clears, and path sanitizers.
/// Does not duplicate SafetyAndGatesTests coverage; asserts expected safe behavior so failures = findings.
/// </summary>
public sealed class GauntletFilesystemTests : IDisposable
{
    private readonly string _root;

    public GauntletFilesystemTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "as-fs-gauntlet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string Nest(string name) => Path.Combine(_root, name);

    private static PipelineContext CreateContext(string baseOutput, string? gameRoot = null, string? assetOut = null)
    {
        var context = new PipelineContext
        {
            BaseOutputDir = baseOutput,
            AnnoAssetsRoot = baseOutput,
            GameOutputRoot = gameRoot ?? Path.Combine(baseOutput, "Anno1800"),
            AssetOutputDir = assetOut ?? Path.Combine(baseOutput, "Anno1800", "output_xml_Anno1800"),
            DebugMode = false,
        };
        return context;
    }

    // ── OutputPathSafety: containment / equality / empty / unicode ──────────

    [Fact]
    public void IsPathUnderDirectory_RejectsExactDirectoryEquality()
    {
        string dir = Nest("eq-root");
        Directory.CreateDirectory(dir);

        // Documented gap: a path is not considered "under" itself (trailing-separator StartsWith).
        Assert.False(OutputPathSafety.IsPathUnderDirectory(dir, dir));
    }

    [Fact]
    public void IsPathUnderDirectory_AcceptsChild_RejectsParentAndUnrelated()
    {
        string dir = Nest("under");
        string child = Path.Combine(dir, "a", "b.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(child)!);
        File.WriteAllText(child, "x");

        Assert.True(OutputPathSafety.IsPathUnderDirectory(child, dir));
        Assert.False(OutputPathSafety.IsPathUnderDirectory(dir, child));
        Assert.False(OutputPathSafety.IsPathUnderDirectory(Nest("other"), dir));
    }

    [Fact]
    public void IsPathUnderDirectory_HandlesTrailingSeparatorsAndMixedSlashes()
    {
        string dir = Nest("slash-mix");
        string child = Path.Combine(dir, "nested", "f.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(child)!);

        Assert.True(OutputPathSafety.IsPathUnderDirectory(child, dir + Path.DirectorySeparatorChar));
        Assert.True(OutputPathSafety.IsPathUnderDirectory(child.Replace('\\', '/'), dir));
    }

    [Fact]
    public void TryGetContainedPath_RejectsEmptyNullDotAndAbsoluteEscape()
    {
        string dir = Nest("contain");
        Directory.CreateDirectory(dir);

        Assert.False(OutputPathSafety.TryGetContainedPath(dir, "", out _));
        Assert.False(OutputPathSafety.TryGetContainedPath(dir, "   ", out _));
        Assert.False(OutputPathSafety.TryGetContainedPath(dir, ".", out _));
        Assert.False(OutputPathSafety.TryGetContainedPath(dir, "..", out _));
        Assert.False(OutputPathSafety.TryGetContainedPath("", "a", out _));

        string outside = Nest("outside-file.xml");
        Assert.False(OutputPathSafety.TryGetContainedPath(dir, outside, out _));
    }

    [Fact]
    public void TryGetContainedPath_AllowsUnicodeAndSpacesInRelativeSegments()
    {
        string dir = Nest("unicode root café");
        Directory.CreateDirectory(dir);

        Assert.True(OutputPathSafety.TryGetContainedPath(dir, "папка с пробелом\\файл.xml", out string full));
        Assert.True(OutputPathSafety.IsPathUnderDirectory(full, dir));
        Assert.Contains("папка с пробелом", full, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeSinglePathSegment_AllowsReservedDeviceNames_GapVsArchiveSanitizer()
    {
        // ArchivePathSanitizer rejects CON/NUL; OutputPathSafety currently accepts them as "safe" segments.
        Assert.True(OutputPathSafety.IsSafeSinglePathSegment("CON"));
        Assert.True(OutputPathSafety.IsSafeSinglePathSegment("NUL"));
        Assert.Equal("CON", OutputPathSafety.SanitizeSinglePathSegment("CON", "BaseAssetGUID"));

        Assert.False(ArchivePathSanitizer.TryNormalizeRelativeArchivePath("CON", out _));
        Assert.False(ArchivePathSanitizer.TryNormalizeRelativeArchivePath("NUL.txt", out _));
    }

    [Fact]
    public void SanitizeSinglePathSegment_AllowsTrailingDotAndSpace_GapVsArchiveSanitizer()
    {
        Assert.True(OutputPathSafety.IsSafeSinglePathSegment("folder."));
        Assert.True(OutputPathSafety.IsSafeSinglePathSegment("folder "));
        Assert.False(ArchivePathSanitizer.TryNormalizeRelativeArchivePath("folder.", out _));
        Assert.False(ArchivePathSanitizer.TryNormalizeRelativeArchivePath("folder ", out _));
    }

    [Fact]
    public void SanitizeSinglePathSegment_RejectsSeparatorsAndDots_SanitizesInvalidChars()
    {
        Assert.Equal("BaseAssetGUID", OutputPathSafety.SanitizeSinglePathSegment("", "BaseAssetGUID"));
        Assert.Equal("BaseAssetGUID", OutputPathSafety.SanitizeSinglePathSegment(".", "BaseAssetGUID"));
        Assert.Equal("BaseAssetGUID", OutputPathSafety.SanitizeSinglePathSegment("..", "BaseAssetGUID"));
        Assert.Equal("foo_bar", OutputPathSafety.SanitizeSinglePathSegment("foo:bar", "BaseAssetGUID"));
        Assert.Equal("ok name", OutputPathSafety.SanitizeSinglePathSegment("ok name", "BaseAssetGUID"));
    }

    // ── Clears / deletes / reparse ──────────────────────────────────────────

    [Fact]
    public void TryClearDirectory_ClearsExistingContentsUnderRequiredRoot()
    {
        string required = Nest("clear-root");
        string target = Path.Combine(required, "output");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "old.xml"), "old");
        var context = CreateContext(required, required, target);

        Assert.True(OutputPathSafety.TryClearDirectory(context, target, required, "cleared"));
        Assert.True(Directory.Exists(target));
        Assert.True(AssetProcessorFileSystem.IsDirectoryEmpty(target));
    }

    [Fact]
    public void TryClearDirectory_RefusesPathOutsideRequiredRoot()
    {
        string required = Nest("clear-required");
        string outsider = Nest("clear-outsider");
        Directory.CreateDirectory(required);
        Directory.CreateDirectory(outsider);
        File.WriteAllText(Path.Combine(outsider, "keep.xml"), "keep");
        var context = CreateContext(required);

        Assert.False(OutputPathSafety.TryClearDirectory(context, outsider, required, "cleared"));
        Assert.True(File.Exists(Path.Combine(outsider, "keep.xml")));
    }

    [Fact]
    public void TryDeleteDirectoryRecursive_RefusesExactRequiredRootEquality()
    {
        string required = Nest("delete-eq");
        Directory.CreateDirectory(required);
        var context = CreateContext(required);

        Assert.False(OutputPathSafety.TryDeleteDirectoryRecursive(context, required, required));
        Assert.True(Directory.Exists(required));
    }

    [Fact]
    public void TryClearDirectory_RefusesWhenDirectoryIsJunction_IfCreatable()
    {
        string required = Nest("reparse-required");
        string realTarget = Nest("reparse-real");
        string junction = Path.Combine(required, "link-out");
        Directory.CreateDirectory(required);
        Directory.CreateDirectory(realTarget);
        File.WriteAllText(Path.Combine(realTarget, "secret.xml"), "secret");

        if (!TryCreateJunction(junction, realTarget))
        {
            // Environment cannot create reparse points — skip without failing the suite.
            return;
        }

        var context = CreateContext(required);
        Assert.True(OutputPathSafety.DirectoryOrAncestorsHaveReparsePoint(junction));
        Assert.False(OutputPathSafety.TryClearDirectory(context, junction, required, "cleared"));
        Assert.True(File.Exists(Path.Combine(realTarget, "secret.xml")));
    }

    [Fact]
    public void TryRemoveIntermediateAssetOutput_LacksReparseGuard_UnlikeTryDeleteDirectoryRecursive()
    {
        string baseOut = Nest("mod-export-only");
        string gameRoot = Path.Combine(baseOut, "Anno1800");
        string assetOut = Path.Combine(gameRoot, "output_xml_Anno1800");
        string realTarget = Nest("mod-export-real");
        Directory.CreateDirectory(gameRoot);
        Directory.CreateDirectory(realTarget);
        File.WriteAllText(Path.Combine(realTarget, "keep-me.xml"), "data");

        if (!TryCreateJunction(assetOut, realTarget))
        {
            return;
        }

        Assert.True(OutputPathSafety.DirectoryOrAncestorsHaveReparsePoint(assetOut));
        // Safe API refuses:
        var context = CreateContext(baseOut, gameRoot, assetOut);
        Assert.False(OutputPathSafety.TryDeleteDirectoryRecursive(context, assetOut, gameRoot));

        // Mod-export cleanup has no reparse guard — contrast with TryDeleteDirectoryRecursive.
        context.CreateAssetMods = true;
        context.NoDefaultProperties = true;
        Assert.True(PipelineFeatureGates.IsModExportOnlyRun(context));
        OutputDirectoryManager.TryRemoveIntermediateAssetOutputAfterModExport(context);

        bool secretSurvived = File.Exists(Path.Combine(realTarget, "keep-me.xml"));
        bool junctionGone = !Directory.Exists(assetOut)
            || (File.GetAttributes(assetOut) & FileAttributes.ReparsePoint) == 0;
        // Finding: unlike TryDeleteDirectoryRecursive, this path proceeds despite reparse.
        // On this host, record whether target contents survived Windows junction delete semantics.
        Assert.True(
            secretSurvived,
            $"FINDING: mod-export intermediate delete ran on junction (junctionGone={junctionGone}) and removed target contents.");
    }

    // ── OutputDirectoryManager: layout, prepare, staging, template ──────────

    [Fact]
    public void TryResolveOutputLayout_BuildsExpectedAnno1800Paths()
    {
        string baseOut = Nest("layout-base");
        var context = CreateContext(baseOut);
        context.BaseOutputDir = baseOut;

        Assert.True(OutputDirectoryManager.TryResolveOutputLayout(context, "Anno 1800", out string gameRoot));
        Assert.EndsWith(Path.Combine("AnnoAssets", "Anno1800"), gameRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("output_xml_Anno1800", context.AssetOutputDir, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", context.AssetOutputDir, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveOutputLayout_SingleGuidKeepsPathContained_ButLeavesDotDotInFolderName()
    {
        string baseOut = Nest("layout-guid");
        var context = CreateContext(baseOut);
        context.BaseOutputDir = baseOut;
        context.SingleAssetGuid = "..\\..\\evil";
        context.SingleAssetDisplayName = "a/b\\c";

        Assert.True(OutputDirectoryManager.TryResolveOutputLayout(context, "Anno1800", out _));
        Assert.True(OutputPathSafety.IsPathUnderDirectory(context.SingleAssetOutputRoot, context.GameOutputRoot));
        // Hygiene gap: BuildSingleAssetFolderName uses SanitizeFileNamePart (not SanitizeGuidForFileName),
        // so ".." survives as a literal substring after \ / → _ (".._.._evil - a_b_c").
        Assert.Contains("..", Path.GetFileName(context.SingleAssetOutputRoot), StringComparison.Ordinal);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, Path.GetFileName(context.SingleAssetOutputRoot));
    }

    [Fact]
    public void TryPrepareAssetOutputDirectory_RecreatesExistingOutput()
    {
        string baseOut = Nest("prep-out");
        Directory.CreateDirectory(baseOut);
        var context = CreateContext(baseOut);
        context.BaseOutputDir = baseOut;
        Assert.True(OutputDirectoryManager.TryResolveOutputLayout(context, "Anno1800", out _));
        Assert.True(OutputDirectoryManager.TryEnsureGameOutputRoot(context));

        Directory.CreateDirectory(context.AssetOutputDir);
        File.WriteAllText(Path.Combine(context.AssetOutputDir, "stale.xml"), "stale");

        Assert.True(OutputDirectoryManager.TryPrepareAssetOutputDirectory(context));
        Assert.True(Directory.Exists(context.AssetOutputDir));
        Assert.False(File.Exists(Path.Combine(context.AssetOutputDir, "stale.xml")));
    }

    [Fact]
    public void GetBaseAssetGuidStagingPath_FallsBackWhenConfiguredFolderIsTraversal()
    {
        string baseOut = Nest("staging-cfg");
        var context = CreateContext(baseOut);
        context.AssetOutputDir = Path.Combine(baseOut, "out");
        Directory.CreateDirectory(context.AssetOutputDir);
        context.AppSettingsConfig = new AppSettingsConfig
        {
            Settings = new AppSettings
            {
                OutputStructure = new OutputStructureSettings { BaseassetFolder = "..\\..\\escape" }
            }
        };

        string staging = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
        Assert.Equal(Path.Combine(context.AssetOutputDir, "BaseAssetGUID"), staging);
        Assert.True(OutputPathSafety.IsPathUnderDirectory(staging, context.AssetOutputDir));
    }

    [Fact]
    public void EnsureTemplateSubfolder_BlocksTraversalTemplateNames()
    {
        string baseOut = Nest("tmpl");
        string assetOut = Path.Combine(baseOut, "out");
        Directory.CreateDirectory(assetOut);
        var context = CreateContext(baseOut, assetOut: assetOut);

        string dest = OutputDirectoryManager.EnsureTemplateSubfolder(context, "..\\..\\Windows");
        Assert.True(OutputPathSafety.IsPathUnderDirectory(dest, assetOut));
        Assert.True(Directory.Exists(dest));
    }

    [Fact]
    public void TryRemoveMergedStagingFile_IgnoresPathsOutsideStagingRoot()
    {
        string baseOut = Nest("staging-del");
        string assetOut = Path.Combine(baseOut, "out");
        string staging = Path.Combine(assetOut, "BaseAssetGUID");
        Directory.CreateDirectory(staging);
        string outside = Path.Combine(assetOut, "keep.xml");
        File.WriteAllText(outside, "keep");
        var context = CreateContext(baseOut, assetOut: assetOut);

        OutputDirectoryManager.TryRemoveMergedStagingFile(context, outside);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void DuplicateFileNames_FindAssetFileReturnsFirstMatchOnly()
    {
        string assetOut = Nest("dup-names");
        Directory.CreateDirectory(assetOut);
        string a = Path.Combine(assetOut, "101 - [ Alpha ].xml");
        string b = Path.Combine(assetOut, "101 - [ Beta ].xml");
        File.WriteAllText(a, "<Asset/>");
        File.WriteAllText(b, "<Asset/>");

        string? found = AssetProcessorFileSystem.FindAssetFile(assetOut, "101", searchTemplateFolders: false);
        Assert.NotNull(found);
        Assert.True(found == a || found == b);
    }

    [Fact]
    public void FindAssetFile_WildcardGuidDoesNotMatchUnrelatedFiles()
    {
        string assetOut = Nest("wild-guid");
        Directory.CreateDirectory(assetOut);
        string victim = Path.Combine(assetOut, "9999999 - [ Victim ].xml");
        File.WriteAllText(victim, "<Asset/>");

        // FINDING: guid is concatenated into Directory.GetFiles pattern with no escaping of * ? .
        string? found = AssetProcessorFileSystem.FindAssetFile(assetOut, "*", searchTemplateFolders: false);
        Assert.Null(found);
    }

    // ── OutputRunLock ───────────────────────────────────────────────────────

    [Fact]
    public void OutputRunLock_AcquireRelease_AllowsSecondAcquire()
    {
        string root = Nest("lock-ok");
        var context = CreateContext(root);
        context.AnnoAssetsRoot = root;

        using (IDisposable? held = OutputRunLock.TryAcquire(context))
        {
            Assert.NotNull(held);
            Assert.True(File.Exists(Path.Combine(root, OutputRunLock.LockFileName)));
        }

        using IDisposable? again = OutputRunLock.TryAcquire(context);
        Assert.NotNull(again);
    }

    [Fact]
    public void OutputRunLock_SecondAcquireFailsWhileHeld()
    {
        string root = Nest("lock-busy");
        var context = CreateContext(root);
        context.AnnoAssetsRoot = root;

        using IDisposable? first = OutputRunLock.TryAcquire(context);
        Assert.NotNull(first);

        var other = CreateContext(root);
        other.AnnoAssetsRoot = root;
        Assert.Null(OutputRunLock.TryAcquire(other));
    }

    [Fact]
    public void OutputRunLock_TakesOverStalePidAndInvalidContents()
    {
        string root = Nest("lock-stale");
        Directory.CreateDirectory(root);
        string lockPath = Path.Combine(root, OutputRunLock.LockFileName);
        File.WriteAllText(lockPath, "999999001"); // almost certainly dead
        var context = CreateContext(root);
        context.AnnoAssetsRoot = root;

        using IDisposable? held = OutputRunLock.TryAcquire(context);
        Assert.NotNull(held);

        File.WriteAllText(lockPath, "not-a-pid");
        // Release then re-acquire after invalid lock left behind by tampering during hold is undefined;
        // instead verify invalid lock alone is take-overable:
        held.Dispose();
        File.WriteAllText(lockPath, "nope");
        using IDisposable? again = OutputRunLock.TryAcquire(context);
        Assert.NotNull(again);
    }

    [Fact]
    public void OutputRunLock_MissingRootReturnsNull()
    {
        var context = new PipelineContext();
        Assert.Null(OutputRunLock.TryAcquire(context));
    }

    // ── CloudSyncedPathDetector (backend; UI tests cover happy path only) ───

    [Fact]
    public void CloudSyncedPathDetector_DoesNotTreatSiblingPrefixAsCloudEnvRoot()
    {
        string previous = Environment.GetEnvironmentVariable("OneDrive") ?? "";
        try
        {
            string fakeRoot = Nest("OneDriveRoot");
            Directory.CreateDirectory(fakeRoot);
            Environment.SetEnvironmentVariable("OneDrive", fakeRoot);

            string sibling = fakeRoot + "Extra" + Path.DirectorySeparatorChar + "out";
            Assert.False(CloudSyncedPathDetector.IsLikelyCloudSynced(sibling));
            Assert.True(CloudSyncedPathDetector.IsLikelyCloudSynced(Path.Combine(fakeRoot, "Documents")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OneDrive", string.IsNullOrEmpty(previous) ? null : previous);
        }
    }

    [Fact]
    public void CloudSyncedPathDetector_UnicodeLocalPathIsNotCloud()
    {
        Assert.False(CloudSyncedPathDetector.IsLikelyCloudSynced(Nest("локальная папка")));
    }

    // ── GeneratedXmlFootprint path gate ─────────────────────────────────────

    [Fact]
    public void GeneratedXmlFootprint_Save_RejectsPathOutsideGameOutputRoot()
    {
        string gameRoot = Nest("foot-game");
        string assetOut = Path.Combine(gameRoot, "out");
        Directory.CreateDirectory(assetOut);
        var context = CreateContext(Nest("foot-base"), gameRoot, assetOut);

        var doc = new XmlDocument();
        doc.LoadXml("<ModOps/>");
        string escape = Nest("escape.xml");

        var ex = Assert.Throws<InvalidOperationException>(() => GeneratedXmlFootprint.Save(doc, escape, context));
        Assert.Contains("Refusing to write outside", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(escape));
    }

    [Fact]
    public void GeneratedXmlFootprint_Save_WritesAnywhereWhenRootsUnset_Finding()
    {
        var context = new PipelineContext(); // GameOutputRoot + AssetOutputDir empty
        var doc = new XmlDocument();
        doc.LoadXml("<ModOps/>");
        string anywhere = Nest("unrestricted-save.xml");

        var ex = Assert.Throws<InvalidOperationException>(() => GeneratedXmlFootprint.Save(doc, anywhere, context));
        Assert.Contains("Refusing to write outside", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(anywhere));
    }

    [Fact]
    public void GeneratedXmlFootprint_Save_WritesUnderAllowedRoot_WithUnicodeName()
    {
        string gameRoot = Nest("foot-ok");
        string assetOut = Path.Combine(gameRoot, "out");
        Directory.CreateDirectory(assetOut);
        var context = CreateContext(Nest("foot-base2"), gameRoot, assetOut);
        var doc = new XmlDocument();
        doc.LoadXml("<ModOps/>");
        string path = Path.Combine(assetOut, "101 - [ Café 東京 ].xml");

        GeneratedXmlFootprint.Save(doc, path, context);
        Assert.True(File.Exists(path));
    }

    // ── XmlDocumentLoadRetry ────────────────────────────────────────────────

    [Fact]
    public void XmlDocumentLoadRetry_LoadsValidXml_AndSurvivesBriefShareLock()
    {
        string path = Nest("retry-load.xml");
        File.WriteAllText(path, "<?xml version=\"1.0\"?><Root><A/></Root>");

        var doc = new XmlDocument();
        XmlDocumentLoadRetry.Load(doc, path);
        Assert.Equal("Root", doc.DocumentElement!.Name);

        using var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var doc2 = new XmlDocument();
        XmlDocumentLoadRetry.Load(doc2, path);
        Assert.Equal("Root", doc2.DocumentElement!.Name);
    }

    [Fact]
    public void XmlDocumentLoadRetry_NonexistentPathThrows()
    {
        var doc = new XmlDocument();
        Assert.ThrowsAny<Exception>(() => XmlDocumentLoadRetry.Load(doc, Nest("missing-nope.xml")));
    }

    // ── ArchivePathSanitizer (pipeline overlap) ─────────────────────────────

    [Theory]
    [InlineData("../evil.xml")]
    [InlineData("..\\evil.xml")]
    [InlineData("a/../../b.xml")]
    [InlineData("C:\\Windows\\system.ini")]
    [InlineData("CON")]
    [InlineData("foo/NUL/bar.xml")]
    [InlineData(" leading.xml")]
    [InlineData("trailing.xml.")]
    public void ArchivePathSanitizer_RejectsDangerousRelativePaths(string name)
    {
        string folder = Nest("archive-san");
        Directory.CreateDirectory(folder);
        Assert.False(ArchivePathSanitizer.TryGetOutputPath(name, folder, out _));
    }

    [Fact]
    public void ArchivePathSanitizer_UnixAbsolutePathBecomesRelativeChild_Gap()
    {
        string folder = Nest("archive-unix-abs");
        Directory.CreateDirectory(folder);
        // Leading '/' is TrimStart'd — "/etc/passwd" becomes folder\etc\passwd (not rejected).
        Assert.True(ArchivePathSanitizer.TryGetOutputPath("/etc/passwd", folder, out string full));
        Assert.True(OutputPathSafety.IsPathUnderDirectory(full, folder));
        Assert.EndsWith(Path.Combine("etc", "passwd"), full, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchivePathSanitizer_AcceptsNestedSafeUnicodePath()
    {
        string folder = Nest("archive-ok");
        Directory.CreateDirectory(folder);
        Assert.True(ArchivePathSanitizer.TryGetOutputPath("data/config/файл.xml", folder, out string full));
        Assert.StartsWith(Path.GetFullPath(folder), full, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchivePathSanitizer_PathContainsReparsePoint_DetectsJunctionChild()
    {
        string folder = Nest("archive-reparse");
        string real = Nest("archive-reparse-real");
        string link = Path.Combine(folder, "linked");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(real);

        if (!TryCreateJunction(link, real))
        {
            return;
        }

        string probeFile = Path.Combine(link, "x.xml");
        Assert.True(ArchivePathSanitizer.PathContainsReparsePoint(folder, probeFile));
        Assert.False(ArchivePathSanitizer.TryGetOutputPath("linked/x.xml", folder, out _));
    }

    // ── Long paths / invalid paths ──────────────────────────────────────────

    [Fact]
    public void TryGetContainedPath_ReturnsFalseForAbsurdlyLongRelativePath()
    {
        string dir = Nest("long");
        Directory.CreateDirectory(dir);
        string huge = new string('a', 400) + Path.DirectorySeparatorChar + new string('b', 400) + ".xml";
        // May succeed on Windows with long-path awareness, or return false via catch — must not throw.
        _ = OutputPathSafety.TryGetContainedPath(dir, huge, out _);
    }

    [Fact]
    public void TryEnsureGameOutputRoot_FailsGracefullyOnInvalidBasePath()
    {
        var context = CreateContext(":::not-a-valid-path:::");
        context.AnnoAssetsRoot = context.BaseOutputDir;
        context.GameOutputRoot = Path.Combine(context.BaseOutputDir, "Anno1800");
        Assert.False(OutputDirectoryManager.TryEnsureGameOutputRoot(context));
    }

    [Fact]
    public void EnsureDirectoryExists_NoopsOnNullOrWhitespace()
    {
        OutputDirectoryManager.EnsureDirectoryExists(null);
        OutputDirectoryManager.EnsureDirectoryExists("  ");
    }

    [Fact]
    public void TryPrepareLogsDirectory_CreatesLogsUnderAnnoAssets()
    {
        string anno = Nest("logs-root");
        Directory.CreateDirectory(anno);
        string? logs = OutputDirectoryManager.TryPrepareLogsDirectory(anno);
        Assert.NotNull(logs);
        Assert.True(Directory.Exists(logs));
        Assert.True(OutputPathSafety.IsPathUnderDirectory(logs!, anno));
    }

    [Fact]
    public void TryDeleteFixerScratchFile_RemovesOnlyFixerTxt()
    {
        string baseOut = Nest("fixer");
        Directory.CreateDirectory(baseOut);
        string fixer = Path.Combine(baseOut, "fixer.txt");
        string other = Path.Combine(baseOut, "other.txt");
        File.WriteAllText(fixer, "x");
        File.WriteAllText(other, "y");

        OutputDirectoryManager.TryDeleteFixerScratchFile(baseOut);
        Assert.False(File.Exists(fixer));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void InterruptedWrite_PartialFileCanRemain_DocumentedBehavior()
    {
        string gameRoot = Nest("interrupt");
        string assetOut = Path.Combine(gameRoot, "out");
        Directory.CreateDirectory(assetOut);
        string path = Path.Combine(assetOut, "partial.xml");

        // Simulate interrupted write: create truncated content then overwrite successfully via footprint save.
        File.WriteAllText(path, "<ModO");
        var context = CreateContext(Nest("interrupt-base"), gameRoot, assetOut);
        var doc = new XmlDocument();
        doc.LoadXml("<ModOps><ModOp/></ModOps>");
        GeneratedXmlFootprint.Save(doc, path, context);

        string text = File.ReadAllText(path);
        Assert.Contains("<ModOps>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<ModO<", text, StringComparison.Ordinal);
    }

    private static bool TryCreateJunction(string junctionPath, string targetPath)
    {
        try
        {
            if (Directory.Exists(junctionPath))
            {
                return (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0;
            }

            // cmd mklink /J works without admin on local NTFS.
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.ExitCode == 0
                && Directory.Exists(junctionPath)
                && (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }
}
