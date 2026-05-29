namespace AssetProcessor;

/// <summary>Single ingredient entry in regional config: a GUID with optional display name and description.</summary>
public record IngredientConfig
{
    /// <summary>Asset GUID of the ingredient (e.g. "1010017").</summary>
    public string Guid { get; init; } = "";

    /// <summary>Human-readable ingredient name — used for documentation only, not processing.</summary>
    public string Name { get; init; } = "";

    /// <summary>Optional notes describing the ingredient's in-game role.</summary>
    public string Description { get; init; } = "";
}
