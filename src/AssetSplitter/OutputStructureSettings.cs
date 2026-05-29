namespace AssetProcessor;

/// <summary>
/// Output folder names loaded from <c>app_settings.json</c>.
/// These names are appended to the game-specific output root (e.g. <c>AnnoAssets/Anno1800/</c>).
/// </summary>
public record OutputStructureSettings
{
    /// <summary>Sub-folder for extracted raw XML source files (assets.xml, templates.xml, etc.).</summary>
    public string SourceXmlFolder { get; init; } = "source_xml";

    /// <summary>Sub-folder for the final ModOp-formatted per-asset XML output.</summary>
    public string ModopsFolder { get; init; } = "modops";

    /// <summary>Sub-folder used as a staging area during BaseAssetGUID dependency resolution.</summary>
    public string BaseassetFolder { get; init; } = "BaseAssetGUID";
}
