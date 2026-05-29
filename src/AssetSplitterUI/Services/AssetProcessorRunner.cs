using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
namespace AssetSplitterUI.Services;

public sealed class AssetProcessorRunner
{
    private static readonly Lock _processLock = new();
    private static Process? _currentProcess;

    public static void TryKillCurrentProcess()
    {
        lock (_processLock)
        {
            var process = _currentProcess;
            if (process is { HasExited: false })
            {
                try
                {
                    process.Kill(true);

                    // More robust wait: up to 4 seconds total with extra final kill
                    if (!process.WaitForExit(4000))
                    {
                        if (!process.HasExited)
                        {
                            try { process.Kill(true); } catch (Exception ex) { UILogger.Debug(nameof(AssetProcessorRunner), "Kill attempt failed: " + ex.Message); }
                            process.WaitForExit(1500);
                        }
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception or ObjectDisposedException)
                {
                    UILogger.Warning(nameof(AssetProcessorRunner), "Could not kill process: " + ex.Message);
                }
            }
            _currentProcess = null;
        }
    }

    public Task RunAsync(
        AssetProcessorRunConfig config,
        CancellationToken token,
        Action<double> onProgress,
        Action<string> onLogLine)
    {
        var annoAssetsPath = Path.GetFileName(Path.TrimEndingDirectorySeparator(config.OutputPath)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? config.OutputPath
            : Path.Combine(config.OutputPath, "AnnoAssets");

        Directory.CreateDirectory(annoAssetsPath);

        var adaptedConfig = new AssetProcessorRunConfig
        {
            GamePath = config.GamePath,
            OutputPath = annoAssetsPath,
            Language = config.Language,
            ConsoleLanguage = config.ConsoleLanguage,
            SingleGuid = config.SingleGuid,
            AddComments = config.AddComments,
            FixDependencies = config.FixDependencies,
            CreateTemplateFolders = config.CreateTemplateFolders,
            ModOpsWrap = config.ModOpsWrap,
            IncludeDefaultProperties = config.IncludeDefaultProperties,
            SplitTemplates = config.SplitTemplates,
            CreateAssetMods = config.CreateAssetMods,
            DebugMode = config.DebugMode
        };

        string backendConsoleLanguage = !string.IsNullOrWhiteSpace(config.ConsoleLanguage) ? config.ConsoleLanguage : "english";

        var runner = new GuiProcessRunner();
        var progressTracker = new PipelineProgressTracker();
        return runner.Run(
            adaptedConfig,
            backendConsoleLanguage,
            token,
            outputLine =>
            {
                progressTracker.Feed(outputLine);
                double percent = progressTracker.OverallPercent;
                if (percent > 0)
                    onProgress(percent);

                if (outputLine != null)
                    onLogLine(outputLine);
            },
            onProcessStarted: process =>
            {
                lock (_processLock)
                {
                    _currentProcess = process;
                }
                try { ChildProcessTracker.AddProcess(process); }
                catch (InvalidOperationException) { /* process already exited */ }
            });
    }
}
