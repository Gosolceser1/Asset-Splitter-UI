using System.Collections.Generic;

namespace AssetSplitterUI.Services;

/// <summary>
/// Tracks pipeline progress by summing each phase's own current/total counters.
/// Every backend progress line updates one operation bucket; switching operations
/// finalizes the previous bucket at its reported total.
/// Overall percent re-anchors when [PLAN]/[PLAN+] expands the budget or when a new
/// phase publishes a larger total so early phases cannot pin the bar at 100%.
/// </summary>
internal sealed class PipelineProgressTracker
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, (long Completed, long Total)> _operations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentOperation = "";
    private long _planFloor;
    private long _lastBudget;
    private double _maxPercent;

    public double OverallPercent
    {
        get
        {
            lock (_sync)
            {
                return Math.Min(_maxPercent, 100.0);
            }
        }
    }

    public void Feed(string outputLine)
    {
        lock (_sync)
        {
            FeedUnlocked(outputLine);
        }
    }

    private void FeedUnlocked(string outputLine)
    {
        if (string.IsNullOrWhiteSpace(outputLine))
        {
            return;
        }

        if (TryParsePlanLine(outputLine, out long planUnits, isAdditive: false))
        {
            FinalizeCurrentOperation();
            long completed = SumCompletedUnits();
            _planFloor = completed + planUnits;
            // Plan lines expand/replace the known budget — allow the bar to drop so Phase 1
            // finishing its own slice cannot pin OverallPercent at 100% for later phases.
            Recalculate(allowDecrease: true);
            return;
        }

        if (TryParsePlanLine(outputLine, out planUnits, isAdditive: true))
        {
            _planFloor += planUnits;
            Recalculate(allowDecrease: true);
            return;
        }

        if (!TryParseProgressLine(outputLine, out long current, out long total, out string operation))
        {
            return;
        }

        string operationKey = GetOperationKey(operation);
        if (operationKey.Length == 0)
        {
            return;
        }

        if (!operationKey.Equals(_currentOperation, StringComparison.OrdinalIgnoreCase))
        {
            FinalizeCurrentOperation();
            _currentOperation = operationKey;
        }

        _operations[operationKey] = (current, total);
        Recalculate();
    }

    private void Recalculate(bool allowDecrease = false)
    {
        long completed = SumCompletedUnits();
        long budget = SumBudgetUnits();
        if (_planFloor > budget)
        {
            budget = _planFloor;
        }

        if (budget <= 0)
        {
            return;
        }

        double pct = (double)completed / budget * 100.0;
        // When a new phase publishes a larger total (language 10k, Extracting 96k, …),
        // re-anchor so Phase 1 finishing cannot pin OverallPercent at 100% for later work.
        // Within a stable budget, keep monotonic Math.Max so parallel ticks never jitter backward.
        bool budgetGrew = budget > _lastBudget;
        _lastBudget = budget;
        _maxPercent = allowDecrease || budgetGrew ? pct : Math.Max(_maxPercent, pct);
    }

    private long SumCompletedUnits()
    {
        long sum = 0;
        foreach (var (_, units) in _operations)
        {
            sum += units.Completed;
        }

        return sum;
    }

    private long SumBudgetUnits()
    {
        long sum = 0;
        foreach (var (_, units) in _operations)
        {
            sum += units.Total;
        }

        return sum;
    }

    private void FinalizeCurrentOperation()
    {
        if (_currentOperation.Length == 0)
        {
            return;
        }

        if (_operations.TryGetValue(_currentOperation, out var units))
        {
            _operations[_currentOperation] = (units.Total, units.Total);
        }

        _currentOperation = "";
    }

    private static bool TryParsePlanLine(string outputLine, out long planUnits, bool isAdditive)
    {
        planUnits = 0;
        string prefix = isAdditive ? "[PLAN+]" : "[PLAN]";
        if (!outputLine.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string numPart = outputLine[prefix.Length..].Trim();
        return TryParseGroupedCount(numPart, out planUnits) && planUnits > 0;
    }

    /// <summary>
    /// Parses a count that may contain culture-specific group separators
    /// (",", ".", "'", U+00A0, U+202F, ...). Only ASCII digits are kept.
    /// </summary>
    private static bool TryParseGroupedCount(string text, out long value)
    {
        value = 0;
        var digits = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
            else if (!char.IsWhiteSpace(c) && c is not (',' or '.' or '\'' or ' ' or ' ' or ' '))
            {
                return false;
            }
        }

        return digits.Length > 0
            && long.TryParse(digits.ToString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseProgressLine(string outputLine, out long current, out long total, out string operation)
    {
        current = 0;
        total = 0;
        operation = "";

        int firstBracket = outputLine.IndexOf('[');
        if (firstBracket < 0)
        {
            return false;
        }

        int secondBracket = outputLine.IndexOf('[', firstBracket + 1);
        if (secondBracket < 0)
        {
            return false;
        }

        int dash = outputLine.IndexOf("] - ", secondBracket, StringComparison.Ordinal);
        if (dash < 0)
        {
            return false;
        }

        string counts = outputLine.Substring(secondBracket + 1, dash - secondBracket - 1).Trim();
        int slash = counts.IndexOf('/');
        if (slash < 0)
        {
            return false;
        }

        if (!TryParseGroupedCount(counts[..slash], out current) || !TryParseGroupedCount(counts[(slash + 1)..], out total) || total <= 0)
        {
            return false;
        }

        operation = outputLine[(dash + 4)..].Trim();
        return true;
    }

    private static string GetOperationKey(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return "";
        }

        int colon = operation.IndexOf(':');
        string key = colon > 0 ? operation[..colon] : operation;
        key = key.Trim().TrimEnd('.', '…').Trim();

        // Legacy backend emitted a final "Formatting..." line with a different key than
        // per-file "Processing:…" lines, which double-counted and pinned the bar at 100%.
        if (key.Equals("Formatting", StringComparison.OrdinalIgnoreCase))
        {
            return "Processing";
        }

        return key;
    }

    public void Reset()
    {
        lock (_sync)
        {
            _operations.Clear();
            _currentOperation = "";
            _planFloor = 0;
            _lastBudget = 0;
            _maxPercent = 0;
        }
    }
}
