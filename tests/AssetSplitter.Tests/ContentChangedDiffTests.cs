using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class ContentChangedDiffTests
{
    [Fact]
    public void TextLineDiff_Compare_ReportsLineNumbersAndOldVsNewText()
    {
        const string oldText = """
            line-a
            old-middle
            line-c
            """;
        const string newText = """
            line-a
            new-middle
            line-c
            """;

        TextLineDiff.Result result = TextLineDiff.Compare(oldText, newText);

        Assert.Equal(2, result.TotalChangeLines);
        Assert.Equal(2, result.Reported.Count);
        Assert.False(result.Reported[0].IsAddition);
        Assert.Equal(2, result.Reported[0].LineNumber);
        Assert.Equal("old-middle", result.Reported[0].Text);
        Assert.True(result.Reported[1].IsAddition);
        Assert.Equal(2, result.Reported[1].LineNumber);
        Assert.Equal("new-middle", result.Reported[1].Text);
    }

    [Fact]
    public void TextLineDiff_Compare_CapsReportedLines_AndTracksTotal()
    {
        string oldText = string.Join('\n', Enumerable.Range(1, 60).Select(i => $"old-{i}"));
        string newText = string.Join('\n', Enumerable.Range(1, 60).Select(i => $"new-{i}"));

        TextLineDiff.Result result = TextLineDiff.Compare(oldText, newText, maxReportedChangeLines: 40);

        Assert.Equal(120, result.TotalChangeLines);
        Assert.Equal(40, result.Reported.Count);
    }

    [Fact]
    public void TextLineDiff_Truncate_AppendsEllipsis()
    {
        Assert.Equal("abc…", TextLineDiff.Truncate("abcdef", maxChars: 4));
        Assert.Equal("abcd", TextLineDiff.Truncate("abcd", maxChars: 10));
    }

    [Fact]
    public void BuildContentChangedDiffLogLines_FormatsExpectedKeys()
    {
        const string oldText = "alpha\nuser-edit\ngamma";
        const string newText = "alpha\nrebuilt\ngamma";

        IReadOnlyList<(string MessageKey, object[] Args)> lines =
            OutputRunCache.BuildContentChangedDiffLogLines(
                "140054 - DiplomacyBalancing",
                OutputRunCache.ContentChangedArtifactKind.FlatXml,
                oldText,
                newText);

        Assert.Equal(3, lines.Count);
        Assert.Equal("contentChangedDiffHeaderXml", lines[0].MessageKey);
        Assert.Equal(["140054 - DiplomacyBalancing"], lines[0].Args);
        Assert.Equal("contentChangedDiffRemoved", lines[1].MessageKey);
        Assert.Equal([2, "user-edit"], lines[1].Args);
        Assert.Equal("contentChangedDiffAdded", lines[2].MessageKey);
        Assert.Equal([2, "rebuilt"], lines[2].Args);

        string header = string.Format(ConsoleMessages.Get(lines[0].MessageKey), lines[0].Args);
        string removed = string.Format(ConsoleMessages.Get(lines[1].MessageKey), lines[1].Args);
        string added = string.Format(ConsoleMessages.Get(lines[2].MessageKey), lines[2].Args);
        Assert.Contains("content-changed detail (XML)", header, StringComparison.Ordinal);
        Assert.Contains("DiplomacyBalancing", header, StringComparison.Ordinal);
        Assert.Equal("[OUTPUT CACHE]     - L2: user-edit", removed);
        Assert.Equal("[OUTPUT CACHE]     + L2: rebuilt", added);
    }

    [Fact]
    public void BuildContentChangedDiffLogLines_IncludesMoreKey_WhenCapped()
    {
        string oldText = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"o{i}"));
        string newText = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"n{i}"));

        IReadOnlyList<(string MessageKey, object[] Args)> lines =
            OutputRunCache.BuildContentChangedDiffLogLines(
                "1001 - [ Test ]",
                OutputRunCache.ContentChangedArtifactKind.ModAssetsXml,
                oldText,
                newText,
                maxReportedChangeLines: 10);

        Assert.Equal("contentChangedDiffHeaderMod", lines[0].MessageKey);
        Assert.Equal(12, lines.Count); // header + 10 changes + more
        Assert.Equal("contentChangedDiffMore", lines[^1].MessageKey);
        Assert.Equal(50, Assert.Single(lines[^1].Args)); // 60 total - 10 reported
    }

    [Fact]
    public void BuildContentChangedDiffLogLines_ReturnsEmpty_WhenIdentical()
    {
        IReadOnlyList<(string MessageKey, object[] Args)> lines =
            OutputRunCache.BuildContentChangedDiffLogLines(
                "1001",
                OutputRunCache.ContentChangedArtifactKind.FlatXml,
                "same\ntext",
                "same\ntext");

        Assert.Empty(lines);
    }

    [Fact]
    public void BuildModPackageTreeDiffLogLines_ReportsPathAndJsonLineDiff()
    {
        string root = Path.Combine(Path.GetTempPath(), "asui-ccdiff-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string oldMod = Path.Combine(root, "old");
            string newMod = Path.Combine(root, "new");
            Directory.CreateDirectory(oldMod);
            Directory.CreateDirectory(newMod);

            File.WriteAllText(Path.Combine(oldMod, "modinfo.json"), "{\n  \"ModID\": \"edited\"\n}\n");
            File.WriteAllText(Path.Combine(oldMod, "assets.xml"), "<Asset/>");
            File.WriteAllText(Path.Combine(oldMod, "extra.txt"), "extra");

            File.WriteAllText(Path.Combine(newMod, "modinfo.json"), "{\n  \"ModID\": \"clean\"\n}\n");
            File.WriteAllText(Path.Combine(newMod, "assets.xml"), "<Asset/>");

            OutputRunCache.ModPackageFileSnapshot? oldSnap = OutputRunCache.TryCaptureModPackageSnapshot(oldMod);
            Assert.NotNull(oldSnap);

            IReadOnlyList<(string MessageKey, object[] Args)> lines =
                OutputRunCache.BuildModPackageTreeDiffLogLines("1001 - Test", oldSnap!, newMod);

            Assert.Contains(lines, l => l.MessageKey == "contentChangedDiffHeaderModPackage");
            Assert.Contains(lines, l => l.MessageKey == "contentChangedDiffPathChanged" && (string)l.Args[0] == "modinfo.json");
            Assert.Contains(lines, l => l.MessageKey == "contentChangedDiffPathRemoved" && (string)l.Args[0] == "extra.txt");
            Assert.Contains(lines, l => l.MessageKey == "contentChangedDiffHeaderModFile" && (string)l.Args[0] == "modinfo.json");

            string header = string.Format(
                ConsoleMessages.Get("contentChangedDiffHeaderModPackage"),
                "1001 - Test");
            Assert.Contains("mod package", header, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void CaptureContentChangedSnapshots_SkipsMissingAndIncomplete()
    {
        string root = Path.Combine(Path.GetTempPath(), "asui-ccdiff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var context = new PipelineContext
            {
                GameOutputRoot = root,
                DetectedGameType = "anno1800",
                DebugMode = true,
            };

            string changedXml = Path.Combine(root, "changed.xml");
            File.WriteAllText(changedXml, "<ModOps><!-- dirty --></ModOps>");

            string incompleteXml = Path.Combine(root, "incomplete.xml");
            File.WriteAllText(incompleteXml, "");

            string modRoot = Path.Combine(root, "mod");
            string modAssets = Path.Combine(modRoot, "data", "config", "export", "main", "asset", "assets.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(modAssets)!);
            File.WriteAllText(modAssets, "<Asset><!-- dirty mod --></Asset>");
            File.WriteAllText(Path.Combine(modRoot, "modinfo.json"), "{}");

            var presence = new OutputRunCache.PresenceResult();
            presence.MissingXml.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "1001",
                XmlRelativePath = "changed.xml",
            });
            presence.XmlFailureKinds["1001"] = OutputRunCache.PresenceIssueKind.ContentChanged;

            presence.MissingXml.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "1002",
                XmlRelativePath = "incomplete.xml",
            });
            presence.XmlFailureKinds["1002"] = OutputRunCache.PresenceIssueKind.Incomplete;

            presence.MissingXml.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "1003",
                XmlRelativePath = "missing.xml",
            });
            presence.XmlFailureKinds["1003"] = OutputRunCache.PresenceIssueKind.Missing;

            presence.MissingMod.Add(new OutputRunCache.InventoryEntry
            {
                Guid = "1001",
                XmlRelativePath = "changed.xml",
                ModRelativePath = "mod",
            });
            presence.ModFailureKinds["1001"] = OutputRunCache.PresenceIssueKind.ContentChanged;

            OutputRunCache.CaptureContentChangedSnapshots(context, presence);

            Assert.NotNull(context.ContentChangedSnapshots);
            Assert.True(context.ContentChangedSnapshots!.FlatXmlByGuid.ContainsKey("1001"));
            Assert.Contains("dirty", context.ContentChangedSnapshots.FlatXmlByGuid["1001"], StringComparison.Ordinal);
            Assert.True(context.ContentChangedSnapshots.ModAssetsXmlByGuid.ContainsKey("1001"));
            Assert.True(context.ContentChangedSnapshots.ModPackageByGuid.ContainsKey("1001"));
            Assert.True(context.ContentChangedSnapshots.ModPackageByGuid["1001"].TextByRelativePath.ContainsKey("modinfo.json"));
            Assert.False(context.ContentChangedSnapshots.FlatXmlByGuid.ContainsKey("1002"));
            Assert.False(context.ContentChangedSnapshots.FlatXmlByGuid.ContainsKey("1003"));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void TryFlushFlatXmlContentChangedDiffs_ClearsSnapshotAfterRewrite()
    {
        string root = Path.Combine(Path.GetTempPath(), "asui-ccdiff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var context = new PipelineContext
            {
                GameOutputRoot = root,
                DebugMode = true,
            };
            context.ContentChangedSnapshots = new OutputRunCache.ContentChangedRepairSnapshots();
            context.ContentChangedSnapshots.FlatXmlByGuid["140054"] = "line-a\nold\nline-c";
            context.ContentChangedSnapshots.LabelsByGuid["140054"] = "140054 - DiplomacyBalancing";

            string newPath = Path.Combine(root, "140054 - [ DiplomacyBalancing ].xml");
            File.WriteAllText(newPath, "line-a\nnew\nline-c");
            context.RepairOutputFiles.Add(newPath);

            IReadOnlyList<(string MessageKey, object[] Args)> expected =
                OutputRunCache.BuildContentChangedDiffLogLines(
                    "140054 - DiplomacyBalancing",
                    OutputRunCache.ContentChangedArtifactKind.FlatXml,
                    "line-a\nold\nline-c",
                    File.ReadAllText(newPath));

            Assert.Equal(3, expected.Count);
            Assert.Equal(
                "[OUTPUT CACHE]     - L2: old",
                string.Format(ConsoleMessages.Get(expected[1].MessageKey), expected[1].Args));
            Assert.Equal(
                "[OUTPUT CACHE]     + L2: new",
                string.Format(ConsoleMessages.Get(expected[2].MessageKey), expected[2].Args));

            OutputRunCache.TryFlushFlatXmlContentChangedDiffs(context);
            Assert.Null(context.ContentChangedSnapshots);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
