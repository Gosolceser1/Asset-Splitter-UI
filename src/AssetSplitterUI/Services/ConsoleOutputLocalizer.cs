using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using AssetSplitterUI.Localization;

namespace AssetSplitterUI.Services;

internal static partial class ConsoleOutputLocalizer
{
    public static string ResolveLocalized(string key, object[]? args)
    {
        string text = StringResourceManager.Instance.GetString(key);
        if (args is null || args.Length == 0)
        {
            return IndentKeys.Contains(key) ? "  " + text.TrimStart() : text;
        }

        if (args.Length == 1 && args[0] is bool isOn)
        {
            return ResolveOptionLine(text, isOn);
        }

        object[] resolvedArgs = ResolveNestedArgs(args);
        if (text.IndexOf('{') >= 0)
        {
            return ApplyLocalizedLinePrefix(key, FormatLocalizedText(text, resolvedArgs));
        }

        string resolvedText = resolvedArgs.Length == 1 ? "  " + text + " " + resolvedArgs[0] : text;
        return ApplyLocalizedLinePrefix(key, resolvedText);
    }

    /// <summary>Resolves a raw backend stdout line to localized UI text when possible.</summary>
    public static string TryResolveBackendDisplay(string originalText)
    {
        if (TryGetStructuredI18nKey(originalText, out string? key, out object[]? args) ||
            TryGetAutoUpdateLocalizationKey(originalText, out key, out args) ||
            TryGetBackendConsoleLocalizationKey(originalText, out key, out args))
        {
            return ResolveLocalized(key, args);
        }

        return LocalizeProgressLine(originalText);
    }

    /// <summary>Parses structured <c>[I18N] JSON</c> markers emitted by the backend for redirected GUI runs.</summary>
    public static bool TryGetStructuredI18nKey(string line, [NotNullWhen(true)] out string? key, out object[]? args)
    {
        key = null;
        args = null;
        const string prefix = "[I18N] ";
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line[prefix.Length..]);
            if (!doc.RootElement.TryGetProperty("key", out System.Text.Json.JsonElement keyEl))
            {
                return false;
            }

            key = keyEl.GetString();
            if (string.IsNullOrWhiteSpace(key))
            {
                key = null;
                return false;
            }

            if (doc.RootElement.TryGetProperty("args", out System.Text.Json.JsonElement argsEl)
                && argsEl.ValueKind == System.Text.Json.JsonValueKind.Array
                && argsEl.GetArrayLength() > 0)
            {
                args = argsEl.EnumerateArray().Select(static e => (object)(e.GetString() ?? "")).ToArray();
            }

            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string ApplyLocalizedLinePrefix(string key, string text) =>
        key is "issueSummary.sampleChildParent" or "issueSummary.sampleGuid" or "issueSummary.sampleFileDetail"
            ? StringResourceManager.Instance.GetString("issueSummary.sampleBullet") + text
            : text;

    private static object[] ResolveNestedArgs(object[] args)
    {
        object[] resolved = new object[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            resolved[i] = args[i] is LocalizedConsoleArgument localized
                ? ResolveLocalized(localized.Key, localized.Args)
                : args[i];
        }

        return resolved;
    }

    private static string ResolveOptionLine(string text, bool isOn)
    {
        if (isOn)
        {
            return "  " + text;
        }

        string label = StripLeadingSymbol(text);
        return "  \u25CB " + label + " " + StringResourceManager.Instance.GetString("consoleMessages.optionOff");
    }

    private static string StripLeadingSymbol(string text)
    {
        int firstSpace = text.IndexOf(' ');
        if (firstSpace is > 0 and <= 4 && !char.IsLetterOrDigit(text[0]))
        {
            return text[(firstSpace + 1)..];
        }

        return text;
    }

    private static string FormatLocalizedText(string text, object[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, text, args);
        }
        catch (FormatException)
        {
            string result = text;
            for (int i = 0; i < args.Length; i++)
            {
                result = result.Replace("{" + i + "}", args[i]?.ToString() ?? "", StringComparison.Ordinal);
            }

            return result;
        }
    }
}
