namespace AssetProcessor;

/// <summary>
/// File-processing tuning knobs loaded from <c>app_settings.json</c>.
/// Controls which files are scanned, how often progress is reported, and console buffer limits.
/// </summary>
public record FileProcessingSettings
{
    /// <summary>Glob pattern for XML files to process (e.g. <c>"*.xml"</c>).</summary>
    public string XmlExtension { get; init; } = "*.xml";

    /// <summary>Emit a progress line every N files processed.</summary>
    public int ProgressInterval { get; init; } = 100;

    /// <summary>Maximum lines retained in the in-process console output buffer.</summary>
    public int MaxConsoleOutputLines { get; init; } = 2000;
}
