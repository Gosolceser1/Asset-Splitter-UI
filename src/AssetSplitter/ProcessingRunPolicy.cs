namespace AssetProcessor;

/// <summary>
/// Normalized processing flags used to decide Phase 1-only vs Phase 3+.
/// ModOps wrap and debug mode are output/logging only — they never skip Phase 1-only by themselves.
/// </summary>
public readonly record struct ProcessingFlags(
    string? SingleAssetGuid,
    bool AddComments,
    bool FixDependencies,
    bool CreateTemplateFolders,
    bool SplitTemplates,
    bool CreateAssetMods,
    bool NoDefaultProperties);

/// <summary>Shared rules for when the pipeline should stop after Phase 1 (source XML only).</summary>
public static class ProcessingRunPolicy
{
    public const string Phase1OnlyMarker = "[PHASE1_ONLY]";

    /// <summary>Only mod packages requested — no XML enrichment, dependency fix, or split templates.</summary>
    public static bool IsModExportOnly(ProcessingFlags flags) =>
        flags.CreateAssetMods
        && !flags.AddComments
        && !flags.FixDependencies
        && !flags.CreateTemplateFolders
        && !flags.SplitTemplates
        && flags.NoDefaultProperties;

    /// <summary>
    /// Phase 1 only when every processing flag is off and no single-GUID filter is set.
    /// </summary>
    public static bool IsSourceExtractionOnly(ProcessingFlags flags) =>
        string.IsNullOrWhiteSpace(flags.SingleAssetGuid)
        && !flags.AddComments
        && !flags.FixDependencies
        && !flags.CreateTemplateFolders
        && !flags.SplitTemplates
        && !flags.CreateAssetMods
        && flags.NoDefaultProperties;

    /// <summary>Matches <see cref="PipelineOrchestrator"/> CreateContext normalization.</summary>
    internal static ProcessingFlags NormalizeFromOptions(AssetProcessorCommandLineOptions options)
    {
        bool singleAssetMode = !string.IsNullOrWhiteSpace(options.SingleAssetGuid);
        return new ProcessingFlags(
            singleAssetMode ? options.SingleAssetGuid : null,
            options.AddComments,
            options.FixDependencies,
            singleAssetMode ? false : options.CreateTemplateFolders,
            !singleAssetMode && options.SplitTemplates,
            options.CreateAssetMods,
            options.NoDefaultProperties);
    }

    public static ProcessingFlags FromContext(PipelineContext context) =>
        new(
            string.IsNullOrWhiteSpace(context.SingleAssetGuid) ? null : context.SingleAssetGuid,
            context.AddComments,
            context.FixDependencies,
            context.CreateTemplateFolders,
            context.SplitTemplates,
            context.CreateAssetMods,
            context.NoDefaultProperties);

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
