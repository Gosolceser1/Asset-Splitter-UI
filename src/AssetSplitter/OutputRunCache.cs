using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;

namespace AssetProcessor;

/// <summary>
/// Stamps successful full-processing output so identical runs can skip or repair
/// instead of wiping and rebuilding everything. Single-GUID, source-only, and
/// mod-export-only runs are not eligible.
/// <para>
/// Complete criteria (same build): stamp schema/outputSchema + options fingerprint +
/// inventory presence + schema-6 side-tree fingerprints. Presence checks file existence,
/// non-empty length, a cheap leading-bytes integrity probe (no leading whitespace before
/// first <c>&lt;</c>), streamed SHA-256 of flat XML (<c>outputHash</c>), and a mod-package
/// <em>tree</em> fingerprint (<c>modOutputHash</c>: sorted relative paths + per-file SHA-256)
/// so edits to modinfo.json, readmes, nested files, or added/removed paths are Repair,
/// not silent Complete. Stamp also stores root digests for <c>source_xml_*</c>,
/// <c>output_xml_*</c>, <c>output_xml_*_mods</c>, and <c>output_templates_*</c>.
/// DiffRepair uses per-GUID <c>sourceHash</c> of extractable <em>source</em> assets
/// (game update), separate from output content hashes.
/// </para>
/// Output schema 2: per-GUID source hashes for game-update diff-repair.
/// Output schema 3: footprint no longer emits leading blank lines before header/root;
/// old stamped installs must Full rebuild (schema mismatch), and Presence treats
/// leading-blank inventoried XML / mod assets.xml as Missing → Repair.
/// Output schema 4: per-artifact output content hashes (flat XML + mod assets.xml only).
/// Output schema 5: <c>modOutputHash</c> is the full mod-package tree fingerprint;
/// schema-4 stamps Full once so inventories are rewritten.
/// Output schema 6: stamp also stores tree fingerprints for <c>source_xml_*</c>,
/// <c>output_xml_*</c>, <c>output_xml_*_mods</c> (when mods enabled), and
/// <c>output_templates_*</c> (when SplitTemplates). Schema-5 stamps Full once.
/// <para>
/// Side-tree policy (same game build): <c>source_xml</c> mismatch → DiffRepair from
/// per-GUID source hashes (or Complete when hashes still match) — never wipe all
/// outputs solely because the source tree digest moved. Templates-only → Repair +
/// resplit (empty <c>RepairOnlyGuids</c>, skip extract). <c>output_xml</c> / mods
/// extras without GUID gaps → Full (cannot map extras to surgical repair).
/// Inventory rows missing <c>sourceHash</c>: backfill from <c>source_xml</c> when
/// the source tree is unchanged and outputs pass presence (Complete + stamp rewrite).
/// If the source tree moved (or a blank-hash GUID fails presence), DiffRepair those
/// GUIDs — never Full-wipe solely for missing hashes when trees are recoverable.
/// </para>
/// </summary>
internal static partial class OutputRunCache
{
    public const string StampFileName = ".asset-splitter-output-cache.json";
    public const string InventoryFileName = ".asset-splitter-output-inventory.jsonl";
    public const int SchemaVersion = 1;
    public const int OutputSchemaVersion = 6;

    public enum RunMode
    {
        Full,
        Repair,
        DiffRepair,
        Complete,
    }

    public enum MissReason
    {
        None,
        ForceFullRebuild,
        NotEligible,
        BuildUnknown,
        StampMissing,
        StampInvalid,
        InventoryMissing,
        BuildChanged,
        OptionsChanged,
        GameTypeMismatch,
        SchemaMismatch,
        HashesMissing,
        SourceAssetsUnavailable,
        /// <summary>output_xml / mods root tree fingerprint mismatch (beyond per-GUID inventory), or source tree when diff is unavailable.</summary>
        SideTreeMismatch,
    }

    public sealed class InventoryEntry
    {
        [JsonPropertyName("guid")]
        public string Guid { get; set; } = "";

        [JsonPropertyName("xml")]
        public string XmlRelativePath { get; set; } = "";

        [JsonPropertyName("mod")]
        public string? ModRelativePath { get; set; }

        [JsonPropertyName("sourceHash")]
        public string SourceHash { get; set; } = "";

        /// <summary>SHA-256 hex of the flat XML at <see cref="XmlRelativePath"/>.</summary>
        [JsonPropertyName("outputHash")]
        public string OutputHash { get; set; } = "";

        /// <summary>
        /// SHA-256 hex tree fingerprint of all files under <see cref="ModRelativePath"/>
        /// (sorted relative path + per-file content hash). Empty when no mod package.
        /// </summary>
        [JsonPropertyName("modOutputHash")]
        public string ModOutputHash { get; set; } = "";
    }

    /// <summary>Why an inventoried XML/mod failed presence (cheap classification during VerifyPresence).</summary>
    public enum PresenceIssueKind
    {
        Missing,
        Incomplete,
        ContentChanged,
    }

    public sealed class PresenceResult
    {
        public List<InventoryEntry> Present { get; } = [];
        public List<InventoryEntry> MissingXml { get; } = [];
        public List<InventoryEntry> MissingMod { get; } = [];
        /// <summary>Per-GUID reason for <see cref="MissingXml"/> rows.</summary>
        public Dictionary<string, PresenceIssueKind> XmlFailureKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Per-GUID reason for <see cref="MissingMod"/> rows.</summary>
        public Dictionary<string, PresenceIssueKind> ModFailureKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> OrphanXmlRelativePaths { get; } = [];
        public List<string> OrphanModRelativePaths { get; } = [];

        public bool IsComplete =>
            MissingXml.Count == 0
            && MissingMod.Count == 0
            && OrphanXmlRelativePaths.Count == 0
            && OrphanModRelativePaths.Count == 0;

        public HashSet<string> GuidsNeedingRebuild
        {
            get
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (InventoryEntry entry in MissingXml)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Guid))
                    {
                        set.Add(entry.Guid);
                    }
                }

                foreach (InventoryEntry entry in MissingMod)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Guid))
                    {
                        set.Add(entry.Guid);
                    }
                }

                return set;
            }
        }
    }

    public sealed class GameDiffResult
    {
        public List<string> Added { get; } = [];
        public List<string> Changed { get; } = [];
        public List<string> Removed { get; } = [];
        public HashSet<string> RebuildGuids { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<InventoryEntry> RemovedEntries { get; } = [];
        /// <summary>Prior inventory rows for rebuild GUIDs (changed / descendants) so old XML/mod paths can be deleted before re-extract.</summary>
        public List<InventoryEntry> ReplacedEntries { get; } = [];

        public bool HasWork =>
            Added.Count > 0 || Changed.Count > 0 || Removed.Count > 0 || RebuildGuids.Count > 0;
    }

    public sealed class Decision
    {
        public required RunMode Mode { get; init; }
        public MissReason Reason { get; init; }
        public PresenceResult? Presence { get; init; }
        public GameDiffResult? GameDiff { get; init; }
        public SideTreeResult? SideTrees { get; init; }
        public int AssetCount { get; init; }
        public int ModCount { get; init; }
        public string BuildDisplay { get; init; } = "";
        /// <summary>When true, Presence Repair must re-run split-templates even with preserve-on.</summary>
        public bool ForceResplitTemplates { get; init; }
    }

    internal sealed class StampDocument
    {
        [JsonPropertyName("schema")]
        public int Schema { get; set; }

        [JsonPropertyName("outputSchema")]
        public int OutputSchema { get; set; }

        [JsonPropertyName("gameType")]
        public string GameType { get; set; } = "";

        [JsonPropertyName("buildFingerprint")]
        public string BuildFingerprint { get; set; } = "";

        [JsonPropertyName("optionsFingerprint")]
        public string OptionsFingerprint { get; set; } = "";

        [JsonPropertyName("buildDisplay")]
        public string BuildDisplay { get; set; } = "";

        [JsonPropertyName("assetXmlCount")]
        public int AssetXmlCount { get; set; }

        [JsonPropertyName("modCount")]
        public int ModCount { get; set; }

        [JsonPropertyName("completedUtc")]
        public string CompletedUtc { get; set; } = "";

        [JsonPropertyName("sourceXmlTreeHash")]
        public string SourceXmlTreeHash { get; set; } = "";

        [JsonPropertyName("outputXmlTreeHash")]
        public string OutputXmlTreeHash { get; set; } = "";

        [JsonPropertyName("outputModsTreeHash")]
        public string OutputModsTreeHash { get; set; } = "";

        [JsonPropertyName("templatesTreeHash")]
        public string TemplatesTreeHash { get; set; } = "";

        [JsonPropertyName("sourceXmlFileHashes")]
        public Dictionary<string, string>? SourceXmlFileHashes { get; set; }

        [JsonPropertyName("templatesFileHashes")]
        public Dictionary<string, string>? TemplatesFileHashes { get; set; }
    }

    public static bool IsEligible(PipelineContext context)
    {
        if (context.IsSingleAssetMode)
        {
            return false;
        }

        if (PipelineFeatureGates.IsSourceExtractionOnlyRun(context))
        {
            return false;
        }

        if (PipelineFeatureGates.IsModExportOnlyRun(context))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(context.GameOutputRoot);
    }

    public static string BuildOptionsFingerprint(PipelineContext context)
    {
        static string Norm(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

        string[] parts =
        [
            "lang=" + Norm(context.AssetLanguage),
            "c=" + (context.AddComments ? "1" : "0"),
            "f=" + (context.FixDependencies ? "1" : "0"),
            "t=" + (context.CreateTemplateFolders ? "1" : "0"),
            "modops=" + (context.ModOpsWrap ? "1" : "0"),
            "nodef=" + (context.NoDefaultProperties ? "1" : "0"),
            "split=" + (context.SplitTemplates ? "1" : "0"),
            "mods=" + (context.CreateAssetMods ? "1" : "0"),
            "u=" + Norm(context.CustomTemplateFile),
            "x=" + Norm(context.CustomFixlistFile),
            "auto=" + (context.AutoTemplates ? "1" : "0"),
            // Effective template list (default, -u, --auto-templates or auto-updated list): adding or
            // removing a template changes which assets are extracted, so it must not be Complete.
            "tpl=" + HashTemplateList(context.AssetTemplatesList),
            "xfile=" + HashOptionalFile(context.CustomFixlistFile),
            "readme=" + Norm(context.ReadmeLanguage),
            "outSchema=" + OutputSchemaVersion.ToString(),
        ];

        return string.Join('|', parts);
    }

    private static string HashTemplateList(string[]? templates)
    {
        if (templates is null || templates.Length == 0)
        {
            return "default";
        }

        string joined = string.Join(
            '\n',
            templates
                .Select(static t => t.Trim())
                .Where(static t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(joined)))[..16];
    }

    private static string HashOptionalFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        try
        {
            return File.Exists(path)
                ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))[..16]
                : "missing";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "unreadable";
        }
    }

    /// <summary>
    /// Source files that feed <em>every</em> output asset, not just one <c>&lt;Asset&gt;</c> node:
    /// templates.xml / properties.xml (Phase 5 template + default-property merge, split templates,
    /// property scan) and the language text files (file names, comments). Per-asset
    /// <c>sourceHash</c> cannot see changes to these, so a change must rebuild everything.
    /// </summary>
    private static bool GlobalSourceInputsChanged(PipelineContext context, StampDocument stamp, SideTreeResult sideTrees)
    {
        if (sideTrees.SourceXml != PresenceIssueKind.ContentChanged)
        {
            return false;
        }

        // Legacy stamp without per-file source hashes: cannot tell which file moved.
        if (stamp.SourceXmlFileHashes is null || stamp.SourceXmlFileHashes.Count == 0)
        {
            return true;
        }

        bool usesLanguage = !string.IsNullOrWhiteSpace(context.AssetLanguage)
            && !context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase);
        string languageFile = usesLanguage ? Path.GetFileName(context.AssetLanguage) : "";

        foreach (string changedPath in sideTrees.SourceChangedPaths)
        {
            string fileName = Path.GetFileName(changedPath.Replace('\\', '/').TrimEnd('/'));
            if (fileName.Equals("templates.xml", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("properties.xml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (usesLanguage
                && (fileName.Equals(languageFile, StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("texts_metadata.xml", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static Decision Evaluate(PipelineContext context, string gameType, bool forceFullRebuild)
    {
        if (forceFullRebuild)
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.ForceFullRebuild };
        }

        if (!IsEligible(context))
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.NotEligible };
        }

        if (context.DetectedGameBuild is null)
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.BuildUnknown };
        }

        string buildFingerprint = SourceXmlCache.BuildFingerprint(context.DetectedGameBuild);
        if (buildFingerprint.Length == 0)
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.BuildUnknown };
        }

        string optionsFingerprint = BuildOptionsFingerprint(context);
        string normalizedGameType = NormalizeGameType(gameType);

        if (!TryReadStamp(context.GameOutputRoot, out StampDocument? stamp) || stamp is null)
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.StampMissing };
        }

        if (stamp.Schema != SchemaVersion || stamp.OutputSchema != OutputSchemaVersion)
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.SchemaMismatch };
        }

        if (!stamp.GameType.Equals(normalizedGameType, StringComparison.OrdinalIgnoreCase))
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.GameTypeMismatch };
        }

        if (!stamp.OptionsFingerprint.Equals(optionsFingerprint, StringComparison.Ordinal))
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.OptionsChanged };
        }

        if (!TryReadInventory(context.GameOutputRoot, out List<InventoryEntry> inventory) || inventory.Count == 0)
        {
            return new Decision { Mode = RunMode.Full, Reason = MissReason.InventoryMissing };
        }

        bool buildMatches = stamp.BuildFingerprint.Equals(buildFingerprint, StringComparison.Ordinal);
        // Side trees first: when output_xml/mods digests match the stamp, VerifyPresence can
        // skip per-file / per-mod SHA-256 (Complete no longer double-hashes tens of thousands of files).
        SideTreeResult sideTrees = VerifySideTrees(context, stamp, normalizedGameType);
        PresenceResult presence = VerifyPresence(context, inventory, sideTrees);
        string buildDisplay = buildMatches
            ? stamp.BuildDisplay
            : context.DetectedGameBuild.ToDisplayString();
        int stampedAssetCount = stamp.AssetXmlCount > 0 ? stamp.AssetXmlCount : inventory.Count;

        if (GlobalSourceInputsChanged(context, stamp, sideTrees))
        {
            return new Decision
            {
                Mode = RunMode.Full,
                Reason = MissReason.SideTreeMismatch,
                Presence = presence,
                SideTrees = sideTrees,
                AssetCount = stampedAssetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        // Missing per-GUID sourceHash: backfill / DiffRepair — never Full solely for blank hashes.
        if (!InventoryHasAllSourceHashes(inventory))
        {
            return DecideFromSourceHashDiff(
                context,
                stamp,
                inventory,
                presence,
                sideTrees,
                normalizedGameType,
                preferredDiffReason: buildMatches ? MissReason.HashesMissing : MissReason.BuildChanged,
                buildDisplay,
                stampedAssetCount,
                allowBlankHashBackfill: true);
        }

        if (buildMatches)
        {
            // Orphans alone must not force Repair: Presence Repair with empty RepairOnlyGuids
            // re-extracts every asset into a preserved template tree and File.Move collisions spam.
            // Schema 6: source mismatch → DiffRepair/Complete; GUID gaps → Repair;
            // output_xml/mods extras → Full; templates-only → Repair+resplit.
            return DecideSameBuildPresence(
                context,
                stamp,
                inventory,
                presence,
                sideTrees,
                normalizedGameType,
                priorDiff: null,
                buildDisplay,
                stampedAssetCount);
        }

        // Game build changed, options still match — try source-hash diff.
        return DecideFromSourceHashDiff(
            context,
            stamp,
            inventory,
            presence,
            sideTrees,
            normalizedGameType,
            preferredDiffReason: MissReason.BuildChanged,
            buildDisplay,
            stampedAssetCount,
            allowBlankHashBackfill: false);
    }

    /// <summary>
    /// Diff extractable source hashes against inventory. When
    /// <paramref name="allowBlankHashBackfill"/> is true, blank inventory
    /// <c>sourceHash</c> values match if output presence is OK (one-time backfill on stamp write);
    /// blank + presence failure still rebuilds that GUID.
    /// </summary>
    private static Decision DecideFromSourceHashDiff(
        PipelineContext context,
        StampDocument stamp,
        IReadOnlyList<InventoryEntry> inventory,
        PresenceResult presence,
        SideTreeResult sideTrees,
        string normalizedGameType,
        MissReason preferredDiffReason,
        string buildDisplay,
        int assetCount,
        bool allowBlankHashBackfill)
    {
        if (!TryLoadAssetsDocument(context, out XmlDocument? assetsXml) || assetsXml is null)
        {
            bool needsGuidRebuild = presence.MissingXml.Count > 0 || presence.MissingMod.Count > 0;
            if (needsGuidRebuild || sideTrees.Templates is not null)
            {
                return new Decision
                {
                    Mode = RunMode.Repair,
                    Reason = MissReason.None,
                    Presence = presence,
                    SideTrees = sideTrees,
                    ForceResplitTemplates = sideTrees.Templates is not null,
                    AssetCount = assetCount,
                    ModCount = stamp.ModCount,
                    BuildDisplay = buildDisplay,
                };
            }

            return new Decision
            {
                Mode = RunMode.Full,
                Reason = MissReason.SourceAssetsUnavailable,
                Presence = presence,
                SideTrees = sideTrees,
                AssetCount = assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        Dictionary<string, string> newHashes = AssetSourceHasher.BuildExtractableHashMap(
            assetsXml,
            normalizedGameType,
            context.AssetTemplatesList ?? context.TemplatesUsed);

        // Blank inventory hashes: backfill (skip rebuild) only when source tree is
        // unchanged and presence is OK. Source digest moved ⇒ treat blanks as dirty.
        ISet<string>? rebuildWhenBlank = ResolveBlankHashRebuildFilter(
            allowBlankHashBackfill,
            sideTrees,
            presence);

        GameDiffResult diff = DiffAgainstInventory(
            inventory,
            newHashes,
            assetsXml,
            context.FixDependencies,
            rebuildWhenBlankSourceHash: rebuildWhenBlank);

        if (diff.HasWork)
        {
            MergePresenceGapsIntoDiff(diff, presence, inventory);
            MissReason diffReason = preferredDiffReason;
            if (allowBlankHashBackfill)
            {
                diffReason = sideTrees.SourceXml is not null
                    ? MissReason.SideTreeMismatch
                    : MissReason.HashesMissing;
            }

            return new Decision
            {
                Mode = RunMode.DiffRepair,
                Reason = diffReason,
                Presence = presence,
                SideTrees = sideTrees,
                GameDiff = diff,
                ForceResplitTemplates = sideTrees.Templates is not null,
                AssetCount = newHashes.Count > 0 ? newHashes.Count : assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        Decision decided = DecideSameBuildPresence(
            context,
            stamp,
            inventory,
            presence,
            sideTrees,
            normalizedGameType,
            priorDiff: diff,
            buildDisplay,
            newHashes.Count > 0 ? newHashes.Count : assetCount);

        // Ensure Complete after backfill refreshes stamp+inventory (writes sourceHash).
        if (allowBlankHashBackfill && decided.Mode == RunMode.Complete)
        {
            return new Decision
            {
                Mode = RunMode.Complete,
                Reason = MissReason.HashesMissing,
                Presence = decided.Presence,
                SideTrees = decided.SideTrees,
                GameDiff = decided.GameDiff ?? diff,
                AssetCount = decided.AssetCount,
                ModCount = decided.ModCount,
                BuildDisplay = decided.BuildDisplay,
                ForceResplitTemplates = decided.ForceResplitTemplates,
            };
        }

        return decided;
    }

    /// <summary>
    /// Same-build (or build-fingerprint-moved-but-hashes-equal) presence + side-tree decision.
    /// Source tree changes prefer DiffRepair/Complete; GUID gaps / templates → Repair;
    /// output_xml/mods extras without GUID gaps → Full.
    /// </summary>
    private static Decision DecideSameBuildPresence(
        PipelineContext context,
        StampDocument stamp,
        IReadOnlyList<InventoryEntry> inventory,
        PresenceResult presence,
        SideTreeResult sideTrees,
        string normalizedGameType,
        GameDiffResult? priorDiff,
        string buildDisplay,
        int assetCount)
    {
        bool needsGuidRebuild = presence.MissingXml.Count > 0 || presence.MissingMod.Count > 0;

        // Source tree first: DiffRepair / Complete / presence Repair — never Full solely for source.
        if (sideTrees.SourceXml is not null)
        {
            return DecideFromSourceTreeChange(
                context,
                stamp,
                inventory,
                presence,
                sideTrees,
                normalizedGameType,
                priorDiff,
                buildDisplay,
                assetCount,
                needsGuidRebuild);
        }

        // GUID inventory gaps (incl. content-changed flat XML / mod packages) beat output-tree Full.
        // Editing one inventoried file also moves the output_xml/mods tree digest — still Repair.
        if (needsGuidRebuild || sideTrees.Templates is not null)
        {
            return new Decision
            {
                Mode = RunMode.Repair,
                Reason = MissReason.None,
                Presence = presence,
                SideTrees = sideTrees,
                GameDiff = priorDiff,
                ForceResplitTemplates = sideTrees.Templates is not null,
                AssetCount = assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        // Extras / orphans under output_xml or mods with no GUID gap → Full.
        if (sideTrees.OutputXml is not null || sideTrees.OutputMods is not null)
        {
            return new Decision
            {
                Mode = RunMode.Full,
                Reason = MissReason.SideTreeMismatch,
                Presence = presence,
                SideTrees = sideTrees,
                GameDiff = priorDiff,
                AssetCount = assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        return new Decision
        {
            Mode = RunMode.Complete,
            Reason = MissReason.None,
            Presence = presence,
            SideTrees = sideTrees,
            GameDiff = priorDiff,
            AssetCount = assetCount,
            ModCount = stamp.ModCount,
            BuildDisplay = buildDisplay,
        };
    }

    /// <summary>
    /// <c>source_xml</c> tree digest moved: diff extractable source hashes against inventory.
    /// Rebuild only changed GUIDs; Complete (refresh stamp) when hashes still match.
    /// </summary>
    private static Decision DecideFromSourceTreeChange(
        PipelineContext context,
        StampDocument stamp,
        IReadOnlyList<InventoryEntry> inventory,
        PresenceResult presence,
        SideTreeResult sideTrees,
        string normalizedGameType,
        GameDiffResult? priorDiff,
        string buildDisplay,
        int assetCount,
        bool needsGuidRebuild)
    {
        if (!TryLoadAssetsDocument(context, out XmlDocument? assetsXml) || assetsXml is null)
        {
            if (needsGuidRebuild || sideTrees.Templates is not null)
            {
                return new Decision
                {
                    Mode = RunMode.Repair,
                    Reason = MissReason.None,
                    Presence = presence,
                    SideTrees = sideTrees,
                    GameDiff = priorDiff,
                    ForceResplitTemplates = sideTrees.Templates is not null,
                    AssetCount = assetCount,
                    ModCount = stamp.ModCount,
                    BuildDisplay = buildDisplay,
                };
            }

            return new Decision
            {
                Mode = RunMode.Full,
                Reason = MissReason.SourceAssetsUnavailable,
                Presence = presence,
                SideTrees = sideTrees,
                GameDiff = priorDiff,
                AssetCount = assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        Dictionary<string, string> newHashes = AssetSourceHasher.BuildExtractableHashMap(
            assetsXml,
            normalizedGameType,
            context.AssetTemplatesList ?? context.TemplatesUsed);

        // Source tree already mismatched here. Blank inventory hashes cannot prove
        // "unchanged" — null filter DiffRepairs every blank-hash GUID.
        bool inventoryLacksHashes = !InventoryHasAllSourceHashes(inventory);
        GameDiffResult diff = DiffAgainstInventory(
            inventory,
            newHashes,
            assetsXml,
            context.FixDependencies,
            rebuildWhenBlankSourceHash: null);

        if (diff.HasWork)
        {
            // Also rebuild presence gaps whose source hash did not change (corrupt/missing output).
            MergePresenceGapsIntoDiff(diff, presence, inventory);

            return new Decision
            {
                Mode = RunMode.DiffRepair,
                Reason = MissReason.SideTreeMismatch,
                Presence = presence,
                SideTrees = sideTrees,
                GameDiff = diff,
                ForceResplitTemplates = sideTrees.Templates is not null,
                AssetCount = newHashes.Count > 0 ? newHashes.Count : assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        if (needsGuidRebuild || sideTrees.Templates is not null)
        {
            return new Decision
            {
                Mode = RunMode.Repair,
                Reason = MissReason.None,
                Presence = presence,
                SideTrees = sideTrees,
                GameDiff = priorDiff,
                ForceResplitTemplates = sideTrees.Templates is not null,
                AssetCount = assetCount,
                ModCount = stamp.ModCount,
                BuildDisplay = buildDisplay,
            };
        }

        // Source tree digest moved (e.g. formatting / non-extractable file) but extractable
        // hashes and outputs still match — Complete and refresh stamp digests.
        // Blank hashes with no dirty GUID work still need stamp rewrite (backfill).
        return new Decision
        {
            Mode = RunMode.Complete,
            Reason = inventoryLacksHashes ? MissReason.HashesMissing : MissReason.None,
            Presence = presence,
            SideTrees = sideTrees,
            GameDiff = priorDiff ?? (inventoryLacksHashes ? new GameDiffResult() : null),
            AssetCount = assetCount,
            ModCount = stamp.ModCount,
            BuildDisplay = buildDisplay,
        };
    }

    /// <summary>
    /// When <paramref name="allowBlankHashBackfill"/> is true and the source tree is unchanged,
    /// blank inventory hashes match unless presence already failed for that GUID.
    /// Otherwise blank hashes are treated as dirty (null ⇒ rebuild all blanks).
    /// </summary>
    private static ISet<string>? ResolveBlankHashRebuildFilter(
        bool allowBlankHashBackfill,
        SideTreeResult sideTrees,
        PresenceResult presence)
    {
        if (!allowBlankHashBackfill || sideTrees.SourceXml is not null)
        {
            return null;
        }

        return presence.GuidsNeedingRebuild;
    }

    private static void MergePresenceGapsIntoDiff(
        GameDiffResult diff,
        PresenceResult presence,
        IReadOnlyList<InventoryEntry> inventory)
    {
        var byGuid = new Dictionary<string, InventoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (InventoryEntry entry in inventory)
        {
            if (entry.Guid.Length > 0)
            {
                byGuid[entry.Guid] = entry;
            }
        }

        foreach (string guid in presence.GuidsNeedingRebuild)
        {
            if (!diff.RebuildGuids.Add(guid))
            {
                continue;
            }

            if (byGuid.TryGetValue(guid, out InventoryEntry? prior)
                && !diff.ReplacedEntries.Exists(e => e.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase)))
            {
                diff.ReplacedEntries.Add(prior);
            }
        }
    }

    public static GameDiffResult DiffAgainstInventory(
        IReadOnlyList<InventoryEntry> inventory,
        IReadOnlyDictionary<string, string> newHashes,
        XmlDocument assetsXml,
        bool expandDependencyDescendants,
        ISet<string>? rebuildWhenBlankSourceHash = null)
    {
        var diff = new GameDiffResult();
        var oldHashes = new Dictionary<string, InventoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (InventoryEntry entry in inventory)
        {
            if (entry.Guid.Length == 0)
            {
                continue;
            }

            oldHashes[entry.Guid] = entry;
        }

        foreach ((string guid, string hash) in newHashes)
        {
            if (!oldHashes.TryGetValue(guid, out InventoryEntry? previous))
            {
                diff.Added.Add(guid);
                diff.RebuildGuids.Add(guid);
                continue;
            }

            if (string.IsNullOrWhiteSpace(previous.SourceHash))
            {
                // Blank sourceHash: backfill when outputs are intact; rebuild only when
                // presence already failed for this GUID (or backfill mode is off).
                if (rebuildWhenBlankSourceHash is null
                    || rebuildWhenBlankSourceHash.Contains(guid))
                {
                    diff.Changed.Add(guid);
                    diff.RebuildGuids.Add(guid);
                }

                continue;
            }

            if (!previous.SourceHash.Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                diff.Changed.Add(guid);
                diff.RebuildGuids.Add(guid);
            }
        }

        foreach ((string guid, InventoryEntry entry) in oldHashes)
        {
            if (newHashes.ContainsKey(guid))
            {
                continue;
            }

            diff.Removed.Add(guid);
            diff.RemovedEntries.Add(entry);
        }

        if (expandDependencyDescendants && (diff.Changed.Count > 0 || diff.Removed.Count > 0))
        {
            Dictionary<string, List<string>> parentToChildren = AssetSourceHasher.BuildParentToChildrenMap(assetsXml);
            var seeds = new HashSet<string>(diff.Changed, StringComparer.OrdinalIgnoreCase);
            seeds.UnionWith(diff.Removed);
            HashSet<string> closure = AssetSourceHasher.ExpandDescendantClosure(seeds, parentToChildren);
            foreach (string guid in closure)
            {
                if (newHashes.ContainsKey(guid))
                {
                    diff.RebuildGuids.Add(guid);
                }
            }
        }

        // Capture prior disk paths for every rebuild GUID that already had inventory output.
        // Without deleting these, ReserveUniqueOutputPath creates guid-2.xml / leaves stale template-folder copies.
        foreach (string guid in diff.RebuildGuids)
        {
            if (oldHashes.TryGetValue(guid, out InventoryEntry? prior))
            {
                diff.ReplacedEntries.Add(prior);
            }
        }

        return diff;
    }

    public static bool InventoryHasAllSourceHashes(IReadOnlyList<InventoryEntry> inventory)
    {
        if (inventory.Count == 0)
        {
            return false;
        }

        foreach (InventoryEntry entry in inventory)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceHash))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when every inventory row has output content hashes needed to trust a matching
    /// side-tree digest (blank hashes force per-file VerifyPresence / Repair backfill).
    /// </summary>
    internal static bool InventoryHasContentHashesForTrust(
        IReadOnlyList<InventoryEntry> inventory,
        bool createMods)
    {
        if (inventory.Count == 0)
        {
            return false;
        }

        foreach (InventoryEntry entry in inventory)
        {
            if (string.IsNullOrWhiteSpace(entry.OutputHash))
            {
                return false;
            }

            if (createMods && string.IsNullOrWhiteSpace(entry.ModOutputHash))
            {
                return false;
            }
        }

        return true;
    }

    public static PresenceResult VerifyPresence(PipelineContext context, IReadOnlyList<InventoryEntry> inventory) =>
        VerifyPresence(context, inventory, sideTrees: null);

    /// <summary>
    /// Verifies inventoried XML/mod artifacts. When <paramref name="sideTrees"/> shows matching
    /// output_xml (and mods, if enabled) digests, content hashing is skipped — the tree walk already
    /// proved those files match the stamp (Complete path stays responsive).
    /// </summary>
    public static PresenceResult VerifyPresence(
        PipelineContext context,
        IReadOnlyList<InventoryEntry> inventory,
        SideTreeResult? sideTrees)
    {
        var result = new PresenceResult();
        var expectedXml = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool createMods = context.CreateAssetMods;
        bool inventoryHasContentHashes = InventoryHasContentHashesForTrust(inventory, createMods);
        bool trustOutputXml = sideTrees is not null
            && sideTrees.OutputXml is null
            && inventoryHasContentHashes;
        bool trustOutputMods = !createMods
            || (sideTrees is not null && sideTrees.OutputMods is null && inventoryHasContentHashes);
        bool trustAllOutputContent = trustOutputXml && trustOutputMods;

        string gameOutputRoot = context.GameOutputRoot;
        string gameType = NormalizeGameType(
            string.IsNullOrWhiteSpace(context.DetectedGameType) ? "anno1800" : context.DetectedGameType);

        if (trustAllOutputContent)
        {
            // Tree digests matched the stamp: every inventoried path is present and content-OK.
            // Still collect expected sets + orphans (enumerate only — no SHA-256).
            foreach (InventoryEntry entry in inventory)
            {
                expectedXml.Add(NormalizeRelative(entry.XmlRelativePath));
                if (createMods)
                {
                    if (string.IsNullOrWhiteSpace(entry.ModRelativePath))
                    {
                        result.MissingMod.Add(entry);
                        if (!string.IsNullOrWhiteSpace(entry.Guid))
                        {
                            result.ModFailureKinds[entry.Guid] = PresenceIssueKind.Missing;
                        }

                        continue;
                    }

                    expectedMods.Add(NormalizeRelative(entry.ModRelativePath));
                }

                result.Present.Add(entry);
            }
        }
        else
        {
            // Classify in parallel: stream-hash tens of thousands of small XMLs without loading DOM.
            var classified = new InventoryEntry[inventory.Count];
            var xmlIssues = new PresenceIssueKind?[inventory.Count];
            var modIssues = new PresenceIssueKind?[inventory.Count];

            Parallel.For(
                0,
                inventory.Count,
                i =>
                {
                    InventoryEntry entry = inventory[i];
                    classified[i] = entry;

                    if (trustOutputXml)
                    {
                        xmlIssues[i] = null;
                    }
                    else
                    {
                        string xmlFull = Path.Combine(gameOutputRoot, entry.XmlRelativePath);
                        xmlIssues[i] = ClassifyOutputArtifact(xmlFull, entry.OutputHash);
                    }

                    if (!createMods)
                    {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(entry.ModRelativePath))
                    {
                        modIssues[i] = PresenceIssueKind.Missing;
                        return;
                    }

                    if (trustOutputMods)
                    {
                        modIssues[i] = null;
                        return;
                    }

                    string modRoot = Path.Combine(gameOutputRoot, entry.ModRelativePath);
                    modIssues[i] = ClassifyModPackage(modRoot, gameType, entry.ModOutputHash);
                });

            for (int i = 0; i < inventory.Count; i++)
            {
                InventoryEntry entry = classified[i];
                PresenceIssueKind? xmlIssue = xmlIssues[i];
                bool xmlOk = xmlIssue is null;
                if (!xmlOk)
                {
                    result.MissingXml.Add(entry);
                    if (!string.IsNullOrWhiteSpace(entry.Guid))
                    {
                        result.XmlFailureKinds[entry.Guid] = xmlIssue!.Value;
                    }
                }
                else
                {
                    expectedXml.Add(NormalizeRelative(entry.XmlRelativePath));
                }

                if (createMods)
                {
                    PresenceIssueKind? modIssue = modIssues[i];
                    if (modIssue is not null)
                    {
                        result.MissingMod.Add(entry);
                        if (!string.IsNullOrWhiteSpace(entry.Guid))
                        {
                            result.ModFailureKinds[entry.Guid] = modIssue.Value;
                        }
                    }
                    else
                    {
                        expectedMods.Add(NormalizeRelative(entry.ModRelativePath!));
                        if (xmlOk)
                        {
                            result.Present.Add(entry);
                        }
                    }
                }
                else if (xmlOk)
                {
                    result.Present.Add(entry);
                }
            }
        }

        CollectXmlOrphans(context.AssetOutputDir, context.GameOutputRoot, expectedXml, result.OrphanXmlRelativePaths);

        if (context.CreateAssetMods && !string.IsNullOrWhiteSpace(context.AssetModOutputRoot))
        {
            CollectModOrphans(context.AssetModOutputRoot, context.GameOutputRoot, expectedMods, result.OrphanModRelativePaths);
        }

        return result;
    }

    public static void WriteStampAndInventory(PipelineContext context, string gameType, PipelineLogger? log = null)
    {
        if (!IsEligible(context) || context.DetectedGameBuild is null)
        {
            return;
        }

        string buildFingerprint = SourceXmlCache.BuildFingerprint(context.DetectedGameBuild);
        if (buildFingerprint.Length == 0)
        {
            return;
        }

        try
        {
            log?.WriteLocalized(ConsoleMessageKind.Cache, "outputCacheWritingStamp", always: true);
            try
            {
                Console.Out.Flush();
            }
            catch (IOException)
            {
            }

            Dictionary<string, string> sourceHashes = [];
            if (TryLoadAssetsDocument(context, out XmlDocument? assetsXml) && assetsXml is not null)
            {
                sourceHashes = AssetSourceHasher.BuildExtractableHashMap(
                    assetsXml,
                    NormalizeGameType(gameType),
                    context.AssetTemplatesList ?? context.TemplatesUsed);
            }

            // Hash side trees first (with progress), then build inventory from those maps
            // so we do not SHA-256 the same ~30k XML / mod files twice.
            var stamp = new StampDocument
            {
                Schema = SchemaVersion,
                OutputSchema = OutputSchemaVersion,
                GameType = NormalizeGameType(gameType),
                BuildFingerprint = buildFingerprint,
                OptionsFingerprint = BuildOptionsFingerprint(context),
                BuildDisplay = context.DetectedGameBuild.ToDisplayString(),
                CompletedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            StampTreeHashMaps treeMaps = PopulateStampTreeFingerprints(context, gameType, stamp);

            List<InventoryEntry> inventory = BuildInventoryFromDisk(
                context,
                sourceHashes,
                gameType,
                treeMaps);
            if (inventory.Count == 0)
            {
                return;
            }

            int modCount = inventory.Count(static e => !string.IsNullOrWhiteSpace(e.ModRelativePath));
            stamp.AssetXmlCount = inventory.Count;
            stamp.ModCount = modCount;

            _ = Directory.CreateDirectory(context.GameOutputRoot);
            WriteInventory(context.GameOutputRoot, inventory);

            string json = JsonSerializer.Serialize(stamp, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(context.GameOutputRoot, StampFileName), json + Environment.NewLine);

            log?.WriteLocalized(
                ConsoleMessageKind.Cache,
                "outputCacheStampWritten",
                always: true,
                inventory.Count);
            try
            {
                Console.Out.Flush();
            }
            catch (IOException)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or XmlException)
        {
            log?.Debug(string.Format(ConsoleMessages.Get("debugOutputCacheWriteFailed"), ex.Message));
        }
    }

    public static List<InventoryEntry> BuildInventoryFromDisk(
        PipelineContext context,
        IReadOnlyDictionary<string, string>? sourceHashes = null,
        string? gameTypeOverride = null) =>
        BuildInventoryFromDisk(context, sourceHashes, gameTypeOverride, precomputedTrees: null);

    internal static List<InventoryEntry> BuildInventoryFromDisk(
        PipelineContext context,
        IReadOnlyDictionary<string, string>? sourceHashes,
        string? gameTypeOverride,
        StampTreeHashMaps? precomputedTrees)
    {
        var entries = new List<InventoryEntry>();
        if (string.IsNullOrWhiteSpace(context.AssetOutputDir) || !Directory.Exists(context.AssetOutputDir))
        {
            return entries;
        }

        Dictionary<string, string> modsByGuid = context.CreateAssetMods
            ? IndexModFoldersByGuid(context.AssetModOutputRoot, context.GameOutputRoot)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(context.AssetOutputDir, "*.xml", SearchOption.AllDirectories).ToList();
        var bag = new ConcurrentBag<InventoryEntry>();
        string gameOutputRoot = context.GameOutputRoot;
        string assetOutputDir = context.AssetOutputDir;
        bool createMods = context.CreateAssetMods;
        IReadOnlyDictionary<string, string>? outputXmlHashes = precomputedTrees?.OutputXmlFileHashes;
        IReadOnlyDictionary<string, string>? modsFileHashes = precomputedTrees?.ModsFileHashes;
        string modsRoot = precomputedTrees?.ModsRoot ?? "";
        bool canReuseOutputHashes = outputXmlHashes is { Count: > 0 }
            && !string.IsNullOrWhiteSpace(precomputedTrees?.OutputXmlRoot);
        bool canReuseModHashes = modsFileHashes is { Count: > 0 }
            && !string.IsNullOrWhiteSpace(modsRoot)
            && Directory.Exists(modsRoot);

        int total = files.Count;
        int processed = 0;
        var progressLock = new Lock();
        // When tree hashes were already computed, inventory is mostly lookups — skip extra progress spam.
        Action<int, int>? inventoryProgress = total > 0 && precomputedTrees is null
            ? CreateInventoryProgress(context)
            : null;

        Parallel.ForEach(
            files,
            file =>
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    Match match = AssetFileNameRegex().Match(fileName);
                    if (!match.Success)
                    {
                        return;
                    }

                    string guid = match.Groups["guid"].Value;
                    string xmlRelative = NormalizeRelative(Path.GetRelativePath(gameOutputRoot, file));
                    modsByGuid.TryGetValue(guid, out string? modRelative);
                    string hash = "";
                    if (sourceHashes is not null
                        && sourceHashes.TryGetValue(guid, out string? foundHash)
                        && !string.IsNullOrEmpty(foundHash))
                    {
                        hash = foundHash;
                    }

                    string outputHash = "";
                    if (canReuseOutputHashes)
                    {
                        string relToOutput = NormalizeRelative(Path.GetRelativePath(assetOutputDir, file));
                        if (outputXmlHashes!.TryGetValue(relToOutput, out string? reused)
                            && !string.IsNullOrEmpty(reused))
                        {
                            outputHash = reused;
                        }
                    }

                    if (string.IsNullOrEmpty(outputHash))
                    {
                        outputHash = TryComputeFileSha256Hex(file) ?? "";
                    }

                    string modOutputHash = "";
                    if (createMods && !string.IsNullOrWhiteSpace(modRelative))
                    {
                        string modRoot = Path.Combine(gameOutputRoot, modRelative);
                        if (canReuseModHashes)
                        {
                            string modRelToMods = NormalizeRelative(Path.GetRelativePath(modsRoot, modRoot));
                            modOutputHash = DirectoryTreeFingerprint.TryDigestSubtree(modsFileHashes!, modRelToMods) ?? "";
                        }

                        if (string.IsNullOrEmpty(modOutputHash))
                        {
                            modOutputHash = TryComputeModPackageTreeFingerprint(modRoot) ?? "";
                        }
                    }

                    bag.Add(new InventoryEntry
                    {
                        Guid = guid,
                        XmlRelativePath = xmlRelative,
                        ModRelativePath = modRelative,
                        SourceHash = hash,
                        OutputHash = outputHash,
                        ModOutputHash = modOutputHash,
                    });
                }
                finally
                {
                    if (inventoryProgress is not null)
                    {
                        int current = Interlocked.Increment(ref processed);
                        if (DirectoryTreeFingerprint.ShouldReportHashProgress(current, total))
                        {
                            lock (progressLock)
                            {
                                inventoryProgress(current, total);
                            }
                        }
                    }
                }
            });

        entries.AddRange(bag);
        return entries;
    }

    private static Action<int, int> CreateInventoryProgress(PipelineContext context)
    {
        const string operation = "Hashing: inventory";
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
            }
        };
    }

    public static void DeleteOrphans(PipelineContext context, PresenceResult presence)
    {
        string requiredRoot = context.GameOutputRoot;
        foreach (string relative in presence.OrphanXmlRelativePaths)
        {
            string full = Path.Combine(requiredRoot, relative);
            TryDeleteFile(context, full, requiredRoot);
        }

        foreach (string relative in presence.OrphanModRelativePaths)
        {
            string full = Path.Combine(requiredRoot, relative);
            if (Directory.Exists(full))
            {
                _ = OutputPathSafety.TryDeleteDirectoryRecursive(context, full, requiredRoot);
            }
        }
    }

    public static void DeleteRemovedEntries(PipelineContext context, GameDiffResult diff) =>
        DeleteInventoryEntries(context, diff.RemovedEntries);

    public static void DeleteReplacedEntries(PipelineContext context, GameDiffResult diff) =>
        DeleteInventoryEntries(context, diff.ReplacedEntries);

    /// <summary>
    /// Before Presence Repair re-extract: remove empty/corrupt/leading-blank/content-changed
    /// inventory XML (and their mods), and remove invalid mod folders for MissingMod-only rows.
    /// Prevents ReserveUniqueOutputPath from creating <c>guid-2.xml</c> when a bad file still
    /// occupies the inventory path.
    /// </summary>
    public static void DeletePresenceRebuildEntries(PipelineContext context, PresenceResult presence)
    {
        DeleteInventoryEntries(context, presence.MissingXml);

        var xmlMissingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InventoryEntry entry in presence.MissingXml)
        {
            if (!string.IsNullOrWhiteSpace(entry.Guid))
            {
                xmlMissingGuids.Add(entry.Guid);
            }
        }

        string requiredRoot = context.GameOutputRoot;
        foreach (InventoryEntry entry in presence.MissingMod)
        {
            if (!string.IsNullOrWhiteSpace(entry.Guid) && xmlMissingGuids.Contains(entry.Guid))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.ModRelativePath))
            {
                continue;
            }

            string modFull = Path.Combine(requiredRoot, entry.ModRelativePath);
            if (Directory.Exists(modFull))
            {
                _ = OutputPathSafety.TryDeleteDirectoryRecursive(context, modFull, requiredRoot);
            }
        }
    }

    public static void DeleteInventoryEntries(PipelineContext context, IEnumerable<InventoryEntry> entries)
    {
        string requiredRoot = context.GameOutputRoot;
        foreach (InventoryEntry entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.XmlRelativePath))
            {
                TryDeleteFile(context, Path.Combine(requiredRoot, entry.XmlRelativePath), requiredRoot);
            }

            if (!string.IsNullOrWhiteSpace(entry.ModRelativePath))
            {
                string modFull = Path.Combine(requiredRoot, entry.ModRelativePath);
                if (Directory.Exists(modFull))
                {
                    _ = OutputPathSafety.TryDeleteDirectoryRecursive(context, modFull, requiredRoot);
                }
            }
        }
    }

    public static bool TryReadInventory(string gameOutputRoot, out List<InventoryEntry> inventory)
    {
        inventory = [];
        string path = Path.Combine(gameOutputRoot, InventoryFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                InventoryEntry? entry = JsonSerializer.Deserialize<InventoryEntry>(line);
                if (entry is null || string.IsNullOrWhiteSpace(entry.Guid) || string.IsNullOrWhiteSpace(entry.XmlRelativePath))
                {
                    continue;
                }

                inventory.Add(entry);
            }

            return inventory.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            inventory = [];
            return false;
        }
    }

    private static bool TryLoadAssetsDocument(PipelineContext context, out XmlDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(context.SourceXmlFolder))
        {
            return false;
        }

        string assetsPath = Path.Combine(context.SourceXmlFolder, "assets.xml");
        if (!File.Exists(assetsPath))
        {
            return false;
        }

        try
        {
            document = new XmlDocument { PreserveWhitespace = false };
            XmlDocumentLoadRetry.Load(document, assetsPath);
            return document.DocumentElement is not null;
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException or NotSupportedException)
        {
            document = null;
            return false;
        }
    }

    private static void WriteInventory(string gameOutputRoot, List<InventoryEntry> inventory)
    {
        string path = Path.Combine(gameOutputRoot, InventoryFileName);
        using var writer = new StreamWriter(path, append: false);
        foreach (InventoryEntry entry in inventory.OrderBy(static e => e.Guid, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine(JsonSerializer.Serialize(entry));
        }
    }

    private static bool TryReadStamp(string gameOutputRoot, out StampDocument? stamp)
    {
        stamp = null;
        string path = Path.Combine(gameOutputRoot, StampFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            stamp = JsonSerializer.Deserialize<StampDocument>(File.ReadAllText(path));
            return stamp is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> IndexModFoldersByGuid(string modRoot, string gameOutputRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(modRoot) || !Directory.Exists(modRoot))
        {
            return map;
        }

        foreach (string modinfo in Directory.EnumerateFiles(modRoot, "modinfo.json", SearchOption.AllDirectories))
        {
            string? folder = Path.GetDirectoryName(modinfo);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            string folderName = Path.GetFileName(folder);
            if (!TryParseModFolderGuid(folderName, out string guid))
            {
                continue;
            }

            map[guid] = NormalizeRelative(Path.GetRelativePath(gameOutputRoot, folder));
        }

        return map;
    }

    private static void CollectXmlOrphans(
        string assetOutputDir,
        string gameOutputRoot,
        HashSet<string> expectedXml,
        List<string> orphans)
    {
        if (!Directory.Exists(assetOutputDir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(assetOutputDir, "*.xml", SearchOption.AllDirectories))
        {
            string relative = NormalizeRelative(Path.GetRelativePath(gameOutputRoot, file));
            if (!expectedXml.Contains(relative) && AssetFileNameRegex().IsMatch(Path.GetFileName(file)))
            {
                orphans.Add(relative);
            }
        }
    }

    private static void CollectModOrphans(
        string modRoot,
        string gameOutputRoot,
        HashSet<string> expectedMods,
        List<string> orphans)
    {
        if (!Directory.Exists(modRoot))
        {
            return;
        }

        foreach (string modinfo in Directory.EnumerateFiles(modRoot, "modinfo.json", SearchOption.AllDirectories))
        {
            string? folder = Path.GetDirectoryName(modinfo);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            string relative = NormalizeRelative(Path.GetRelativePath(gameOutputRoot, folder));
            if (!expectedMods.Contains(relative))
            {
                orphans.Add(relative);
            }
        }
    }

    private static bool IsValidModPackage(string modRoot, string gameType, string expectedModOutputHash) =>
        ClassifyModPackage(modRoot, gameType, expectedModOutputHash) is null;

    /// <summary>
    /// Classifies a flat output XML (or mod <c>assets.xml</c>): missing file, incomplete/corrupt
    /// leading bytes, or content hash mismatch. Returns null when the artifact is intact and matches.
    /// </summary>
    internal static PresenceIssueKind? ClassifyOutputArtifact(string path, string? expectedHash)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return PresenceIssueKind.Missing;
            }

            if (info.Length == 0 || HasLeadingWhitespaceOrNonMarkupPrefix(path))
            {
                return PresenceIssueKind.Incomplete;
            }

            if (!HasMatchingOutputHash(path, expectedHash))
            {
                return PresenceIssueKind.ContentChanged;
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return PresenceIssueKind.Incomplete;
        }
    }

    internal static PresenceIssueKind? ClassifyModPackage(string modRoot, string gameType, string expectedModOutputHash)
    {
        if (!Directory.Exists(modRoot) || !File.Exists(Path.Combine(modRoot, "modinfo.json")))
        {
            return PresenceIssueKind.Missing;
        }

        string assetsPath = GetModAssetsXmlPath(modRoot, gameType);
        try
        {
            var assetsInfo = new FileInfo(assetsPath);
            if (!assetsInfo.Exists)
            {
                return PresenceIssueKind.Missing;
            }

            if (assetsInfo.Length == 0 || HasLeadingWhitespaceOrNonMarkupPrefix(assetsPath))
            {
                return PresenceIssueKind.Incomplete;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return PresenceIssueKind.Incomplete;
        }

        if (string.IsNullOrWhiteSpace(expectedModOutputHash))
        {
            return PresenceIssueKind.ContentChanged;
        }

        string? actualTree = TryComputeModPackageTreeFingerprint(modRoot);
        if (actualTree is null)
        {
            return PresenceIssueKind.Incomplete;
        }

        if (!actualTree.Equals(expectedModOutputHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return PresenceIssueKind.ContentChanged;
        }

        return null;
    }

    internal static string GetModAssetsXmlPath(string modRoot, string gameType) =>
        gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(modRoot, "data", "base", "config", "export", "assets.xml")
            : Path.Combine(modRoot, "data", "config", "export", "main", "asset", "assets.xml");

    /// <summary>
    /// Merkle-style fingerprint of every file under <paramref name="modRoot"/>:
    /// sorted relative paths (forward slashes) each paired with a streamed SHA-256 of
    /// that file's bytes, then SHA-256 of the concatenated <c>path\0fileHash\n</c> lines.
    /// Missing/extra/edited files all change the digest. Returns null on I/O failure.
    /// </summary>
    internal static string? TryComputeModPackageTreeFingerprint(string modRoot) =>
        DirectoryTreeFingerprint.TryCompute(modRoot);

    /// <summary>
    /// Enumerates every file under <paramref name="modRoot"/> as normalized relative paths
    /// (forward slashes). Returns empty when the folder is missing.
    /// </summary>
    internal static List<string> ListModPackageRelativePaths(string modRoot) =>
        DirectoryTreeFingerprint.ListRelativePaths(modRoot);

    /// <summary>
    /// True when <paramref name="expectedHash"/> is non-empty and matches a streamed SHA-256 of the file.
    /// Missing/blank expected hashes fail so Complete never skips unverified content.
    /// </summary>
    internal static bool HasMatchingOutputHash(string path, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        string? actual = TryComputeFileSha256Hex(path);
        return actual is not null
            && actual.Equals(expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Streams file bytes through SHA-256; returns lowercase hex or null on I/O failure.</summary>
    internal static string? TryComputeFileSha256Hex(string path) =>
        DirectoryTreeFingerprint.TryComputeFileSha256Hex(path);

    /// <summary>
    /// Cheap presence/integrity probe: exists, non-empty, and first markup byte is <c>&lt;</c>
    /// (after optional UTF-8 BOM). Leading newlines/spaces before the header or root — the
    /// pre-schema-3 footprint bug — fail this check and are treated as Missing.
    /// Content edits that still start with <c>&lt;</c> are caught by <see cref="HasMatchingOutputHash"/>.
    /// </summary>
    internal static bool IsIntactOutputXml(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0)
            {
                return false;
            }

            return !HasLeadingWhitespaceOrNonMarkupPrefix(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true when the file is missing a leading <c>&lt;</c> after an optional UTF-8 BOM
    /// (includes leading CR/LF/space/tab and other non-markup prefixes).
    /// </summary>
    internal static bool HasLeadingWhitespaceOrNonMarkupPrefix(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            Span<byte> buffer = stackalloc byte[8];
            int read = stream.Read(buffer);
            if (read <= 0)
            {
                return true;
            }

            int index = 0;
            if (read >= 3
                && buffer[0] == 0xEF
                && buffer[1] == 0xBB
                && buffer[2] == 0xBF)
            {
                index = 3;
            }

            if (index >= read)
            {
                return true;
            }

            return buffer[index] != (byte)'<';
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return true;
        }
    }

    private static void TryDeleteFile(PipelineContext context, string fullPath, string requiredRoot)
    {
        if (!File.Exists(fullPath))
        {
            return;
        }

        if (!OutputPathSafety.IsPathUnderDirectory(fullPath, requiredRoot))
        {
            return;
        }

        try
        {
            File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputCacheOrphanDeleteFailed"), fullPath, ex.Message));
        }
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string NormalizeGameType(string gameType) =>
        GameTypeDetector.IsAnno117(gameType) ? GameTypeDetector.Anno117 : GameTypeDetector.Anno1800;

    /// <summary>
    /// Mod folders use <c>minus-N</c> for negative GUIDs (see <c>NormalizeIdPart</c>);
    /// inventory must reverse that so GUID -1 packages are not permanent orphans.
    /// </summary>
    internal static bool TryParseModFolderGuid(string folderName, out string guid)
    {
        guid = "";
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        int sep = folderName.IndexOf(" - ", StringComparison.Ordinal);
        string token = sep > 0 ? folderName[..sep].Trim() : folderName.Trim();
        if (token.Length == 0)
        {
            return false;
        }

        if (token.StartsWith("minus-", StringComparison.OrdinalIgnoreCase))
        {
            token = "-" + token["minus-".Length..];
        }

        if (token.Length == 0 || token is "-")
        {
            return false;
        }

        int digitStart = token[0] == '-' ? 1 : 0;
        if (digitStart >= token.Length)
        {
            return false;
        }

        for (int i = digitStart; i < token.Length; i++)
        {
            if (!char.IsDigit(token[i]))
            {
                return false;
            }
        }

        guid = token;
        return true;
    }

    // Allow optional leading '-' so negative GUIDs (e.g. -1) match the mod exporter regex.
    [GeneratedRegex(@"^(?<guid>-?\d+)\s*-\s*\[\s*(?<name>.+?)\s*\]\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AssetFileNameRegex();
}
