using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class OutputRunCacheTests
{
    [Fact]
    public void BuildOptionsFingerprint_Changes_WhenToggleFlips()
    {
        var a = CreateContext(addComments: true);
        var b = CreateContext(addComments: false);

        Assert.NotEqual(OutputRunCache.BuildOptionsFingerprint(a), OutputRunCache.BuildOptionsFingerprint(b));
    }

    [Fact]
    public void BuildOptionsFingerprint_IsStable_ForSameOptions()
    {
        var a = CreateContext(addComments: true, createAssetMods: true);
        var b = CreateContext(addComments: true, createAssetMods: true);

        Assert.Equal(OutputRunCache.BuildOptionsFingerprint(a), OutputRunCache.BuildOptionsFingerprint(b));
    }

    [Fact]
    public void Evaluate_ForceFullRebuild_Wins()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: true);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.ForceFullRebuild, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenStampAndInventoryPresent()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
            Assert.Equal(1, decision.AssetCount);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void VerifyPresence_WhenOutputTreesTrusted_MarksInventoryPresentWithoutRehash()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);
            Assert.True(OutputRunCache.TryReadInventory(root, out List<OutputRunCache.InventoryEntry>? inventory));
            Assert.NotNull(inventory);
            Assert.True(OutputRunCache.InventoryHasContentHashesForTrust(inventory!, createMods: false));

            var trustedTrees = new OutputRunCache.SideTreeResult();
            OutputRunCache.PresenceResult presence = OutputRunCache.VerifyPresence(context, inventory!, trustedTrees);

            Assert.Equal(inventory!.Count, presence.Present.Count);
            Assert.Empty(presence.MissingXml);
            Assert.True(presence.IsComplete);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void VerifyPresence_WhenOutputXmlTreeOkButInventoryHashBlank_StillClassifies()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);
            Assert.True(OutputRunCache.TryReadInventory(root, out List<OutputRunCache.InventoryEntry>? inventory));
            Assert.NotNull(inventory);

            foreach (OutputRunCache.InventoryEntry entry in inventory!)
            {
                entry.OutputHash = "";
            }

            // Trees OK is not enough without inventory output hashes (schema-4 backfill path).
            var trustedTrees = new OutputRunCache.SideTreeResult();
            OutputRunCache.PresenceResult presence = OutputRunCache.VerifyPresence(context, inventory, trustedTrees);

            Assert.Empty(presence.Present);
            Assert.Single(presence.MissingXml);
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.ContentChanged,
                presence.XmlFailureKinds["1001"]);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void VerifyPresence_WhenOutputXmlTreeMismatched_UsesPerFileHashing()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);
            Assert.True(OutputRunCache.TryReadInventory(root, out List<OutputRunCache.InventoryEntry>? inventory));
            Assert.NotNull(inventory);

            foreach (OutputRunCache.InventoryEntry entry in inventory!)
            {
                entry.OutputHash = "deadbeef";
            }

            var mismatched = new OutputRunCache.SideTreeResult
            {
                OutputXml = OutputRunCache.PresenceIssueKind.ContentChanged,
            };
            OutputRunCache.PresenceResult presence = OutputRunCache.VerifyPresence(context, inventory, mismatched);

            Assert.Empty(presence.Present);
            Assert.Single(presence.MissingXml);
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.ContentChanged,
                presence.XmlFailureKinds["1001"]);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenStampOutputSchemaIsOlder()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, outputSchemaOverride: OutputRunCache.OutputSchemaVersion - 1);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SchemaMismatch, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenInventoryXmlContentChanged_ButStillStartsWithMarkup()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            string xmlFull = Path.Combine(root, "output_xml_anno1800", "1001 - [ Test ].xml");
            Assert.True(OutputRunCache.IsIntactOutputXml(xmlFull));
            File.WriteAllText(xmlFull, "<ModOps><ModOp GUID=\"1001\"/><!-- edited --></ModOps>");
            Assert.True(OutputRunCache.IsIntactOutputXml(xmlFull), "Leading-byte probe alone must still pass");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Single(decision.Presence!.MissingXml);
            Assert.Contains("1001", decision.Presence.GuidsNeedingRebuild);
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.ContentChanged,
                decision.Presence.XmlFailureKinds["1001"]);

            IReadOnlyList<OutputRunCache.RepairDetailItem> details =
                OutputRunCache.BuildPresenceRepairDetails(decision.Presence, context);
            OutputRunCache.RepairDetailItem item = Assert.Single(details);
            Assert.Equal("1001", item.Guid);
            Assert.Equal("outputCacheRepairItemContentChanged", item.MessageKey);
            Assert.Equal("1001 - [ Test ]", item.Label);

            string formatted = string.Format(ConsoleMessages.Get(item.MessageKey), item.Label);
            Assert.Equal(
                string.Format(ConsoleMessages.Get("outputCacheRepairItemContentChanged"), "1001 - [ Test ]"),
                formatted);
            Assert.Contains("no longer matches cache", formatted, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("will rebuild", formatted, StringComparison.OrdinalIgnoreCase);

            OutputRunCache.StoreRepairGuidMessageKeys(context, details);
            Assert.NotNull(context.RepairGuidMessageKeys);
            Assert.Equal("outputCacheRepairItemContentChanged", context.RepairGuidMessageKeys!["1001"]);
            Assert.Equal(
                "modRepairBecauseContentChanged",
                OutputRunCache.TryMapItemKeyToModRepairActionKey(item.MessageKey));
            Assert.Equal(
                "extractRepairBecauseContentChanged",
                OutputRunCache.TryMapItemKeyToExtractRepairActionKey(item.MessageKey));
            Assert.Equal(
                string.Format(ConsoleMessages.Get("modRepairBecauseContentChanged"), item.Label),
                string.Format(
                    ConsoleMessages.Get(OutputRunCache.TryMapItemKeyToModRepairActionKey(item.MessageKey)!),
                    item.Label));
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenModAssetsXmlContentChanged_ButStillStartsWithMarkup()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            string modAssets = Path.Combine(
                context.AssetModOutputRoot,
                "1001 - Test",
                "data",
                "config",
                "export",
                "main",
                "asset",
                "assets.xml");
            File.WriteAllText(modAssets, "<Asset><!-- user edit --></Asset>");
            Assert.True(OutputRunCache.IsIntactOutputXml(modAssets));

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Empty(decision.Presence!.MissingXml);
            Assert.Single(decision.Presence.MissingMod);
            Assert.Contains("1001", decision.Presence.GuidsNeedingRebuild);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenOutputHashesMatch()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
            Assert.Equal(1, decision.AssetCount);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenTemplatesTreeFileEdited()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false, splitTemplates: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, withTemplates: true);

            string templateFile = Path.Combine(
                OutputDirectoryManager.GetTemplatesOutputDirectory(root, "anno1800"),
                "AchievementReward.xml");
            File.WriteAllText(templateFile, "<Template><!-- edited --></Template>");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.True(decision.ForceResplitTemplates);
            Assert.NotNull(decision.SideTrees);
            Assert.Equal(OutputRunCache.PresenceIssueKind.ContentChanged, decision.SideTrees!.Templates);
            Assert.Contains(
                "AchievementReward.xml",
                decision.SideTrees.TemplatesChangedPaths,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenTemplatesTreeMatches()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false, splitTemplates: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, withTemplates: true);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
            Assert.NotNull(decision.SideTrees);
            Assert.True(decision.SideTrees!.IsOk);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenSourceXmlTreeEdited_ButExtractableHashesMatch()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            string assetXml = """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """;
            File.WriteAllText(assetsPath, assetXml);
            string sourceHash = AssetSourceHasher.HashAssetNode(LoadFirstAsset(assetsPath));
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: sourceHash);

            // Non-extractable file change moves source tree hash without changing asset hashes.
            File.WriteAllText(
                Path.Combine(root, "source_xml_anno1800", "properties.xml"),
                "<Properties><!-- edited --></Properties>");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
            Assert.Equal(OutputRunCache.PresenceIssueKind.ContentChanged, decision.SideTrees!.SourceXml);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_DiffRepair_WhenSourceXmlExtractableAssetEdited()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            string originalAsset = """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """;
            File.WriteAllText(assetsPath, originalAsset);
            string sourceHash = AssetSourceHasher.HashAssetNode(LoadFirstAsset(assetsPath));
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: sourceHash);

            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test Edited</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.DiffRepair, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SideTreeMismatch, decision.Reason);
            Assert.NotNull(decision.GameDiff);
            Assert.Contains("1001", decision.GameDiff!.Changed);
            Assert.Contains("1001", decision.GameDiff.RebuildGuids);
            Assert.Single(decision.GameDiff.RebuildGuids);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenOutputXmlTreeHasExtraNonInventoryFile()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            File.WriteAllText(Path.Combine(context.AssetOutputDir, "notes.txt"), "user added");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SideTreeMismatch, decision.Reason);
            Assert.Equal(OutputRunCache.PresenceIssueKind.ContentChanged, decision.SideTrees!.OutputXml);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenStampIsSchema5_WithoutSideTrees()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, outputSchemaOverride: 5);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SchemaMismatch, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenStampIsSchema3_WithoutOutputHashes()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            // Schema 3 stamps lack outputHash; older schema forces Full so the next success writes hashes.
            WriteMinimalInventory(
                context,
                root,
                guid: "1001",
                withMod: false,
                outputSchemaOverride: 3,
                includeOutputHashes: false);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SchemaMismatch, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenStampIsSchema4_AssetsOnlyModHash()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true, outputSchemaOverride: 4);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SchemaMismatch, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenModInfoJsonContentChanged()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            string modInfo = Path.Combine(context.AssetModOutputRoot, "1001 - Test", "modinfo.json");
            File.WriteAllText(modInfo, """{"ModID":"user-edited"}""");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Empty(decision.Presence!.MissingXml);
            Assert.Single(decision.Presence.MissingMod);
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.ContentChanged,
                decision.Presence.ModFailureKinds["1001"]);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenExtraFileAddedUnderModFolder()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            File.WriteAllText(
                Path.Combine(context.AssetModOutputRoot, "1001 - Test", "notes.txt"),
                "user added file");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Single(decision.Presence!.MissingMod);
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.ContentChanged,
                decision.Presence.ModFailureKinds["1001"]);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void TryComputeModPackageTreeFingerprint_Changes_WhenAnyFileEdited()
    {
        string root = CreateTempGameOutput();
        try
        {
            string modRoot = Path.Combine(root, "mod");
            Directory.CreateDirectory(Path.Combine(modRoot, "data"));
            File.WriteAllText(Path.Combine(modRoot, "modinfo.json"), "{}");
            File.WriteAllText(Path.Combine(modRoot, "data", "assets.xml"), "<Asset/>");

            string before = OutputRunCache.TryComputeModPackageTreeFingerprint(modRoot) ?? "";
            Assert.Equal(64, before.Length);

            File.WriteAllText(Path.Combine(modRoot, "modinfo.json"), """{"x":1}""");
            string afterEdit = OutputRunCache.TryComputeModPackageTreeFingerprint(modRoot) ?? "";
            Assert.NotEqual(before, afterEdit);

            File.WriteAllText(Path.Combine(modRoot, "modinfo.json"), "{}");
            File.WriteAllText(Path.Combine(modRoot, "extra.txt"), "x");
            string afterAdd = OutputRunCache.TryComputeModPackageTreeFingerprint(modRoot) ?? "";
            Assert.NotEqual(before, afterAdd);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenSchema4InventoryMissingOutputHash()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, includeOutputHashes: false);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Single(decision.Presence!.MissingXml);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void BuildInventoryFromDisk_WritesOutputHashes()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, createAssetMods: true);
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            List<OutputRunCache.InventoryEntry> inventory = OutputRunCache.BuildInventoryFromDisk(context);
            OutputRunCache.InventoryEntry entry = Assert.Single(inventory);
            Assert.False(string.IsNullOrWhiteSpace(entry.OutputHash));
            Assert.False(string.IsNullOrWhiteSpace(entry.ModOutputHash));
            Assert.Equal(64, entry.OutputHash.Length);
            Assert.Equal(64, entry.ModOutputHash.Length);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenInventoryXmlHasLeadingBlankLines()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            string xmlFull = Path.Combine(root, "output_xml_anno1800", "1001 - [ Test ].xml");
            File.WriteAllText(xmlFull, "\r\n\r\n<!-- Generated by Asset Splitter. -->\r\n\r\n<ModOps><ModOp GUID=\"1001\"/></ModOps>");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Single(decision.Presence!.MissingXml);
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.Incomplete,
                decision.Presence.XmlFailureKinds["1001"]);
            Assert.False(OutputRunCache.IsIntactOutputXml(xmlFull));
            Assert.Contains("1001", decision.Presence.GuidsNeedingRebuild);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenModAssetsXmlHasLeadingBlankLines()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            string modAssets = Path.Combine(
                context.AssetModOutputRoot,
                "1001 - Test",
                "data",
                "config",
                "export",
                "main",
                "asset",
                "assets.xml");
            File.WriteAllText(modAssets, "\n\n<!-- Generated by Asset Splitter. -->\n\n<Asset/>");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Empty(decision.Presence!.MissingXml);
            Assert.Single(decision.Presence.MissingMod);
            Assert.Contains("1001", decision.Presence.GuidsNeedingRebuild);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void IsIntactOutputXml_AcceptsBomThenMarkup_RejectsLeadingWhitespace()
    {
        string root = CreateTempGameOutput();
        try
        {
            string good = Path.Combine(root, "good.xml");
            string bomGood = Path.Combine(root, "bom-good.xml");
            string blank = Path.Combine(root, "blank.xml");
            string empty = Path.Combine(root, "empty.xml");

            File.WriteAllText(good, "<!-- hdr -->\n<root/>");
            File.WriteAllBytes(bomGood, [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes("<root/>")]);
            File.WriteAllText(blank, "\n<root/>");
            File.WriteAllText(empty, "");

            Assert.True(OutputRunCache.IsIntactOutputXml(good));
            Assert.True(OutputRunCache.IsIntactOutputXml(bomGood));
            Assert.False(OutputRunCache.IsIntactOutputXml(blank));
            Assert.False(OutputRunCache.IsIntactOutputXml(empty));
            Assert.False(OutputRunCache.IsIntactOutputXml(Path.Combine(root, "missing.xml")));
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Repair_WhenXmlMissing()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);
            File.Delete(Path.Combine(root, "output_xml_anno1800", "1001 - [ Test ].xml"));

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Repair, decision.Mode);
            Assert.NotNull(decision.Presence);
            Assert.Single(decision.Presence!.MissingXml);
            Assert.Contains("1001", decision.Presence.GuidsNeedingRebuild);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenOptionsChanged()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            context.ModOpsWrap = false; // stay eligible; change fingerprint
            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.OptionsChanged, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenSingleGuid_NotEligible()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, createAssetMods: false);
            context.SingleAssetGuid = "1001";
            context.DetectedGameBuild = SampleBuild();

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.NotEligible, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Parse_ForceFullRebuild_Flag()
    {
        var options = AssetProcessorCommandLineOptions.Parse(
        [
            @"C:\Games\Anno",
            @"C:\Out",
            "english",
            "-c",
            "--force-full-rebuild",
        ]);

        Assert.True(options.ForceFullRebuild);
        Assert.True(options.AddComments);
    }

    private static PipelineContext CreateContext(
        string? gameOutputRoot = null,
        bool addComments = false,
        bool createAssetMods = false,
        bool splitTemplates = false)
    {
        string root = gameOutputRoot ?? Path.Combine(Path.GetTempPath(), "as-out-" + Guid.NewGuid().ToString("N"));
        string sourceXml = Path.Combine(root, "source_xml_anno1800");
        return new PipelineContext
        {
            GameOutputRoot = root,
            AnnoAssetsRoot = root,
            AssetOutputDir = Path.Combine(root, "output_xml_anno1800"),
            AssetModOutputRoot = Path.Combine(root, "output_xml_anno1800_mods"),
            SourceXmlFolder = sourceXml + Path.DirectorySeparatorChar,
            AssetLanguage = "texts_english.xml",
            ReadmeLanguage = "english",
            AddComments = addComments,
            CreateAssetMods = createAssetMods,
            SplitTemplates = splitTemplates,
            ModOpsWrap = true,
            NoDefaultProperties = true,
            DetectedGameType = "anno1800",
        };
    }

    private static GameBuildInfo SampleBuild() =>
        new("1.0.0.1", "//branch", "1", "Wed Jun 24 19:41:11 2026", @"C:\Games\Anno\Anno.exe");

    private static void EnsureMinimalSourceXml(string root, string gameType = "anno1800")
    {
        string sourceDir = Path.Combine(root, "source_xml_" + gameType);
        Directory.CreateDirectory(sourceDir);
        foreach (string name in SourceXmlCatalog.GetExpectedFileNames(gameType, "texts_english.xml"))
        {
            string path = Path.Combine(sourceDir, name);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "<root/>");
            }
        }
    }

    private static void WriteMinimalInventory(
        PipelineContext context,
        string root,
        string guid,
        bool withMod,
        int? outputSchemaOverride = null,
        bool includeOutputHashes = true,
        bool withTemplates = false,
        string? sourceHashOverride = null)
    {
        EnsureMinimalSourceXml(root, "anno1800");
        context.SourceXmlFolder = Path.Combine(root, "source_xml_anno1800") + Path.DirectorySeparatorChar;

        string xmlDir = context.AssetOutputDir;
        Directory.CreateDirectory(xmlDir);
        string xmlRelative = Path.Combine("output_xml_anno1800", $"{guid} - [ Test ].xml").Replace('\\', '/');
        string xmlFull = Path.Combine(root, "output_xml_anno1800", $"{guid} - [ Test ].xml");
        File.WriteAllText(xmlFull, "<ModOps><ModOp GUID=\"" + guid + "\"/></ModOps>");

        string? modRelative = null;
        if (withMod)
        {
            string modDir = Path.Combine(context.AssetModOutputRoot, $"{guid} - Test");
            Directory.CreateDirectory(Path.Combine(modDir, "data", "config", "export", "main", "asset"));
            File.WriteAllText(Path.Combine(modDir, "modinfo.json"), "{}");
            string modAssetsFull = Path.Combine(modDir, "data", "config", "export", "main", "asset", "assets.xml");
            File.WriteAllText(modAssetsFull, "<Asset/>");
            modRelative = Path.GetRelativePath(root, modDir).Replace('\\', '/');
        }

        if (withTemplates || context.SplitTemplates)
        {
            context.SplitTemplates = true;
            string templatesDir = OutputDirectoryManager.GetTemplatesOutputDirectory(root, "anno1800");
            Directory.CreateDirectory(templatesDir);
            File.WriteAllText(Path.Combine(templatesDir, "AchievementReward.xml"), "<Template/>");
        }

        context.DetectedGameBuild = SampleBuild();
        var entry = new OutputRunCache.InventoryEntry
        {
            Guid = guid,
            XmlRelativePath = xmlRelative,
            ModRelativePath = modRelative,
            SourceHash = sourceHashOverride ?? "a".PadRight(64, '0'),
            OutputHash = includeOutputHashes
                ? (OutputRunCache.TryComputeFileSha256Hex(xmlFull) ?? "")
                : "",
            ModOutputHash = includeOutputHashes && withMod
                ? (OutputRunCache.TryComputeModPackageTreeFingerprint(
                    Path.Combine(context.AssetModOutputRoot, $"{guid} - Test")) ?? "")
                : "",
        };

        string inventoryPath = Path.Combine(root, OutputRunCache.InventoryFileName);
        File.WriteAllText(inventoryPath, System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine);

        WriteSchemaStamp(context, root, withMod ? 1 : 0, outputSchemaOverride);
    }

    private static System.Xml.XmlNode LoadFirstAsset(string assetsPath)
    {
        var doc = new System.Xml.XmlDocument();
        doc.Load(assetsPath);
        System.Xml.XmlNode? asset = doc.SelectSingleNode("//Asset");
        Assert.NotNull(asset);
        return asset!;
    }

    private static void WriteSchemaStamp(
        PipelineContext context,
        string root,
        int modCount,
        int? outputSchemaOverride = null)
    {
        var stamp = new OutputRunCache.StampDocument
        {
            Schema = OutputRunCache.SchemaVersion,
            OutputSchema = outputSchemaOverride ?? OutputRunCache.OutputSchemaVersion,
            GameType = "anno1800",
            BuildFingerprint = SourceXmlCache.BuildFingerprint(SampleBuild()),
            OptionsFingerprint = OutputRunCache.BuildOptionsFingerprint(context),
            BuildDisplay = SampleBuild().ToDisplayString(),
            AssetXmlCount = 1,
            ModCount = modCount,
            CompletedUtc = "2026-01-01T00:00:00Z",
        };

        if (outputSchemaOverride is null || outputSchemaOverride == OutputRunCache.OutputSchemaVersion)
        {
            OutputRunCache.PopulateStampTreeFingerprints(context, "anno1800", stamp);
        }

        File.WriteAllText(
            Path.Combine(root, OutputRunCache.StampFileName),
            System.Text.Json.JsonSerializer.Serialize(stamp, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void DiffAgainstInventory_Classifies_Added_Changed_Removed()
    {
        var inventory = new List<OutputRunCache.InventoryEntry>
        {
            new() { Guid = "1", XmlRelativePath = "a.xml", SourceHash = "aaa" },
            new() { Guid = "2", XmlRelativePath = "b.xml", SourceHash = "bbb" },
            new() { Guid = "3", XmlRelativePath = "c.xml", SourceHash = "ccc" },
        };
        var newHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "aaa", // unchanged
            ["2"] = "BBB-changed",
            ["4"] = "ddd", // added
        };

        var assetsXml = new System.Xml.XmlDocument();
        assetsXml.LoadXml("<Groups/>");

        OutputRunCache.GameDiffResult diff = OutputRunCache.DiffAgainstInventory(
            inventory,
            newHashes,
            assetsXml,
            expandDependencyDescendants: false);

        Assert.Contains("4", diff.Added);
        Assert.Contains("2", diff.Changed);
        Assert.Contains("3", diff.Removed);
        Assert.DoesNotContain("1", diff.RebuildGuids);
        Assert.Contains("2", diff.RebuildGuids);
        Assert.Contains("4", diff.RebuildGuids);
        Assert.DoesNotContain("3", diff.RebuildGuids);
        Assert.Single(diff.ReplacedEntries);
        Assert.Equal("2", diff.ReplacedEntries[0].Guid);
        Assert.Equal("b.xml", diff.ReplacedEntries[0].XmlRelativePath);
    }

    [Fact]
    public void DiffAgainstInventory_WithFixDependencies_IncludesDescendants()
    {
        var inventory = new List<OutputRunCache.InventoryEntry>
        {
            new() { Guid = "10", XmlRelativePath = "p.xml", SourceHash = "parent-old" },
            new() { Guid = "11", XmlRelativePath = "c.xml", SourceHash = "child-same" },
        };
        var newHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["10"] = "parent-new",
            ["11"] = "child-same",
        };

        var assetsXml = new System.Xml.XmlDocument();
        assetsXml.LoadXml("""
            <Groups>
              <Assets>
                <Asset>
                  <BaseAssetGUID>10</BaseAssetGUID>
                  <Values><Standard><GUID>11</GUID></Standard></Values>
                </Asset>
                <Asset>
                  <Template>Building</Template>
                  <Values><Standard><GUID>10</GUID></Standard></Values>
                </Asset>
              </Assets>
            </Groups>
            """);

        OutputRunCache.GameDiffResult diff = OutputRunCache.DiffAgainstInventory(
            inventory,
            newHashes,
            assetsXml,
            expandDependencyDescendants: true);

        Assert.Contains("10", diff.Changed);
        Assert.Contains("10", diff.RebuildGuids);
        Assert.Contains("11", diff.RebuildGuids);
        Assert.Equal(2, diff.ReplacedEntries.Count);
        Assert.Contains(diff.ReplacedEntries, e => e.Guid == "10" && e.XmlRelativePath == "p.xml");
        Assert.Contains(diff.ReplacedEntries, e => e.Guid == "11" && e.XmlRelativePath == "c.xml");
    }

    [Fact]
    public void DeleteReplacedEntries_RemovesPriorXmlAndModFolder()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, createAssetMods: true);
            string xmlRel = "output_xml_anno1800/Templates/Building/2 - [ Changed ].xml";
            string modRel = "mods/2 - [ Changed ]";
            string xmlFull = Path.Combine(root, xmlRel.Replace('/', Path.DirectorySeparatorChar));
            string modFull = Path.Combine(root, modRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(xmlFull)!);
            Directory.CreateDirectory(modFull);
            File.WriteAllText(xmlFull, "<Asset/>");
            File.WriteAllText(Path.Combine(modFull, "modinfo.json"), "{}");

            var diff = new OutputRunCache.GameDiffResult();
            diff.ReplacedEntries.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "2",
                XmlRelativePath = xmlRel,
                ModRelativePath = modRel,
                SourceHash = "old",
            });

            OutputRunCache.DeleteReplacedEntries(context, diff);

            Assert.False(File.Exists(xmlFull));
            Assert.False(Directory.Exists(modFull));
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void DeletePresenceRebuildEntries_RemovesEmptyXmlAndBrokenModFolder()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, createAssetMods: true);
            string emptyXmlRel = "output_xml_anno1800/Templates/Building/9 - [ Empty ].xml";
            string emptyModRel = "mods/9 - [ Empty ]";
            string emptyXmlFull = Path.Combine(root, emptyXmlRel.Replace('/', Path.DirectorySeparatorChar));
            string emptyModFull = Path.Combine(root, emptyModRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(emptyXmlFull)!);
            Directory.CreateDirectory(emptyModFull);
            File.WriteAllText(emptyXmlFull, ""); // zero-byte still occupies path for ReserveUniqueOutputPath
            File.WriteAllText(Path.Combine(emptyModFull, "modinfo.json"), "{}");

            string okXmlRel = "output_xml_anno1800/Templates/Building/8 - [ Ok ].xml";
            string brokenModRel = "mods/8 - [ Ok ]";
            string okXmlFull = Path.Combine(root, okXmlRel.Replace('/', Path.DirectorySeparatorChar));
            string brokenModFull = Path.Combine(root, brokenModRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(okXmlFull)!);
            Directory.CreateDirectory(brokenModFull);
            File.WriteAllText(okXmlFull, "<Asset/>");
            // Invalid package: modinfo only, no assets.xml

            var presence = new OutputRunCache.PresenceResult();
            presence.MissingXml.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "9",
                XmlRelativePath = emptyXmlRel,
                ModRelativePath = emptyModRel,
            });
            presence.MissingMod.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "8",
                XmlRelativePath = okXmlRel,
                ModRelativePath = brokenModRel,
            });

            OutputRunCache.DeletePresenceRebuildEntries(context, presence);

            Assert.False(File.Exists(emptyXmlFull));
            Assert.False(Directory.Exists(emptyModFull));
            Assert.True(File.Exists(okXmlFull), "Valid XML for MissingMod-only must stay");
            Assert.False(Directory.Exists(brokenModFull));
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_DiffRepair_WhenInventoryMissingSourceHashes_AndSourceXmlEdited()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: "");

            // User deleted / edited extractable source asset content (still well-formed).
            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test Truncated</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.DiffRepair, decision.Mode);
            Assert.NotEqual(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SideTreeMismatch, decision.Reason);
            Assert.NotNull(decision.GameDiff);
            Assert.Contains("1001", decision.GameDiff!.RebuildGuids);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_DiffRepair_WhenSourceXmlTruncated_RemovesAsset_NotFull()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Keep</Name></Standard></Values>
                    </Asset>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1002</GUID><Name>Drop</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);

            var doc = new System.Xml.XmlDocument();
            doc.Load(assetsPath);
            System.Xml.XmlNodeList assets = doc.SelectNodes("//Asset")!;
            string hash1001 = AssetSourceHasher.HashAssetNode(assets[0]!);
            string hash1002 = AssetSourceHasher.HashAssetNode(assets[1]!);

            // Stamp + inventory while both assets still exist in source_xml.
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: hash1001);

            string xml2 = Path.Combine(root, "output_xml_anno1800", "1002 - [ Drop ].xml");
            File.WriteAllText(xml2, "<ModOps><ModOp GUID=\"1002\"/></ModOps>");
            File.AppendAllText(
                Path.Combine(root, OutputRunCache.InventoryFileName),
                System.Text.Json.JsonSerializer.Serialize(new OutputRunCache.InventoryEntry
                {
                    Guid = "1002",
                    XmlRelativePath = "output_xml_anno1800/1002 - [ Drop ].xml",
                    SourceHash = hash1002,
                    OutputHash = OutputRunCache.TryComputeFileSha256Hex(xml2) ?? "",
                }) + Environment.NewLine);

            // Truncate source: delete the 1002 asset (still well-formed XML).
            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Keep</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.DiffRepair, decision.Mode);
            Assert.NotEqual(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.NotNull(decision.GameDiff);
            Assert.Contains("1002", decision.GameDiff!.Removed);
            Assert.DoesNotContain("1001", decision.GameDiff.RebuildGuids);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenInventoryMissingSourceHashes_AndOutputsIntact()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            string assetXml = """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """;
            File.WriteAllText(assetsPath, assetXml);
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: "");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.HashesMissing, decision.Reason);
            Assert.NotNull(decision.GameDiff);
            Assert.False(decision.GameDiff!.HasWork);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_DiffRepair_WhenInventoryMissingSourceHashes_AndOutputContentChanged()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: "");

            string xmlFull = Path.Combine(root, "output_xml_anno1800", "1001 - [ Test ].xml");
            File.WriteAllText(xmlFull, "<ModOps><ModOp GUID=\"1001\"/><!-- edited --></ModOps>");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.DiffRepair, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.HashesMissing, decision.Reason);
            Assert.NotNull(decision.GameDiff);
            Assert.Contains("1001", decision.GameDiff!.RebuildGuids);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenInventoryMissingSourceHashes_OnBuildChange_AndOutputsIntact()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            context.AssetTemplatesList = ["Building"];

            string assetsPath = Path.Combine(root, "source_xml_anno1800", "assets.xml");
            EnsureMinimalSourceXml(root);
            File.WriteAllText(
                assetsPath,
                """
                <Groups>
                  <Assets>
                    <Asset>
                      <Template>Building</Template>
                      <Values><Standard><GUID>1001</GUID><Name>Test</Name></Standard></Values>
                    </Asset>
                  </Assets>
                </Groups>
                """);
            WriteMinimalInventory(context, root, guid: "1001", withMod: false, sourceHashOverride: "");

            context.DetectedGameBuild = new GameBuildInfo("2.0.0.2", "//branch", "2", "Thu Jan 1 00:00:00 2099", @"C:\Games\Anno\Anno.exe");
            context.SourceXmlFolder = Path.Combine(root, "source_xml_anno1800") + Path.DirectorySeparatorChar;

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.HashesMissing, decision.Reason);
            Assert.NotEqual(OutputRunCache.RunMode.Full, decision.Mode);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Full_WhenInventoryMissingSourceHashes_OnBuildChange()
    {
        // Legacy name kept: blank hashes no longer force Full when outputs/source are usable.
        // Retained as a guard that SourceAssetsUnavailable still Fulls when assets.xml is unusable.
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: false);

            string invPath = Path.Combine(root, OutputRunCache.InventoryFileName);
            var entry = new OutputRunCache.InventoryEntry
            {
                Guid = "1001",
                XmlRelativePath = "output_xml_anno1800/1001 - [ Test ].xml",
                SourceHash = "",
                OutputHash = OutputRunCache.TryComputeFileSha256Hex(
                    Path.Combine(root, "output_xml_anno1800", "1001 - [ Test ].xml")) ?? "",
            };
            File.WriteAllText(invPath, System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine);

            context.DetectedGameBuild = new GameBuildInfo("2.0.0.2", "//branch", "2", "Thu Jan 1 00:00:00 2099", @"C:\Games\Anno\Anno.exe");
            context.SourceXmlFolder = Path.Combine(root, "source_xml_anno1800") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(context.SourceXmlFolder);
            // Unreadable / non-XML assets.xml → cannot backfill.
            File.WriteAllText(Path.Combine(context.SourceXmlFolder, "assets.xml"), "NOT XML");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SourceAssetsUnavailable, decision.Reason);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void InventoryHasAllSourceHashes_False_WhenAnyBlank()
    {
        var inventory = new List<OutputRunCache.InventoryEntry>
        {
            new() { Guid = "1", XmlRelativePath = "a.xml", SourceHash = "abc" },
            new() { Guid = "2", XmlRelativePath = "b.xml", SourceHash = "" },
        };
        Assert.False(OutputRunCache.InventoryHasAllSourceHashes(inventory));
    }

    [Fact]
    public void Evaluate_Full_WhenOnlyOrphanMods_ChangeModsTreeHash()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: true);
            context.DetectedGameBuild = SampleBuild();
            WriteMinimalInventory(context, root, guid: "1001", withMod: true);

            // Orphan mod folder changes the mods root tree fingerprint → Full (not silent Complete).
            string orphanMod = Path.Combine(context.AssetModOutputRoot, "Sequence", "minus-1 - Archive Quest Entry");
            Directory.CreateDirectory(Path.Combine(orphanMod, "data", "config", "export", "main", "asset"));
            File.WriteAllText(Path.Combine(orphanMod, "modinfo.json"), "{}");
            File.WriteAllText(Path.Combine(orphanMod, "data", "config", "export", "main", "asset", "assets.xml"), "<Asset/>");

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Full, decision.Mode);
            Assert.Equal(OutputRunCache.MissReason.SideTreeMismatch, decision.Reason);
            Assert.NotNull(decision.SideTrees);
            Assert.Equal(OutputRunCache.PresenceIssueKind.ContentChanged, decision.SideTrees!.OutputMods);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void Evaluate_Complete_WhenInventoryMatches_WithTemplateFolders()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, addComments: true, createAssetMods: false);
            context.CreateTemplateFolders = true;
            context.DetectedGameBuild = SampleBuild();
            EnsureMinimalSourceXml(root);

            string xmlDir = Path.Combine(context.AssetOutputDir, "Building");
            Directory.CreateDirectory(xmlDir);
            string xmlFull = Path.Combine(xmlDir, "1001 - [ Test ].xml");
            File.WriteAllText(xmlFull, "<ModOps><ModOp GUID=\"1001\"/></ModOps>");
            string xmlRelative = Path.GetRelativePath(root, xmlFull).Replace('\\', '/');

            var entry = new OutputRunCache.InventoryEntry
            {
                Guid = "1001",
                XmlRelativePath = xmlRelative,
                SourceHash = "a".PadRight(64, '0'),
                OutputHash = OutputRunCache.TryComputeFileSha256Hex(xmlFull) ?? "",
            };
            File.WriteAllText(
                Path.Combine(root, OutputRunCache.InventoryFileName),
                System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine);

            WriteSchemaStamp(context, root, modCount: 0);

            OutputRunCache.Decision decision = OutputRunCache.Evaluate(context, "anno1800", forceFullRebuild: false);
            Assert.Equal(OutputRunCache.RunMode.Complete, decision.Mode);
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Theory]
    [InlineData("1001", "Bare Name")]
    [InlineData("1001", "1001 - Already Prefixed")]
    [InlineData("1001", "")]
    public void FormatGuidAndName_UsesLocalizedLabelTemplate(string guid, string name)
    {
        string actual = OutputRunCache.FormatGuidAndName(guid, name);
        if (string.IsNullOrEmpty(name))
        {
            Assert.Equal(guid, actual);
            return;
        }

        if (name.StartsWith(guid, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(name, actual);
            return;
        }

        Assert.Equal(
            string.Format(ConsoleMessages.Get("outputCacheRepairAssetLabel"), guid, name),
            actual);
    }

    [Fact]
    public void ResolveRepairAssetLabel_UsesInventoryPathStem()
    {
        var entry = new OutputRunCache.InventoryEntry
        {
            Guid = "9001",
            XmlRelativePath = "output_xml_anno1800/Buff/9001 - Sample Stem.xml",
        };

        Assert.Equal(
            "9001 - Sample Stem",
            OutputRunCache.ResolveRepairAssetLabel(entry));
    }

    [Fact]
    public void ResolveRepairAssetLabel_PrefersInventoryPathOverRegistry()
    {
        var context = CreateContext();
        context.AssetNames["9001"] = "Registry Name Only";
        var entry = new OutputRunCache.InventoryEntry
        {
            Guid = "9001",
            XmlRelativePath = "output_xml_anno1800/9001 - [ Inventory Stem ].xml",
        };

        Assert.Equal(
            "9001 - [ Inventory Stem ]",
            OutputRunCache.ResolveRepairAssetLabel(entry, context));
    }

    [Fact]
    public void ResolveRepairAssetLabel_FallsBackToRegistryWhenNoPath()
    {
        var context = CreateContext();
        context.AssetNames["9001"] = "Registry Name Only";
        var entry = new OutputRunCache.InventoryEntry
        {
            Guid = "9001",
            XmlRelativePath = "",
        };

        Assert.Equal(
            string.Format(ConsoleMessages.Get("outputCacheRepairAssetLabel"), "9001", "Registry Name Only"),
            OutputRunCache.ResolveRepairAssetLabel(entry, context));
    }

    [Fact]
    public void BuildPresenceRepairDetails_TruncatesToLimitWithMoreCount()
    {
        var presence = new OutputRunCache.PresenceResult();
        for (int i = 1; i <= OutputRunCache.RepairDetailListLimit + 5; i++)
        {
            string guid = i.ToString();
            presence.MissingXml.Add(new OutputRunCache.InventoryEntry
            {
                Guid = guid,
                XmlRelativePath = $"output/{guid} - Asset {guid}.xml",
            });
            presence.XmlFailureKinds[guid] = OutputRunCache.PresenceIssueKind.Missing;
        }

        IReadOnlyList<OutputRunCache.RepairDetailItem> details =
            OutputRunCache.BuildPresenceRepairDetails(presence);
        Assert.Equal(OutputRunCache.RepairDetailListLimit + 5, details.Count);

        int listed = Math.Min(details.Count, OutputRunCache.RepairDetailListLimit);
        int omitted = details.Count - listed;
        Assert.Equal(OutputRunCache.RepairDetailListLimit, listed);
        Assert.Equal(5, omitted);
        Assert.Contains("{0", ConsoleMessages.Get("outputCacheRepairMore"));
        Assert.Equal(
            string.Format(ConsoleMessages.Get("outputCacheRepairMore"), 5),
            string.Format(ConsoleMessages.Get("outputCacheRepairMore"), omitted));
        Assert.All(
            details.Take(listed),
            item => Assert.Equal("outputCacheRepairItemMissing", item.MessageKey));
    }

    [Fact]
    public void ClassifyOutputArtifact_DistinguishesMissingIncompleteAndContentChanged()
    {
        string root = CreateTempGameOutput();
        try
        {
            string missing = Path.Combine(root, "gone.xml");
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.Missing,
                OutputRunCache.ClassifyOutputArtifact(missing, "abc"));

            string incomplete = Path.Combine(root, "blank.xml");
            File.WriteAllText(incomplete, "\r\n<root/>");
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.Incomplete,
                OutputRunCache.ClassifyOutputArtifact(incomplete, "abc"));

            string changed = Path.Combine(root, "changed.xml");
            File.WriteAllText(changed, "<root/>");
            string hash = OutputRunCache.TryComputeFileSha256Hex(changed) ?? "";
            Assert.Null(OutputRunCache.ClassifyOutputArtifact(changed, hash));
            Assert.Equal(
                OutputRunCache.PresenceIssueKind.ContentChanged,
                OutputRunCache.ClassifyOutputArtifact(changed, "0".PadLeft(64, '0')));
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Theory]
    [InlineData("1001 - Test", "1001")]
    [InlineData("minus-1 - Archive Quest Entry", "-1")]
    [InlineData("minus-12 - Name", "-12")]
    [InlineData("-1 - Archive Quest Entry", "-1")]
    [InlineData("not-a-guid - x", null)]
    public void TryParseModFolderGuid_HandlesMinusPrefix(string folderName, string? expected)
    {
        bool ok = OutputRunCache.TryParseModFolderGuid(folderName, out string guid);
        if (expected is null)
        {
            Assert.False(ok);
        }
        else
        {
            Assert.True(ok);
            Assert.Equal(expected, guid);
        }
    }

    [Fact]
    public void BuildInventoryFromDisk_IncludesNegativeGuidXmlAndMinusModFolder()
    {
        string root = CreateTempGameOutput();
        try
        {
            var context = CreateContext(gameOutputRoot: root, createAssetMods: true);
            context.CreateTemplateFolders = true;
            Directory.CreateDirectory(Path.Combine(context.AssetOutputDir, "Sequence"));
            File.WriteAllText(
                Path.Combine(context.AssetOutputDir, "Sequence", "-1 - [ Archive Quest Entry ].xml"),
                "<ModOps><ModOp GUID=\"-1\"/></ModOps>");

            string modDir = Path.Combine(context.AssetModOutputRoot, "Sequence", "minus-1 - Archive Quest Entry");
            Directory.CreateDirectory(Path.Combine(modDir, "data", "config", "export", "main", "asset"));
            File.WriteAllText(Path.Combine(modDir, "modinfo.json"), "{}");
            File.WriteAllText(Path.Combine(modDir, "data", "config", "export", "main", "asset", "assets.xml"), "<Asset/>");

            List<OutputRunCache.InventoryEntry> inventory = OutputRunCache.BuildInventoryFromDisk(context);
            Assert.Contains(inventory, e => e.Guid == "-1" && e.XmlRelativePath.Contains("-1 - [ Archive Quest Entry ].xml", StringComparison.Ordinal));
            Assert.Contains(inventory, e => e.Guid == "-1" && e.ModRelativePath != null && e.ModRelativePath.Contains("minus-1", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTemp(root);
        }
    }

    [Fact]
    public void RepairActionMessageKeys_FormatWithLabelOnly()
    {
        Assert.Equal(
            "[MODS] Rebuilding mod because content-changed: 49840 - [ Buff Prydein Radiance 1 ]",
            string.Format(
                ConsoleMessages.Get("modRepairBecauseContentChanged"),
                "49840 - [ Buff Prydein Radiance 1 ]"));
        Assert.Equal(
            "[EXTRACT] Rebuilding XML because missing: 1001 - [ Test ]",
            string.Format(ConsoleMessages.Get("extractRepairBecauseMissing"), "1001 - [ Test ]"));
        Assert.Equal(
            "[MODS] Recreated 1 mod folder(s) after output repair. Skipped 0.",
            string.Format(ConsoleMessages.Get("assetModsCreatedSummaryRepair"), "1", "0"));
        Assert.Contains(
            "not found on disk",
            string.Format(ConsoleMessages.Get("outputCacheRepairItemMissing"), "guid"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "empty, corrupt, or incomplete",
            string.Format(ConsoleMessages.Get("outputCacheRepairItemIncomplete"), "guid"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMapItemKeyToRepairActionKeys_CoversPresenceAndDiffReasons()
    {
        Assert.Equal("modRepairBecauseMissing", OutputRunCache.TryMapItemKeyToModRepairActionKey("outputCacheRepairItemMissing"));
        Assert.Equal("modRepairBecauseIncomplete", OutputRunCache.TryMapItemKeyToModRepairActionKey("outputCacheRepairItemIncomplete"));
        Assert.Equal("modRepairBecauseAdded", OutputRunCache.TryMapItemKeyToModRepairActionKey("outputCacheRepairItemAdded"));
        Assert.Equal("modRepairBecauseChanged", OutputRunCache.TryMapItemKeyToModRepairActionKey("outputCacheRepairItemChanged"));
        Assert.Equal("modRepairBecauseRebuild", OutputRunCache.TryMapItemKeyToModRepairActionKey("outputCacheRepairItemRebuild"));
        Assert.Null(OutputRunCache.TryMapItemKeyToModRepairActionKey("unknownKey"));
        Assert.Equal("extractRepairBecauseChanged", OutputRunCache.TryMapItemKeyToExtractRepairActionKey("outputCacheRepairItemChanged"));
    }

    private static string CreateTempGameOutput()
    {
        string root = Path.Combine(Path.GetTempPath(), "as-outcache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemp(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
