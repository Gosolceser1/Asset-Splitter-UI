using System.Collections.ObjectModel;

namespace AssetSplitterUI.Services;

public partial class SettingsCoordinator
{
    private void LoadWindowSize()
    {
        if (_settings is null)
        {
            ClearSavedWindowPlacement();
            return;
        }

        SavedWindowMaximized = _settings.WindowMaximized;
        SavedWindowLeft = _settings.WindowLeft;
        SavedWindowTop = _settings.WindowTop;
        SavedWindowDisplayName = _settings.WindowDisplayName;

        if (_settings.WindowMaximized)
        {
            SavedWindowWidth = 0;
            SavedWindowHeight = 0;
            return;
        }

        if (_settings.WindowWidth >= WindowPlacementHelper.MinWidth
            && _settings.WindowHeight >= WindowPlacementHelper.MinHeight)
        {
            SavedWindowWidth = _settings.WindowWidth;
            SavedWindowHeight = _settings.WindowHeight;
        }
        else
        {
            SavedWindowWidth = 0;
            SavedWindowHeight = 0;
        }
    }

    private void ClearSavedWindowPlacement()
    {
        SavedWindowWidth = 0;
        SavedWindowHeight = 0;
        SavedWindowLeft = WindowPlacementSettings.UnsetCoordinate;
        SavedWindowTop = WindowPlacementSettings.UnsetCoordinate;
        SavedWindowMaximized = false;
        SavedWindowDisplayName = null;
    }
}
