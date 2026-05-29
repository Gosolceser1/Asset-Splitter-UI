namespace AssetProcessor;

/// <summary>Phase-end counter summaries (per-item lines are logged separately in developer mode).</summary>
public sealed class PipelineDebugStats
{
    private long _templateMergeApplied;
    private long _templateMergeSkipped;
    private long _parentCacheHits;
    private long _parentCacheDiskLoads;
    private long _formatCommentFiles;
    private long _formatCommentNodes;
    private long _formatTemplateMoves;
    private long _formatMissingTemplateNode;
    private long _modPackagesCreated;
    private long _modPackagesSkipped;

    public void RecordTemplateMergeApplied() => Interlocked.Increment(ref _templateMergeApplied);
    public void RecordTemplateMergeSkipped() => Interlocked.Increment(ref _templateMergeSkipped);
    public void RecordParentCacheHit() => Interlocked.Increment(ref _parentCacheHits);
    public void RecordParentCacheDiskLoad() => Interlocked.Increment(ref _parentCacheDiskLoads);
    public void RecordFormatComments(int commentCount)
    {
        Interlocked.Increment(ref _formatCommentFiles);
        Interlocked.Add(ref _formatCommentNodes, commentCount);
    }

    public void RecordFormatTemplateMove() => Interlocked.Increment(ref _formatTemplateMoves);
    public void RecordFormatMissingTemplateNode() => Interlocked.Increment(ref _formatMissingTemplateNode);
    public void RecordModPackageCreated() => Interlocked.Increment(ref _modPackagesCreated);
    public void RecordModPackageSkipped() => Interlocked.Increment(ref _modPackagesSkipped);

    public void WriteTemplateMergeSummary(PipelineLogger log)
    {
        long applied = Interlocked.Read(ref _templateMergeApplied);
        long skipped = Interlocked.Read(ref _templateMergeSkipped);
        if (applied == 0 && skipped == 0)
            return;

        log.Debug(string.Format(ConsoleMessages.Get("debugTemplateMergeStats"), applied.ToString("N0"), skipped.ToString("N0")));
    }

    public void WriteParentCacheSummary(PipelineLogger log)
    {
        long hits = Interlocked.Read(ref _parentCacheHits);
        long disk = Interlocked.Read(ref _parentCacheDiskLoads);
        if (hits == 0 && disk == 0)
            return;

        log.Debug(string.Format(ConsoleMessages.Get("debugParentCacheStats"), disk.ToString("N0"), hits.ToString("N0")));
    }

    public void WriteFormattingSummary(PipelineLogger log)
    {
        long commentFiles = Interlocked.Read(ref _formatCommentFiles);
        long commentNodes = Interlocked.Read(ref _formatCommentNodes);
        long moves = Interlocked.Read(ref _formatTemplateMoves);
        long missingTemplate = Interlocked.Read(ref _formatMissingTemplateNode);
        if (commentFiles == 0 && moves == 0 && missingTemplate == 0)
            return;

        log.Debug(string.Format(
            ConsoleMessages.Get("debugFormattingFileStats"),
            commentFiles.ToString("N0"),
            commentNodes.ToString("N0"),
            moves.ToString("N0"),
            missingTemplate.ToString("N0")));
    }

    public void WriteModPackageSummary(PipelineLogger log)
    {
        long created = Interlocked.Read(ref _modPackagesCreated);
        long skipped = Interlocked.Read(ref _modPackagesSkipped);
        if (created == 0 && skipped == 0)
            return;

        log.Debug(string.Format(ConsoleMessages.Get("debugModPackageStats"), created.ToString("N0"), skipped.ToString("N0")));
    }
}
