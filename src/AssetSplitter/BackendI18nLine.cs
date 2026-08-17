using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AssetProcessor;

/// <summary>
/// Structured localization markers for GUI stdout parsing.
/// CLI (interactive) still prints human English via <see cref="ConsoleMessages"/>;
/// when stdout is redirected to the UI, emit <c>[I18N] JSON</c> so the GUI can resolve
/// <c>consoleMessages.*</c> keys without English string matching.
/// </summary>
public static class BackendI18nLine
{
    public const string Prefix = "[I18N] ";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Format(string key, params object[]? args)
    {
        var payload = new I18nPayload
        {
            Key = key,
            Args = args is { Length: > 0 } ? args.Select(static a => a?.ToString() ?? "").ToArray() : null,
        };
        return Prefix + JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// Writes a localized line for the GUI when redirected; otherwise writes formatted English.
    /// <paramref name="consoleMessagesKey"/> is the short <see cref="ConsoleMessages"/> key
    /// (e.g. <c>extractionCompleteHeader</c>); the GUI key becomes <c>consoleMessages.{key}</c>.
    /// </summary>
    public static void Write(string consoleMessagesKey, params object[] args)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(Format("consoleMessages." + consoleMessagesKey, args));
            return;
        }

        string template = ConsoleMessages.Get(consoleMessagesKey);
        Console.WriteLine(args.Length == 0 ? template : string.Format(template, args));
    }

    public static bool TryParse(string line, [NotNullWhen(true)] out string? key, out object[]? args)
    {
        key = null;
        args = null;
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<I18nPayload>(line[Prefix.Length..], JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Key))
            {
                return false;
            }

            key = payload.Key;
            args = payload.Args is { Length: > 0 } ? payload.Args.Cast<object>().ToArray() : null;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class I18nPayload
    {
        public string Key { get; set; } = "";
        public string[]? Args { get; set; }
    }
}
