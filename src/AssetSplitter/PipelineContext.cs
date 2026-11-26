using System.Collections.Concurrent;

namespace AssetProcessor;

/// <summary>Holds all pipeline configuration and shared state as instance properties, replacing static Program fields.</summary>
public sealed class PipelineContext
{
    public string AssetRoot { get; set; } = "";
    public string BaseOutputDir { get; set; } = "";
    public string AssetLanguage { get; set; } = "";
    public string ReadmeLanguage { get; set; } = "english";
    public string CustomFixlistFile { get; set; } = "";
    public string AssetOutputDir { get; set; } = "";
    public string SourceXmlFolder { get; set; } = "";
    public string GameOutputRoot { get; set; } = "";
    public string AnnoAssetsRoot { get; set; } = "";
    public string SingleAssetOutputRoot { get; set; } = "";
    public string SingleAssetDisplayName { get; set; } = "";
    public string SingleAssetModOutputRoot { get; set; } = "";
    public string AssetModOutputRoot { get; set; } = "";
    public bool AddComments { get; set; }
    public bool FixDependencies { get; set; }
    public bool CreateTemplateFolders { get; set; }
    public bool ModOpsWrap { get; set; }
    public bool NoDefaultProperties { get; set; }
    public bool SplitTemplates { get; set; }
    public bool CreateAssetMods { get; set; }
    public bool DebugMode { get; set; }
    public bool ForceFullRebuild { get; set; }
    public string SingleAssetGuid { get; set; } = "";
    public bool IsSingleAssetMode => !string.IsNullOrWhiteSpace(SingleAssetGuid);

    /// <summary>When true, prepare steps must not wipe existing output trees (repair mode).</summary>
    public bool PreserveOutputDirectories { get; set; }

    /// <summary>
    /// When true with <see cref="PreserveOutputDirectories"/>, Phase 2B still clears and re-splits
    /// <c>output_templates_*</c> (templates tree content-changed).
    /// </summary>
    public bool ForceResplitTemplates { get; set; }

    /// <summary>When set, Phase 3 extracts only these GUIDs (repair gaps).</summary>
    public HashSet<string>? RepairOnlyGuids { get; set; }

    /// <summary>GUIDs whose mod packages must be recreated during repair (xml and/or mod gaps).</summary>
    public HashSet<string>? RepairPackageGuids { get; set; }

    /// <summary>
    /// Per-GUID <see cref="ConsoleMessages"/> item key explaining why repair is needed
    /// (e.g. <c>outputCacheRepairItemContentChanged</c>). Used to log action→reason lines.
    /// </summary>
    public Dictionary<string, string>? RepairGuidMessageKeys { get; set; }

    /// <summary>
    /// Dirty on-disk XML/mod text for content-changed presence repair rows, captured before
    /// delete/rebuild so post-write logging can show line-oriented old vs new diffs.
    /// </summary>
    internal OutputRunCache.ContentChangedRepairSnapshots? ContentChangedSnapshots { get; set; }

    /// <summary>True when this run short-circuited because output cache was already complete.</summary>
    public bool OutputCacheCompleteHit { get; set; }

    /// <summary>Absolute paths written or needing enrich during the current repair pass.</summary>
    public List<string> RepairOutputFiles { get; } = [];

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    /// <summary>Set during init; used by Phase 2 config bootstrap.</summary>
    public string DetectedGameType { get; set; } = "";
    public GameBuildInfo? DetectedGameBuild { get; set; }

    public bool AutoTemplates { get; set; }

    public string CustomTemplateFile { get; set; } = "";

    /// <summary>Developer mode (-d) logs full per-operation trace including merge xpath detail.</summary>
    public bool ShouldLogMergeXpathDetail(MergeTraceKind kind) => DebugMode;
    public RegionalIngredientsConfig? RegionalIngredientsConfig { get; set; }
    public AppSettingsConfig? AppSettingsConfig { get; set; }
    public string[]? AssetTemplatesList { get; set; }

    /// <summary>True once <see cref="Phase2ConfigLoading.LoadProcessingPrerequisites"/> ran for this run.</summary>
    public bool ProcessingPrerequisitesLoaded { get; set; }
    public string[] TemplatesUsed { get; set; } = [];
    public HashSet<string> LoadedPropertyNames { get; set; } = [];
    public PropertyScanResult? PropertyScan { get; set; }
    public GuidFileIndex? GuidIndex { get; set; }
    public ConcurrentDictionary<string, string> Translator { get; } = new();
    public ConcurrentDictionary<string, string> MetadataNames { get; } = new();
    public ConcurrentDictionary<string, string> AssetNames { get; } = new();
    public LineIdContextIndex? LineIdContext { get; set; }
    public PipelineLogger Log { get; }
    public AssetProcessorProgressReporter ProgressReporter { get; } = new();
    public PipelineIssueTracker Issues { get; } = new();
    public PipelineDebugStats DebugStats { get; } = new();

    public PipelineContext()
    {
        Log = new PipelineLogger(this);
    }
}
