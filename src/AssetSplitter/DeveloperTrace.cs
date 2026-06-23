namespace AssetProcessor;

/// <summary>Developer mode (-d): emit every progress step and per-item trace line.</summary>
internal static class DeveloperTrace
{
    public static bool IsVerbose(PipelineContext context) => context.DebugMode;

    public static bool ShouldReportProgress(PipelineContext context, int current, int total, int normalInterval = 100) =>
        context.DebugMode || current % normalInterval == 0 || total <= 5 || current == total;

    public static bool ShouldEmitProgressLine(PipelineContext context, int current, int total) =>
        context.DebugMode || current <= 1 || current >= total || ShouldUseInterval(context, current, total);

    private static bool ShouldUseInterval(PipelineContext context, int current, int total)
    {
        int defaultInterval = total >= 5_000 ? 500 : 100;
        int interval = context.AppSettingsConfig?.Settings?.FileProcessing?.ProgressInterval ?? defaultInterval;
        return interval > 0 && current % interval == 0;
    }
}
