using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetProcessor;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Sets StatusText from a localization key (with optional format args) and stores both so the status bar can be re-resolved on UI language change.</summary>
    private void SetStatusTextLocalized(string key, params ReadOnlySpan<object> args) =>
        _statusTextState.SetLocalized(key, args);

    /// <summary>Public alias for view code-behind to set status from a localization key.</summary>
    public void SetStatusText(string key) => SetStatusTextLocalized(key);

    /// <summary>Sets status from a localization key and leaves it refreshable when the UI language changes.</summary>
    public void SetLocalizedStatusText(string key) => SetStatusTextLocalized(key);

    /// <summary>Sets StatusText to a raw string (e.g. live backend output); clears the stored localization key so no re-resolution happens on language change.</summary>
    private void SetStatusTextRaw(string text) => _statusTextState.SetRaw(text);
}
