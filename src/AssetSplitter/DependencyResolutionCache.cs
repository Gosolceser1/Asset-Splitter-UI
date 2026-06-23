using System.Threading;
using System.Xml;

namespace AssetProcessor;

internal sealed class DependencyResolutionCache(
    GuidFileIndex guidIndex,
    PipelineContext context,
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
        {
            return null;
        }

        lock (_parentAssetCacheLock)
        {
            if (_parentAssetCache != null && _parentAssetCache.TryGetValue(parentGuid, out XmlDocument? cached))
            {
                debugStats?.RecordParentCacheHit();
                WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetLoadedFromCache"), parentGuid), "CACHE");
                return cached;
            }
        }

        string? filePath = ResolveExistingPath(parentGuid);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetLoadFailed"), parentGuid, ex.Message), "ERROR");
            issues.ReportParentLoadFailed(parentGuid, ex.Message, childDisplayName, childFilePath);
            return null;
        }
    }

    /// <summary>
    /// GUID index is built before Phase 6; merged parents leave staging but the index may still point there.
    /// Fall back to a live disk search under <see cref="PipelineContext.AssetOut"/>.
    /// </summary>
    private string? ResolveExistingPath(string guid)
    {
        string? indexedPath = guidIndex.Find(guid);
        if (!string.IsNullOrEmpty(indexedPath) && File.Exists(indexedPath))
        {
            return indexedPath;
        }

        string? resolved;
        try
        {
            resolved = AssetProcessorFileSystem.FindAssetFile(context.AssetOut, guid, searchTemplateFolders: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetLoadFailed"), guid, ex.Message), "ERROR");
            return indexedPath;
        }

        if (resolved != null)
        {
            guidIndex.UpdatePath(guid, resolved);
            WriteDebug(string.Format(ConsoleMessages.Get("debugParentAssetRelocated"), guid, resolved), "CACHE");
            return resolved;
        }

        return indexedPath;
    }

    public void Clear()
    {
        lock (_parentAssetCacheLock)
        {
            if (_parentAssetCache != null)
            {
                foreach (var doc in _parentAssetCache.Values)
                {
                    doc.RemoveAll();
                }

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
        {
            writeMessage(message, messageType);
        }
    }
}
