namespace AssetProcessor;

/// <summary>Stable codes for pipeline issues (used in JSON reports and grouping).</summary>
public static class PipelineIssueCodes
{
    public const string ParentAssetNotInGuidIndex = "ParentAssetNotInGuidIndex";
    public const string ParentAssetLoadFailed = "ParentAssetLoadFailed";
    public const string ExtractAssetFailed = "ExtractAssetFailed";
    public const string MergeAssetFailed = "MergeAssetFailed";
    public const string FormatFileFailed = "FormatFileFailed";
    public const string UnexpectedFileProcessingError = "UnexpectedFileProcessingError";
    public const string MoveToTemplateFolderFailed = "MoveToTemplateFolderFailed";
    public const string ModPackageSkippedInvalidXml = "ModPackageSkippedInvalidXml";
    public const string ModPackageReadXmlFailed = "ModPackageReadXmlFailed";
    public const string RdaExtractionFailed = "RdaExtractionFailed";
    public const string PipelineFatalError = "PipelineFatalError";
}
