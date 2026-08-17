namespace AssetSplitterUI.Services;

public static class UILogger
{
    /// <summary>
    /// Optional action to write a visible message to the main console log.
    /// Set this once from the ViewModel / coordinator layer.
    /// </summary>
    public static Action<string>? VisibleLog { get; set; }

    public static void Debug(string source, string message) =>
        System.Diagnostics.Debug.WriteLine($"[{source}] {message}");

    public static void Debug(string source, Exception ex) =>
        System.Diagnostics.Debug.WriteLine($"[{source}] {ex.GetType().Name}: {ex.Message}");

    /// <summary>Writes a warning that is visible to the user in the console.</summary>
    public static void Warning(string source, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{source}] WARNING: {message}");
        VisibleLog?.Invoke($"⚠ {message}");
    }

    /// <summary>Writes an error that is visible to the user in the console.</summary>
    public static void Error(string source, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{source}] ERROR: {message}");
        VisibleLog?.Invoke($"✗ {message}");
    }

    public static void Error(string source, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[{source}] ERROR: {ex.GetType().Name}: {ex.Message}");
        VisibleLog?.Invoke($"✗ {source}: {ex.Message}");
    }
}
