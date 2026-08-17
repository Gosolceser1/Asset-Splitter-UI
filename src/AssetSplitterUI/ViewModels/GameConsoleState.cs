namespace AssetSplitterUI.ViewModels;

/// <summary>Snapshot of console and run state for one game path.</summary>
public sealed class GameConsoleState
{
    /// <summary>Log lines to restore when switching back to this game.</summary>
    public List<LogLine> LogLines { get; set; } = [];

    /// <summary>Last progress value from 0 to 100.</summary>
    public double Progress { get; set; }

    /// <summary>True when extraction or processing completed for this game.</summary>
    public bool IsComplete { get; set; }

    /// <summary>True when Phase 1 RDA extraction is done for this game.</summary>
    public bool Phase1Complete { get; set; }

    /// <summary>Last status line text.</summary>
    public string StatusText { get; set; } = "";

    /// <summary>True when status indicates an error.</summary>
    public bool StatusIsError { get; set; }

    /// <summary>Localization key for status, when applicable.</summary>
    public string? StatusTextKey { get; set; }

    /// <summary>Format arguments for <see cref="StatusTextKey"/>.</summary>
    public object[]? StatusTextArgs { get; set; }
}
