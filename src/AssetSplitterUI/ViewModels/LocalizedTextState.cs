using AssetSplitterUI.Localization;

namespace AssetSplitterUI.ViewModels;

internal sealed class LocalizedTextState(Action<string> applyText, Action? notifyRawRefresh = null)
{
    private readonly Action<string> _applyText = applyText;
    private readonly Action? _notifyRawRefresh = notifyRawRefresh;

    public string? Key { get; private set; }
    public object[]? Args { get; private set; }

    public void SetLocalized(string key, params ReadOnlySpan<object> args)
    {
        Key = key;
        Args = args.Length > 0 ? args.ToArray() : null;
        _applyText(Resolve(key, Args));
    }

    public void SetRaw(string text)
    {
        Key = null;
        Args = null;
        _applyText(text);
    }

    public void Restore(string? text, string? key, object[]? args)
    {
        Key = key;
        Args = args;
        _applyText(text ?? "");
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(Key))
        {
            _notifyRawRefresh?.Invoke();
            return;
        }

        _applyText(Resolve(Key, Args));
    }

    private static string Resolve(string key, object[]? args)
    {
        var template = StringResourceManager.Instance.GetString(key);
        if (args is not { Length: > 0 })
            return template;

        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
