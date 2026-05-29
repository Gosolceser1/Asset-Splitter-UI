using System.Collections.Generic;

namespace AssetSplitterUI.Services;

/// <summary>
/// Tracks cumulative pipeline progress across multiple phases by parsing
/// "[current/total]" from each progress line the backend emits.
/// 
/// The denominator is max(totalSeen, completed * 2) - this prevents a small
/// 186-item phase from showing 100% at the start, while allowing the bar to
/// naturally approach 100% as large phases (30K+ items each) complete.
///
/// 100% is reserved for the coordinator's explicit process-exit signal.
/// </summary>
internal sealed class PipelineProgressTracker
{
    private readonly Dictionary<string, long> _operationTotals = new();
    private long _completedSum;
    private long _totalSeen;
    private string _currentOperation = "";
    private long _currentCount;

    private double _maxPercent;
    private long _plannedTotal;

    public double OverallPercent => Math.Min(_maxPercent, 99.9);

    public void Feed(string outputLine)
    {
        if (string.IsNullOrWhiteSpace(outputLine)) return;

        // Parse backend work-plan announcement: "[PLAN] 94214"
        if (outputLine.StartsWith("[PLAN]", StringComparison.Ordinal))
        {
            string numPart = outputLine.AsSpan(6).Trim().ToString();
            if (long.TryParse(numPart.Replace(",", "").Replace(" ", ""), out long plan))
            {
                _plannedTotal = plan;
                RecalculateFromPlan();
            }
            return;
        }

        // Format: "[ XX.X%] [current/total] - Operation"
        int b1 = outputLine.IndexOf('[');
        if (b1 < 0) return;
        int b2 = outputLine.IndexOf('[', b1 + 1);
        if (b2 < 0) return;
        int dash = outputLine.IndexOf("] - ", b2, StringComparison.Ordinal);
        if (dash < 0) return;

        string counts = outputLine.Substring(b2 + 1, dash - b2 - 1).Trim();
        int slash = counts.IndexOf('/');
        if (slash < 0) return;

        string cur = counts[..slash].Trim().Replace(",", "").Replace(" ", "");
        string tot = counts[(slash + 1)..].Trim().Replace(",", "").Replace(" ", "");

        if (!long.TryParse(cur, out long current) || !long.TryParse(tot, out long total) || total <= 0)
            return;

        string op = outputLine[(dash + 4)..].Trim();
        string operationKey = GetOperationKey(op);

        if (operationKey != _currentOperation)
        {
            FinalizeOperation();
            _currentOperation = operationKey;
        }

        if (_operationTotals.TryGetValue(operationKey, out long existing) && total > existing)
        {
            _totalSeen += (total - existing);
        }
        else if (!_operationTotals.ContainsKey(operationKey))
        {
            _totalSeen += total;
        }

        _operationTotals[operationKey] = total;
        _currentCount = current;
        UpdateMax();
    }

    private void UpdateMax()
    {
        long completed = _completedSum + _currentCount;

        long denominator;
        if (_plannedTotal > 0)
        {
            // Backend gave us a real total - use it unless later phase totals prove larger.
            denominator = Math.Max(_plannedTotal, _totalSeen);
        }
        else
        {
            // Before backend announces the plan, use dynamic estimate
            long total = _totalSeen;
            denominator = Math.Max(total, completed * 2);
        }

        if (denominator <= 0) return;
        double pct = (double)completed / denominator * 100.0;
        if (_plannedTotal > 0)
        {
            _maxPercent = pct;
        }
        else if (pct > _maxPercent)
        {
            _maxPercent = pct;
        }
    }

    private void RecalculateFromPlan()
    {
        if (_plannedTotal <= 0) return;

        long completed = _completedSum + _currentCount;
        long denominator = Math.Max(_plannedTotal, _totalSeen);
        if (denominator <= 0) return;

        _maxPercent = (double)completed / denominator * 100.0;
    }

    private void FinalizeOperation()
    {
        if (_currentOperation.Length == 0) return;
        if (!_operationTotals.TryGetValue(_currentOperation, out long total)) return;
        _completedSum += total;
        _currentOperation = "";
        _currentCount = 0;
    }

    private static string GetOperationKey(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return "";

        int colon = operation.IndexOf(':');
        string key = colon > 0
            ? operation[..colon]
            : operation;

        return key.Trim().TrimEnd('.', '…').Trim();
    }

    public void Reset()
    {
        _operationTotals.Clear();
        _completedSum = 0;
        _totalSeen = 0;
        _currentOperation = "";
        _currentCount = 0;
        _maxPercent = 0;
        _plannedTotal = 0;
    }
}
