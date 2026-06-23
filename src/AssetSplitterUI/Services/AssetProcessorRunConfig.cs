namespace AssetSplitterUI.Services;

/// <summary>Options passed from the UI when launching <c>AssetProcessor.exe</c>.</summary>
public sealed class AssetProcessorRunConfig
{
    public string GamePath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string Language { get; set; } = "";
    public string ConsoleLanguage { get; set; } = "";
    public string ReadmeLanguage { get; set; } = "";
    public string SingleGuid { get; set; } = "";
    public bool AddComments { get; set; }
    public bool FixDependencies { get; set; }
    public bool CreateTemplateFolders { get; set; }
    public bool ModOpsWrap { get; set; }
    public bool IncludeDefaultProperties { get; set; }
    public bool SplitTemplates { get; set; }
    public bool CreateAssetMods { get; set; }
    public bool DebugMode { get; set; }

    /// <summary>Stop after Phase 1 (RDA → source_xml); no per-asset XML output.</summary>
    public bool SourceExtractionOnly { get; set; }
}
