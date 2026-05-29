using System.Threading;
using System.Xml;

namespace AssetProcessor;

internal sealed class DependencyResolutionCache(
    GuidFileIndex guidIndex,
    PipelineIssueTracker issues,
    Action<string, string> writeMessage,
    bool debugMode,
    PipelineDebugStats? debugStats)
{
    private readonly Lock _parentAssetCacheLock = new();
    private Dictionary<string, XmlDocument>? _parentAssetCache;

    public void Initialize()
    {
        lock (_parentAssetCacheLock)
        {
            _parentAssetCache ??= new(StringComparer.OrdinalIgnoreCase);
        }

        WriteDebug(string.Format(ConsoleMessages.Get("debugDependencyCacheInitialized"), guidIndex.Count), "FIX");
    }

    public XmlDocument? GetOrLoadParentAsset(string parentGuid, string? childDisplayName = null, string? childFilePath = null)
    {
        if (string.IsNullOrEmpty(parentGuid))
            return null;

            lock (_parentAssetCacheLock)
            {
                if (_parentAssetCache != null && _parentAssetCache.TryGetValue(parentGuid, out XmlDocument? cached))
                {
                    debugStats?.RecordParentCacheHit();
                    WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetLoadedFromCache"), parentGuid), "CACHE");
                    return cached;
                }
            }

        string? filePath = guidIndex.Find(parentGuid);
        if (string.IsNullOrEmpty(filePath))
        {
            WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetNotFound"), parentGuid), "WARNING");
            issues.ReportParentNotInGuidIndex(parentGuid, childDisplayName, childFilePath);
            return null;
        }

        try
        {
            XmlDocument parentDoc = new();
            parentDoc.Load(filePath);

            lock (_parentAssetCacheLock)
            {
                _parentAssetCache?.TryAdd(parentGuid, parentDoc);
            }

            debugStats?.RecordParentCacheDiskLoad();
            WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetLoadedFromDisk"), parentGuid, filePath), "CACHE");
            return parentDoc;
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetLoadFailed"), parentGuid, ex.Message), "ERROR");
            issues.ReportParentLoadFailed(parentGuid, ex.Message, childDisplayName, childFilePath);
            return null;
        }
    }

    public void Clear()
    {
        lock (_parentAssetCacheLock)
        {
            if (_parentAssetCache != null)
            {
                foreach (var doc in _parentAssetCache.Values)
                    doc.RemoveAll();
                _parentAssetCache.Clear();
                _parentAssetCache = null;
            }
        }

    }

    public void WriteSummary(PipelineLogger log)
    {
        debugStats?.WriteParentCacheSummary(log);
        WriteDebug(ConsoleMessages.Get("debugDependencyCacheCleared"), "COMPLETE");
    }

    private void WriteDebug(string message, string messageType)
    {
        if (debugMode)
            writeMessage(message, messageType);
    }
}
