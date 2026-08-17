using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace AssetProcessor;

public static partial class AssetModPackageExporter
{
    private static string NormalizeIdPart(string value)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith("-", StringComparison.Ordinal) ? "minus-" + trimmed[1..] : Slugify(trimmed, 40);
    }

    private static string Slugify(string value, int maxLength)
    {
        var builder = new StringBuilder(value.Length);
        bool previousWasDash = false;

        foreach (char c in value.ToLowerInvariant())
        {
            bool allowed = char.IsAsciiLetterOrDigit(c);
            if (allowed)
            {
                builder.Append(c);
                previousWasDash = false;
            }
            else if (!previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        string slug = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "asset";
        }

        return slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
    }

    private static string SanitizePathPart(string value, int maxLength)
    {
        static bool IsAllowed(char c) =>
            c >= ' ' && c is not ('<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (char c in value.Trim())
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            if (IsAllowed(c))
            {
                builder.Append(c);
            }
        }

        string result = builder.ToString().Trim('.', ' ');
        return string.IsNullOrEmpty(result) ? "asset" : result;
    }

    [GeneratedRegex("^(?<guid>-?\\d+) - \\[ (?<name>.*) \\]\\.xml$", RegexOptions.CultureInvariant)]
    private static partial Regex AssetFileNameRegex();
}
