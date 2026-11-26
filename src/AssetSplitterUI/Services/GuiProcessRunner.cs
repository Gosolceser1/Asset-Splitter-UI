using System.Diagnostics;
using System.Globalization;
using System.Text;
using AssetSplitterUI.Localization;

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

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(StringResourceManager.Instance.GetString("consoleMessages.processStartFailed"));
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
                outputPrefix: "", readErrorKey: "consoleMessages.errorReadingStdout", outputCallback,
                onLineRead: () =>
                {
                    try
                    {
                        inactivityCts.CancelAfter(BackendInactivityTimeout);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Run() disposed the inactivity timer after readers were asked to stop.
                    }
                });

            Task stderrTask = StartOutputReader(
                process.StandardError, stderrBuilder, stderrLock,
                outputPrefix: StringResourceManager.Instance.GetString("consoleMessages.errorPrefix") + " ",
                readErrorKey: "consoleMessages.errorReadingStderr", outputCallback,
                onLineRead: () =>
                {
                    try
                    {
                        inactivityCts.CancelAfter(BackendInactivityTimeout);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Run() disposed the inactivity timer after readers were asked to stop.
                    }
                });

            bool wasCancelled = await WaitForExitOrCancellationAsync(
                process,
                outputCallback,
                runCancellationToken,
                // User cancel: the cancel signal file was written; let the backend stop at its next
                // checkpoint (exit 130) instead of killing it mid-write. Inactivity timeout: kill now.
                () => userCancellationToken.IsCancellationRequested ? CooperativeCancelGracePeriod : TimeSpan.Zero);
            bool timedOut = inactivityCts.IsCancellationRequested && !userCancellationToken.IsCancellationRequested;

            if (timedOut)
            {
                outputCallback?.Invoke(
                    StringResourceManager.Instance.GetString("consoleMessages.errorPrefix") + " "
                    + StringResourceManager.Instance.GetString("consoleMessages.backendInactivityTimeout"));
            }

            await WaitForReadersAsync(process, stdoutTask, stderrTask);

            if (timedOut)
            {
                throw new InvalidOperationException(
                    StringResourceManager.Instance.GetString("consoleMessages.backendInactivityTimeout"));
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
        if (config.ForceFullRebuild)
        {
            source.Add("--force-full-rebuild");
        }
        if (!string.IsNullOrEmpty(config.SingleGuid))
        {
            if (!config.SingleGuid.All(char.IsDigit))
            {
                throw new ArgumentException("GUIDs must be numeric.", nameof(config));
            }

            source.Add("-g:" + config.SingleGuid);
        }

        if (config.DebugMode)
        {
            source.Add("-d");
        }
        source.Add("-l:" + uiLanguage.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(config.ReadmeLanguage))
        {
            string readmeLanguage = config.ReadmeLanguage.Trim().ToLowerInvariant();
            if (readmeLanguage.Contains('.', StringComparison.Ordinal)
                || readmeLanguage.Contains('/', StringComparison.Ordinal)
                || readmeLanguage.Contains('\\', StringComparison.Ordinal)
                || readmeLanguage.Contains("..", StringComparison.Ordinal)
                || !readmeLanguage.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                throw new ArgumentException("Invalid readme language.", nameof(config));
            }

            source.Add("--readme-lang:" + readmeLanguage);
        }
        return source;
    }

    private static string ResolveAssetProcessorPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string tfmFolder = ResolveOutputTfmFolder();
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
        candidates.Add(Path.Combine(devRoot, "src", "AssetSplitter", "bin", Config, tfmFolder, "AssetProcessor.exe"));

        string altDevRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(altDevRoot, "src", "AssetSplitter", "bin", Config, tfmFolder, "AssetProcessor.exe"));

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        string attempted = string.Join(Environment.NewLine, candidates);
        throw new FileNotFoundException(
            string.Format(
                CultureInfo.InvariantCulture,
                StringResourceManager.Instance.GetString("consoleMessages.assetProcessorNotFound"),
                attempted),
            candidates[0]);
    }

    internal static string ResolveOutputTfmFolder()
    {
        const string prefix = ".NETCoreApp,Version=v";
        string? name = AppContext.TargetFrameworkName;
        if (!string.IsNullOrEmpty(name) && name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return "net" + name[prefix.Length..];
        }

        return "net10.0";
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
        string outputPrefix, string readErrorKey, Action<string>? outputCallback,
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
                outputCallback?.Invoke(
                    StringResourceManager.Instance.GetString(readErrorKey) + " " + ex.Message);
            }
        }));
    }

    /// <summary>How long a user-cancelled backend may take to exit cooperatively before it is killed.</summary>
    private static readonly TimeSpan CooperativeCancelGracePeriod = TimeSpan.FromSeconds(8);

    private static async ValueTask<bool> WaitForExitOrCancellationAsync(
        Process process,
        Action<string>? outputCallback,
        CancellationToken cancellationToken,
        Func<TimeSpan> getGracePeriod)
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
                TimeSpan grace = getGracePeriod();
                if (grace > TimeSpan.Zero && !process.HasExited)
                {
                    using var graceCts = new CancellationTokenSource(grace);
                    try
                    {
                        await process.WaitForExitAsync(graceCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Backend did not stop in time; fall through to the hard kill.
                    }
                }

                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                outputCallback?.Invoke(
                    StringResourceManager.Instance.GetString("consoleMessages.cancelKillWarning") + " " + ex.Message);
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
        // Backend uses exit 130 for cooperative cancel; also treat an already-requested
        // cancellation as cancel even if WaitForExit raced ahead of wasCancelled.
        const int cancelledExitCode = 130;
        bool treatAsCancelled = wasCancelled
            || cancellationToken.IsCancellationRequested
            || process.ExitCode == cancelledExitCode;
        if (treatAsCancelled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new OperationCanceledException();
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
            errorMessage = string.Format(
                CultureInfo.InvariantCulture,
                StringResourceManager.Instance.GetString("consoleMessages.processExitCode"),
                process.ExitCode);
        }

        throw new InvalidOperationException(
            StringResourceManager.Instance.GetString("consoleMessages.extractionFailedPrefix") + errorMessage.Trim());
    }
}
