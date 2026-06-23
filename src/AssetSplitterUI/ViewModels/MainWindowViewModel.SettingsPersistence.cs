using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    #region Settings Persistence

    /// <summary>Width to restore on next launch (0 = use default). Set by LoadSettings.</summary>
    public double SavedWindowWidth { get; private set; }

    /// <summary>Height to restore on next launch (0 = use default). Set by LoadSettings.</summary>
    public double SavedWindowHeight { get; private set; }

    public int SavedWindowLeft { get; private set; } = WindowPlacementSettings.UnsetCoordinate;
    public int SavedWindowTop { get; private set; } = WindowPlacementSettings.UnsetCoordinate;
    public bool SavedWindowMaximized { get; private set; }
    public string? SavedWindowDisplayName { get; private set; }

    /// <summary>Call before SaveSettings() so the current window bounds are persisted. MainWindow calls this in OnClosing.</summary>
    public void SetWindowPlacementForSave(WindowPlacementSettings placement) =>
        _settingsCoordinator.SetWindowPlacementForSave(placement);

    /// <summary>Legacy size-only save.</summary>
    public void SetWindowSizeForSave(double width, double height) =>
        _settingsCoordinator.SetWindowSizeForSave(width, height);

    /// <summary>Persists current paths, options, theme, language, and recent lists to app data JSON.</summary>
    public void SaveSettings()
    {
        _settingsCoordinator.Save(
            GamePath, OutputPath,
            AddComments, FixDependencies, CreateTemplateFolders,
            ModOpsWrap, IncludeDefaultProperties, SplitTemplates, CreateAssetMods,
            SelectedUILanguage, SelectedTheme, SelectedLanguage,
            RecentGamePaths, RecentOutputPaths);
        OnPropertyChanged(nameof(ShowFirstRunHint));
        NotifyFirstRunStepHintsChanged();
    }

    /// <summary>Builds the command-line arguments string (for display only). Delegates to <c>GuiProcessRunner.BuildArguments</c>.</summary>
    private string BuildBackendArgs(string gamePath, string outputPath, string language, string? singleGuidOverride = null)
    {
        bool singleGuidMode = !string.IsNullOrWhiteSpace(singleGuidOverride ?? SingleGuid);
        var config = new AssetProcessorRunConfig
        {
            GamePath = gamePath,
            OutputPath = outputPath,
            Language = language,
            ConsoleLanguage = "english",
            ReadmeLanguage = GetBackendConsoleLanguage(),
            SingleGuid = singleGuidOverride ?? SingleGuid.Trim(),
            AddComments = AddComments,
            FixDependencies = FixDependencies,
            CreateTemplateFolders = CreateTemplateFolders && !singleGuidMode,
            ModOpsWrap = ModOpsWrap,
            IncludeDefaultProperties = IncludeDefaultProperties,
            SplitTemplates = SplitTemplates && !singleGuidMode,
            CreateAssetMods = CreateAssetMods,
            DebugMode = DebugMode
        };
        var args = GuiProcessRunner.BuildArguments(config, "english");
        return string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
    }

    private string GetBackendConsoleLanguage() =>
        StringResourceManager.GetLanguageCode(SelectedUILanguage) switch
        {
            "" => "en",
            var code => code
        };

    /// <summary>Release UI-bound resources quickly so window close does not wait on log flush or backend exit.</summary>
    public void PrepareForShutdown()
    {
        _logStore.BeginShutdown();
        _consoleOutput.Stop(discardPending: true);
        CloseRawConsoleLogWriter();

        if (_extractionCoordinator.IsRunning)
        {
            _extractionCoordinator.Cancel(fastKill: true);
        }
        else
        {
            AssetProcessorRunner.TryKillCurrentProcess(maxWaitMilliseconds: 0);
        }

        _logStore.ClearWithoutNotify();
    }

    public void Dispose()
    {
        StringResourceManager.Instance.PropertyChanged -= OnStringResourceManagerPropertyChanged;
        ApplicationThemeService.ThemeChanged -= OnApplicationThemeChanged;
        AvailableGameLanguages.CollectionChanged -= OnAvailableGameLanguagesChanged;
        _singleGuidLookupCts?.Cancel();
        _singleGuidLookupCts?.Dispose();

        PrepareForShutdown();
        _busyAnimator.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
