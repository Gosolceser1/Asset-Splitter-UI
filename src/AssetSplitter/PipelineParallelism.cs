namespace AssetProcessor;

/// <summary>Chooses parallel degree for XML file I/O; lowers it on cloud-synced output paths.</summary>
internal static class PipelineParallelism
{
    public static int ForXmlFileIo(PipelineContext context)
    {
        int cpuBound = Math.Max(1, Environment.ProcessorCount - 1);
        bool cloudSynced =
            CloudSyncedPathDetector.IsLikelyCloudSynced(context.AssetOutputDir)
            || CloudSyncedPathDetector.IsLikelyCloudSynced(context.BaseOutputDir)
            || CloudSyncedPathDetector.IsLikelyCloudSynced(context.AnnoAssetsRoot);

        // OneDrive/Dropbox lock files during sync; many writers amplify retry sleeps in load/save.
        return cloudSynced ? Math.Min(cpuBound, 2) : cpuBound;
    }
}
