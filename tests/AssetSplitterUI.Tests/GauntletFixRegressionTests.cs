using System.Reflection;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class GauntletFixRegressionTests
{
    [Fact]
    public void ResolveOutputTfmFolder_IsNetTfmNotFrameworkDisplayName()
    {
        string folder = GuiProcessRunner.ResolveOutputTfmFolder();
        Assert.StartsWith("net", folder, StringComparison.Ordinal);
        Assert.DoesNotContain("NETCoreApp", folder, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=", folder, StringComparison.Ordinal);
        Assert.DoesNotContain(",", folder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PipelineProgressTracker_ConcurrentFeed_StaysConsistent()
    {
        var tracker = new PipelineProgressTracker();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        using var started = new ManualResetEventSlim(false);

        Task[] workers = Enumerable.Range(0, 8).Select(threadIndex => Task.Run(() =>
        {
            started.Wait();
            try
            {
                for (int n = 0; n < 4_000; n++)
                {
                    tracker.Feed($"[ 50.0%] [     {n % 100}/   100] - Op{threadIndex % 4}: item-{n}");
                    _ = tracker.OverallPercent;
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        })).ToArray();

        started.Set();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Empty(errors);
        Assert.InRange(tracker.OverallPercent, 0, 100);
    }

    [Fact]
    public async Task ExtractionCoordinator_RejectsOverlappingRun()
    {
        var coordinator = new ExtractionCoordinator();
        FieldInfo? running = typeof(ExtractionCoordinator).GetField("_running", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(running);
        running!.SetValue(coordinator, 1);

        var result = await coordinator.RunAsync(
            new AssetProcessorRunConfig
            {
                GamePath = "C:\\missing-game",
                OutputPath = Path.GetTempPath(),
                Language = "english",
            },
            _ => { },
            _ => { });

        Assert.Equal(ExtractionResult.StatusKind.Error, result.Status);
        Assert.Contains("already running", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindowLogStore_IgnoresAppendsAfterShutdown()
    {
        var store = new MainWindowLogStore(() => { });
        store.AppendRaw("before");
        store.BeginShutdown();
        store.AppendRaw("after-shutdown");
        Assert.DoesNotContain(store.Lines, line => line.Text.Contains("after-shutdown", StringComparison.Ordinal));
        Assert.Contains(store.Lines, line => line.Text.Contains("before", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildArguments_RejectsNonNumericSingleGuid()
    {
        var config = new AssetProcessorRunConfig
        {
            GamePath = @"C:\Games\Anno",
            OutputPath = @"C:\AnnoAssets",
            Language = "english",
            SingleGuid = "not-a-digit",
        };

        Assert.Throws<ArgumentException>(() => GuiProcessRunner.BuildArguments(config, "english"));
    }

    [Fact]
    public void BuildArguments_RejectsReadmeLanguagePathEscape()
    {
        var config = new AssetProcessorRunConfig
        {
            GamePath = @"C:\Games\Anno",
            OutputPath = @"C:\AnnoAssets",
            Language = "english",
            ReadmeLanguage = "../evil",
        };

        Assert.Throws<ArgumentException>(() => GuiProcessRunner.BuildArguments(config, "english"));
    }

    [Fact]
    public void BuildArguments_IncludesForceFullRebuild_WhenSet()
    {
        var config = new AssetProcessorRunConfig
        {
            GamePath = @"C:\Games\Anno",
            OutputPath = @"C:\AnnoAssets",
            Language = "english",
            ForceFullRebuild = true,
        };

        List<string> args = GuiProcessRunner.BuildArguments(config, "english");
        Assert.Contains("--force-full-rebuild", args);
    }

    [Fact]
    public void LocaleKeys_TemplatesIgnoredInSingleGuid_PresentInEnglish()
    {
        string languagesDir = Path.Combine(AppContext.BaseDirectory, "Localization", "Languages");
        if (!Directory.Exists(languagesDir))
        {
            // Walk up to repo Localization when not copied beside test output.
            string? dir = AppContext.BaseDirectory;
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "Localization", "Languages");
                if (File.Exists(Path.Combine(candidate, "Strings.json")))
                {
                    languagesDir = candidate;
                    break;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }
        }

        string json = File.ReadAllText(Path.Combine(languagesDir, "Strings.json"));
        Assert.Contains("templatesIgnoredInSingleGuid", json, StringComparison.Ordinal);
        Assert.Contains("splitTemplatesIgnoredInSingleGuid", json, StringComparison.Ordinal);
    }
}
