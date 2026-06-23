namespace AssetProcessor;

/// <summary>
/// Normalized processing flags used to decide Phase 1-only vs Phase 3+.
/// ModOps wrap and debug mode are output/logging only — they never skip Phase 1-only by themselves.
/// </summary>
public readonly record struct ProcessingFlags(
    string? SingleAssetGuid,
    bool AssetComments,
    bool AssetFix,
    bool AssetTemplates,
    bool AssetSplitTemplates,
    bool CreateAssetMods,
    bool AssetNoDefaultProperties);

/// <summary>Shared rules for when the pipeline should stop after Phase 1 (source XML only).</summary>
public static class ProcessingRunPolicy
{
    public const string Phase1OnlyMarker = "[PHASE1_ONLY]";

    /// <summary>Only mod packages requested — no XML enrichment, dependency fix, or split templates.</summary>
    public static bool IsModExportOnly(ProcessingFlags flags) =>
        flags.CreateAssetMods
        && !flags.AssetComments
        && !flags.AssetFix
        && !flags.AssetTemplates
        && !flags.AssetSplitTemplates
        && flags.AssetNoDefaultProperties;

    /// <summary>
    /// Phase 1 only when every processing flag is off and no single-GUID filter is set.
    /// </summary>
    public static bool IsSourceExtractionOnly(ProcessingFlags flags) =>
        string.IsNullOrWhiteSpace(flags.SingleAssetGuid)
        && !flags.AssetComments
        && !flags.AssetFix
        && !flags.AssetTemplates
        && !flags.AssetSplitTemplates
        && !flags.CreateAssetMods
        && flags.AssetNoDefaultProperties;

    /// <summary>Matches <see cref="PipelineOrchestrator"/> CreateContext normalization.</summary>
    internal static ProcessingFlags NormalizeFromOptions(AssetProcessorCommandLineOptions options)
    {
        bool singleAssetMode = !string.IsNullOrWhiteSpace(options.SingleAssetGuid);
        return new ProcessingFlags(
            singleAssetMode ? options.SingleAssetGuid : null,
            options.AssetComments,
            options.AssetFix,
            singleAssetMode ? false : options.AssetTemplates,
            !singleAssetMode && options.AssetSplitTemplates,
            options.CreateAssetMods,
            options.AssetNoDefaultProperties);
    }

    public static ProcessingFlags FromContext(PipelineContext context) =>
        new(
            string.IsNullOrWhiteSpace(context.SingleAssetGuid) ? null : context.SingleAssetGuid,
            context.AssetComments,
            context.AssetFix,
            context.AssetTemplates,
            context.AssetSplitTemplates,
            context.CreateAssetMods,
            context.AssetNoDefaultProperties);

    /// <summary>Same rules as GUI BuildRunConfig / CreateContext.</summary>
    public static ProcessingFlags NormalizeFromGui(
        string singleGuid,
        bool addComments,
        bool fixDependencies,
        bool createTemplateFolders,
        bool splitTemplates,
        bool createAssetMods,
        bool includeDefaultProperties)
    {
        bool singleAssetMode = !string.IsNullOrWhiteSpace(singleGuid);
        return new ProcessingFlags(
            singleAssetMode ? singleGuid.Trim() : null,
            addComments,
            fixDependencies,
            singleAssetMode ? false : createTemplateFolders,
            !singleAssetMode && splitTemplates,
            createAssetMods,
            !includeDefaultProperties);
    }
}
