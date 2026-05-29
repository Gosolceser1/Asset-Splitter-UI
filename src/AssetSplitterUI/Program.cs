using Avalonia;

namespace AssetSplitterUI;

/// <summary>Desktop entry point. Configures and starts the Avalonia app with classic desktop lifetime (single MainWindow).</summary>
class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Configures Avalonia app (platform, font, logging). Used by designer and startup.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
