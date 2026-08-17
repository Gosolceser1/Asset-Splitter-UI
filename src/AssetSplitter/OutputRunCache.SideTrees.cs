namespace AssetProcessor;

internal static partial class OutputRunCache
{
    /// <summary>Result of verifying stamped side-output / source tree fingerprints.</summary>
    public sealed class SideTreeResult
    {
        public PresenceIssueKind? SourceXml { get; set; }
        public PresenceIssueKind? OutputXml { get; set; }
        public PresenceIssueKind? OutputMods { get; set; }
        public PresenceIssueKind? Templates { get; set; }

        public List<string> SourceChangedPaths { get; } = [];
        public List<string> TemplatesChangedPaths { get; } = [];

        public bool IsOk =>
            SourceXml is null
            && OutputXml is null
            && OutputMods is null
            && Templates is null;

        /// <summary>True when only the split-templates tree failed (inventory/output/mod trees OK).</summary>
        public bool OnlyTemplatesFailed =>
            Templates is not null
            && SourceXml is null
            && OutputXml is null
            && OutputMods is null;

        /// <summary>
        /// Flat XML / mods root extras (beyond per-GUID inventory) need Full.
        /// Source tree changes are handled via DiffRepair / Complete — not this flag.
        /// </summary>
        public bool RequiresFull =>
            OutputXml is not null
            || OutputMods is not null;
    }

    internal static string ResolveSourceXmlDirectory(PipelineContext context, string gameType)
    {
        if (!string.IsNullOrWhiteSpace(context.SourceXmlFolder))
        {
            return Path.TrimEndingDirectorySeparator(context.SourceXmlFolder);
        }

        return OutputDirectoryManager.GetSourceXmlDirectory(context.GameOutputRoot, gameType);
    }

    internal static string ResolveTemplatesDirectory(PipelineContext context, string gameType) =>
        OutputDirectoryManager.GetTemplatesOutputDirectory(context.GameOutputRoot, gameType);

    /// <summary>
    /// Recomputes tree fingerprints for source_xml, output_xml, mods (when enabled), and
    /// output_templates (when SplitTemplates). Stamp/cache files are excluded from digests.
    /// Emits hashing progress so Complete evaluation does not look frozen on large trees.
    /// </summary>
    public static SideTreeResult VerifySideTrees(PipelineContext context, StampDocument stamp, string gameType)
    {
        var result = new SideTreeResult();
        string normalizedGameType = NormalizeGameType(gameType);

        VerifyOneTree(
            context,
            "source_xml",
            ResolveSourceXmlDirectory(context, normalizedGameType),
            stamp.SourceXmlTreeHash,
            stamp.SourceXmlFileHashes,
            mustExist: true,
            excludeFileNames: [SourceXmlCache.StampFileName, StampFileName, InventoryFileName],
            out PresenceIssueKind? sourceIssue,
            result.SourceChangedPaths);
        result.SourceXml = sourceIssue;

        string outputXmlRoot = string.IsNullOrWhiteSpace(context.AssetOutputDir)
            ? OutputDirectoryManager.GetAssetXmlOutputDirectory(context.GameOutputRoot, normalizedGameType)
            : context.AssetOutputDir;
        VerifyOneTree(
            context,
            "output_xml",
            outputXmlRoot,
            stamp.OutputXmlTreeHash,
            expectedFileHashes: null,
            mustExist: true,
            excludeFileNames: [StampFileName, InventoryFileName],
            out PresenceIssueKind? outputXmlIssue,
            changedPaths: null);
        result.OutputXml = outputXmlIssue;

        if (context.CreateAssetMods)
        {
            string modsRoot = string.IsNullOrWhiteSpace(context.AssetModOutputRoot)
                ? OutputDirectoryManager.GetAssetModOutputDirectory(context.GameOutputRoot, normalizedGameType)
                : context.AssetModOutputRoot;
            VerifyOneTree(
                context,
                "output_mods",
                modsRoot,
                stamp.OutputModsTreeHash,
                expectedFileHashes: null,
                mustExist: true,
                excludeFileNames: [StampFileName, InventoryFileName],
                out PresenceIssueKind? modsIssue,
                changedPaths: null);
            result.OutputMods = modsIssue;
        }

        if (context.SplitTemplates)
        {
            VerifyOneTree(
                context,
                "output_templates",
                ResolveTemplatesDirectory(context, normalizedGameType),
                stamp.TemplatesTreeHash,
                stamp.TemplatesFileHashes,
                mustExist: true,
                excludeFileNames: [StampFileName, InventoryFileName],
                out PresenceIssueKind? templatesIssue,
                result.TemplatesChangedPaths);
            result.Templates = templatesIssue;
        }

        return result;
    }

    private static void VerifyOneTree(
        PipelineContext context,
        string treeLabel,
        string root,
        string expectedTreeHash,
        IReadOnlyDictionary<string, string>? expectedFileHashes,
        bool mustExist,
        string[] excludeFileNames,
        out PresenceIssueKind? issue,
        List<string>? changedPaths)
    {
        issue = null;
        if (string.IsNullOrWhiteSpace(expectedTreeHash))
        {
            issue = PresenceIssueKind.ContentChanged;
            return;
        }

        if (!Directory.Exists(root))
        {
            issue = mustExist ? PresenceIssueKind.Missing : PresenceIssueKind.ContentChanged;
            return;
        }

        Dictionary<string, string>? actualHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
            root,
            CreateHashProgress(context, treeLabel),
            excludeFileNames);
        if (actualHashes is null)
        {
            issue = PresenceIssueKind.Incomplete;
            return;
        }

        string actualTree = DirectoryTreeFingerprint.DigestFromFileHashes(actualHashes);
        if (actualTree.Equals(expectedTreeHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        issue = PresenceIssueKind.ContentChanged;
        if (changedPaths is not null && expectedFileHashes is not null)
        {
            var added = new List<string>();
            var removed = new List<string>();
            var changed = new List<string>();
            DirectoryTreeFingerprint.DiffFileHashes(expectedFileHashes, actualHashes, added, removed, changed);
            changedPaths.AddRange(changed);
            changedPaths.AddRange(added);
            changedPaths.AddRange(removed);
        }
    }

    /// <summary>
    /// Per-file hashes collected while stamping side trees. Inventory build reuses these
    /// so output XML / mod packages are not SHA-256'd a second time.
    /// </summary>
    internal sealed class StampTreeHashMaps
    {
        public string OutputXmlRoot { get; init; } = "";
        public Dictionary<string, string>? OutputXmlFileHashes { get; init; }
        public string ModsRoot { get; init; } = "";
        public Dictionary<string, string>? ModsFileHashes { get; init; }
    }

    /// <summary>
    /// Hashes source_xml, output_xml, mods (when enabled), and output_templates (when SplitTemplates).
    /// Emits progress via <see cref="AssetProcessorProgressReporter"/> so the UI does not look frozen.
    /// </summary>
    internal static StampTreeHashMaps PopulateStampTreeFingerprints(
        PipelineContext context,
        string gameType,
        StampDocument stamp)
    {
        string normalizedGameType = NormalizeGameType(gameType);
        string[] exclude = [StampFileName, InventoryFileName, SourceXmlCache.StampFileName];

        string sourceRoot = ResolveSourceXmlDirectory(context, normalizedGameType);
        Dictionary<string, string>? sourceHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
            sourceRoot,
            CreateHashProgress(context, "source_xml"),
            exclude);
        if (sourceHashes is not null)
        {
            stamp.SourceXmlTreeHash = DirectoryTreeFingerprint.DigestFromFileHashes(sourceHashes);
            stamp.SourceXmlFileHashes = sourceHashes;
        }

        string outputXmlRoot = string.IsNullOrWhiteSpace(context.AssetOutputDir)
            ? OutputDirectoryManager.GetAssetXmlOutputDirectory(context.GameOutputRoot, normalizedGameType)
            : context.AssetOutputDir;
        Dictionary<string, string>? outputXmlHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
            outputXmlRoot,
            CreateHashProgress(context, "output_xml"),
            StampFileName,
            InventoryFileName);
        stamp.OutputXmlTreeHash = outputXmlHashes is null
            ? ""
            : DirectoryTreeFingerprint.DigestFromFileHashes(outputXmlHashes);

        string modsRoot = "";
        Dictionary<string, string>? modsHashes = null;
        if (context.CreateAssetMods)
        {
            modsRoot = string.IsNullOrWhiteSpace(context.AssetModOutputRoot)
                ? OutputDirectoryManager.GetAssetModOutputDirectory(context.GameOutputRoot, normalizedGameType)
                : context.AssetModOutputRoot;
            modsHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
                modsRoot,
                CreateHashProgress(context, "output_mods"),
                StampFileName,
                InventoryFileName);
            stamp.OutputModsTreeHash = modsHashes is null
                ? ""
                : DirectoryTreeFingerprint.DigestFromFileHashes(modsHashes);
        }
        else
        {
            stamp.OutputModsTreeHash = "";
        }

        if (context.SplitTemplates)
        {
            string templatesRoot = ResolveTemplatesDirectory(context, normalizedGameType);
            Dictionary<string, string>? templateHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
                templatesRoot,
                CreateHashProgress(context, "output_templates"),
                StampFileName,
                InventoryFileName);
            if (templateHashes is not null)
            {
                stamp.TemplatesTreeHash = DirectoryTreeFingerprint.DigestFromFileHashes(templateHashes);
                stamp.TemplatesFileHashes = templateHashes;
            }
            else
            {
                stamp.TemplatesTreeHash = "";
                stamp.TemplatesFileHashes = null;
            }
        }
        else
        {
            stamp.TemplatesTreeHash = "";
            stamp.TemplatesFileHashes = null;
        }

        return new StampTreeHashMaps
        {
            OutputXmlRoot = outputXmlRoot,
            OutputXmlFileHashes = outputXmlHashes,
            ModsRoot = modsRoot,
            ModsFileHashes = modsHashes,
        };
    }

    private static Action<int, int> CreateHashProgress(PipelineContext context, string treeLabel)
    {
        string operation = "Hashing: " + treeLabel;
        return (processed, total) =>
        {
            if (total <= 0)
            {
                return;
            }

            context.ProgressReporter.ReportProgress(operation, processed.ToString(), total.ToString());
            try
            {
                Console.Out.Flush();
            }
            catch (IOException)
            {
                // Best effort — redirected GUI pipes can throw on flush after close.
            }
        };
    }

    /// <summary>Logs side-tree content-changed headers and capped relative paths (templates/source).</summary>
    public static void WriteSideTreeMismatchDetails(PipelineLogger? log, SideTreeResult trees, string gameType)
    {
        if (log is null || trees.IsOk)
        {
            return;
        }

        string normalized = NormalizeGameType(gameType);
        if (trees.SourceXml is not null)
        {
            WriteTreeIssueLines(
                log,
                "contentChangedDiffHeaderSourceXml",
                "source_xml_" + normalized,
                trees.SourceXml.Value,
                trees.SourceChangedPaths);
        }

        if (trees.OutputXml is not null)
        {
            WriteTreeIssueLines(
                log,
                "contentChangedDiffHeaderOutputXml",
                "output_xml_" + normalized,
                trees.OutputXml.Value,
                changedPaths: null);
        }

        if (trees.OutputMods is not null)
        {
            WriteTreeIssueLines(
                log,
                "contentChangedDiffHeaderOutputMods",
                "output_xml_" + normalized + "_mods",
                trees.OutputMods.Value,
                changedPaths: null);
        }

        if (trees.Templates is not null)
        {
            WriteTreeIssueLines(
                log,
                "contentChangedDiffHeaderTemplates",
                "output_templates_" + normalized,
                trees.Templates.Value,
                trees.TemplatesChangedPaths);
        }
    }

    private static void WriteTreeIssueLines(
        PipelineLogger log,
        string headerKey,
        string folderLabel,
        PresenceIssueKind kind,
        List<string>? changedPaths)
    {
        string itemKey = PresenceItemMessageKey(kind);
        log.WriteLocalized(ConsoleMessageKind.Ok, itemKey, always: true, folderLabel);
        log.WriteLocalized(ConsoleMessageKind.Ok, headerKey, always: true, folderLabel);

        if (changedPaths is null || changedPaths.Count == 0)
        {
            return;
        }

        const int limit = ContentChangedTreeDiffPathLimit;
        int listed = Math.Min(changedPaths.Count, limit);
        for (int i = 0; i < listed; i++)
        {
            log.WriteLocalized(ConsoleMessageKind.Ok, "contentChangedDiffPathChanged", always: true, changedPaths[i]);
        }

        int omitted = changedPaths.Count - listed;
        if (omitted > 0)
        {
            log.WriteLocalized(ConsoleMessageKind.Ok, "contentChangedDiffPathMore", always: true, omitted);
        }
    }
}
