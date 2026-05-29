using System.Diagnostics;
using System.Text;

namespace AssetSplitterUI.Services;

/// <summary>Starts and monitors <c>AssetProcessor.exe</c> as a child process for the GUI.</summary>
public sealed class GuiProcessRunner
{
    public async Task Run(
        AssetProcessorRunConfig config,
        string uiLanguage,
        CancellationToken cancellationToken,
        Action<string>? outputCallback = null,
        Action<Process>? onProcessStarted = null)
    {
        List<string> source = BuildArguments(config, uiLanguage);

        string assetProcessorExePath = ResolveAssetProcessorPath();
        ProcessStartInfo startInfo = CreateStartInfo(assetProcessorExePath, source);

        Process? process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start AssetProcessor process.");

        onProcessStarted?.Invoke(process);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        cancellationToken = linkedCts.Token;

        try
        {
            StringBuilder stdoutBuilder = new();
            var stderrBuilder = new StringBuilder();
            Lock stdoutLock = new();
            Lock stderrLock = new();

            Task stdoutTask = StartOutputReader(
                process.StandardOutput, stdoutBuilder, stdoutLock,
                outputPrefix: "", readErrorPrefix: "Error reading output: ", outputCallback);

            Task stderrTask = StartOutputReader(
                process.StandardError, stderrBuilder, stderrLock,
                outputPrefix: "ERROR: ", readErrorPrefix: "Error reading error output: ", outputCallback);

            bool wasCancelled = await WaitForExitOrCancellationAsync(process, linkedCts.Token, outputCallback);

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                outputCallback?.Invoke("ERROR: Backend process exceeded hard timeout (30 minutes) and was terminated.");

            try
            {
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (Exception ex)
            {
                outputCallback?.Invoke("Warning: Output reader error: " + ex.Message);
            }

            EnsureSuccessfulExit(process, wasCancelled, cancellationToken, stdoutBuilder, stdoutLock, stderrBuilder, stderrLock);
        }
        finally
        {
            process?.Dispose();
        }
    }

    public static List<string> BuildArguments(AssetProcessorRunConfig config, string uiLanguage)
    {
        List<string> source = [config.GamePath, config.OutputPath, config.Language];
        if (config.AddComments) source.Add("-c");
        if (config.FixDependencies) source.Add("-f");
        if (config.CreateTemplateFolders) source.Add("-t");
        source.Add("-y");
        if (config.DebugMode) source.Add("-d");
        if (!config.ModOpsWrap && !config.CreateAssetMods) source.Add("--no-modops-wrap");
        if (!config.IncludeDefaultProperties) source.Add("--no-default-properties");
        if (config.SplitTemplates) source.Add("--split-templates");
        if (config.CreateAssetMods) source.Add("--create-asset-mods");
        source.Add("-l:" + uiLanguage.ToLowerInvariant());
        if (!string.IsNullOrEmpty(config.SingleGuid)) source.Add("-g:" + config.SingleGuid);
        return source;
    }

    private static string ResolveAssetProcessorPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string tfm = AppContext.TargetFrameworkName ?? "net10.0";
#if DEBUG
        string config = "Debug";
#else
        string config = "Release";
#endif

        var candidates = new List<string>
        {
            Path.Combine(baseDir, "AssetProcessor.exe"),
            Path.Combine(AppContext.BaseDirectory, "AssetProcessor.exe"),
            Path.Combine(baseDir, "..", "AssetProcessor.exe"),
        };

        string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(devRoot, "src", "AssetSplitter", "bin", config, tfm, "AssetProcessor.exe"));

        string altDevRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(altDevRoot, "src", "AssetSplitter", "bin", config, tfm, "AssetProcessor.exe"));

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
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
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static Task StartOutputReader(
        TextReader reader, StringBuilder output, Lock syncRoot,
        string outputPrefix, string readErrorPrefix, Action<string>? outputCallback)
    {
        return Task.Run((Action)(() =>
        {
            try
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lock (syncRoot) output.AppendLine(line);
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
        Process process, CancellationToken cancellationToken, Action<string>? outputCallback)
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
                    process.Kill(true);
                    process.WaitForExit(2000);
                }
            }
            catch (Exception ex)
            {
                outputCallback?.Invoke("Warning: Could not kill process during cancellation: " + ex.Message);
            }

            return true;
        }
    }

    private static void EnsureSuccessfulExit(
        Process process, bool wasCancelled, CancellationToken cancellationToken,
        StringBuilder stdoutBuilder, Lock stdoutLock, StringBuilder stderrBuilder, Lock stderrLock)
    {
        if (wasCancelled) cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode == 0) return;

        string stderrOutput, stdoutOutput;
        lock (stderrLock) stderrOutput = stderrBuilder.ToString();
        lock (stdoutLock) stdoutOutput = stdoutBuilder.ToString();

        string errorMessage = string.IsNullOrEmpty(stderrOutput) ? stdoutOutput : stderrOutput;
        if (string.IsNullOrEmpty(errorMessage))
            errorMessage = $"AssetSplit process exited with code {process.ExitCode}";
        throw new InvalidOperationException("Asset extraction failed: " + errorMessage.Trim());
    }
}
