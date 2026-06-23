using System.Collections.Generic;

namespace AssetSplitterUI.Services;

/// <summary>
/// Tracks pipeline progress by summing each phase's own current/total counters.
/// Every backend progress line updates one operation bucket; switching operations
/// finalizes the previous bucket at its reported total.
/// </summary>
internal sealed class PipelineProgressTracker
{
    private readonly Dictionary<string, (long Completed, long Total)> _operations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentOperation = "";
    private long _planFloor;
    private double _maxPercent;
    private bool _planReceived;

    public double OverallPercent => Math.Min(_maxPercent, 100.0);

    public void Feed(string outputLine)
    {
        if (string.IsNullOrWhiteSpace(outputLine))
        {
            return;
        }

        if (TryParsePlanLine(outputLine, out long planUnits, isAdditive: false))
        {
            _planReceived = true;
            FinalizeCurrentOperation();
            long completed = SumCompletedUnits();
            _planFloor = completed + planUnits;
            Recalculate();
            return;
        }

        if (TryParsePlanLine(outputLine, out planUnits, isAdditive: true))
        {
            _planReceived = true;
            _planFloor += planUnits;
            Recalculate();
            return;
        }

        if (!_planReceived)
        {
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

    private void Recalculate()
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
        _maxPercent = pct;
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
        return long.TryParse(numPart.Replace(",", "").Replace(" ", ""), out planUnits) && planUnits > 0;
    }

    private static bool TryParseProgressLine(string outputLine, out long current, out long total, out string operation)
    {
        current = 0;
        total = 0;
        operation = "";

        int b1 = outputLine.IndexOf('[');
        if (b1 < 0)
        {
            return false;
        }

        int b2 = outputLine.IndexOf('[', b1 + 1);
        if (b2 < 0)
        {
            return false;
        }

        int dash = outputLine.IndexOf("] - ", b2, StringComparison.Ordinal);
        if (dash < 0)
        {
            return false;
        }

        string counts = outputLine.Substring(b2 + 1, dash - b2 - 1).Trim();
        int slash = counts.IndexOf('/');
        if (slash < 0)
        {
            return false;
        }

        string cur = counts[..slash].Trim().Replace(",", "").Replace(" ", "");
        string tot = counts[(slash + 1)..].Trim().Replace(",", "").Replace(" ", "");

        if (!long.TryParse(cur, out current) || !long.TryParse(tot, out total) || total <= 0)
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
        return key.Trim().TrimEnd('.', '…').Trim();
    }

    public void Reset()
    {
        _operations.Clear();
        _currentOperation = "";
        _planFloor = 0;
        _maxPercent = 0;
        _planReceived = false;
    }
}
