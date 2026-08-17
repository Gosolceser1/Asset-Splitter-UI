namespace AssetProcessor;

/// <summary>
/// A named region entry in regional_ingredients.json (e.g. "Africa" or "Default")
/// that holds the ordered list of ingredient GUIDs applied to that region's building costs.
/// </summary>
public record RegionConfig
{
    /// <summary>Human-readable description of this region (documentation only).</summary>
    public string Description { get; init; } = "";

    /// <summary>Ordered list of ingredient GUIDs for this region.</summary>
    public List<IngredientConfig> Ingredients { get; init; } = [];
}
