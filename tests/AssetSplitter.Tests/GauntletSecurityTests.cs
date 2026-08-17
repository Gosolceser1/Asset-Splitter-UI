using AssetProcessor;
using RDAExplorer;

namespace AssetSplitter.Tests;

/// <summary>
/// Wave 1 defensive security gauntlet: safe PoCs that demonstrate failures (or document controls).
/// Does not touch production machines outside temp directories.
/// </summary>
public sealed class GauntletSecurityTests : IDisposable
{
    private readonly string _sandbox;

    public GauntletSecurityTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "assetsplitter-gauntlet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try
        {
            if (!Directory.Exists(_sandbox))
            {
                return;
            }

            // Junctions must be removed non-recursively or Directory.Delete(recursive) can fail on Windows.
            foreach (string path in Directory.EnumerateFileSystemEntries(_sandbox, "*", SearchOption.AllDirectories)
                .OrderByDescending(static p => p.Length))
            {
                try
                {
                    if (Directory.Exists(path)
                        && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        Directory.Delete(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            Directory.Delete(_sandbox, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // -------------------------------------------------------------------------
    // Finding: SanitizeFileNamePart leaves ".." intact → Path.Combine escapes
    // -------------------------------------------------------------------------

    [Fact]
    public void SanitizeFileNamePart_MustNotPreserveDotDotAsPathSegment()
    {
        // Secure expectation: ".." must not survive as a Path.Combine parent segment.
        // Currently fails — SanitizeFileNamePart leaves ".." unchanged.
        string sanitized = AssetTextSanitizer.SanitizeFileNamePart("..", 90);
        Assert.False(
            sanitized is "." or "..",
            $"SanitizeFileNamePart(\"..\") returned '{sanitized}', which Path.Combine treats as parent.");

        string modRoot = Path.Combine(_sandbox, "mods");
        Directory.CreateDirectory(modRoot);
        string combined = Path.GetFullPath(Path.Combine(modRoot, sanitized, "1010017 - Coins"));
        string modsPrefix = Path.GetFullPath(modRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        Assert.True(
            combined.StartsWith(modsPrefix, StringComparison.OrdinalIgnoreCase),
            "Sanitized template segment must stay under the mod output root.");
    }

    [Fact]
    public void AssetModPackageExporter_TemplateDotDot_WritesOutsideModOutputRoot()
    {
        string gameRoot = Path.Combine(_sandbox, "AnnoAssets", "Anno1800");
        string assetOut = Path.Combine(gameRoot, "output_xml");
        string modOut = Path.Combine(gameRoot, "output_mods");
        Directory.CreateDirectory(assetOut);
        Directory.CreateDirectory(modOut);

        string sourceXml = Path.Combine(assetOut, "1010017 - [ Coins ].xml");
        File.WriteAllText(sourceXml, """
            <?xml version="1.0" encoding="utf-8"?>
            <Asset>
              <Template>..</Template>
              <Values>
                <Standard>
                  <GUID>1010017</GUID>
                  <Name>Coins</Name>
                </Standard>
              </Values>
            </Asset>
            """);

        var context = new PipelineContext
        {
            AssetOutputDir = assetOut,
            AssetModOutputRoot = modOut,
            GameOutputRoot = gameRoot,
            AnnoAssetsRoot = Path.Combine(_sandbox, "AnnoAssets"),
            BaseOutputDir = _sandbox,
            CreateTemplateFolders = true,
            CreateAssetMods = true,
            AssetLanguage = "none",
            ReadmeLanguage = "english",
        };
        context.ProgressReporter.Initialize(context);

        AssetModPackageExportResult result = AssetModPackageExporter.Export(context, "anno1800");
        Assert.True(result.Prepared);
        Assert.True(result.CreatedCount >= 1, "Expected at least one mod package to be created.");

        // Escaped destination would be gameRoot/<guid> - Coins/ (sibling of output_mods).
        string escapedModDir = Path.Combine(gameRoot, "1010017 - Coins");
        Assert.False(
            Directory.Exists(escapedModDir),
            "Template \"..\" must not create a mod folder beside output_mods.");
        Assert.False(
            File.Exists(Path.Combine(gameRoot, "INDEX.md")),
            "Template \"..\" must not write INDEX.md at the game output root.");

        string modsPrefix = Path.GetFullPath(modOut.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        string assetPrefix = Path.GetFullPath(assetOut.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        string[] writtenUnderGame = Directory.Exists(gameRoot)
            ? Directory.GetFileSystemEntries(gameRoot, "*", SearchOption.AllDirectories)
            : [];
        string[] outsiders = writtenUnderGame.Where(path =>
        {
            string full = Path.GetFullPath(path);
            if (full.Equals(Path.GetFullPath(modOut), StringComparison.OrdinalIgnoreCase)
                || full.Equals(Path.GetFullPath(assetOut), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !full.StartsWith(modsPrefix, StringComparison.OrdinalIgnoreCase)
                && !full.StartsWith(assetPrefix, StringComparison.OrdinalIgnoreCase);
        }).ToArray();

        Assert.True(
            outsiders.Length == 0,
            "Template \"..\" with CreateTemplateFolders must not write outside AssetModOutputRoot. "
            + "outsiders=" + string.Join("; ", outsiders));
    }

    [Fact]
    public void EnsureTemplateSubfolder_BlocksDotDot_UnlikeModPackageExporter()
    {
        string assetOut = Path.Combine(_sandbox, "template-contrast-out");
        Directory.CreateDirectory(assetOut);
        var context = new PipelineContext { AssetOutputDir = assetOut };

        string dest = OutputDirectoryManager.EnsureTemplateSubfolder(context, "..");
        Assert.True(OutputPathSafety.IsPathUnderDirectory(dest, assetOut));
        Assert.Equal(Path.Combine(assetOut, "Unknown"), dest);
    }

    // -------------------------------------------------------------------------
    // RDA archive path sanitizer controls (expected PASS)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("foo/../../evil.txt")]
    [InlineData("C:\\Windows\\system.ini")]
    [InlineData("CON")]
    [InlineData("foo/NUL.txt")]
    public void ArchivePathSanitizer_RejectsHostileArchiveNames(string archiveName)
    {
        string folder = Path.Combine(_sandbox, "rda-out");
        Directory.CreateDirectory(folder);
        Assert.False(ArchivePathSanitizer.TryGetOutputPath(archiveName, folder, out _));
    }

    [Fact]
    public void ArchivePathSanitizer_RejectsExistingJunctionSegment()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string extractRoot = Path.Combine(_sandbox, "rda-junc-root");
        string outside = Path.Combine(_sandbox, "rda-junc-outside");
        Directory.CreateDirectory(extractRoot);
        Directory.CreateDirectory(outside);

        string junction = Path.Combine(extractRoot, "linked");
        CreateJunction(junction, outside);

        Assert.False(ArchivePathSanitizer.TryGetOutputPath("linked/payload.txt", extractRoot, out _));
    }

    // -------------------------------------------------------------------------
    // Staging folder from app_settings (expected PASS — SanitizeSinglePathSegment)
    // -------------------------------------------------------------------------

    [Fact]
    public void BaseAssetStaging_RejectsTraversalFromAppSettings()
    {
        string assetOut = Path.Combine(_sandbox, "staging-out");
        Directory.CreateDirectory(assetOut);
        var context = new PipelineContext
        {
            AssetOutputDir = assetOut,
            AppSettingsConfig = new AppSettingsConfig
            {
                Settings = new AppSettings
                {
                    OutputStructure = new OutputStructureSettings { BaseassetFolder = "..\\..\\escape" }
                }
            }
        };

        string staging = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
        Assert.True(OutputPathSafety.IsPathUnderDirectory(staging, assetOut));
        Assert.EndsWith("BaseAssetGUID", staging, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // CLI language / option injection
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("../evil")]
    [InlineData("..\\evil")]
    [InlineData("texts_../x.xml")]
    [InlineData("english/../../x")]
    public void CommandLine_RejectsLanguagePathEscape(string language)
    {
        Assert.Throws<ArgumentException>(() => AssetProcessorCommandLineOptions.Parse(
        [
            @"C:\Games\Anno",
            @"C:\AnnoAssets",
            language
        ]));
    }

    [Fact]
    public void CommandLine_CustomTemplatePath_ShouldRejectPathsOutsideConfigTree()
    {
        // -u/-x must be a single config file name segment (no rooted / traversal paths).
        string secret = Path.Combine(_sandbox, "secret-templates.txt");
        File.WriteAllText(secret, "HostileTemplate\n");

        void Act() => AssetProcessorCommandLineOptions.Parse(
        [
            @"C:\Games\Anno",
            @"C:\AnnoAssets",
            "english",
            "-u:" + secret
        ]);

        Assert.ThrowsAny<Exception>(Act);
    }

    [Fact]
    public void CommandLine_ReadmeLanguage_ShouldRejectPathLikeValues()
    {
        // Secure expectation: readme language should use the same stem rules as asset language.
        Assert.Throws<ArgumentException>(() => AssetProcessorCommandLineOptions.Parse(
        [
            @"C:\Games\Anno",
            @"C:\AnnoAssets",
            "english",
            "--readme-lang:../evil"
        ]));
    }

    // -------------------------------------------------------------------------
    // Newtonsoft.Json — TypeNameHandling default (expected PASS / informational)
    // -------------------------------------------------------------------------

    [Fact]
    public void NewtonsoftConfigLoad_DoesNotHonorTypeNameHandlingAutoPayload()
    {
        string path = Path.Combine(_sandbox, "typed.json");
        File.WriteAllText(path, """
            {
              "$type": "System.IO.FileInfo, System.IO.FileSystem",
              "Games": { }
            }
            """);

        bool loaded = AssetProcessorConfiguration.TryLoadJsonConfig(path, out RegionalIngredientsConfig? config, out _);
        // TypeNameHandling defaults to None: either fail/null or bind only known members — never a FileInfo.
        Assert.True(!loaded || config is not null);
        Assert.IsNotType<System.IO.FileInfo>(config);
    }

    // -------------------------------------------------------------------------
    // Allocation / DoS bound (documents 512 MiB cap via public ZLib API)
    // -------------------------------------------------------------------------

    [Fact]
    public void ZLibUncompress_RejectsOverMaxArchiveBuffer()
    {
        // RDAValidation.MaxArchiveBufferSize is 512 MiB; int.MaxValue must be rejected before allocate.
        Assert.Throws<InvalidDataException>(() =>
            RDAExplorer.ZLib.ZLib.Uncompress([0x78, 0x9c, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01], int.MaxValue));
    }

    // -------------------------------------------------------------------------
    // Reparse fail-open / footprint containment nuance
    // -------------------------------------------------------------------------

    [Fact]
    public void DirectoryOrAncestorsHaveReparsePoint_FailOpenOnEmptyPath()
    {
        // Empty / invalid input returns false (treated as "safe") — fail-open.
        Assert.False(OutputPathSafety.DirectoryOrAncestorsHaveReparsePoint(""));
        Assert.False(OutputPathSafety.DirectoryOrAncestorsHaveReparsePoint("   "));
    }

    [Fact]
    public void GeneratedXmlFootprint_ShouldRefuseWriteWhenNoRootsConfigured()
    {
        // Refuse writes until an output root is configured.
        string target = Path.Combine(_sandbox, "unrooted", "out.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var context = new PipelineContext();
        var doc = new System.Xml.XmlDocument();
        doc.LoadXml("<root/>");

        Assert.Throws<InvalidOperationException>(() => GeneratedXmlFootprint.Save(doc, target, context));
        Assert.False(File.Exists(target));
    }

    private static void CreateJunction(string junctionPath, string targetPath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            ArgumentList = { "/c", "mklink", "/J", junctionPath, targetPath },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start mklink.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("mklink failed: " + process.StandardError.ReadToEnd());
        }
    }
}
