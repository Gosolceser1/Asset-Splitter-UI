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
        if (IsProcessing)
        {
            return;
        }

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
        if (IsProcessing)
        {
            return;
        }

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

    private async Task OpenOutputFolderAsync()
    {
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            await _platformServices.OpenFolderAsync(OutputPath);
        }
    }

    private async Task PromptOpenOutputFolderAfterSuccessAsync()
    {
        if (string.IsNullOrEmpty(OutputPath) || !Directory.Exists(OutputPath))
        {
            return;
        }

        var strings = StringResourceManager.Instance;
        bool openFolder = await _platformServices.ShowYesNoDialogAsync(
            strings.GetString("dialogs.extractionSuccessTitle"),
            strings.GetString("dialogs.extractionSuccessMsg"),
            strings.GetString("buttons.yes"),
            strings.GetString("buttons.no"));

        if (openFolder)
        {
            await OpenOutputFolderAsync();
        }
    }
}
