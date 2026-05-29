using System.Collections.Concurrent;

namespace AssetProcessor;

/// <summary>Holds all pipeline configuration and shared state as instance properties, replacing static Program fields.</summary>
public sealed class PipelineContext
{
    public string AssetRoot { get; set; } = "";
    public string BaseOutputDir { get; set; } = "";
    public string AssetLanguage { get; set; } = "";
    public string CustomFixlistFile { get; set; } = "";
    public string AssetOut { get; set; } = "";
    public string SourceXmlFolder { get; set; } = "";
    public string GameOutputRoot { get; set; } = "";
    public string AnnoAssetsRoot { get; set; } = "";
    public string SingleAssetOutputRoot { get; set; } = "";
    public string SingleAssetDisplayName { get; set; } = "";
    public string SingleAssetModOutputRoot { get; set; } = "";
    public string AssetModOutputRoot { get; set; } = "";
    public bool AssetComments { get; set; }
    public bool AssetFix { get; set; }
    public bool AssetTemplates { get; set; }
    public bool AssetModOpsWrap { get; set; }
    public bool AssetNoDefaultProperties { get; set; }
    public bool AssetSplitTemplates { get; set; }
    public bool CreateAssetMods { get; set; }
    public bool DebugMode { get; set; }
    public string SingleAssetGuid { get; set; } = "";
    public bool IsSingleAssetMode => !string.IsNullOrWhiteSpace(SingleAssetGuid);

    /// <summary>Developer mode (-d) logs full per-operation trace including merge xpath detail.</summary>
    public bool ShouldLogMergeXpathDetail(MergeTraceKind kind) => DebugMode;
    public RegionalIngredientsConfig? RegionalIngredientsConfig { get; set; }
    public AppSettingsConfig? AppSettingsConfig { get; set; }
    public string[]? AssetTemplatesList { get; set; }
    public string[] TemplatesUsed { get; set; } = [];
    public HashSet<string> LoadedPropertyNames { get; set; } = [];
    public PropertyScanResult? PropertyScan { get; set; }
    public GuidFileIndex? GuidIndex { get; set; }
    public ConcurrentDictionary<string, string> Translator { get; } = new();
    public ConcurrentDictionary<string, string> AssetNames { get; } = new();
    public PipelineLogger Log { get; }
    public AssetProcessorProgressReporter ProgressReporter { get; } = new();
    public PipelineIssueTracker Issues { get; } = new();
    public PipelineDebugStats DebugStats { get; } = new();

    public PipelineContext()
    {
        Log = new PipelineLogger(this);
    }
}
