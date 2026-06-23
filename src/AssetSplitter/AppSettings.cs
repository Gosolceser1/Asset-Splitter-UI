namespace AssetProcessor;

/// <summary>
/// The <c>settings</c> section of <c>app_settings.json</c>.
/// Bundles default paths, file-processing knobs, output folder names,
/// supported language codes, and per-game required-file lists.
/// </summary>
public record AppSettings
{
    /// <summary>Named default folder paths (e.g. "game_path", "output_path").</summary>
    public Dictionary<string, string> DefaultPaths { get; init; } = [];

    /// <summary>File-processing tuning: extension filter, progress interval, buffer limit.</summary>
    public FileProcessingSettings FileProcessing { get; init; } = new();

    /// <summary>Output folder names: source_xml, modops, BaseAssetGUID.</summary>
    public OutputStructureSettings OutputStructure { get; init; } = new();

    /// <summary>Language codes whose GUID comment annotations are supported (e.g. "english", "german").</summary>
    public List<string> SupportedLanguages { get; init; } = [];

    /// <summary>Per-game or per-phase list of file paths that must exist before processing starts.</summary>
    public Dictionary<string, List<string>> RequiredFiles { get; init; } = [];
}
