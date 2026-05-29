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
    private void ScheduleSingleGuidLookup()
    {
        _singleGuidLookupCts?.Cancel();
        _singleGuidLookupCts?.Dispose();

        string guid = SingleGuid.Trim();
        if (guid.Length == 0)
        {
            SingleGuidStatusText = "";
            _singleGuidLookupCts = null;
            return;
        }

        _singleGuidLookupCts = new CancellationTokenSource();
        CancellationToken token = _singleGuidLookupCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);

                string gamePath = GamePath;
                string outputPath = OutputPath;
                string selectedGameType = SelectedDetectedGame?.GameType ?? DetectedGameType;
                string selectedLanguage = SelectedLanguage;
                var result = await Task.Run(
                    () => SingleGuidAssetLookup.Find(outputPath, gamePath, selectedGameType, selectedLanguage, guid),
                    token);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested && SingleGuid.Trim().Equals(guid, StringComparison.Ordinal))
                        ApplySingleGuidLookupResult(guid, result);
                });
            }
            catch (OperationCanceledException)
            {
                // New keystroke/path change superseded this lookup.
            }
            catch (Exception ex)
            {
                UILogger.Debug(nameof(MainWindowViewModel), ex);
            }
        }, token);
    }

    private void ApplySingleGuidLookupResult(string guid, SingleGuidLookupResult result)
    {
        SingleGuidStatusText = result.Status switch
        {
            SingleGuidLookupStatus.Invalid => StringResourceManager.Instance.GetString("statusMessages.singleGuidNumericOnly"),
            SingleGuidLookupStatus.SourceXmlMissing => StringResourceManager.Instance.GetString("statusMessages.singleGuidSourceXmlMissing"),
            SingleGuidLookupStatus.Found => string.Format(
                StringResourceManager.Instance.GetString("statusMessages.singleGuidFound"),
                guid,
                DisplaySingleGuidMatch(result)),
            SingleGuidLookupStatus.NotFound => string.Format(
                StringResourceManager.Instance.GetString("statusMessages.singleGuidNotFound"),
                guid),
            SingleGuidLookupStatus.Error => StringResourceManager.Instance.GetString("statusMessages.singleGuidLookupError"),
            _ => string.Empty
        };
    }

    private static string DisplaySingleGuidMatch(SingleGuidLookupResult result)
    {
        string assetName = string.IsNullOrWhiteSpace(result.AssetName)
            ? StringResourceManager.Instance.GetString("statusMessages.singleGuidUnnamedAsset")
            : result.AssetName;
        return string.IsNullOrWhiteSpace(result.TemplateName)
            ? assetName
            : $"{assetName} ({result.TemplateName})";
    }

    private void SetGameLanguageSelection(string id)
    {
        SelectedLanguage = id;
    }

    /// <summary>Validates paths, logs the run header and option summary, spawns AssetProcessor through the coordinator, then handles Phase 1/2 completion or error/cancellation.</summary>
}
