namespace AssetProcessor;

/// <summary>
/// Bounded line-oriented text diff for content-changed repair logging.
/// Prefer common-prefix/suffix trimming, then LCS on the middle when cheap;
/// otherwise a linear fallback so huge files do not allocate N×M tables.
/// </summary>
internal static class TextLineDiff
{
    internal const int DefaultMaxReportedChangeLines = 40;
    internal const int DefaultMaxLineDisplayChars = 200;

    /// <summary>Soft cap on LCS table cells (oldMid × newMid) before falling back.</summary>
    private const long MaxLcsCells = 2_000_000;

    internal readonly record struct ChangeLine(bool IsAddition, int LineNumber, string Text);

    internal readonly record struct Result(IReadOnlyList<ChangeLine> Reported, int TotalChangeLines);

    public static Result Compare(
        string? oldText,
        string? newText,
        int maxReportedChangeLines = DefaultMaxReportedChangeLines,
        int maxLineDisplayChars = DefaultMaxLineDisplayChars)
    {
        string[] oldLines = SplitLines(oldText);
        string[] newLines = SplitLines(newText);
        List<ChangeLine> all = BuildEditScript(oldLines, newLines);

        int total = all.Count;
        if (total == 0 || maxReportedChangeLines <= 0)
        {
            return new Result([], total);
        }

        int take = Math.Min(total, maxReportedChangeLines);
        var reported = new List<ChangeLine>(take);
        for (int i = 0; i < take; i++)
        {
            ChangeLine line = all[i];
            reported.Add(line with { Text = Truncate(line.Text, maxLineDisplayChars) });
        }

        return new Result(reported, total);
    }

    internal static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    internal static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
        {
            return text;
        }

        if (maxChars <= 1)
        {
            return "…";
        }

        return text[..(maxChars - 1)] + "…";
    }

    private static List<ChangeLine> BuildEditScript(string[] oldLines, string[] newLines)
    {
        int start = 0;
        while (start < oldLines.Length
               && start < newLines.Length
               && string.Equals(oldLines[start], newLines[start], StringComparison.Ordinal))
        {
            start++;
        }

        int oldEnd = oldLines.Length;
        int newEnd = newLines.Length;
        while (oldEnd > start
               && newEnd > start
               && string.Equals(oldLines[oldEnd - 1], newLines[newEnd - 1], StringComparison.Ordinal))
        {
            oldEnd--;
            newEnd--;
        }

        int oldMid = oldEnd - start;
        int newMid = newEnd - start;
        if (oldMid == 0 && newMid == 0)
        {
            return [];
        }

        if ((long)oldMid * newMid > MaxLcsCells)
        {
            return BuildLinearFallback(oldLines, newLines, start, oldEnd, newEnd);
        }

        return BuildLcsEditScript(oldLines, newLines, start, oldEnd, newEnd);
    }

    private static List<ChangeLine> BuildLinearFallback(
        string[] oldLines,
        string[] newLines,
        int start,
        int oldEnd,
        int newEnd)
    {
        var changes = new List<ChangeLine>();
        int oldIndex = start;
        int newIndex = start;
        while (oldIndex < oldEnd && newIndex < newEnd)
        {
            if (string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal))
            {
                oldIndex++;
                newIndex++;
                continue;
            }

            changes.Add(new ChangeLine(IsAddition: false, LineNumber: oldIndex + 1, Text: oldLines[oldIndex]));
            changes.Add(new ChangeLine(IsAddition: true, LineNumber: newIndex + 1, Text: newLines[newIndex]));
            oldIndex++;
            newIndex++;
        }

        while (oldIndex < oldEnd)
        {
            changes.Add(new ChangeLine(IsAddition: false, LineNumber: oldIndex + 1, Text: oldLines[oldIndex]));
            oldIndex++;
        }

        while (newIndex < newEnd)
        {
            changes.Add(new ChangeLine(IsAddition: true, LineNumber: newIndex + 1, Text: newLines[newIndex]));
            newIndex++;
        }

        return changes;
    }

    private static List<ChangeLine> BuildLcsEditScript(
        string[] oldLines,
        string[] newLines,
        int start,
        int oldEnd,
        int newEnd)
    {
        int oldMid = oldEnd - start;
        int newMid = newEnd - start;
        // lengths[i,j] = LCS length of old[start..start+i) vs new[start..start+j)
        var lengths = new int[oldMid + 1, newMid + 1];
        for (int i = 1; i <= oldMid; i++)
        {
            for (int j = 1; j <= newMid; j++)
            {
                if (string.Equals(oldLines[start + i - 1], newLines[start + j - 1], StringComparison.Ordinal))
                {
                    lengths[i, j] = lengths[i - 1, j - 1] + 1;
                }
                else
                {
                    lengths[i, j] = Math.Max(lengths[i - 1, j], lengths[i, j - 1]);
                }
            }
        }

        var reverse = new List<ChangeLine>();
        int oi = oldMid;
        int nj = newMid;
        while (oi > 0 || nj > 0)
        {
            if (oi > 0
                && nj > 0
                && string.Equals(oldLines[start + oi - 1], newLines[start + nj - 1], StringComparison.Ordinal))
            {
                oi--;
                nj--;
                continue;
            }

            if (nj > 0 && (oi == 0 || lengths[oi, nj - 1] >= lengths[oi - 1, nj]))
            {
                reverse.Add(new ChangeLine(
                    IsAddition: true,
                    LineNumber: start + nj,
                    Text: newLines[start + nj - 1]));
                nj--;
            }
            else
            {
                reverse.Add(new ChangeLine(
                    IsAddition: false,
                    LineNumber: start + oi,
                    Text: oldLines[start + oi - 1]));
                oi--;
            }
        }

        reverse.Reverse();
        return reverse;
    }
}
