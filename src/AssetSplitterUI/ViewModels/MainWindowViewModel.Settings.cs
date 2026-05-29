using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    public void LoadSettings()
    {
        _settingsCoordinator.Load(
            setGamePath: value => GamePath = value,
            setOutputPath: value => OutputPath = value,
            setAddComments: value => AddComments = value,
            setFixDependencies: value => FixDependencies = value,
            setCreateTemplateFolders: value => CreateTemplateFolders = value,
            setModOpsWrap: value => ModOpsWrap = value,
            setIncludeDefaultProperties: value => IncludeDefaultProperties = value,
            setSplitTemplates: value => SplitTemplates = value,
            setCreateAssetMods: value => CreateAssetMods = value,
            setUILanguage: value => SelectedUILanguage = value,
            setTheme: value => SelectedTheme = value,
            RecentGamePaths,
            RecentOutputPaths,
            AvailableUILanguages,
            AvailableThemes);

        SavedWindowWidth = _settingsCoordinator.SavedWindowWidth;
        SavedWindowHeight = _settingsCoordinator.SavedWindowHeight;

        _ = SchedulePostLoadSettingsAsync();
    }

    private async Task SchedulePostLoadSettingsAsync()
    {
        try
        {
            await Task.Delay(100);
            await RefreshDetectedGamesAsync(showStatus: false);
            await Dispatcher.UIThread.InvokeAsync(CheckSourceFilesExist);
            await RefreshAvailableLanguagesAsync();

            string? lastLang = _settingsCoordinator.LastGameLanguage;
            if (AvailableGameLanguages.Count > 0 && !string.IsNullOrEmpty(lastLang))
            {
                SetGameLanguageSelection(
                    AvailableGameLanguages.Contains(lastLang, StringComparer.OrdinalIgnoreCase)
                        ? lastLang
                        : AvailableGameLanguages[0]);
            }
            else if (AvailableGameLanguages.Count == 0)
            {
                SelectedLanguage = "";
            }
        }
        catch (Exception ex)
        {
            UILogger.Debug(nameof(MainWindowViewModel), ex);
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ApplyTheme(value);
        SaveSettingsIfReady(); // Don't save during LoadSettings (would write incomplete state).
        // Force log to re-render so LogLineKindConverter (theme-aware colors) picks up the new theme.
        if (LogLines.Count > 0)
            Dispatcher.UIThread.Post(_logStore.RefreshColors, DispatcherPriority.Background);
    }

    partial void OnAddCommentsChanged(bool value) => SaveSettingsIfReady();
    partial void OnFixDependenciesChanged(bool value) => SaveSettingsIfReady();
    partial void OnCreateTemplateFoldersChanged(bool value) => SaveSettingsIfReady();
    partial void OnModOpsWrapChanged(bool value) => SaveSettingsIfReady();
    partial void OnIncludeDefaultPropertiesChanged(bool value) => SaveSettingsIfReady();
    partial void OnSplitTemplatesChanged(bool value) => SaveSettingsIfReady();
    partial void OnCreateAssetModsChanged(bool value)
    {
        if (value)
        {
            ModOpsWrap = true;
            if (!IsSingleGuidMode)
                CreateTemplateFolders = true;
        }

        SaveSettingsIfReady();
    }

    /// <summary>Saves settings only when a full load is not in progress.</summary>
    private void SaveSettingsIfReady() { if (!_settingsCoordinator.IsLoading) SaveSettings(); }

    /// <summary>Applies Light/Auto/Dark theme to the application.</summary>
    /// <param name="themeMode">"Light", "Dark", or "Auto" (follow system).</param>
    public static void ApplyTheme(string themeMode) => ApplicationThemeService.ApplyTheme(themeMode);

    partial void OnSelectedLanguageChanged(string value)
    {
        ScheduleSingleGuidLookup();
        SaveSettingsIfReady();
    }

    partial void OnSingleGuidChanged(string value)
    {
        ScheduleSingleGuidLookup();
    }

    partial void OnSelectedUILanguageChanged(string value)
    {
        StringResourceManager.Instance.CurrentLanguage = value;
        // Computed localized property (not a {loc:Localize} binding) must be refreshed explicitly.
        OnPropertyChanged(nameof(ExtractButtonText));
        // Re-evaluate live single-GUID hint text in the newly selected language.
        ScheduleSingleGuidLookup();
        _statusTextState.Refresh();
        SaveSettingsIfReady(); // Persist UI language preference.
    }

    partial void OnGamePathChanged(string value)
    {
        CheckSourceFilesExist();
        ScheduleSingleGuidLookup();
        _ = RefreshAvailableLanguagesAsync();
        StartExtractionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartExtraction));
    }

    /// <summary>
    /// When the user picks a different game from the detected-games selector,
    /// update GamePath, recent paths, refresh source files + languages. Save the
    /// current game's console state and restore the selected game's state (or clear if none).
    /// </summary>
    partial void OnSelectedDetectedGameChanged(GameInstallation? value)
    {
        if (value is null) return;

        // Do not apply game switch while a run is in progress (dropdown is disabled, but guard for safety).
        if (IsProcessing) return;

        _consoleStateStore.Save(GamePath, CaptureConsoleState());

        var path = PathDisplayHelper.GetPathWithActualCasing(value.Path);
        GamePath = path;
        PathDisplayHelper.AddToRecentPaths(RecentGamePaths, path);
        CheckSourceFilesExist();
        ScheduleSingleGuidLookup();
        _ = RefreshAvailableLanguagesAsync();

        if (_consoleStateStore.TryGet(path, out var state))
            RestoreConsoleState(state);
        else
            ResetConsoleAndRunState();
    }

    /// <summary>Captures current log, progress, phase and status into a snapshot.</summary>
    private GameConsoleState CaptureConsoleState() =>
        new()
        {
            LogLines = [.. _logStore.Snapshot()],
            Progress = Progress,
            IsComplete = IsComplete,
            Phase1Complete = Phase1Complete,
            StatusText = StatusText,
            StatusIsError = StatusIsError,
            StatusTextKey = _statusTextState.Key,
            StatusTextArgs = _statusTextState.Args
        };

    /// <summary>Restores log, progress, phase and status from a snapshot.</summary>
    private void RestoreConsoleState(GameConsoleState state)
    {
        if (IsProcessing) return;

        _logStore.Restore(state.LogLines);
        Progress = state.Progress;
        IsComplete = state.IsComplete;
        Phase1Complete = state.Phase1Complete;
        StatusIsError = state.StatusIsError;
        _statusTextState.Restore(state.StatusText, state.StatusTextKey, state.StatusTextArgs);

        OnPropertyChanged(nameof(ShowPhase2Banner));
        OnPropertyChanged(nameof(ExtractButtonText));
        OnPropertyChanged(nameof(CanUseSingleGuid));
    }

    /// <summary>Clears the console log and resets progress/phase/status so the UI reflects "no run for current selection".</summary>
    private void ResetConsoleAndRunState()
    {
        if (IsProcessing) return;

        _logStore.Clear();
        Progress = 0;
        IsComplete = false;
        Phase1Complete = false;
        StatusIsError = false;
        SetStatusTextLocalized("console.ready");
        OnPropertyChanged(nameof(ShowPhase2Banner));
        OnPropertyChanged(nameof(ExtractButtonText));
        OnPropertyChanged(nameof(CanUseSingleGuid));
    }

    partial void OnOutputPathChanged(string value)
    {
        CheckSourceFilesExist();
        ScheduleSingleGuidLookup();
        _ = RefreshAvailableLanguagesAsync();
        OnPropertyChanged(nameof(ShowPhase2Banner));
        OnPropertyChanged(nameof(ExtractButtonText));
        StartExtractionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartExtraction));
    }

    partial void OnIsProcessingChanged(bool value)
    {
        StartExtractionCommand.NotifyCanExecuteChanged();
        ((RelayCommand)CancelExtractionCommand).NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartExtraction));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(AreBroadOutputOptionsEnabled));
        OnPropertyChanged(nameof(CanUseSingleGuid));

        if (value)
        {
            _busyAnimator.Start();
            _lastDispatchedProgress = -1;
            _consoleOutput.Start();
        }
        else
        {
            _busyAnimator.Stop();
            _consoleOutput.Stop();
        }
    }

}
