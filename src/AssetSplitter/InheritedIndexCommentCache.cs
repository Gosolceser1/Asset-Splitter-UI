using System.Collections.Concurrent;
using System.Xml;

namespace AssetProcessor;

/// <summary>
/// Loads each InheritedIndex parent asset XML once per formatting pass and serializes
/// reads on that document (XmlDocument is not safe for concurrent readers).
/// </summary>
internal sealed class InheritedIndexCommentCache(PipelineContext context)
{
    private readonly ConcurrentDictionary<string, Lazy<XmlDocument?>> _parentsByGuid =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, object> _locksByGuid =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryResolveInheritedProperty(
        string baseAssetGuid,
        string itemCollectionName,
        int inheritedIndex,
        out string propertyName,
        out string propertyGuid)
    {
        propertyName = "";
        propertyGuid = "";

        if (string.IsNullOrEmpty(baseAssetGuid))
        {
            return false;
        }

        XmlDocument? parentDoc;
        if (_parentsByGuid.TryGetValue(baseAssetGuid, out Lazy<XmlDocument?>? existing))
        {
            context.DebugStats.RecordParentCacheHit();
            parentDoc = existing.Value;
        }
        else
        {
            parentDoc = _parentsByGuid.GetOrAdd(baseAssetGuid, LoadParentLazy).Value;
        }

        if (parentDoc is null)
        {
            return false;
        }

        object docLock = _locksByGuid.GetOrAdd(baseAssetGuid, static _ => new object());
        lock (docLock)
        {
            XmlNodeList? parentItems = parentDoc.SelectNodes($"//Asset/Values//{itemCollectionName}/Item");
            if (parentItems is null || inheritedIndex < 0 || inheritedIndex >= parentItems.Count)
            {
                return false;
            }

            if (parentItems[inheritedIndex] is not XmlNode parentItem)
            {
                return false;
            }

            foreach (XmlNode child in parentItem.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element
                    || string.IsNullOrWhiteSpace(child.InnerText)
                    || child.InnerXml.Contains('<')
                    || child.InnerText.Trim().Length == 0)
                {
                    continue;
                }

                propertyName = child.Name;
                propertyGuid = child.InnerText.Trim();
                return true;
            }
        }

        return false;
    }

    private Lazy<XmlDocument?> LoadParentLazy(string baseAssetGuid) =>
        new(() => LoadParentDocument(baseAssetGuid), LazyThreadSafetyMode.ExecutionAndPublication);

    private XmlDocument? LoadParentDocument(string baseAssetGuid)
    {
        string? parentAssetFile = context.GuidIndex?.Find(baseAssetGuid);
        if (string.IsNullOrEmpty(parentAssetFile))
        {
            return null;
        }

        try
        {
            XmlDocument parentDoc = new();
            XmlDocumentLoadRetry.Load(parentDoc, parentAssetFile);
            context.DebugStats.RecordParentCacheDiskLoad();
            return parentDoc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugInheritedIndexProcessingError"), ex.Message));
            return null;
        }
    }
}
