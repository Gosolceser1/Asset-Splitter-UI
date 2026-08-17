using System.Text;
using System.Xml;
using AssetProcessor;
using RDAExplorer;

namespace AssetSplitter.Tests;

/// <summary>
/// Independent critic regressions: these tests encode secure/correct expectations.
/// A failing test proves a live defect; do not Skip to hide it.
/// </summary>
public sealed class CriticRegressionDefectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "as-critic-" + Guid.NewGuid().ToString("N"));

    public CriticRegressionDefectTests()
    {
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Phase3 catches XmlException from DtdProcessing.Prohibit, then LoadXml(Unicode)
    /// which expands entities — undoing the secure loader for UTF-16 assets.xml.
    /// </summary>
    [Fact]
    public void Phase3_Utf16AssetsWithDtd_MustNotExpandEntitiesViaLoadXmlFallback()
    {
        string sourceXml = Path.Combine(_root, "source_xml");
        string assetOut = Path.Combine(_root, "out");
        Directory.CreateDirectory(sourceXml);
        Directory.CreateDirectory(assetOut);

        // Small expansion so the resulting Name still fits filesystem limits.
        const string dtdBomb = """
            <?xml version="1.0"?>
            <!DOCTYPE lol [
              <!ENTITY boom "EXPANDED_DTD_PAYLOAD">
            ]>
            <Assets>
              <Asset>
                <Template>Product</Template>
                <Values><Standard><GUID>9001</GUID><Name>&boom;</Name></Standard></Values>
              </Asset>
            </Assets>
            """;
        File.WriteAllText(Path.Combine(sourceXml, "assets.xml"), dtdBomb, Encoding.Unicode);

        var context = new PipelineContext
        {
            SourceXmlFolder = sourceXml,
            AssetOutputDir = assetOut,
            GameOutputRoot = _root,
            BaseOutputDir = _root,
            AnnoAssetsRoot = _root,
            AssetLanguage = "none",
            ModOpsWrap = true,
        };

        int exit = Phase3AssetExtraction.ExecutePhase3AssetExtraction(
            context,
            singleAssetGuid: "",
            gameType: "Anno1800",
            gameOutputRoot: _root,
            out _,
            out _);

        // Secure expectation: refuse DTD (non-zero) or load without entity expansion.
        // Live defect: Prohibit throws XmlException → LoadXml(Unicode) fallback expands &boom;.
        string[] written = Directory.Exists(assetOut)
            ? Directory.GetFiles(assetOut, "*.xml", SearchOption.AllDirectories)
            : [];

        bool expanded = written.Any(file =>
            File.ReadAllText(file).Contains("EXPANDED_DTD_PAYLOAD", StringComparison.Ordinal));
        string sample = written.Length == 0
            ? "<none>"
            : File.ReadAllText(written[0])[..Math.Min(240, File.ReadAllText(written[0]).Length)];

        // Live defect: XmlDocumentLoadRetry Prohibit fails, then LoadXml(Unicode) accepts the
        // DTD-bearing document and Phase3 continues (exit=0, extracted file written).
        Assert.True(
            exit != 0 && written.Length == 0,
            "Phase3 must fail closed on DTD-bearing assets.xml (no LoadXml fallback). "
            + $"exit={exit}, files={written.Length}, expandedPayload={expanded}, sample={sample}");
    }

    /// <summary>
    /// TemplateMergeOrchestrator loads properties.xml/templates.xml via LoadXml after ReadAllText,
    /// which expands DTDs (unlike XmlDocumentLoadRetry).
    /// </summary>
    [Fact]
    public void TemplateMergeOrchestrator_LoadXmlPath_MustRejectDtdEntityExpansion()
    {
        string sourceXml = Path.Combine(_root, "merge-source");
        string assetOut = Path.Combine(_root, "merge-out");
        Directory.CreateDirectory(sourceXml);
        Directory.CreateDirectory(assetOut);

        const string dtdTemplates = """
            <?xml version="1.0"?>
            <!DOCTYPE x [ <!ENTITY a "AAAA"> <!ENTITY b "&a;&a;&a;&a;"> ]>
            <Templates>
              <Template>
                <Name>Product</Name>
                <Properties>
                  <Standard><GUID>0</GUID><Name>&b;</Name></Standard>
                </Properties>
              </Template>
            </Templates>
            """;
        File.WriteAllText(Path.Combine(sourceXml, "templates.xml"), dtdTemplates, Encoding.UTF8);
        File.WriteAllText(Path.Combine(sourceXml, "properties.xml"), """
            <?xml version="1.0"?><Properties><DefaultValues/></Properties>
            """, Encoding.UTF8);

        var context = new PipelineContext
        {
            SourceXmlFolder = sourceXml,
            AssetOutputDir = assetOut,
            GameOutputRoot = _root,
            BaseOutputDir = _root,
            AnnoAssetsRoot = _root,
            AssetLanguage = "none",
            ModOpsWrap = true,
            CustomFixlistFile = Path.Combine(_root, "fixlist.txt"),
        };
        File.WriteAllText(context.CustomFixlistFile, "Product\n");

        string assetPath = Path.Combine(assetOut, "1 - [ X ].xml");
        File.WriteAllText(assetPath, """
            <?xml version="1.0"?>
            <ModOps><ModOp>
              <Asset>
                <Template>Product</Template>
                <Values><Standard><GUID>1</GUID><Name>X</Name></Standard></Values>
              </Asset>
            </ModOp></ModOps>
            """, Encoding.UTF8);

        var templatesDoc = new XmlDocument();
        Exception? loadEx = Record.Exception(() =>
            XmlDocumentLoadRetry.Load(templatesDoc, Path.Combine(sourceXml, "templates.xml")));

        Assert.True(
            loadEx is XmlException or InvalidOperationException,
            "XmlDocumentLoadRetry (used by TemplateMergeOrchestrator) must reject DTD. "
            + $"got: {(loadEx is null ? "null" : loadEx.GetType().Name + ": " + loadEx.Message)}");

        int mergeResult = TemplateMergeOrchestrator.Execute(context, "anno1800", [assetPath]);
        string merged = File.Exists(assetPath) ? File.ReadAllText(assetPath) : "";
        Assert.DoesNotContain("AAAAAAAA", merged, StringComparison.Ordinal);
        Assert.True(mergeResult != 0 || !merged.Contains("AAAA", StringComparison.Ordinal));
    }

    /// <summary>
    /// Pipeline RDA extract uses bare:true; same basename in different folders silently overwrites
    /// within one archive when skipExistingBareOutput is false (Anno 117 path).
    /// </summary>
    [Fact]
    public void BareExtract_SameBasenameDifferentFolders_MustNotSilentlyOverwrite()
    {
        string folder = Path.Combine(_root, "bare");
        Directory.CreateDirectory(folder);

        using var s1 = new MemoryStream("CONTENT_A"u8.ToArray());
        using var s2 = new MemoryStream("CONTENT_B"u8.ToArray());
        using var br1 = new BinaryReader(s1);
        using var br2 = new BinaryReader(s2);
        var first = new RDAFile
        {
            FileName = @"data\config\assets.xml",
            UncompressedSize = 9,
            CompressedSize = 9,
            BinaryFile = br1,
        };
        var second = new RDAFile
        {
            FileName = @"other\path\assets.xml",
            UncompressedSize = 9,
            CompressedSize = 9,
            BinaryFile = br2,
        };

        Assert.True(RdaFileExtractor.ExtractToRoot(first, folder, "/", bare: true));
        bool secondExtracted = RdaFileExtractor.ExtractToRoot(second, folder, "/", bare: true);

        string dest = Path.Combine(folder, "assets.xml");
        Assert.True(File.Exists(dest));
        string body = File.ReadAllText(dest);

        // Secure expectation: keep first write or refuse second; never silently replace with last.
        Assert.Equal("CONTENT_A", body);
        Assert.False(
            secondExtracted && body == "CONTENT_B",
            "Bare extract must not silently overwrite an existing basename from a different archive path.");
    }

    /// <summary>
    /// TemplateExtractor still uses bare XDocument.Load, which expands internal DTD entities
    /// (unlike XmlDocumentLoadRetry with DtdProcessing.Prohibit). Evidence: TemplateExtractor.cs:88.
    /// </summary>
    [Fact]
    public void TemplateExtractor_MustNotExpandDtdEntitiesViaXDocumentLoad()
    {
        string templatesPath = Path.Combine(_root, "templates-dtd.xml");
        File.WriteAllText(templatesPath, """
            <?xml version="1.0"?>
            <!DOCTYPE x [ <!ENTITY boom "EXPANDED_TEMPLATE_NAME"> ]>
            <Templates>
              <Template>
                <Name>&boom;</Name>
              </Template>
            </Templates>
            """, Encoding.UTF8);

        HashSet<string>? names = null;
        Exception? loadEx = Record.Exception(() => names = TemplateExtractor.ExtractTemplatesFromXml(templatesPath));

        bool expanded = names is not null && names.Contains("EXPANDED_TEMPLATE_NAME");
        Assert.True(
            loadEx is XmlException || (loadEx is null && !expanded),
            "TemplateExtractor must reject or ignore DTD entities, not expand them via XDocument.Load. "
            + $"ex={(loadEx is null ? "null" : loadEx.GetType().Name + ": " + loadEx.Message)}, "
            + $"expanded={expanded}, names=[{(names is null ? "<threw>" : string.Join(", ", names))}]");
    }

    /// <summary>
    /// ReadTemplateNameFromFile (TemplateMergeOrchestrator.cs:231) uses XDocument.Load first.
    /// Live defect: entity expands → fixlist match on expanded name → secure Load then throws → HasErrors.
    /// Secure: refuse DTD up front (or fall through to literal "&amp;t;") so expanded name never drives merge.
    /// </summary>
    [Fact]
    public void TemplateMerge_ReadTemplateName_MustNotExpandDtdEntitiesViaXDocumentLoad()
    {
        string sourceXml = Path.Combine(_root, "merge-dtd-source");
        string assetOut = Path.Combine(_root, "merge-dtd-out");
        Directory.CreateDirectory(sourceXml);
        Directory.CreateDirectory(assetOut);

        File.WriteAllText(Path.Combine(sourceXml, "templates.xml"), """
            <?xml version="1.0"?>
            <Templates>
              <Template>
                <Name>Product</Name>
                <Properties>
                  <Standard><GUID>0</GUID><Name>Base</Name></Standard>
                </Properties>
              </Template>
            </Templates>
            """, Encoding.UTF8);
        File.WriteAllText(Path.Combine(sourceXml, "properties.xml"), """
            <?xml version="1.0"?><Properties><DefaultValues/></Properties>
            """, Encoding.UTF8);

        string assetPath = Path.Combine(assetOut, "2 - [ Y ].xml");
        File.WriteAllText(assetPath, """
            <?xml version="1.0"?>
            <!DOCTYPE x [ <!ENTITY t "EXPANDED_VIA_XDOCUMENT"> ]>
            <ModOps><ModOp>
              <Asset>
                <Template>&t;</Template>
                <Values><Standard><GUID>2</GUID><Name>Y</Name></Standard></Values>
              </Asset>
            </ModOp></ModOps>
            """, Encoding.UTF8);

        var context = new PipelineContext
        {
            SourceXmlFolder = sourceXml,
            AssetOutputDir = assetOut,
            GameOutputRoot = _root,
            BaseOutputDir = _root,
            AnnoAssetsRoot = _root,
            AssetLanguage = "none",
            ModOpsWrap = true,
            // Only the expanded entity text — literal "&t;" must not match if DTD is refused.
            CustomFixlistFile = Path.Combine(_root, "fixlist-dtd.txt"),
        };
        File.WriteAllText(context.CustomFixlistFile, "EXPANDED_VIA_XDOCUMENT\n");

        int mergeResult = TemplateMergeOrchestrator.Execute(context, "anno1800", [assetPath]);

        Assert.False(
            context.Issues.HasErrors || mergeResult != 0,
            "ReadTemplateNameFromFile must not expand DTD entities into fixlist matches "
            + "(then fail the subsequent Prohibit load). "
            + $"mergeResult={mergeResult}, hasErrors={context.Issues.HasErrors}");
    }

    [Fact]
    public void ConsolePl_ReadmeModOpsRefMerge_MustKeepFormatPlaceholders()
    {
        string consoleDir = FindConsoleMessagesDirectory();
        string en = File.ReadAllText(Path.Combine(consoleDir, "console_en.json"));
        string pl = File.ReadAllText(Path.Combine(consoleDir, "console_pl.json"));

        Assert.Contains("\"readmeModOpsRefMerge\"", en, StringComparison.Ordinal);
        Assert.Contains("{0}", ExtractJsonStringValue(en, "readmeModOpsRefMerge"), StringComparison.Ordinal);
        Assert.Contains("{1}", ExtractJsonStringValue(en, "readmeModOpsRefMerge"), StringComparison.Ordinal);

        string plMerge = ExtractJsonStringValue(pl, "readmeModOpsRefMerge");
        Assert.Contains("{0}", plMerge, StringComparison.Ordinal);
        Assert.Contains("{1}", plMerge, StringComparison.Ordinal);

        string formatted = string.Format(plMerge, "123456", "Example asset");
        Assert.Contains("123456", formatted, StringComparison.Ordinal);
    }

    private static string ExtractJsonStringValue(string json, string key)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(key).GetString()
            ?? throw new InvalidOperationException("Missing key " + key);
    }

    private static string FindConsoleMessagesDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "config", "05_Console_Messages");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "console_en.json")))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate config/05_Console_Messages.");
    }
}
