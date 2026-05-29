namespace AssetProcessor;

/// <summary>
/// Root type for <c>config/03_Regional_Ingredients/regional_ingredients.json</c>.
/// Maps game IDs (e.g. "anno1800") to their regional ingredient configuration,
/// which drives Africa vs. Default building-cost GUID injection.
/// </summary>
public record RegionalIngredientsConfig
{
    /// <summary>Short description of this config file (documentation only).</summary>
    public string Description { get; init; } = "";

    /// <summary>Config schema or format version.</summary>
    public string Version { get; init; } = "";

    /// <summary>Game-ID → regional ingredient configuration.</summary>
    public Dictionary<string, GameRegionalConfig> Games { get; init; } = [];
}
