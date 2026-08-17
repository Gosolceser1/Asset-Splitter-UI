namespace AssetProcessor;

internal static partial class OutputRunCache
{
    internal const int RepairDetailListLimit = 20;

    /// <summary>One repaired GUID for console detail lines (message key + display label).</summary>
    internal readonly record struct RepairDetailItem(string Guid, string Label, string MessageKey);

    /// <summary>
    /// After the Presence Repair summary line, list each repaired GUID with a cheap reason key.
    /// </summary>
    public static void WritePresenceRepairDetails(PipelineContext context, PresenceResult presence)
    {
        IReadOnlyList<RepairDetailItem> items = BuildPresenceRepairDetails(presence, context);
        WriteRepairDetailLines(context.Log, items);
    }

    /// <summary>
    /// After the DiffRepair summary line, list rebuild GUIDs with reason keys (added / changed / rebuild).
    /// </summary>
    public static void WriteDiffRepairDetails(PipelineContext context, GameDiffResult diff)
    {
        IReadOnlyList<RepairDetailItem> items = BuildDiffRepairDetails(diff, context);
        WriteRepairDetailLines(context.Log, items);
    }

    internal static IReadOnlyList<RepairDetailItem> BuildPresenceRepairDetails(
        PresenceResult presence,
        PipelineContext? context = null)
    {
        var byGuid = new Dictionary<string, RepairDetailItem>(StringComparer.OrdinalIgnoreCase);

        foreach (InventoryEntry entry in presence.MissingXml)
        {
            if (string.IsNullOrWhiteSpace(entry.Guid))
            {
                continue;
            }

            PresenceIssueKind kind = presence.XmlFailureKinds.TryGetValue(entry.Guid, out PresenceIssueKind found)
                ? found
                : PresenceIssueKind.Incomplete;
            byGuid[entry.Guid] = new RepairDetailItem(
                entry.Guid,
                ResolveRepairAssetLabel(entry, context),
                PresenceItemMessageKey(kind));
        }

        foreach (InventoryEntry entry in presence.MissingMod)
        {
            if (string.IsNullOrWhiteSpace(entry.Guid) || byGuid.ContainsKey(entry.Guid))
            {
                continue;
            }

            PresenceIssueKind kind = presence.ModFailureKinds.TryGetValue(entry.Guid, out PresenceIssueKind found)
                ? found
                : PresenceIssueKind.Incomplete;
            byGuid[entry.Guid] = new RepairDetailItem(
                entry.Guid,
                ResolveRepairAssetLabel(entry, context),
                PresenceItemMessageKey(kind));
        }

        return byGuid.Values
            .OrderBy(static i => i.Guid, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IReadOnlyList<RepairDetailItem> BuildDiffRepairDetails(
        GameDiffResult diff,
        PipelineContext? context = null)
    {
        var priorByGuid = new Dictionary<string, InventoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (InventoryEntry entry in diff.ReplacedEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Guid))
            {
                priorByGuid[entry.Guid] = entry;
            }
        }

        foreach (InventoryEntry entry in diff.RemovedEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Guid))
            {
                priorByGuid.TryAdd(entry.Guid, entry);
            }
        }

        var added = new HashSet<string>(diff.Added, StringComparer.OrdinalIgnoreCase);
        var changed = new HashSet<string>(diff.Changed, StringComparer.OrdinalIgnoreCase);
        var items = new List<RepairDetailItem>();

        foreach (string guid in diff.RebuildGuids.OrderBy(static g => g, StringComparer.OrdinalIgnoreCase))
        {
            string messageKey = added.Contains(guid)
                ? "outputCacheRepairItemAdded"
                : changed.Contains(guid)
                    ? "outputCacheRepairItemChanged"
                    : "outputCacheRepairItemRebuild";

            priorByGuid.TryGetValue(guid, out InventoryEntry? entry);
            string label = entry is not null
                ? ResolveRepairAssetLabel(entry, context)
                : ResolveRepairAssetLabel(guid, xmlRelativePath: null, modRelativePath: null, context);

            items.Add(new RepairDetailItem(guid, label, messageKey));
        }

        return items;
    }

    internal static void WriteRepairDetailLines(PipelineLogger? log, IReadOnlyList<RepairDetailItem> items)
    {
        if (log is null || items.Count == 0)
        {
            return;
        }

        int listed = Math.Min(items.Count, RepairDetailListLimit);
        for (int i = 0; i < listed; i++)
        {
            RepairDetailItem item = items[i];
            log.WriteLocalized(
                ConsoleMessageKind.Ok,
                item.MessageKey,
                always: true,
                item.Label);
        }

        int omitted = items.Count - listed;
        if (omitted > 0)
        {
            log.WriteLocalized(ConsoleMessageKind.Ok, "outputCacheRepairMore", always: true, omitted);
        }
    }

    internal static string PresenceItemMessageKey(PresenceIssueKind kind) =>
        kind switch
        {
            PresenceIssueKind.Missing => "outputCacheRepairItemMissing",
            PresenceIssueKind.ContentChanged => "outputCacheRepairItemContentChanged",
            _ => "outputCacheRepairItemIncomplete",
        };

    /// <summary>
    /// Stores per-GUID item message keys on <paramref name="context"/> so later phases can
    /// log action→reason lines without hardcoding GUID lists.
    /// </summary>
    public static void StoreRepairGuidMessageKeys(PipelineContext context, IReadOnlyList<RepairDetailItem> items)
    {
        if (items.Count == 0)
        {
            context.RepairGuidMessageKeys = null;
            return;
        }

        var map = new Dictionary<string, string>(items.Count, StringComparer.OrdinalIgnoreCase);
        foreach (RepairDetailItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Guid) || string.IsNullOrWhiteSpace(item.MessageKey))
            {
                continue;
            }

            map[item.Guid] = item.MessageKey;
        }

        context.RepairGuidMessageKeys = map.Count > 0 ? map : null;
    }

    /// <summary>Maps an OUTPUT CACHE item key to the Phase 3 extract repair action key.</summary>
    internal static string? TryMapItemKeyToExtractRepairActionKey(string itemMessageKey) =>
        itemMessageKey switch
        {
            "outputCacheRepairItemMissing" => "extractRepairBecauseMissing",
            "outputCacheRepairItemIncomplete" => "extractRepairBecauseIncomplete",
            "outputCacheRepairItemContentChanged" => "extractRepairBecauseContentChanged",
            "outputCacheRepairItemAdded" => "extractRepairBecauseAdded",
            "outputCacheRepairItemChanged" => "extractRepairBecauseChanged",
            "outputCacheRepairItemRebuild" => "extractRepairBecauseRebuild",
            _ => null,
        };

    /// <summary>Maps an OUTPUT CACHE item key to the Phase 8 mod repair action key.</summary>
    internal static string? TryMapItemKeyToModRepairActionKey(string itemMessageKey) =>
        itemMessageKey switch
        {
            "outputCacheRepairItemMissing" => "modRepairBecauseMissing",
            "outputCacheRepairItemIncomplete" => "modRepairBecauseIncomplete",
            "outputCacheRepairItemContentChanged" => "modRepairBecauseContentChanged",
            "outputCacheRepairItemAdded" => "modRepairBecauseAdded",
            "outputCacheRepairItemChanged" => "modRepairBecauseChanged",
            "outputCacheRepairItemRebuild" => "modRepairBecauseRebuild",
            _ => null,
        };

    /// <summary>
    /// When <paramref name="guid"/> is in repair metadata, writes a localized action→reason line.
    /// </summary>
    public static void TryWriteRepairActionLine(
        PipelineContext context,
        string guid,
        string label,
        Func<string, string?> mapItemKeyToActionKey,
        ConsoleMessageKind kind)
    {
        if (string.IsNullOrWhiteSpace(guid)
            || context.RepairGuidMessageKeys is null
            || !context.RepairGuidMessageKeys.TryGetValue(guid, out string? itemKey)
            || string.IsNullOrWhiteSpace(itemKey))
        {
            return;
        }

        string? actionKey = mapItemKeyToActionKey(itemKey);
        if (string.IsNullOrWhiteSpace(actionKey))
        {
            return;
        }

        string displayLabel = string.IsNullOrWhiteSpace(label) ? guid.Trim() : label.Trim();
        context.Log.WriteLocalized(kind, actionKey, always: true, displayLabel);
    }

    /// <summary>
    /// Display label for a repaired asset: inventory XML/mod path stem when present,
    /// else registry name (if already loaded) via <c>outputCacheRepairAssetLabel</c>, else GUID.
    /// </summary>
    internal static string ResolveRepairAssetLabel(InventoryEntry entry, PipelineContext? context = null) =>
        ResolveRepairAssetLabel(entry.Guid, entry.XmlRelativePath, entry.ModRelativePath, context);

    internal static string ResolveRepairAssetLabel(
        string guid,
        string? xmlRelativePath,
        string? modRelativePath,
        PipelineContext? context = null)
    {
        string? fromXml = TryStemLabelFromRelativePath(xmlRelativePath, isDirectory: false);
        if (!string.IsNullOrWhiteSpace(fromXml))
        {
            return fromXml;
        }

        string? fromMod = TryStemLabelFromRelativePath(modRelativePath, isDirectory: true);
        if (!string.IsNullOrWhiteSpace(fromMod))
        {
            return fromMod;
        }

        string trimmedGuid = string.IsNullOrWhiteSpace(guid) ? "" : guid.Trim();
        if (context is not null
            && trimmedGuid.Length > 0
            && context.AssetNames.TryGetValue(trimmedGuid, out string? registryName)
            && !string.IsNullOrWhiteSpace(registryName))
        {
            return FormatGuidAndName(trimmedGuid, registryName.Trim());
        }

        return trimmedGuid;
    }

    /// <summary>Joins GUID + bare name using <c>outputCacheRepairAssetLabel</c> ({0}=guid, {1}=name).</summary>
    internal static string FormatGuidAndName(string guid, string name)
    {
        string trimmedGuid = guid.Trim();
        string trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            return trimmedGuid;
        }

        // Inventory stems already include the GUID prefix — keep unchanged.
        if (trimmedGuid.Length > 0
            && trimmedName.StartsWith(trimmedGuid, StringComparison.OrdinalIgnoreCase))
        {
            return trimmedName;
        }

        return string.Format(ConsoleMessages.Get("outputCacheRepairAssetLabel"), trimmedGuid, trimmedName);
    }

    private static string? TryStemLabelFromRelativePath(string? relativePath, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string normalized = relativePath.Replace('\\', '/').TrimEnd('/');
        string leaf = isDirectory
            ? Path.GetFileName(normalized)
            : Path.GetFileNameWithoutExtension(normalized);
        return string.IsNullOrWhiteSpace(leaf) ? null : leaf.Trim();
    }
}
