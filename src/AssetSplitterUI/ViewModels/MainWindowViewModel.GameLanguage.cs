using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>GUID comments (-c) unlocks the game language dropdown when texts_*.xml files exist.</summary>
    public bool IsGameLanguageSelectionEnabled =>
        GameLanguageRunPolicy.IsSelectionEnabled(AddComments, HasGameLanguages);

    /// <summary>Hover tooltip for the game language selector (state-aware).</summary>
    public string GameLanguageToolTipText =>
        StringResourceManager.Instance.GetString(
            GameLanguageRunPolicy.GetTooltipLocalizationKey(AddComments));

    public string ResolveBackendLanguage() =>
        GameLanguageRunPolicy.ResolveBackendLanguage(AddComments, SelectedLanguage);

    private void NotifyGameLanguageUiChanged()
    {
        OnPropertyChanged(nameof(HasGameLanguages));
        OnPropertyChanged(nameof(IsGameLanguageSelectionEnabled));
        OnPropertyChanged(nameof(GameLanguageToolTipText));
    }
}
