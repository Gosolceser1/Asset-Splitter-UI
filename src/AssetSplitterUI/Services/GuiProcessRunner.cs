using System.Diagnostics;
using System.Text;

namespace AssetSplitterUI.Services;

/// <summary>Starts and monitors <c>AssetProcessor.exe</c> as a child process for the GUI.</summary>
public sealed class GuiProcessRunner
{
    private static readonly TimeSpan BackendInactivityTimeout = TimeSpan.FromMinutes(60);

    public static async Task Run(
        AssetProcessorRunConfig config,
        string uiLanguage,
        Action<string>? outputCallback = null,
        Action<Process>? onProcessStarted = null,
        CancellationToken cancellationToken = default)
    {
        List<string> source = BuildArguments(config, uiLanguage);

        string assetProcessorExePath = ResolveAssetProcessorPath();
        ProcessStartInfo startInfo = CreateStartInfo(assetProcessorExePath, source);

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AssetProcessor process.");
        onProcessStarted?.Invoke(process);

        CancellationToken userCancellationToken = cancellationToken;
        using var inactivityCts = new CancellationTokenSource(BackendInactivityTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken, inactivityCts.Token);
        CancellationToken runCancellationToken = linkedCts.Token;

        try
        {
            StringBuilder stdoutBuilder = new();
            var stderrBuilder = new StringBuilder();
            Lock stdoutLock = new();
            Lock stderrLock = new();

            Task stdoutTask = StartOutputReader(
                process.StandardOutput, stdoutBuilder, stdoutLock,
                outputPrefix: "", readErrorPrefix: "Error reading output: ", outputCallback,
                onLineRead: () => inactivityCts.CancelAfter(BackendInactivityTimeout));

            Task stderrTask = StartOutputReader(
                process.StandardError, stderrBuilder, stderrLock,
                outputPrefix: "ERROR: ", readErrorPrefix: "Error reading error output: ", outputCallback,
                onLineRead: () => inactivityCts.CancelAfter(BackendInactivityTimeout));

            bool wasCancelled = await WaitForExitOrCancellationAsync(process, outputCallback, runCancellationToken);
            bool timedOut = inactivityCts.IsCancellationRequested && !userCancellationToken.IsCancellationRequested;

            if (timedOut)
            {
                outputCallback?.Invoke("ERROR: Backend process produced no output for 60 minutes and was terminated.");
            }

            await WaitForReadersAsync(process, stdoutTask, stderrTask);

            if (timedOut)
            {
                throw new InvalidOperationException("Backend process produced no output for 60 minutes and was terminated.");
            }

            EnsureSuccessfulExit(process, wasCancelled, stdoutBuilder, stdoutLock, stderrBuilder, stderrLock, userCancellationToken);
        }
        finally
        {
            process?.Dispose();
        }
    }

    public static List<string> BuildArguments(AssetProcessorRunConfig config, string uiLanguage)
    {
        List<string> source = [config.GamePath, config.OutputPath, config.Language];
        if (config.AddComments)
        {
            source.Add("-c");
        }
        if (config.FixDependencies)
        {
            source.Add("-f");
        }
        if (config.CreateTemplateFolders)
        {
            source.Add("-t");
        }
        if (!config.ModOpsWrap)
        {
            source.Add("--no-modops-wrap");
        }
        if (!config.IncludeDefaultProperties)
        {
            source.Add("--no-default-properties");
        }
        if (config.SplitTemplates)
        {
            source.Add("--split-templates");
        }
        if (config.CreateAssetMods)
        {
            source.Add("--create-asset-mods");
        }
        if (config.SourceExtractionOnly)
        {
            source.Add("--source-extraction-only");
        }
        if (!string.IsNullOrEmpty(config.SingleGuid))
        {
            source.Add("-g:" + config.SingleGuid);
        }

        if (config.DebugMode)
        {
            source.Add("-d");
        }
        source.Add("-l:" + uiLanguage.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(config.ReadmeLanguage))
        {
            source.Add("--readme-lang:" + config.ReadmeLanguage.ToLowerInvariant());
        }
        return source;
    }

    private static string ResolveAssetProcessorPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string tfm = AppContext.TargetFrameworkName ?? "net10.0";
#if DEBUG
        const string Config = "Debug";
#else
        const string Config = "Release";
#endif

        List<string> candidates =
        [
            Path.Combine(baseDir, "AssetProcessor.exe"),
            Path.Combine(AppContext.BaseDirectory, "AssetProcessor.exe"),
            Path.Combine(baseDir, "..", "AssetProcessor.exe"),
        ];

        string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(devRoot, "src", "AssetSplitter", "bin", Config, tfm, "AssetProcessor.exe"));

        string altDevRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(altDevRoot, "src", "AssetSplitter", "bin", Config, tfm, "AssetProcessor.exe"));

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        string attempted = string.Join(Environment.NewLine, candidates);
        throw new FileNotFoundException(
            $"Could not find AssetProcessor.exe. Searched the following locations:\n{attempted}",
            candidates[0]);
    }

    private static ProcessStartInfo CreateStartInfo(string assetProcessorExePath, IEnumerable<string> arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = assetProcessorExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(assetProcessorExePath) ?? "",
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return startInfo;
    }

    private static Task StartOutputReader(
        TextReader reader, StringBuilder output, Lock syncRoot,
        string outputPrefix, string readErrorPrefix, Action<string>? outputCallback,
        Action? onLineRead = null)
    {
        return Task.Run((Action)(() =>
        {
            try
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lock (syncRoot)
                    {
                        output.AppendLine(line);
                    }
                    onLineRead?.Invoke();
                    outputCallback?.Invoke(outputPrefix + line);
                }
            }
            catch (Exception ex)
            {
                outputCallback?.Invoke(readErrorPrefix + ex.Message);
            }
        }));
    }

    private static async ValueTask<bool> WaitForExitOrCancellationAsync(
        Process process, Action<string>? outputCallback, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return false;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    TryCancelPipeReads(process);
                    process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                outputCallback?.Invoke("Warning: Could not kill process during cancellation: " + ex.Message);
            }

            return true;
        }
    }

    private static async Task WaitForReadersAsync(Process process, Task stdoutTask, Task stderrTask)
    {
        try
        {
            var allReaders = Task.WhenAll(stdoutTask, stderrTask);
            if (await Task.WhenAny(allReaders, Task.Delay(TimeSpan.FromSeconds(2))) == allReaders)
            {
                await allReaders;
            }
            else
            {
                TryCancelPipeReads(process);
            }
        }
        catch (Exception ex)
        {
            // Reader tasks exit when pipes are cancelled or the process dies.
            Debug.WriteLine(ex);
        }
    }

    private static void TryCancelPipeReads(Process process)
    {
        try { process.CancelOutputRead(); }
        catch (Exception ex)
        {
            // Best-effort cancellation; pipe may already be closed.
            Debug.WriteLine(ex);
        }

        try { process.CancelErrorRead(); }
        catch (Exception ex)
        {
            // Best-effort cancellation; pipe may already be closed.
            Debug.WriteLine(ex);
        }
    }

    private static void EnsureSuccessfulExit(
        Process process, bool wasCancelled,
        StringBuilder stdoutBuilder, Lock stdoutLock, StringBuilder stderrBuilder, Lock stderrLock,
        CancellationToken cancellationToken)
    {
        if (wasCancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (process.ExitCode == 0)
        {
            return;
        }

        string stderrOutput, stdoutOutput;
        lock (stderrLock)
        {
            stderrOutput = stderrBuilder.ToString();
        }
        lock (stdoutLock)
        {
            stdoutOutput = stdoutBuilder.ToString();
        }

        string errorMessage = string.IsNullOrEmpty(stderrOutput) ? stdoutOutput : stderrOutput;
        if (string.IsNullOrEmpty(errorMessage))
        {
            errorMessage = $"AssetSplit process exited with code {process.ExitCode}";
        }
        throw new InvalidOperationException("Asset extraction failed: " + errorMessage.Trim());
    }
}
