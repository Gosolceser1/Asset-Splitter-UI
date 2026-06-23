using AssetProcessor;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    private bool _lastRunWasSourceExtractionOnly;
    private bool _lastRunWasModExportOnly;

    private static ProcessingFlags BuildProcessingFlags(AssetProcessorRunConfig config) =>
        ProcessingRunPolicy.NormalizeFromGui(
            config.SingleGuid,
            config.AddComments,
            config.FixDependencies,
            config.CreateTemplateFolders,
            config.SplitTemplates,
            config.CreateAssetMods,
            config.IncludeDefaultProperties);

    /// <summary>First run (no source_xml yet) always sends Phase-1-only flags regardless of UI toggle state.</summary>
    private AssetProcessorRunConfig BuildEffectiveRunConfig(string language)
    {
        if (!SourceFilesExist)
        {
            return CreateSourceExtractionRunConfig();
        }

        var config = BuildRunConfig(language);
        if (ProcessingRunPolicy.IsSourceExtractionOnly(BuildProcessingFlags(config)))
        {
            config.SourceExtractionOnly = true;
        }

        return config;
    }

    private AssetProcessorRunConfig CreateSourceExtractionRunConfig() =>
        new()
        {
            GamePath = GamePath,
            OutputPath = OutputPath,
            Language = "none",
            ConsoleLanguage = "english",
            ReadmeLanguage = GetBackendConsoleLanguage(),
            SingleGuid = "",
            AddComments = false,
            FixDependencies = false,
            CreateTemplateFolders = false,
            ModOpsWrap = ModOpsWrap,
            IncludeDefaultProperties = false,
            SplitTemplates = false,
            CreateAssetMods = false,
            DebugMode = DebugMode,
            SourceExtractionOnly = true
        };

    private void NotifyProcessingOptionsUiChanged()
    {
        OnPropertyChanged(nameof(AreProcessingOptionsEnabled));
        OnPropertyChanged(nameof(AreBroadOutputOptionsEnabled));
        OnPropertyChanged(nameof(ExtractionOptionsToolTipText));
        OnPropertyChanged(nameof(ShowFirstRunHint));
    }
}
