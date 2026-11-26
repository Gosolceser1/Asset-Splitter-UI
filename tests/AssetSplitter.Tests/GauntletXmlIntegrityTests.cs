using System.Text;
using System.Xml;
using AssetProcessor;

namespace AssetSplitter.Tests;

/// <summary>
/// Wave 1 gauntlet: FIND + REPRODUCE XML split/merge/format integrity issues.
/// Does not modify production behavior — assertions document observed outcomes.
/// </summary>
public sealed class GauntletXmlIntegrityTests : IDisposable
{
    private readonly string _root;
    private readonly List<string> _findings = [];

    public GauntletXmlIntegrityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "assetsplitter-gauntlet-" + Guid.NewGuid().ToString("N"));
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
        catch
        {
            // Best-effort cleanup for temp gauntlet trees.
        }
    }

    // ── 1. Happy-path process + well-formedness + expected GUID files ─────

    [Fact]
    public void Extract_SyntheticAssets_ProducesWellFormedFilesForExpectedGuids()
    {
        PipelineContext context = CreateContext(Path.Combine(_root, "extract-happy"));
        XmlDocument source = LoadAssetsXml(BuildAssetsXml(
            Asset("1001", "Alpha", "Product"),
            Asset("1002", "Beta", "Product")));

        AssetExtractor.ExtractAssets(context, source, gameType: "anno1800");

        string[] files = Directory.GetFiles(context.AssetOutputDir, "*.xml");
        Assert.Equal(2, files.Length);

        foreach (string file in files)
        {
            AssertWellFormed(file);
        }

        Assert.Contains(files, f => Path.GetFileName(f).StartsWith("1001", StringComparison.Ordinal));
        Assert.Contains(files, f => Path.GetFileName(f).StartsWith("1002", StringComparison.Ordinal));

        foreach (string file in files)
        {
            XmlDocument doc = new();
            XmlDocumentLoadRetry.Load(doc, file);
            string guid = XmlNodeText.GetValue(doc.DocumentElement!, "//Values/Standard/GUID");
            Assert.False(string.IsNullOrEmpty(guid));
            Assert.Contains(guid, new[] { "1001", "1002" });
        }
    }

    [Fact]
    public void MergeAndNormalize_RoundTrip_PreservesGuidAndLeafValues()
    {
        string outDir = Path.Combine(_root, "merge-round");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "merged.xml");

        PipelineContext context = CreateContext(outDir);
        context.ModOpsWrap = true;

        XmlDocument baseDoc = LoadAssetsXml(BuildModOpAsset("2001", "Child", "Product", """
            <Values>
              <Standard>
                <GUID>2001</GUID>
                <Name>Child</Name>
              </Standard>
              <Cost>
                <Costs>
                  <Item>
                    <Ingredient>1010017</Ingredient>
                    <Amount>10</Amount>
                  </Item>
                </Costs>
              </Cost>
            </Values>
            """));

        XmlDocument fixDoc = LoadAssetsXml(BuildModOpAsset("2001", "Child", "Product", """
            <Values>
              <Standard>
                <GUID>2001</GUID>
                <Name>Child</Name>
              </Standard>
              <Cost>
                <Costs>
                  <Item>
                    <Ingredient>1010017</Ingredient>
                    <Amount>99</Amount>
                  </Item>
                </Costs>
              </Cost>
              <Building>
                <BuildingLevel>3</BuildingLevel>
              </Building>
            </Values>
            """));

        TemplateMergeService.FixFile(context, baseDoc, fixDoc, outPath);

        Assert.True(File.Exists(outPath));
        AssertWellFormed(outPath);

        XmlDocument merged = new();
        XmlDocumentLoadRetry.Load(merged, outPath);
        Assert.Equal("2001", XmlNodeText.GetValue(merged.DocumentElement!, "//Standard/GUID"));
        Assert.Equal("99", XmlNodeText.GetValue(merged.DocumentElement!, "//Cost/Costs/Item/Amount"));
        Assert.Equal("3", XmlNodeText.GetValue(merged.DocumentElement!, "//Building/BuildingLevel"));

        // AssetXmlStructureNormalizer should stamp GUID on ModOp when wrap is on.
        XmlNode? modOp = merged.SelectSingleNode("//ModOps/ModOp");
        Assert.NotNull(modOp);
        Assert.Equal("2001", modOp!.Attributes?["GUID"]?.Value);
    }

    [Fact]
    public void FormatXml_RewritesWellFormed_AndIsIdempotentWithoutComments()
    {
        string outDir = Path.Combine(_root, "format-round");
        PipelineContext context = CreateContext(outDir);
        context.NoDefaultProperties = true; // skip enrichment cleanup side effects
        context.ModOpsWrap = true;

        XmlDocument source = LoadAssetsXml(BuildAssetsXml(Asset("3001", "FormatMe", "Product")));
        AssetExtractor.ExtractAssets(context, source);
        string[] files = Directory.GetFiles(context.AssetOutputDir, "*.xml");
        Assert.Single(files);

        string before = File.ReadAllText(files[0]);
        FormattingService.FormatXml(context, files, PropertyScanner.Empty, allowMove: false, skipInitialMessage: true);
        AssertWellFormed(files[0]);
        string after1 = File.ReadAllText(files[0]);

        FormattingService.FormatXml(context, files, PropertyScanner.Empty, allowMove: false, skipInitialMessage: true);
        string after2 = File.ReadAllText(files[0]);

        Assert.Equal(after1, after2);
        // Content may change vs raw extract (footprint / whitespace), but GUID must survive.
        XmlDocument doc = new();
        XmlDocumentLoadRetry.Load(doc, files[0]);
        Assert.Equal("3001", XmlNodeText.GetValue(doc.DocumentElement!, "//Standard/GUID"));
        _ = before; // retained for local debugging of diffs
    }

    // ── 2. Determinism ────────────────────────────────────────────────────

    [Fact]
    public void Extract_RepeatedRuns_AreByteIdenticalWhenNoGameBuildFootprint()
    {
        string runA = Path.Combine(_root, "det-a");
        string runB = Path.Combine(_root, "det-b");
        PipelineContext ctxA = CreateContext(runA);
        PipelineContext ctxB = CreateContext(runB);
        // DetectedGameBuild null ⇒ GeneratedXmlFootprint emits no timestamp comment.
        Assert.Null(ctxA.DetectedGameBuild);

        XmlDocument source = LoadAssetsXml(BuildAssetsXml(
            Asset("4001", "Same", "Product"),
            Asset("4002", "SameToo", "Product")));

        AssetExtractor.ExtractAssets(ctxA, source);
        AssetExtractor.ExtractAssets(ctxB, (XmlDocument)source.Clone());

        Dictionary<string, string> mapA = IndexByFileName(ctxA.AssetOutputDir);
        Dictionary<string, string> mapB = IndexByFileName(ctxB.AssetOutputDir);
        Assert.Equal(mapA.Keys.OrderBy(k => k), mapB.Keys.OrderBy(k => k));

        foreach (string name in mapA.Keys)
        {
            byte[] bytesA = File.ReadAllBytes(mapA[name]);
            byte[] bytesB = File.ReadAllBytes(mapB[name]);
            Assert.True(
                bytesA.AsSpan().SequenceEqual(bytesB),
                $"Non-deterministic extract for {name}: {bytesA.Length} vs {bytesB.Length} bytes");
        }
    }

    [Fact]
    public void Merge_RepeatedRuns_AreByteIdentical()
    {
        string dir = Path.Combine(_root, "det-merge");
        Directory.CreateDirectory(dir);
        string path1 = Path.Combine(dir, "m1.xml");
        string path2 = Path.Combine(dir, "m2.xml");

        PipelineContext context = CreateContext(dir);
        context.ModOpsWrap = true;

        string baseXml = BuildModOpAsset("5001", "Det", "Product", """
            <Values><Standard><GUID>5001</GUID><Name>Det</Name></Standard></Values>
            """);
        string fixXml = BuildModOpAsset("5001", "Det", "Product", """
            <Values>
              <Standard><GUID>5001</GUID><Name>Det</Name></Standard>
              <Extra><Flag>1</Flag></Extra>
            </Values>
            """);

        TemplateMergeService.FixFile(context, LoadAssetsXml(baseXml), LoadAssetsXml(fixXml), path1);
        TemplateMergeService.FixFile(context, LoadAssetsXml(baseXml), LoadAssetsXml(fixXml), path2);

        Assert.True(File.ReadAllBytes(path1).AsSpan().SequenceEqual(File.ReadAllBytes(path2)));
    }

    // ── 3. Empty / malformed / huge / adversarial ─────────────────────────

    [Fact]
    public void Extract_EmptyDocument_WritesNothing()
    {
        PipelineContext context = CreateContext(Path.Combine(_root, "empty"));
        XmlDocument empty = new();
        empty.LoadXml("<Group/>");
        // No Assets/Asset children and no matching templates ⇒ nothing extracted.
        AssetExtractor.ExtractAssets(context, empty);
        Assert.Empty(Directory.GetFiles(context.AssetOutputDir, "*.xml", SearchOption.AllDirectories));
    }

    [Fact]
    public void Extract_MalformedAssetsXml_FailsAtLoad_BeforeExtract()
    {
        Assert.Throws<XmlException>(() =>
        {
            XmlDocument doc = new();
            doc.LoadXml("<Assets><Asset><Values><Standard><GUID>1</GUID></Standard></Values>"); // unclosed
        });
    }

    [Fact]
    public void FormatXml_MalformedFile_SkipsWithoutThrowing()
    {
        string dir = Path.Combine(_root, "malformed-format");
        Directory.CreateDirectory(dir);
        string badPath = Path.Combine(dir, "broken.xml");
        File.WriteAllText(badPath, "<Asset><Values><GUID>1</GUID></Values>"); // unclosed

        PipelineContext context = CreateContext(dir);
        context.NoDefaultProperties = true;

        int result = FormattingService.FormatXml(
            context,
            [badPath],
            PropertyScanner.Empty,
            allowMove: false,
            skipInitialMessage: true);
        Assert.Equal(1, result);
        Assert.True(context.Issues.HasErrors);
        Assert.True(File.Exists(badPath));
        Assert.Contains("<Asset>", File.ReadAllText(badPath)); // unchanged garbage remains
    }

    [Fact]
    public void Extract_ReasonableHugeAssetCount_CompletesAndIsWellFormed()
    {
        const int count = 400; // parallel extract stress without multi-MB fixtures
        var sb = new StringBuilder();
        sb.Append("<Group><Assets>");
        for (int i = 0; i < count; i++)
        {
            string guid = (800000 + i).ToString();
            sb.Append(Asset(guid, $"Name{i}", "Product"));
        }
        sb.Append("</Assets></Group>");

        PipelineContext context = CreateContext(Path.Combine(_root, "huge"));
        XmlDocument source = LoadAssetsXml(sb.ToString());
        AssetExtractor.ExtractAssets(context, source);

        string[] files = Directory.GetFiles(context.AssetOutputDir, "*.xml");
        Assert.Equal(count, files.Length);

        // Spot-check well-formedness on edges + middle (full scan is slow but OK for 400).
        foreach (string file in files)
        {
            AssertWellFormed(file);
        }
    }

    [Fact]
    public void XmlDocumentLoadRetry_BillionLaughs_IsRejectedByDtdProhibit()
    {
        string path = Path.Combine(_root, "lol.xml");
        // Small entity-expansion probe (not a true billion-laughs memory bomb).
        const string probe = """
            <?xml version="1.0"?>
            <!DOCTYPE lol [
              <!ENTITY a "aaaaaaaaaa">
              <!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">
              <!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">
            ]>
            <root>&c;</root>
            """;
        File.WriteAllText(path, probe, Encoding.UTF8);

        XmlDocument doc = new();
        var ex = Record.Exception(() => XmlDocumentLoadRetry.Load(doc, path));
        Assert.NotNull(ex);
        Assert.True(
            ex is XmlException or InvalidOperationException,
            $"Expected DTD reject, got: {ex!.GetType().Name}: {ex.Message}");
        Note("PASS: XmlDocumentLoadRetry rejects DTD entity expansion (DtdProcessing.Prohibit).");
    }

    [Fact]
    public void BareXmlDocumentLoad_WithDtd_DocumentsParserPolicyGap()
    {
        string path = Path.Combine(_root, "lol-bare.xml");
        // Even smaller expansion; documents whether bare Load allows DTD.
        const string probe = """
            <?xml version="1.0"?>
            <!DOCTYPE x [ <!ENTITY a "aaaa"> <!ENTITY b "&a;&a;&a;&a;"> ]>
            <root>&b;</root>
            """;
        File.WriteAllText(path, probe, Encoding.UTF8);

        XmlDocument doc = new();
        Exception? ex = Record.Exception(() => doc.Load(path));
        if (ex is null)
        {
            Note("FINDING: XmlDocument.Load(path) accepted a DOCTYPE with entities. "
                + "Several pipeline loaders (AssetNameRegistry, TemplateMergeOrchestrator, DependencyResolutionCache, "
                + "TemplateSplitterService, LineIdContextIndex) use bare Load and are less hardened than XmlDocumentLoadRetry.");
            Assert.Equal("aaaaaaaaaaaaaaaa", doc.DocumentElement?.InnerText);
        }
        else
        {
            Note($"INFO: Bare XmlDocument.Load rejected DTD: {ex.GetType().Name}: {ex.Message}");
            Assert.NotNull(ex);
        }
    }

    // ── 4. Encoding ───────────────────────────────────────────────────────

    [Fact]
    public void XmlDocumentLoadRetry_AcceptsUtf8BomAndUtf16()
    {
        string utf8Path = Path.Combine(_root, "utf8bom.xml");
        string utf16Path = Path.Combine(_root, "utf16.xml");
        const string body = """<?xml version="1.0"?><Asset><Values><Standard><GUID>9001</GUID><Name>Enc</Name></Standard></Values></Asset>""";

        File.WriteAllText(utf8Path, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(utf16Path, body, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

        XmlDocument utf8Doc = new();
        XmlDocumentLoadRetry.Load(utf8Doc, utf8Path);
        Assert.Equal("9001", XmlNodeText.GetValue(utf8Doc.DocumentElement!, "//GUID"));

        XmlDocument utf16Doc = new();
        XmlDocumentLoadRetry.Load(utf16Doc, utf16Path);
        Assert.Equal("9001", XmlNodeText.GetValue(utf16Doc.DocumentElement!, "//GUID"));

        // Round-trip through GeneratedXmlFootprint.Save (UTF-8 writer path).
        PipelineContext context = CreateContext(Path.Combine(_root, "enc-out"));
        string saved = Path.Combine(context.AssetOutputDir, "saved.xml");
        GeneratedXmlFootprint.Save(utf16Doc, saved, context);
        AssertWellFormed(saved);
        byte[] savedBytes = File.ReadAllBytes(saved);
        // XmlDocument.Save typically writes UTF-8; BOM may or may not be present.
        Note($"INFO: GeneratedXmlFootprint.Save wrote {savedBytes.Length} bytes; "
            + $"BOM={(savedBytes.Length >= 3 && savedBytes[0] == 0xEF && savedBytes[1] == 0xBB && savedBytes[2] == 0xBF)}");
    }

    // ── 5. GUID / filename collisions ─────────────────────────────────────

    [Fact]
    public void Extract_DuplicateGuidDifferentNames_WritesTwoFilesSameGuid()
    {
        PipelineContext context = CreateContext(Path.Combine(_root, "dup-guid-diff-name"));
        // Filename = "{guid} - [ {name} ].xml" — same GUID + different names ⇒ two paths.
        XmlDocument source = LoadAssetsXml(BuildAssetsXml(
            Asset("7001", "FirstName", "Product"),
            Asset("7001", "SecondName", "Product")));

        AssetExtractor.ExtractAssets(context, source);

        string[] all = Directory.GetFiles(context.AssetOutputDir, "*.xml");
        Assert.Equal(2, all.Length);

        var guids = new List<string>();
        foreach (string file in all)
        {
            XmlDocument doc = new();
            XmlDocumentLoadRetry.Load(doc, file);
            guids.Add(XmlNodeText.GetValue(doc.DocumentElement!, "//Standard/GUID"));
        }

        Assert.All(guids, g => Assert.Equal("7001", g));
        Note("FINDING: Duplicate GUID with different Standard/Name produces two output files that both "
            + "embed GUID 7001. AssetExtractor does not detect or report GUID uniqueness violations.");
    }

    [Fact]
    public void Extract_DuplicateGuidSameName_KeepsBothFiles()
    {
        PipelineContext context = CreateContext(Path.Combine(_root, "dup-guid-same-name"));
        XmlDocument source = LoadAssetsXml(BuildAssetsXml(
            Asset("7002", "Clash", "Product", extraValues: "<Building><BuildingLevel>1</BuildingLevel></Building>"),
            Asset("7002", "Clash", "Product", extraValues: "<Building><BuildingLevel>9</BuildingLevel></Building>")));

        AssetExtractor.ExtractAssets(context, source);

        string[] files = Directory.GetFiles(context.AssetOutputDir, "*.xml");
        Assert.True(files.Length >= 2, "Duplicate GUID+Name must not silently overwrite; both assets must be kept.");

        var levels = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            XmlDocument doc = new();
            XmlDocumentLoadRetry.Load(doc, file);
            levels.Add(XmlNodeText.GetValue(doc.DocumentElement!, "//Building/BuildingLevel"));
        }

        Assert.Contains("1", levels);
        Assert.Contains("9", levels);
    }

    [Fact]
    public void Extract_SameDisplayNameDifferentGuids_ProducesTwoFiles()
    {
        PipelineContext context = CreateContext(Path.Combine(_root, "same-name"));
        XmlDocument source = LoadAssetsXml(BuildAssetsXml(
            Asset("7101", "Identical", "Product"),
            Asset("7102", "Identical", "Product")));

        AssetExtractor.ExtractAssets(context, source);
        string[] files = Directory.GetFiles(context.AssetOutputDir, "*.xml");
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void Format_MoveToTemplateFolder_WhenDestExists_Replaces()
    {
        string dir = Path.Combine(_root, "move-collide");
        PipelineContext context = CreateContext(dir);
        context.NoDefaultProperties = true;
        context.CreateTemplateFolders = true;
        context.ModOpsWrap = false;

        string fileA = Path.Combine(context.AssetOutputDir, "9999 - [ Clash ].xml");
        string fileB = Path.Combine(context.AssetOutputDir, "sub", "9999 - [ Clash ].xml");
        Directory.CreateDirectory(Path.GetDirectoryName(fileB)!);
        File.WriteAllText(fileA, BuildBareAsset("9999", "A", "Product"));
        File.WriteAllText(fileB, BuildBareAsset("9999", "B", "Product"));

        FormattingService.FormatXml(context, [fileA], PropertyScanner.Empty, allowMove: true, skipInitialMessage: true);
        string dest = Path.Combine(context.AssetOutputDir, "Product", "9999 - [ Clash ].xml");
        Assert.True(File.Exists(dest), "Expected first file moved into template folder");

        Exception? moveEx = Record.Exception(() =>
            FormattingService.FormatXml(context, [fileB], PropertyScanner.Empty, allowMove: true, skipInitialMessage: true));
        Assert.Null(moveEx);
        Assert.False(File.Exists(fileB), "Source should be moved away after replace");
        Assert.True(File.Exists(dest));
        Assert.Contains("<Name>B</Name>", File.ReadAllText(dest), StringComparison.Ordinal);
        Assert.DoesNotContain(context.Issues.GetAll(), i => i.Code == PipelineIssueCodes.MoveToTemplateFolderFailed);
    }

    // ── 6. Partial write / temp replace patterns ──────────────────────────

    [Fact]
    public void GeneratedXmlFootprint_Save_WritesDirectlyWithoutTempReplace()
    {
        // Evidence via behavior: Save opens the destination path for writing immediately.
        // Interrupting mid-save would leave a truncated file — we cannot safely crash the process mid-write
        // in a unit test, so we document the API contract by concurrent overwrite + source inspection proxy:
        // SaveWithRetry calls document.Save(outputPath) with no *.tmp + File.Replace pattern.
        string dir = Path.Combine(_root, "partial");
        PipelineContext context = CreateContext(dir);
        string path = Path.Combine(context.AssetOutputDir, "direct.xml");

        XmlDocument doc = new();
        doc.LoadXml("<Asset><Values><Standard><GUID>1</GUID><Name>X</Name></Standard></Values></Asset>");
        GeneratedXmlFootprint.Save(doc, path, context);
        Assert.True(File.Exists(path));
        Assert.False(Directory.GetFiles(context.AssetOutputDir, "*.tmp").Any());
        Assert.False(Directory.GetFiles(context.AssetOutputDir, "*.tmp.*").Any());

        // Concurrent overwrite of same path: no atomic replace — last completed Save wins; no backup retained.
        XmlDocument doc2 = new();
        doc2.LoadXml("<Asset><Values><Standard><GUID>1</GUID><Name>Y</Name></Standard></Values></Asset>");
        Parallel.Invoke(
            () => GeneratedXmlFootprint.Save(doc, path, context),
            () => GeneratedXmlFootprint.Save(doc2, path, context));

        AssertWellFormed(path); // usually still well-formed if writers don't interleave bytes (OS-level)
        string text = File.ReadAllText(path);
        Assert.True(
            text.Contains("<Name>X</Name>", StringComparison.Ordinal)
            || text.Contains("<Name>Y</Name>", StringComparison.Ordinal));

        Note("FINDING: GeneratedXmlFootprint.SaveWithRetry uses XmlDocument.Save(outputPath) directly "
            + "(no temp file + atomic File.Replace). Crash/kill mid-write can leave truncated XML; "
            + "concurrent Saves race without a lock.");
    }

    [Fact]
    public void AssetXmlPathEditor_ListItemMerge_MisassignsIndexedSiblings()
    {
        // Reproduces list/index merge integrity risk: EnsurePathExistsOptimized creates bare <Item>
        // for Item[2] when missing, which can become Item[1] in document order.
        PipelineContext context = CreateContext(Path.Combine(_root, "item-merge"));
        context.ModOpsWrap = true;
        string outPath = Path.Combine(context.AssetOutputDir, "items.xml");

        XmlDocument baseDoc = LoadAssetsXml(BuildModOpAsset("6001", "Listy", "Product", """
            <Values>
              <Standard><GUID>6001</GUID><Name>Listy</Name></Standard>
              <Cost><Costs></Costs></Cost>
            </Values>
            """));

        XmlDocument fixDoc = LoadAssetsXml(BuildModOpAsset("6001", "Listy", "Product", """
            <Values>
              <Standard><GUID>6001</GUID><Name>Listy</Name></Standard>
              <Cost>
                <Costs>
                  <Item><Ingredient>AAA</Ingredient><Amount>1</Amount></Item>
                  <Item><Ingredient>BBB</Ingredient><Amount>2</Amount></Item>
                </Costs>
              </Cost>
            </Values>
            """));

        TemplateMergeService.FixFile(context, baseDoc, fixDoc, outPath);
        AssertWellFormed(outPath);

        XmlDocument merged = new();
        XmlDocumentLoadRetry.Load(merged, outPath);
        XmlNodeList? items = merged.SelectNodes("//Cost/Costs/Item");
        Assert.NotNull(items);

        var ingredients = items!.Cast<XmlNode>()
            .Select(n => n.SelectSingleNode("Ingredient")?.InnerText ?? "")
            .Where(s => s.Length > 0)
            .ToList();

        Note($"INFO: After merging 2 Cost Items into empty Costs, got {items.Count} Item nodes; "
            + $"ingredients=[{string.Join(",", ingredients)}]");

        // Expected integrity: both AAA and BBB present once each.
        // If broken, we may see duplicates, missing entries, or collapsed siblings.
        bool intact = ingredients.Count == 2
            && ingredients.Contains("AAA")
            && ingredients.Contains("BBB");
        if (!intact)
        {
            Note("FINDING: TemplateMergeService/AssetXmlPathEditor failed to preserve indexed Item list "
                + $"integrity (got [{string.Join(",", ingredients)}]). Upsert uses SelectSingleNode(name) "
                + "and EnsurePathExistsOptimized creates unindexed elements for Item[n] segments.");
        }

        Assert.True(
            intact,
            $"Item list merge integrity failed: [{string.Join(",", ingredients)}]");
    }

    [Fact]
    public void AssetXmlPathEditor_EnsurePath_Item2WhenItem1Exists_CreatesThirdUnindexedItem()
    {
        var target = new XmlDocument();
        target.LoadXml("""
            <ModOps><ModOp>
              <Asset>
                <Values>
                  <Cost>
                    <Costs>
                      <Item><Ingredient>KEEP</Ingredient></Item>
                    </Costs>
                  </Cost>
                </Values>
              </Asset>
            </ModOp></ModOps>
            """);

        XmlNode targetRoot = target.SelectSingleNode("//ModOp")!;
        var cache = new Dictionary<string, XmlNode?>(StringComparer.OrdinalIgnoreCase);

        // Ask for path ending at Item[2]/Amount — Item[2] does not exist yet.
        AssetXmlPathEditor.EnsurePathExistsOptimized(
            target,
            targetRoot,
            "Asset/Values/Cost/Costs/Item[2]/Amount",
            outputXmlPath: "synthetic.xml",
            cache,
            debugMode: false,
            writeMessage: static (_, _) => { });

        XmlNodeList items = target.SelectNodes("//Cost/Costs/Item")!;
        Note($"INFO: After EnsurePathExistsOptimized for Item[2]/Amount with existing Item[1], Item count={items.Count}");

        // Correct behavior would ensure exactly two Items (index 1 kept, index 2 created).
        // Observed risk: CreateElement("Item") appends a new Item which becomes Item[2] — often OK —
        // OR if SelectSingleNode("Item[2]") fails oddly, duplicates appear.
        Assert.True(items.Count >= 2, $"Expected at least 2 Items, got {items.Count}");
        Assert.Equal("KEEP", items[0]!.SelectSingleNode("Ingredient")?.InnerText);

        // Now upsert Amount onto Item[2] via full FixFile-style Upsert that ignores index on leaf parent lookup...
        var source = new XmlDocument();
        source.LoadXml("""
            <ModOps><ModOp>
              <Asset><Values><Cost><Costs>
                <Item><Ingredient>KEEP</Ingredient></Item>
                <Item><Ingredient>NEW</Ingredient><Amount>5</Amount></Item>
              </Costs></Cost></Values></Asset>
            </ModOp></ModOps>
            """);
        XmlNode sourceAmount = source.SelectSingleNode("//Item[2]/Amount")!;
        AssetXmlPathEditor.UpsertNode(
            target,
            source,
            targetRoot,
            sourceAmount,
            "Asset/Values/Cost/Costs/Item[2]/Amount",
            addInheritanceComments: false);

        string? amountOn1 = target.SelectSingleNode("//Item[1]/Amount")?.InnerText;
        string? amountOn2 = target.SelectSingleNode("//Item[2]/Amount")?.InnerText;
        Note($"INFO: After Upsert of Item[2]/Amount → Item[1]/Amount={amountOn1 ?? "(none)"}, Item[2]/Amount={amountOn2 ?? "(none)"}");

        // Parent path includes Item[2], so Amount should land on the second Item.
        Assert.Equal("5", amountOn2);
        Assert.True(string.IsNullOrEmpty(amountOn1), "Amount must not leak onto Item[1] when xpath targets Item[2]");
    }

    [Fact]
    public void AssetXmlPathEditor_Upsert_UnindexedItemPath_TargetsFirstSibling()
    {
        var target = new XmlDocument();
        target.LoadXml("""
            <ModOps><ModOp>
              <Asset>
                <Values>
                  <Cost>
                    <Costs>
                      <Item><Amount>1</Amount></Item>
                      <Item><Amount>2</Amount></Item>
                    </Costs>
                  </Cost>
                </Values>
              </Asset>
            </ModOp></ModOps>
            """);

        var source = new XmlDocument();
        source.LoadXml("""
            <ModOps><ModOp>
              <Asset>
                <Values>
                  <Cost>
                    <Costs>
                      <Item><Amount>9</Amount></Item>
                    </Costs>
                  </Cost>
                </Values>
              </Asset>
            </ModOp></ModOps>
            """);

        XmlNode targetRoot = target.SelectSingleNode("//ModOp")!;
        XmlNode sourceAmount = source.SelectSingleNode("//Item/Amount")!;

        // Unindexed Item path (XmlLeafPathBuilder emits this for the first sibling only).
        AssetXmlPathEditor.UpsertNode(
            target,
            source,
            targetRoot,
            sourceAmount,
            "Asset/Values/Cost/Costs/Item/Amount",
            addInheritanceComments: false);

        string first = target.SelectSingleNode("//Item[1]/Amount")!.InnerText;
        string second = target.SelectSingleNode("//Item[2]/Amount")!.InnerText;
        Assert.Equal("9", first);
        Assert.Equal("2", second);
        Note("INFO: Unindexed Item/Amount upsert correctly updates only Item[1]; Item[2] unchanged. "
            + "XmlLeafPathBuilder emits Item[n] for n>=2 so TemplateMergeService usually stays consistent.");
    }

    [Fact]
    public void AssetXmlPathEditor_Upsert_RepeatedLeafSiblings_KeepsEachSlot()
    {
        var target = new XmlDocument();
        target.LoadXml("""
            <ModOps><ModOp>
              <Asset><Values><Tags><Tag>old</Tag></Tags></Values></Asset>
            </ModOp></ModOps>
            """);

        var source = new XmlDocument();
        source.LoadXml("""
            <ModOps><ModOp>
              <Asset><Values><Tags><Tag>a</Tag><Tag>b</Tag></Tags></Values></Asset>
            </ModOp></ModOps>
            """);

        XmlNode targetRoot = target.SelectSingleNode("//ModOp")!;
        XmlNode sourceRoot = source.SelectSingleNode("//ModOp")!;

        // XmlLeafPathBuilder emits Tag and Tag[2] for repeated leaf siblings.
        foreach (string xpath in new[] { "Asset/Values/Tags/Tag", "Asset/Values/Tags/Tag[2]" })
        {
            AssetXmlPathEditor.UpsertNode(
                target,
                source,
                targetRoot,
                sourceRoot.SelectSingleNode(xpath)!,
                xpath,
                addInheritanceComments: false);
        }

        XmlNodeList tags = target.SelectNodes("//Tags/Tag")!;
        Assert.Equal(2, tags.Count);
        Assert.Equal("a", tags[0]!.InnerText);
        Assert.Equal("b", tags[1]!.InnerText);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private PipelineContext CreateContext(string workDir)
    {
        Directory.CreateDirectory(workDir);
        string assetOut = Path.Combine(workDir, "output_xml");
        Directory.CreateDirectory(assetOut);

        return new PipelineContext
        {
            AssetRoot = workDir,
            BaseOutputDir = workDir,
            GameOutputRoot = workDir,
            AssetOutputDir = assetOut,
            SourceXmlFolder = workDir,
            AssetLanguage = "none",
            AssetTemplatesList = ["Product"],
            TemplatesUsed = ["Product"],
            NoDefaultProperties = false,
            ModOpsWrap = false,
            DebugMode = false,
        };
    }

    private static XmlDocument LoadAssetsXml(string xml)
    {
        XmlDocument doc = new();
        doc.LoadXml(xml);
        return doc;
    }

    private static string BuildAssetsXml(params string[] assets) =>
        $"<Group><Assets>{string.Join("", assets)}</Assets></Group>";

    private static string Asset(string guid, string name, string template, string extraValues = "") => $"""
        <Asset>
          <Template>{template}</Template>
          <Values>
            <Standard>
              <GUID>{guid}</GUID>
              <Name>{name}</Name>
            </Standard>
            {extraValues}
          </Values>
        </Asset>
        """;

    private static string BuildBareAsset(string guid, string name, string template) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Asset>
          <Template>{template}</Template>
          <Values>
            <Standard>
              <GUID>{guid}</GUID>
              <Name>{name}</Name>
            </Standard>
          </Values>
        </Asset>
        """;

    private static string BuildModOpAsset(string guid, string name, string template, string valuesInner) => $"""
        <ModOps>
          <ModOp GUID="{guid}" Type="Replace" Path="/">
            <Asset>
              <Template>{template}</Template>
              {valuesInner}
            </Asset>
          </ModOp>
        </ModOps>
        """;

    private static void AssertWellFormed(string path)
    {
        XmlDocument doc = new();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using var reader = XmlReader.Create(path, settings);
        doc.Load(reader);
        Assert.NotNull(doc.DocumentElement);
    }

    private static Dictionary<string, string> IndexByFileName(string dir) =>
        Directory.GetFiles(dir, "*.xml")
            .ToDictionary(p => Path.GetFileName(p)!, p => p, StringComparer.OrdinalIgnoreCase);

    private void Note(string finding) => _findings.Add(finding);
}
