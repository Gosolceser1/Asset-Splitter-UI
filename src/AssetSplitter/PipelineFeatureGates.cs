namespace AssetProcessor;

/// <summary>
/// Central rules for which pipeline phases run.
/// Phase 1-only: every processing flag off (<see cref="ProcessingRunPolicy"/>).
/// Split-only: Phase 3 without Phases 4–8 when flags are off but default properties is on.
/// </summary>
internal static class PipelineFeatureGates
{
    /// <summary>Phase 5: fixlist template merge + properties.xml defaults (GUI: Include default properties).</summary>
    public static bool NeedsTemplateMerge(PipelineContext context) =>
        !context.AssetNoDefaultProperties;

    /// <summary>Phase 2 property scan + Phase 7 translated comments (-c).</summary>
    public static bool NeedsPropertyScan(PipelineContext context) =>
        context.AssetComments;

    /// <summary>Phase 4 GUID index (-f parent lookup; -c InheritedIndex comments).</summary>
    public static bool NeedsGuidIndex(PipelineContext context) =>
        context.AssetFix || context.AssetComments;

    /// <summary>Phase 7 XML enrichment (comments, template folders, default-property merge cleanup).</summary>
    public static bool NeedsXmlEnrichment(PipelineContext context) =>
        context.AssetComments
        || context.AssetTemplates
        || NeedsTemplateMerge(context);

    /// <summary>Phase 7/8 — formatting and/or mod package export.</summary>
    public static bool NeedsFormatting(PipelineContext context) =>
        NeedsXmlEnrichment(context) || context.CreateAssetMods;

    /// <summary>Only --create-asset-mods: build mod folders from extracted XML, then drop intermediate output_xml.</summary>
    public static bool IsModExportOnlyRun(PipelineContext context) =>
        context.CreateAssetMods
        && !NeedsXmlEnrichment(context)
        && !context.AssetFix
        && !context.AssetSplitTemplates;

    /// <summary>Phase 7 vector cleanup and Anno 1800 regional ingredients (part of enrichment, not split-only).</summary>
    public static bool NeedsEnrichmentCleanup(PipelineContext context) =>
        NeedsTemplateMerge(context);

    /// <summary>Phase 3 complete — no Phase 4–8 work scheduled.</summary>
    public static bool IsSplitOnlyRun(PipelineContext context) =>
        !NeedsGuidIndex(context)
        && !NeedsTemplateMerge(context)
        && !context.AssetFix
        && !NeedsFormatting(context);

    /// <summary>Phase 1 only — no per-asset XML output; all processing flags off on normalized context.</summary>
    public static bool IsSourceExtractionOnlyRun(PipelineContext context) =>
        ProcessingRunPolicy.IsSourceExtractionOnly(ProcessingRunPolicy.FromContext(context));

    /// <summary>Phase 1 always pulls the full core source bundle from RDA (independent of later phase flags).</summary>
    public static string GetRdaExtractFilter(string gameType) =>
        gameType.Contains("117", StringComparison.OrdinalIgnoreCase) ? Anno117RdaPaths : Anno1800RdaPaths;

    private const string Anno117RdaPaths =
        "data/base/config/export/properties.xml;data/base/config/export/properties-meta.xml;data/base/config/export/templates.xml;data/base/config/export/assets.xml;data/base/config/game/datasets.xml;data/base/config/export/audio_generated.xml;data/base/config/gui/texts_";

    private const string Anno1800RdaPaths =
        "data/config/export/main/asset/properties.xml;data/config/export/main/asset/properties-toolone.xml;data/config/export/main/asset/templates.xml;data/config/export/main/asset/assets.xml;data/config/export/main/asset/datasets.xml;data/config/gui/texts_";
}
