namespace AssetProcessor;

/// <summary>
/// Root type for <c>config/app_settings.json</c>.
/// Holds the top-level description, schema version, and the <see cref="AppSettings"/> payload.
/// </summary>
public record AppSettingsConfig
{
    /// <summary>Short description of this config file (documentation only).</summary>
    public string Description { get; init; } = "";

    /// <summary>Config schema or format version.</summary>
    public string Version { get; init; } = "";

    /// <summary>Application settings: paths, file processing, output structure, language codes.</summary>
    public AppSettings Settings { get; init; } = new();
}
