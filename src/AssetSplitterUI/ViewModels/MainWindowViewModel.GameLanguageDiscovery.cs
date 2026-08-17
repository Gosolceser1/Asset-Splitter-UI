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
        string normalized = GameLanguageIdNormalizer.Normalize(languageId);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return AvailableGameLanguages.FirstOrDefault(language =>
            GameLanguageIdNormalizer.Normalize(language).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string CapitalizeLanguageId(string languageId) =>
        GameLanguageIdNormalizer.Capitalize(languageId);

    /// <summary>Refreshes detected language files when the language dropdown is opened.</summary>
    public Task RefreshAvailableLanguagesForDropdownAsync() => RefreshAvailableLanguagesAsync();
}
