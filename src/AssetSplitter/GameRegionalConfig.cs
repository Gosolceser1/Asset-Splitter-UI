namespace AssetProcessor;

/// <summary>
/// Per-game regional configuration inside regional_ingredients.json (e.g. the "anno1800" entry).
/// Holds separate ingredient maps for the Africa DLC region and the default European region.
/// </summary>
public record GameRegionalConfig
{
    /// <summary>
    /// When <see langword="false"/> the entire entry is skipped during ingredient resolution.
    /// <see langword="null"/> is treated as enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>Human-readable description of this game's regional setup (documentation only).</summary>
    public string Description { get; init; } = "";

    /// <summary>Africa DLC ingredient map: region-key → ingredient list.</summary>
    public Dictionary<string, RegionConfig> Africa { get; init; } = [];

    /// <summary>Default (European) ingredient map: region-key → ingredient list.</summary>
    public Dictionary<string, RegionConfig> Default { get; init; } = [];

    /// <summary>Optional universal ingredient map applied regardless of region.</summary>
    public Dictionary<string, RegionConfig>? Universal { get; init; }
}
