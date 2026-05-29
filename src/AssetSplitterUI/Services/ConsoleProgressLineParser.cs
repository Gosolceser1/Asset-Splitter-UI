namespace AssetSplitterUI.Services;

/// <summary>Parses structured progress lines from backend stdout (<c>[pct] [n/total] - ...</c>).</summary>
internal static class ConsoleProgressLineParser
{
    public static bool IsProgressLine(string t)
    {
        int percentEnd = t.IndexOf('%');
        if (percentEnd <= 0)
            return false;

        int firstOpen = t.IndexOf('[');
        int firstClose = t.IndexOf(']');
        if (firstOpen != 0 || firstClose <= firstOpen || percentEnd > firstClose)
            return false;

        int secondOpen = t.IndexOf('[', firstClose + 1);
        int secondClose = t.IndexOf(']', secondOpen + 1);
        if (secondOpen < 0 || secondClose <= secondOpen)
            return false;

        string counts = t[(secondOpen + 1)..secondClose];
        return counts.Contains('/', StringComparison.Ordinal);
    }

    public static bool TrySplit(
        string text,
        out string metrics,
        out string operation,
        out string assetDetail,
        out string templateDetail,
        out string guid,
        out string assetName)
    {
        metrics = "";
        operation = "";
        assetDetail = "";
        templateDetail = "";
        guid = "";
        assetName = "";

        string t = text.TrimStart();
        if (!IsProgressLine(t))
            return false;

        int secondClose = t.IndexOf(']', t.IndexOf('[', t.IndexOf(']') + 1) + 1);
        if (secondClose < 0)
            return false;

        metrics = t[..(secondClose + 1)];
        string rest = t[(secondClose + 1)..].TrimStart();
        if (rest.StartsWith("-", StringComparison.Ordinal))
            rest = rest[1..].TrimStart();

        int colon = rest.IndexOf(':');
        if (colon < 0)
        {
            operation = rest;
            return true;
        }

        operation = rest[..(colon + 1)].TrimEnd();
        assetDetail = rest[(colon + 1)..].TrimStart();

        int templateStart = assetDetail.LastIndexOf(" (", StringComparison.Ordinal);
        if (templateStart > 0 && assetDetail.EndsWith(")", StringComparison.Ordinal))
        {
            templateDetail = assetDetail[templateStart..];
            assetDetail = assetDetail[..templateStart].TrimEnd();
        }

        int arrow = assetDetail.IndexOf(" <- ", StringComparison.Ordinal);
        if (arrow > 0)
        {
            assetName = assetDetail[..arrow].TrimEnd();
            guid = assetDetail[(arrow + 4)..].TrimStart();
        }
        else
        {
            int guidSeparator = assetDetail.IndexOf(" - ", StringComparison.Ordinal);
            if (guidSeparator > 0 && assetDetail[..guidSeparator].All(char.IsDigit))
            {
                guid = assetDetail[..guidSeparator];
                assetName = assetDetail[(guidSeparator + 3)..].TrimStart();
            }
            else
            {
                assetName = assetDetail;
            }
        }

        return true;
    }

    /// <summary>From metrics like <c>[ 38.7%] [12,200/31,565]</c> returns <c>38.7%</c>.</summary>
    public static string FormatStepPercent(string metrics)
    {
        if (string.IsNullOrEmpty(metrics))
            return "";

        int firstOpen = metrics.IndexOf('[');
        int firstClose = metrics.IndexOf(']');
        if (firstOpen < 0 || firstClose <= firstOpen)
            return "";

        return metrics[(firstOpen + 1)..firstClose].Trim();
    }

    /// <summary>From metrics like <c>[ 38.7%] [12,200/31,565]</c> returns <c>12,200 / 31,565</c>.</summary>
    public static string FormatCountPair(string metrics)
    {
        if (string.IsNullOrEmpty(metrics))
            return "";

        int firstClose = metrics.IndexOf(']');
        if (firstClose < 0)
            return "";

        int secondOpen = metrics.IndexOf('[', firstClose + 1);
        int secondClose = metrics.IndexOf(']', secondOpen + 1);
        if (secondOpen < 0 || secondClose <= secondOpen)
            return "";

        string inner = metrics[(secondOpen + 1)..secondClose].Trim();
        int slash = inner.IndexOf('/');
        if (slash < 0)
            return inner;

        return inner[..slash].Trim() + " / " + inner[(slash + 1)..].Trim();
    }
}
