using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetProcessor;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    private bool TryValidateAndInitRun(out string language)
    {
        language = string.Empty;
        string? validationError = ExtractionRunResultAppender.ValidatePaths(GamePath, OutputPath);
        if (validationError is not null)
        {
            SetStatusTextLocalized(validationError);
            return false;
        }

        string normalizedSingleGuid;
        if (SourceFilesExist)
        {
            if (!TryNormalizeSingleGuid(out normalizedSingleGuid))
            {
                return false;
            }
        }
        else
        {
            normalizedSingleGuid = "";
        }

        if (SourceFilesExist
            && !GameLanguageRunPolicy.TryValidate(AddComments, HasGameLanguages, SelectedLanguage, out string? languageValidationKey))
        {
            SetStatusTextLocalized(languageValidationKey);
            StatusIsError = true;
            return false;
        }

        if (SourceFilesExist && AddComments)
        {
            string? fileBackedLanguage = FindAvailableGameLanguage(SelectedLanguage);
            if (fileBackedLanguage is null)
            {
                SetStatusTextLocalized("dialogs.selectLanguageForComments");
                StatusIsError = true;
                return false;
            }

            if (!fileBackedLanguage.Equals(SelectedLanguage, StringComparison.Ordinal))
            {
                SetGameLanguageSelection(fileBackedLanguage);
            }
        }

        IsProcessing = true;
        Progress = 0;
        IsComplete = false;
        ResetRunStatus();
        _logStore.Clear();
        _logStore.DeveloperConsoleMode = DebugMode;
        CloseRawConsoleLogWriter();
        if (DebugMode)
        {
            TryOpenRawConsoleLogWriter();
        }

        StatusIsError = false;
        Phase1Complete = false;
        SetStatusTextLocalized("statusMessages.initializingExtraction");

        bool initialSourceExtraction = !SourceFilesExist;
        language = initialSourceExtraction ? "none" : ResolveBackendLanguage();
        _lastRunBackendAssetLanguage = language;
        _lastRunBackendSingleGuid = normalizedSingleGuid.Trim();
        string annoAssetsPath = Path.GetFileName(Path.TrimEndingDirectorySeparator(OutputPath)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? OutputPath
            : Path.Combine(OutputPath, "AnnoAssets");
        var effectiveConfig = BuildEffectiveRunConfig(language);
        string? gameBuildLabel = GameBuildDetector
            .TryDetect(GamePath, SelectedDetectedGame?.GameType ?? DetectedGameType)
            ?.ToDisplayString();
        ExtractionRunResultAppender.AppendRunHeader(
            _logStore, GamePath, OutputPath, language, effectiveConfig.SingleGuid,
            effectiveConfig.AddComments, effectiveConfig.FixDependencies, effectiveConfig.CreateTemplateFolders,
            effectiveConfig.ModOpsWrap, effectiveConfig.IncludeDefaultProperties, effectiveConfig.SplitTemplates,
            effectiveConfig.CreateAssetMods, effectiveConfig.DebugMode,
            resolvedAnnoAssetsPath: annoAssetsPath,
            uiLanguageLabel: DebugMode ? SelectedUILanguage : null,
            gameBuildLabel: gameBuildLabel);

        if (DebugMode)
        {
            _logStore.AppendRaw("  AssetProcessor.exe " + BuildBackendArgs(GamePath, annoAssetsPath, language, effectiveConfig.SingleGuid));
        }

        return true;
    }

    private bool TryNormalizeSingleGuid(out string normalizedSingleGuid)
    {
        normalizedSingleGuid = SingleGuid.Trim();
        if (normalizedSingleGuid.Length == 0)
        {
            return true;
        }

        if (normalizedSingleGuid.All(char.IsDigit))
        {
            if (!SingleGuid.Equals(normalizedSingleGuid, StringComparison.Ordinal))
            {
                SingleGuid = normalizedSingleGuid;
            }

            return true;
        }

        SetStatusTextLocalized("statusMessages.singleGuidNumericOnly");
        StatusIsError = true;
        return false;
    }

    private AssetProcessorRunConfig BuildRunConfig(string language)
    {
        bool singleGuidMode = !string.IsNullOrWhiteSpace(SingleGuid);
        return new AssetProcessorRunConfig
        {
            GamePath = GamePath,
            OutputPath = OutputPath,
            Language = language,
            ConsoleLanguage = "english",
            ReadmeLanguage = GetBackendConsoleLanguage(),
            SingleGuid = SingleGuid.Trim(),
            AddComments = AddComments,
            FixDependencies = FixDependencies,
            CreateTemplateFolders = CreateTemplateFolders && !singleGuidMode,
            ModOpsWrap = ModOpsWrap,
            IncludeDefaultProperties = IncludeDefaultProperties,
            SplitTemplates = SplitTemplates && !singleGuidMode,
            CreateAssetMods = CreateAssetMods,
            DebugMode = DebugMode,
            ForceFullRebuild = ForceFullRebuild
        };
    }
}
