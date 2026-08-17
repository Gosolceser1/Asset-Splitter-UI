using System.Text;
using System.Text.RegularExpressions;

namespace AssetProcessor;

internal static partial class OutputRunCache
{
    internal const int ContentChangedDiffLineLimit = TextLineDiff.DefaultMaxReportedChangeLines;
    internal const int ContentChangedDiffMaxLineChars = TextLineDiff.DefaultMaxLineDisplayChars;
    internal const int ContentChangedTreeDiffPathLimit = 20;
    internal const int MaxModPackageSnapshotFileBytes = 2 * 1024 * 1024;

    /// <summary>Which on-disk artifact was content-changed and snapshotted before rebuild.</summary>
    internal enum ContentChangedArtifactKind
    {
        FlatXml,
        ModAssetsXml,
        ModFile,
    }

    /// <summary>
    /// Snapshot of one mod package folder before content-changed repair deletes it.
    /// Text entries hold file contents; binary paths are listed separately (no bytes retained).
    /// <see cref="ContentHashByRelativePath"/> covers every snapshotted path for equality checks.
    /// </summary>
    internal sealed class ModPackageFileSnapshot
    {
        public Dictionary<string, string> TextByRelativePath { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BinaryRelativePaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ContentHashByRelativePath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> AllRelativePaths =>
            new(ContentHashByRelativePath.Keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Dirty on-disk text for content-changed inventory rows, keyed by GUID.
    /// Captured before delete/rebuild so post-write logging can show old vs new lines.
    /// </summary>
    internal sealed class ContentChangedRepairSnapshots
    {
        public Dictionary<string, string> FlatXmlByGuid { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ModAssetsXmlByGuid { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ModPackageFileSnapshot> ModPackageByGuid { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> LabelsByGuid { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool HasAny =>
            FlatXmlByGuid.Count > 0
            || ModAssetsXmlByGuid.Count > 0
            || ModPackageByGuid.Count > 0;
    }

    /// <summary>
    /// Before Presence Repair deletes content-changed files, snapshot their text for later diff logging.
    /// Missing/incomplete rows are skipped (nothing meaningful to compare).
    /// </summary>
    public static void CaptureContentChangedSnapshots(PipelineContext context, PresenceResult presence)
    {
        var snapshots = new ContentChangedRepairSnapshots();
        string gameOutputRoot = context.GameOutputRoot;
        string gameType = NormalizeGameType(
            string.IsNullOrWhiteSpace(context.DetectedGameType) ? "anno1800" : context.DetectedGameType);

        foreach (InventoryEntry entry in presence.MissingXml)
        {
            if (string.IsNullOrWhiteSpace(entry.Guid)
                || !presence.XmlFailureKinds.TryGetValue(entry.Guid, out PresenceIssueKind kind)
                || kind != PresenceIssueKind.ContentChanged
                || string.IsNullOrWhiteSpace(entry.XmlRelativePath))
            {
                continue;
            }

            string fullPath = Path.Combine(gameOutputRoot, entry.XmlRelativePath);
            if (!TryReadAllText(fullPath, out string text))
            {
                continue;
            }

            snapshots.FlatXmlByGuid[entry.Guid] = text;
            snapshots.LabelsByGuid[entry.Guid] = ResolveRepairAssetLabel(entry, context);
        }

        foreach (InventoryEntry entry in presence.MissingMod)
        {
            if (string.IsNullOrWhiteSpace(entry.Guid)
                || !presence.ModFailureKinds.TryGetValue(entry.Guid, out PresenceIssueKind kind)
                || kind != PresenceIssueKind.ContentChanged
                || string.IsNullOrWhiteSpace(entry.ModRelativePath))
            {
                continue;
            }

            string modRoot = Path.Combine(gameOutputRoot, entry.ModRelativePath);
            ModPackageFileSnapshot? package = TryCaptureModPackageSnapshot(modRoot);
            if (package is not null)
            {
                snapshots.ModPackageByGuid[entry.Guid] = package;
            }

            string assetsPath = GetModAssetsXmlPath(modRoot, gameType);
            if (TryReadAllText(assetsPath, out string assetsText))
            {
                snapshots.ModAssetsXmlByGuid[entry.Guid] = assetsText;
            }

            snapshots.LabelsByGuid.TryAdd(entry.Guid, ResolveRepairAssetLabel(entry, context));
        }

        context.ContentChangedSnapshots = snapshots.HasAny ? snapshots : null;
    }

    /// <summary>
    /// After flat XML for repair GUIDs has been rewritten (extract and/or format),
    /// log capped line diffs for content-changed snapshots and clear those entries.
    /// </summary>
    public static void TryFlushFlatXmlContentChangedDiffs(PipelineContext context)
    {
        ContentChangedRepairSnapshots? snapshots = context.ContentChangedSnapshots;
        if (snapshots is null || snapshots.FlatXmlByGuid.Count == 0)
        {
            return;
        }

        foreach (string guid in snapshots.FlatXmlByGuid.Keys.OrderBy(static g => g, StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (!snapshots.FlatXmlByGuid.TryGetValue(guid, out string? oldText))
            {
                continue;
            }

            string? newPath = TryFindRepairOutputPath(context, guid);
            if (string.IsNullOrWhiteSpace(newPath) || !TryReadAllText(newPath, out string newText))
            {
                snapshots.FlatXmlByGuid.Remove(guid);
                continue;
            }

            string label = ResolveSnapshotLabel(snapshots, guid);
            WriteContentChangedDiffLog(
                context,
                label,
                ContentChangedArtifactKind.FlatXml,
                oldText,
                newText);
            snapshots.FlatXmlByGuid.Remove(guid);
        }

        if (!snapshots.HasAny)
        {
            context.ContentChangedSnapshots = null;
        }
    }

    /// <summary>
    /// After a mod package is rewritten, log tree path diffs and per-file text/binary detail
    /// when a content-changed snapshot exists for that GUID.
    /// </summary>
    public static void TryWriteContentChangedModDiff(
        PipelineContext context,
        string guid,
        string label,
        string newModAssetsPath)
    {
        ContentChangedRepairSnapshots? snapshots = context.ContentChangedSnapshots;
        if (snapshots is null || string.IsNullOrWhiteSpace(guid))
        {
            return;
        }

        string displayLabel = string.IsNullOrWhiteSpace(label)
            ? ResolveSnapshotLabel(snapshots, guid)
            : label.Trim();

        string? modRoot = TryFindModRootFromAssetsPath(newModAssetsPath);
        bool wrotePackageDiff = false;
        if (modRoot is not null
            && snapshots.ModPackageByGuid.TryGetValue(guid, out ModPackageFileSnapshot? oldPackage)
            && oldPackage is not null)
        {
            snapshots.ModPackageByGuid.Remove(guid);
            WriteModPackageTreeDiffLog(context, displayLabel, oldPackage, modRoot);
            wrotePackageDiff = true;
        }

        if (snapshots.ModAssetsXmlByGuid.TryGetValue(guid, out string? oldAssetsText))
        {
            snapshots.ModAssetsXmlByGuid.Remove(guid);
            // Package tree logging already covers assets.xml line diffs when that file changed.
            if (!wrotePackageDiff && TryReadAllText(newModAssetsPath, out string newAssetsText))
            {
                WriteContentChangedDiffLog(
                    context,
                    displayLabel,
                    ContentChangedArtifactKind.ModAssetsXml,
                    oldAssetsText,
                    newAssetsText);
            }
        }

        if (!snapshots.HasAny)
        {
            context.ContentChangedSnapshots = null;
        }
    }

    /// <summary>
    /// Builds localized console lines for a content-changed text diff (header + change lines + optional more).
    /// Returns empty when there is nothing meaningful to compare.
    /// </summary>
    internal static IReadOnlyList<(string MessageKey, object[] Args)> BuildContentChangedDiffLogLines(
        string label,
        ContentChangedArtifactKind kind,
        string? oldText,
        string? newText,
        int maxReportedChangeLines = ContentChangedDiffLineLimit,
        int maxLineDisplayChars = ContentChangedDiffMaxLineChars,
        string? relativePath = null)
    {
        TextLineDiff.Result diff = TextLineDiff.Compare(
            oldText,
            newText,
            maxReportedChangeLines,
            maxLineDisplayChars);
        if (diff.TotalChangeLines == 0)
        {
            return [];
        }

        string headerKey = kind switch
        {
            ContentChangedArtifactKind.ModAssetsXml => "contentChangedDiffHeaderMod",
            ContentChangedArtifactKind.ModFile => "contentChangedDiffHeaderModFile",
            _ => "contentChangedDiffHeaderXml",
        };

        object[] headerArgs = kind == ContentChangedArtifactKind.ModFile
            ? [relativePath ?? "", label]
            : [label];

        var lines = new List<(string MessageKey, object[] Args)>(1 + diff.Reported.Count + 1)
        {
            (headerKey, headerArgs),
        };

        foreach (TextLineDiff.ChangeLine change in diff.Reported)
        {
            string lineKey = change.IsAddition ? "contentChangedDiffAdded" : "contentChangedDiffRemoved";
            lines.Add((lineKey, [change.LineNumber, change.Text]));
        }

        int omitted = diff.TotalChangeLines - diff.Reported.Count;
        if (omitted > 0)
        {
            lines.Add(("contentChangedDiffMore", [omitted]));
        }

        return lines;
    }

    /// <summary>
    /// Builds capped path-level + per-file detail lines comparing a pre-repair mod snapshot to the rebuilt folder.
    /// </summary>
    internal static IReadOnlyList<(string MessageKey, object[] Args)> BuildModPackageTreeDiffLogLines(
        string label,
        ModPackageFileSnapshot oldPackage,
        string newModRoot,
        int maxPaths = ContentChangedTreeDiffPathLimit)
    {
        var lines = new List<(string MessageKey, object[] Args)>();
        if (string.IsNullOrWhiteSpace(newModRoot) || !Directory.Exists(newModRoot))
        {
            return lines;
        }

        Dictionary<string, string?> newTextByPath = [];
        var newHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newBinary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string relative in ListModPackageRelativePaths(newModRoot))
        {
            string full = Path.Combine(newModRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            string? hash = TryComputeFileSha256Hex(full);
            if (hash is not null)
            {
                newHashes[relative] = hash;
            }

            if (TryReadSnapshotFile(full, out string? text, out bool isBinary))
            {
                if (isBinary)
                {
                    newBinary.Add(relative);
                    newTextByPath[relative] = null;
                }
                else
                {
                    newTextByPath[relative] = text ?? "";
                }
            }
        }

        HashSet<string> oldPaths = oldPackage.AllRelativePaths;
        var newPaths = new HashSet<string>(newHashes.Keys, StringComparer.OrdinalIgnoreCase);

        var removed = oldPaths.Where(p => !newPaths.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var added = newPaths.Where(p => !oldPaths.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var changed = new List<string>();
        foreach (string path in oldPaths.Intersect(newPaths, StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            oldPackage.ContentHashByRelativePath.TryGetValue(path, out string? oldHash);
            newHashes.TryGetValue(path, out string? newHash);
            if (!string.Equals(oldHash, newHash, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(path);
            }
        }

        int totalPathChanges = removed.Count + added.Count + changed.Count;
        if (totalPathChanges == 0)
        {
            return lines;
        }

        lines.Add(("contentChangedDiffHeaderModPackage", [label]));

        var pathEntries = new List<(string Key, string Path)>(totalPathChanges);
        foreach (string path in removed)
        {
            pathEntries.Add(("contentChangedDiffPathRemoved", path));
        }

        foreach (string path in added)
        {
            pathEntries.Add(("contentChangedDiffPathAdded", path));
        }

        foreach (string path in changed)
        {
            pathEntries.Add(("contentChangedDiffPathChanged", path));
        }

        int pathTake = Math.Min(pathEntries.Count, Math.Max(0, maxPaths));
        for (int i = 0; i < pathTake; i++)
        {
            lines.Add((pathEntries[i].Key, [pathEntries[i].Path]));
        }

        int pathOmitted = pathEntries.Count - pathTake;
        if (pathOmitted > 0)
        {
            lines.Add(("contentChangedDiffPathMore", [pathOmitted]));
        }

        // Prefer modinfo.json / assets.xml first for detailed line diffs.
        foreach (string path in PrioritizeModDiffPaths(changed))
        {
            bool oldBinary = oldPackage.BinaryRelativePaths.Contains(path);
            bool newIsBinary = newBinary.Contains(path);
            if (oldBinary || newIsBinary)
            {
                lines.Add(("contentChangedDiffBinary", [path]));
                continue;
            }

            oldPackage.TextByRelativePath.TryGetValue(path, out string? oldText);
            newTextByPath.TryGetValue(path, out string? newText);
            IReadOnlyList<(string MessageKey, object[] Args)> fileLines = BuildContentChangedDiffLogLines(
                label,
                ContentChangedArtifactKind.ModFile,
                oldText,
                newText,
                relativePath: path);
            lines.AddRange(fileLines);
        }

        return lines;
    }

    private static void WriteModPackageTreeDiffLog(
        PipelineContext context,
        string label,
        ModPackageFileSnapshot oldPackage,
        string newModRoot)
    {
        IReadOnlyList<(string MessageKey, object[] Args)> lines = BuildModPackageTreeDiffLogLines(
            label,
            oldPackage,
            newModRoot);
        foreach ((string messageKey, object[] args) in lines)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, messageKey, always: true, args);
        }
    }

    private static void WriteContentChangedDiffLog(
        PipelineContext context,
        string label,
        ContentChangedArtifactKind kind,
        string oldText,
        string newText,
        string? relativePath = null)
    {
        IReadOnlyList<(string MessageKey, object[] Args)> lines = BuildContentChangedDiffLogLines(
            label,
            kind,
            oldText,
            newText,
            relativePath: relativePath);
        if (lines.Count == 0)
        {
            return;
        }

        foreach ((string messageKey, object[] args) in lines)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, messageKey, always: true, args);
        }
    }

    private static string ResolveSnapshotLabel(ContentChangedRepairSnapshots snapshots, string guid)
    {
        if (snapshots.LabelsByGuid.TryGetValue(guid, out string? label) && !string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        return guid.Trim();
    }

    private static string? TryFindRepairOutputPath(PipelineContext context, string guid)
    {
        string trimmed = guid.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        foreach (string path in context.RepairOutputFiles)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            Match match = AssetFileNameRegex().Match(Path.GetFileName(path));
            if (match.Success
                && match.Groups["guid"].Value.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    internal static ModPackageFileSnapshot? TryCaptureModPackageSnapshot(string modRoot)
    {
        if (string.IsNullOrWhiteSpace(modRoot) || !Directory.Exists(modRoot))
        {
            return null;
        }

        var snapshot = new ModPackageFileSnapshot();
        foreach (string relative in ListModPackageRelativePaths(modRoot))
        {
            string full = Path.Combine(modRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            string? hash = TryComputeFileSha256Hex(full);
            if (hash is not null)
            {
                snapshot.ContentHashByRelativePath[relative] = hash;
            }

            if (!TryReadSnapshotFile(full, out string? text, out bool isBinary))
            {
                continue;
            }

            if (isBinary)
            {
                snapshot.BinaryRelativePaths.Add(relative);
            }
            else
            {
                snapshot.TextByRelativePath[relative] = text ?? "";
            }
        }

        return snapshot.ContentHashByRelativePath.Count > 0 ? snapshot : null;
    }

    internal static string? TryFindModRootFromAssetsPath(string assetsPath)
    {
        if (string.IsNullOrWhiteSpace(assetsPath))
        {
            return null;
        }

        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(assetsPath));
            while (!string.IsNullOrWhiteSpace(dir))
            {
                if (File.Exists(Path.Combine(dir, "modinfo.json")))
                {
                    return dir;
                }

                DirectoryInfo? parent = Directory.GetParent(dir);
                if (parent is null)
                {
                    break;
                }

                dir = parent.FullName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<string> PrioritizeModDiffPaths(IReadOnlyList<string> changed)
    {
        static int Rank(string path)
        {
            if (path.Equals("modinfo.json", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (path.EndsWith("/assets.xml", StringComparison.OrdinalIgnoreCase)
                || path.Equals("assets.xml", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        return changed.OrderBy(Rank).ThenBy(p => p, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryReadSnapshotFile(string path, out string? text, out bool isBinary)
    {
        text = null;
        isBinary = false;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var info = new FileInfo(path);
            if (info.Length > MaxModPackageSnapshotFileBytes)
            {
                isBinary = true;
                return true;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (ContainsNullByte(bytes))
            {
                isBinary = true;
                return true;
            }

            text = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or DecoderFallbackException)
        {
            isBinary = true;
            return true;
        }
    }

    private static bool ContainsNullByte(ReadOnlySpan<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadAllText(string path, out string text)
    {
        text = "";
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            text = File.ReadAllText(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
