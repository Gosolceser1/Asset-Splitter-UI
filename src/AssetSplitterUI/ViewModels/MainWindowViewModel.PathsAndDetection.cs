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
    private async Task BrowseGamePathAsync()
    {
        string? result = await _platformServices.ShowFolderPickerAsync(
            StringResourceManager.Instance.GetString("fileDialogs.selectGameDirectory"),
            string.IsNullOrEmpty(GamePath) ? null : GamePath
        );

        if (!string.IsNullOrEmpty(result))
        {
            result = PathDisplayHelper.GetPathWithActualCasing(result);
            GamePath = result;
            PathDisplayHelper.AddToRecentPaths(RecentGamePaths, result);
            await ValidateManualGamePathAsync();
        }
    }

    /// <summary>Opens the native folder picker for the output path, defaulting to Documents when no output path is set; persists to recent paths.</summary>
    private async Task BrowseOutputPathAsync()
    {
        // Use a different initial location from game path: prefer last output path, else a neutral default (Documents) so the picker never opens inside the game folder.
        string? initialForOutput = null;
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            initialForOutput = OutputPath;
        }
        else
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs) && Directory.Exists(docs))
                {
                    initialForOutput = docs;
                }
            }
            catch { /* fallback: null, picker uses system default */ }
        }
        string? result = await _platformServices.ShowFolderPickerAsync(
            StringResourceManager.Instance.GetString("fileDialogs.selectOutputDirectory"),
            initialForOutput
        );

        if (!string.IsNullOrEmpty(result))
        {
            result = PathDisplayHelper.GetPathWithActualCasing(result);
            OutputPath = result;
            PathDisplayHelper.AddToRecentPaths(RecentOutputPaths, result);
        }
    }

    /// <summary>Scans for Anno installations via PlatformServices, populates DetectedGames, updates DetectStatusText, and selects the preferred match.</summary>
    private async Task DetectGamesAsync()
    {
        await RefreshDetectedGamesAsync(showStatus: true);
    }

    private async Task RefreshDetectedGamesAsync(bool showStatus)
    {
        // Detection feedback goes to DetectStatusText (inline, next to the Auto-Detect button).
        if (showStatus)
        {
            _detectStatusKey = "statusMessages.searchingForAnnoInstallations";
            _detectStatusFormatArgs = null;
            DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
        }

        var games = await _platformServices.DetectGameInstallationsAsync();

        DetectedGames.Clear();
        foreach (var game in games)
        {
            DetectedGames.Add(game);
        }

        // Notify the UI that HasDetectedGames may have changed.
        OnPropertyChanged(nameof(HasDetectedGames));

        if (games.Count > 0)
        {
            var preferred = games.FirstOrDefault(g =>
                PathDisplayHelper.PathsEqual(g.Path, GamePath));

            // Only auto-fill game path when the user explicitly ran Auto-Detect and nothing is set yet.
            if (preferred is not null)
            {
                ApplyRecognizedGameInstallation(preferred);
            }
            else if (string.IsNullOrWhiteSpace(GamePath))
            {
                ApplyRecognizedGameInstallation(games[0]);
            }
            else
            {
                SelectedDetectedGame = null;
            }

            try
            {
                if (games.Count == 1)
                {
                    if (showStatus)
                    {
                        var game = preferred ?? games[0];
                        _detectStatusKey = "statusMessages.foundAnnoInstallation";
                        _detectStatusFormatArgs = [GetLocalizedGameNameArgument(game)];
                        DetectStatusText = ResolveLocalizedText(_detectStatusKey, _detectStatusFormatArgs);
                    }
                }
                else
                {
                    if (showStatus)
                    {
                        _detectStatusKey = "statusMessages.foundMultipleAnnoInstallations";
                        _detectStatusFormatArgs = [games.Count];
                        DetectStatusText = ResolveLocalizedText(_detectStatusKey, _detectStatusFormatArgs);
                    }
                }
            }
            catch (FormatException)
            {
                if (showStatus)
                {
                    _detectStatusKey = "statusMessages.foundMultipleAnnoInstallations";
                    _detectStatusFormatArgs = [games.Count];
                    DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
                }
            }

            await RefreshAvailableLanguagesAsync();
        }
        else
        {
            SelectedDetectedGame = null;
            if (showStatus)
            {
                _detectStatusKey = "statusMessages.noAnnoInstallationsFound";
                _detectStatusFormatArgs = null;
                DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
            }
        }
    }

    private void ScheduleManualGamePathValidation()
    {
        if (_updatingGamePathFromSelector)
        {
            return;
        }

        _gamePathValidationCts?.Cancel();
        _gamePathValidationCts = new CancellationTokenSource();
        CancellationToken token = _gamePathValidationCts.Token;
        _ = ValidateManualGamePathDebouncedAsync(token);
    }

    private async Task ValidateManualGamePathDebouncedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
            await ValidateManualGamePathAsync(token);
        }
        catch (OperationCanceledException)
        {
            /* superseded by a newer edit */
        }
    }

    private Task ValidateManualGamePathAsync(CancellationToken token = default) =>
        ValidateManualGamePathCoreAsync(token);

    private async Task ValidateManualGamePathCoreAsync(CancellationToken token)
    {
        string path = GamePath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            await Dispatcher.UIThread.InvokeAsync(ClearManualGamePathRecognition);
            return;
        }

        IReadOnlyList<GameInstallation> matches = await Task.Run(
            () => _platformServices.RecognizeGameInstallations(path),
            token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            ApplyManualGamePathRecognition(path, matches);
        });
    }

    private void ApplyManualGamePathRecognition(string requestedPath, IReadOnlyList<GameInstallation> matches)
    {
        if (!PathDisplayHelper.PathsEqual(requestedPath, GamePath))
        {
            return;
        }

        OnPropertyChanged(nameof(HasDetectedGames));

        if (matches.Count == 1)
        {
            ApplyRecognizedGameInstallation(matches[0]);
            return;
        }

        if (matches.Count > 1)
        {
            DetectedGames.Clear();
            foreach (GameInstallation game in matches)
            {
                DetectedGames.Add(game);
            }

            SelectedDetectedGame = null;
            IsGamePathRecognized = false;
            SetDetectStatus("statusMessages.selectGameSubfolder");
            return;
        }

        SelectedDetectedGame = null;
        IsGamePathRecognized = false;
        SetDetectStatus("statusMessages.invalidGameDirectory");
    }

    private void ApplyRecognizedGameInstallation(GameInstallation game)
    {
        string path = PathDisplayHelper.GetPathWithActualCasing(game.Path);

        if (!DetectedGames.Any(existing => PathDisplayHelper.PathsEqual(existing.Path, path)))
        {
            DetectedGames.Add(game);
        }

        _updatingGamePathFromSelector = true;
        try
        {
            if (!PathDisplayHelper.PathsEqual(path, GamePath))
            {
                GamePath = path;
            }

            SelectedDetectedGame = game;
        }
        finally
        {
            _updatingGamePathFromSelector = false;
        }

        PathDisplayHelper.AddToRecentPaths(RecentGamePaths, path);
        IsGamePathRecognized = true;
        SetDetectStatus("statusMessages.foundAnnoInstallation", GetLocalizedGameNameArgument(game));
    }

    private void ClearManualGamePathRecognition()
    {
        IsGamePathRecognized = false;
        _detectStatusKey = null;
        _detectStatusFormatArgs = null;
        DetectStatusText = "";
        OnPropertyChanged(nameof(HasDetectStatus));
    }

    private void SetDetectStatus(string key, params object[] args)
    {
        _detectStatusKey = key;
        _detectStatusFormatArgs = args.Length > 0 ? args : null;
        try
        {
            DetectStatusText = ResolveLocalizedText(key, _detectStatusFormatArgs);
        }
        catch (FormatException)
        {
            DetectStatusText = StringResourceManager.Instance.GetString(key);
        }

        OnPropertyChanged(nameof(HasDetectStatus));
    }

    private static LocalizedConsoleArgument GetLocalizedGameNameArgument(GameInstallation game) =>
        new(game.DisplayName);

    /// <summary>Scans source_xml folders for texts_*.xml files and rebuilds AvailableGameLanguages on the UI thread; restores or clears the language selection accordingly.</summary>
    private async Task RefreshAvailableLanguagesAsync()
    {
        int refreshVersion = Interlocked.Increment(ref _availableLanguageRefreshVersion);
        string selectedGameType = SelectedDetectedGame?.GameType ?? "";
        var languages = await Task.Run(() =>
            ExtractedAssetSourceLocator.FindAvailableLanguages(OutputPath, GamePath, selectedGameType));

        string? preservedSelection = null;
        bool languagesChanged = false;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _availableLanguageRefreshVersion)
            {
                return;
            }

            preservedSelection = GetPreservedGameLanguageSelection();

            languagesChanged = !AvailableGameLanguages.SequenceEqual(languages, StringComparer.OrdinalIgnoreCase);
            if (!languagesChanged)
            {
                return;
            }

            AvailableGameLanguages.Clear();
            foreach (string? lang in languages)
            {
                AvailableGameLanguages.Add(lang);
            }
        });

        // Defer selection until UI finishes processing Clear+Add so ComboBox doesn't ignore it mid-rebuild
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _availableLanguageRefreshVersion)
            {
                return;
            }

            if (AvailableGameLanguages.Count > 0)
            {
                string? currentSelection = FindAvailableGameLanguage(SelectedLanguage);
                if (languagesChanged || currentSelection is null)
                {
                    SetGameLanguageSelection(
                        currentSelection
                        ?? FindAvailableGameLanguage(preservedSelection)
                        ?? GetDefaultGameLanguageSelection());
                }
            }
            else
            {
                SelectedLanguage = "";
            }

            // Update banner visibility now that languages are populated
            OnPropertyChanged(nameof(ExtractButtonText));
        });
    }

    /// <summary>Language id to keep when rebuilding the dropdown (before Clear() wipes the bound selection).</summary>
    private string? GetPreservedGameLanguageSelection()
    {
        if (!string.IsNullOrEmpty(SelectedLanguage))
        {
            return SelectedLanguage;
        }

        if (!string.IsNullOrEmpty(_lastRunBackendAssetLanguage)
            && !_lastRunBackendAssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return CapitalizeLanguageId(_lastRunBackendAssetLanguage);
        }

        string? lastSaved = _settingsCoordinator.LastGameLanguage;
        return string.IsNullOrEmpty(lastSaved) ? null : lastSaved;
    }

    private string GetDefaultGameLanguageSelection() =>
        FindAvailableGameLanguage("English")
        ?? AvailableGameLanguages.FirstOrDefault()
        ?? "";

    private string? FindAvailableGameLanguage(string? languageId)
    {
        string normalized = NormalizeGameLanguageId(languageId);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return AvailableGameLanguages.FirstOrDefault(language =>
            NormalizeGameLanguageId(language).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeGameLanguageId(string? languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            return "";
        }

        string normalized = languageId.Trim();
        if (normalized.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["texts_".Length..];
        }

        if (normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".xml".Length];
        }

        normalized = normalized
            .ToLowerInvariant()
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        return normalized switch
        {
            "brazilian_portuguese" => "brazilian",
            "mexican_spanish" => "mexican",
            "chinese_simplified" => "chinese",
            "simplified_chinese" => "chinese",
            "traditional_chinese" => "tchinese",
            "traditionalchinese" => "tchinese",
            _ => normalized
        };
    }

    private static string CapitalizeLanguageId(string languageId) =>
        languageId.Length == 0
            ? languageId
            : char.ToUpperInvariant(languageId[0]) + languageId[1..];

    /// <summary>Refreshes detected language files when the language dropdown is opened.</summary>
    public Task RefreshAvailableLanguagesForDropdownAsync() => RefreshAvailableLanguagesAsync();

    /// <summary>
    /// Checks if source XML files already exist and detects which game type
    /// </summary>
    private void CheckSourceFilesExist()
    {
        DetectedGameType = ExtractedAssetSourceLocator.DetectGameType(OutputPath, GamePath);
        if (!CanUseSingleGuid)
        {
            _singleGuidLookupCts?.Cancel();
            SingleGuid = "";
            SingleGuidStatusText = "";
        }
    }

    private async Task OpenOutputFolderAsync()
    {
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            await _platformServices.OpenFolderAsync(OutputPath);
        }
    }

    /// <summary>Re-resolves all UI-generated log lines so the console shows the current UI language.</summary>
    private void RefreshLocalizedLogLinesInternal() => _logStore.RefreshLocalizedLines();

}
