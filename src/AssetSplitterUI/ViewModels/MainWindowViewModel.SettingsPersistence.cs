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

    /// <summary>Call before SaveSettings() so the current window size is persisted. MainWindow calls this in OnClosing.</summary>
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
            ConsoleLanguage = GetBackendConsoleLanguage(),
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
        var args = GuiProcessRunner.BuildArguments(config, config.ConsoleLanguage);
        return string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
    }

    private string GetBackendConsoleLanguage() =>
        StringResourceManager.GetLanguageCode(SelectedUILanguage) switch
        {
            "" => "en",
            var code => code
        };

    public void Dispose()
    {
        StringResourceManager.Instance.PropertyChanged -= OnStringResourceManagerPropertyChanged;
        ApplicationThemeService.ThemeChanged -= OnApplicationThemeChanged;
        AvailableGameLanguages.CollectionChanged -= OnAvailableGameLanguagesChanged;
        _singleGuidLookupCts?.Cancel();
        _singleGuidLookupCts?.Dispose();

        _consoleOutput.Stop();

        if (_extractionCoordinator.IsRunning)
            _extractionCoordinator.Cancel();

        AssetProcessorRunner.TryKillCurrentProcess();

        _busyAnimator.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
