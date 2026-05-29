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
        var result = await _platformServices.ShowFolderPickerAsync(
            StringResourceManager.Instance.GetString("fileDialogs.selectGameDirectory"),
            string.IsNullOrEmpty(GamePath) ? null : GamePath
        );

        if (!string.IsNullOrEmpty(result))
        {
            result = PathDisplayHelper.GetPathWithActualCasing(result);
            GamePath = result;
            PathDisplayHelper.AddToRecentPaths(RecentGamePaths, result);
        }
    }

    /// <summary>Opens the native folder picker for the output path, defaulting to Documents when no output path is set; persists to recent paths.</summary>
    private async Task BrowseOutputPathAsync()
    {
        // Use a different initial location from game path: prefer last output path, else a neutral default (Documents) so the picker never opens inside the game folder.
        string? initialForOutput = null;
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
            initialForOutput = OutputPath;
        else
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs) && Directory.Exists(docs))
                    initialForOutput = docs;
            }
            catch { /* fallback: null, picker uses system default */ }
        }
        var result = await _platformServices.ShowFolderPickerAsync(
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
            DetectedGames.Add(game);

        // Notify the UI that HasDetectedGames may have changed.
        OnPropertyChanged(nameof(HasDetectedGames));

        if (games.Count > 0)
        {
            // Prefer a game that matches the path the user already had, otherwise take the first.
            var preferred = games.FirstOrDefault(g =>
                g.Path.Equals(GamePath, StringComparison.OrdinalIgnoreCase)) ?? games[0];

            // Setting SelectedDetectedGame triggers OnSelectedDetectedGameChanged which
            // updates GamePath, RecentGamePaths, and CheckSourceFilesExist.
            SelectedDetectedGame = preferred;

            try
            {
                if (games.Count == 1)
                {
                    if (showStatus)
                    {
                        _detectStatusKey = "statusMessages.foundAnnoInstallation";
                        _detectStatusFormatArgs = [preferred.DisplayName];
                        DetectStatusText = preferred.DisplayName;
                    }
                }
                else
                {
                    if (showStatus)
                    {
                        _detectStatusKey = "statusMessages.foundMultipleAnnoInstallations";
                        _detectStatusFormatArgs = [games.Count];
                        DetectStatusText = string.Format(StringResourceManager.Instance.GetString(_detectStatusKey), _detectStatusFormatArgs);
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

    /// <summary>Scans source_xml folders for texts_*.xml files and rebuilds AvailableGameLanguages on the UI thread; restores or clears the language selection accordingly.</summary>
    private async Task RefreshAvailableLanguagesAsync()
    {
        int refreshVersion = Interlocked.Increment(ref _availableLanguageRefreshVersion);
        var selectedGameType = SelectedDetectedGame?.GameType ?? "";
        var languages = await Task.Run(() =>
            ExtractedAssetSourceLocator.FindAvailableLanguages(OutputPath, GamePath, selectedGameType));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _availableLanguageRefreshVersion)
                return;

            AvailableGameLanguages.Clear();
            foreach (var lang in languages.OrderBy(l => l))
            {
                AvailableGameLanguages.Add(lang);
            }
        });

        // Defer selection until UI finishes processing Clear+Add so ComboBox doesn't ignore it mid-rebuild
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _availableLanguageRefreshVersion)
                return;

            if (AvailableGameLanguages.Count > 0)
            {
                var currentId = SelectedLanguage;
                if (string.IsNullOrEmpty(currentId) || !AvailableGameLanguages.Contains(currentId, StringComparer.OrdinalIgnoreCase))
                {
                    SetGameLanguageSelection(AvailableGameLanguages[0]);
                }
                else
                {
                    SetGameLanguageSelection(currentId);
                }
            }
            else
            {
                SelectedLanguage = "";
            }

            // Update banner visibility now that languages are populated
            OnPropertyChanged(nameof(ShowPhase2Banner));
            OnPropertyChanged(nameof(ExtractButtonText));
        });
    }

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
