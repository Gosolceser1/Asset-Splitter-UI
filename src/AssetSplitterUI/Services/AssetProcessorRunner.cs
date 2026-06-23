using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
namespace AssetSplitterUI.Services;

public sealed class AssetProcessorRunner
{
    private static readonly Lock _processLock = new();
    private static Process? _currentProcess;

    /// <summary>Kills the currently running backend process if one exists.</summary>
    /// <param name="maxWaitMilliseconds">
    /// How long to block waiting for exit after <see cref="Process.Kill"/>.
    /// Use 0 during UI shutdown so the window can close immediately; child cleanup is handled by
    /// <see cref="ChildProcessTracker"/> and a final kill on <see cref="AppDomain.ProcessExit"/>.
    /// </param>
    public static void TryKillCurrentProcess(int maxWaitMilliseconds = 500)
    {
        lock (_processLock)
        {
            Process? process = _currentProcess;
            try
            {
                if (process is { HasExited: false })
                {
                    TryCancelPipeReads(process);
                    process.Kill(true);

                    if (maxWaitMilliseconds > 0 && !process.WaitForExit(maxWaitMilliseconds))
                    {
                        try { process.Kill(true); }
                        catch (Exception ex) { UILogger.Debug(nameof(AssetProcessorRunner), "Kill retry failed: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception or ObjectDisposedException)
            {
                UILogger.Warning(nameof(AssetProcessorRunner), "Could not kill process: " + ex.Message);
            }

            _currentProcess = null;
        }
    }

    private static void TryCancelPipeReads(Process process)
    {
        try { process.CancelOutputRead(); }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { }

        try { process.CancelErrorRead(); }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { }
    }

    public static async Task RunAsync(
        AssetProcessorRunConfig config,
        Action<double> onProgress,
        Action<string> onLogLine,
        CancellationToken token)
    {
        string annoAssetsPath = Path.GetFileName(Path.TrimEndingDirectorySeparator(config.OutputPath)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? config.OutputPath
            : Path.Combine(config.OutputPath, "AnnoAssets");

        AssetProcessorRunConfig adaptedConfig = new()
        {
            GamePath = config.GamePath,
            OutputPath = annoAssetsPath,
            Language = config.Language,
            ConsoleLanguage = config.ConsoleLanguage,
            ReadmeLanguage = config.ReadmeLanguage,
            SingleGuid = config.SingleGuid,
            AddComments = config.AddComments,
            FixDependencies = config.FixDependencies,
            CreateTemplateFolders = config.CreateTemplateFolders,
            ModOpsWrap = config.ModOpsWrap,
            IncludeDefaultProperties = config.IncludeDefaultProperties,
            SplitTemplates = config.SplitTemplates,
            CreateAssetMods = config.CreateAssetMods,
            DebugMode = config.DebugMode,
            SourceExtractionOnly = config.SourceExtractionOnly
        };

        // Always run the backend in English so the GUI's ConsoleOutputLocalizer can match
        // English patterns and localize output to the current UI language.  If the backend
        // emitted already-localized text, language switching couldn't re-localize it.
        const string backendConsoleLanguage = "english";

        PipelineProgressTracker progressTracker = new();
        Process? startedProcess = null;
        try
        {
            await GuiProcessRunner.Run(
                adaptedConfig,
                backendConsoleLanguage,
                outputLine =>
                {
                    progressTracker.Feed(outputLine);
                    double percent = progressTracker.OverallPercent;
                    if (percent > 0)
                    {
                        onProgress(percent);
                    }

                    if (outputLine != null)
                    {
                        onLogLine(outputLine);
                    }
                },
                onProcessStarted: process =>
                {
                    lock (_processLock)
                    {
                        startedProcess = process;
                        _currentProcess = process;
                    }
                    try { ChildProcessTracker.AddProcess(process); }
                    catch (InvalidOperationException) { /* process already exited */ }
                },
                cancellationToken: token);
        }
        finally
        {
            lock (_processLock)
            {
                if (ReferenceEquals(_currentProcess, startedProcess))
                {
                    _currentProcess = null;
                }
            }
        }
    }
}
