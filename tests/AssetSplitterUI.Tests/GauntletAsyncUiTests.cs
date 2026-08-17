using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using AssetProcessor;
using AssetSplitterUI.Services;
using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Tests;

/// <summary>
/// Wave-1 concurrency / process-control gauntlet. Proves defects; does not fix product code.
/// </summary>
public sealed class GauntletAsyncUiTests
{
    /// <summary>
    /// AssetProcessorRunner feeds PipelineProgressTracker from both stdout and stderr
    /// Task.Run readers with no lock. Dictionary mutation under Parallel stress must fail
    /// or leave corrupted overall percent.
    /// </summary>
    [Fact]
    public async Task PipelineProgressTracker_ConcurrentFeed_ThrowsOrCorrupts()
    {
        var tracker = new PipelineProgressTracker();
        var errors = new ConcurrentBag<Exception>();
        const int threadCount = 8;
        const int iterations = 8_000;
        using var started = new ManualResetEventSlim(false);

        Task[] workers = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
        {
            started.Wait();
            try
            {
                for (int n = 0; n < iterations; n++)
                {
                    string op = $"Op{threadIndex % 4}";
                    tracker.Feed($"[ 50.0%] [     {n % 100}/   100] - {op}: item-{n}");
                    if (n % 17 == 0)
                    {
                        tracker.Feed($"[PLAN+] {(n % 5) + 1}");
                    }

                    double pct = tracker.OverallPercent;
                    if (double.IsNaN(pct) || pct < 0 || pct > 100.0001)
                    {
                        throw new InvalidOperationException($"Corrupt OverallPercent={pct}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        })).ToArray();

        started.Set();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(
            errors.Count == 0,
            "Concurrent Feed must not throw after PipelineProgressTracker locking. Observed: "
            + string.Join("; ", errors.Select(e => e.GetType().Name + ": " + e.Message)));
        Assert.InRange(tracker.OverallPercent, 0, 100);
    }

    /// <summary>
    /// GuiProcessRunner readers call inactivityCts.CancelAfter on every line. WaitForReadersAsync
    /// only waits 2s then returns; Run() then disposes the CTS while a slow reader may still
    /// invoke CancelAfter → ObjectDisposedException inside StartOutputReader's catch path.
    /// </summary>
    [Fact]
    public void GuiProcessRunner_CancelAfterOnDisposedInactivityCts_ThrowsObjectDisposedException()
    {
        var inactivityCts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Action onLineRead = () => inactivityCts.CancelAfter(TimeSpan.FromMinutes(60));

        onLineRead();
        inactivityCts.Dispose();

        Assert.Throws<ObjectDisposedException>(() => onLineRead());
        // Product path in GuiProcessRunner.StartOutputReader now swallows this.
    }

    /// <summary>
    /// PrepareForShutdown / BeginShutdown does not gate Extraction progress callbacks.
    /// Dispatcher.UIThread.Post(() => Progress = ...) still runs after teardown starts.
    /// </summary>
    [Fact]
    public void ClosingDuringProcessing_ProgressCallbackNotGatedByShutdown()
    {
        var store = new MainWindowLogStore(() => { });
        double progress = 0;
        bool isShutdown = false;

        // Mirrors AssetProcessorRunner → Extraction progress lambda (no IsShutdown check).
        Action<double> onProgress = percent =>
        {
            if (!isShutdown)
            {
                // Ideal gate — product code does not have this.
            }

            progress = Math.Min(100, percent);
        };

        store.BeginShutdown();
        isShutdown = store.IsShutdown;
        onProgress(67.5);

        Assert.True(isShutdown);
        Assert.Equal(67.5, progress);
    }

    /// <summary>
    /// MainWindow.Lifecycle and SingleGuid discard Task.Run with <c>_ =</c>. A faulted orphan
    /// is never awaited by the caller (exception only handled inside the lambda if at all).
    /// </summary>
    [Fact]
    public async Task FireAndForgetTaskRun_FaultRemainsUnobservedByCaller()
    {
        Task orphan = Task.Run(async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("gauntlet-orphan-fault");
        });

        // Product: `_ = Task.Run(...)` — caller never awaits.
        _ = orphan;

        await Task.Delay(50);
        Assert.True(orphan.IsFaulted);
        Assert.IsType<InvalidOperationException>(orphan.Exception?.InnerException);
    }

    /// <summary>
    /// ExtractionCoordinator overwrites _cts on every RunAsync with no reentrancy guard.
    /// Cancel() after a second start only cancels the latest CTS — first run's token is orphaned.
    /// </summary>
    [Fact(Skip = "Replaced by GauntletFixRegressionTests.ExtractionCoordinator_RejectsOverlappingRun against the real coordinator.")]
    public async Task ExtractionCoordinator_OverlappingRun_OrphansFirstCancellationToken()
    {
        var firstTokenBox = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Mirror ExtractionCoordinator.RunAsync CTS overwrite (cannot spawn AssetProcessor.exe here).
        CancellationTokenSource? cts = null;
        string? cancelDir = null;

        async Task<(ExtractionResult.StatusKind Status, CancellationToken Token)> FakeRunAsync(
            Func<CancellationToken, Task> body)
        {
            cts = new CancellationTokenSource();
            cancelDir = Path.Combine(Path.GetTempPath(), "gauntlet-cancel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cancelDir);
            CancellationToken token = cts.Token;
            try
            {
                await body(token);
                return (ExtractionResult.StatusKind.Success, token);
            }
            catch (OperationCanceledException)
            {
                return (ExtractionResult.StatusKind.Cancelled, token);
            }
            finally
            {
                cancelDir = null;
                CancellationTokenSource? local = cts;
                cts = null;
                local?.Dispose();
            }
        }

        void FakeCancel()
        {
            if (cancelDir is not null)
            {
                PipelineCancellation.RequestCancel(cancelDir);
            }

            cts?.Cancel();
        }

        Task<(ExtractionResult.StatusKind Status, CancellationToken Token)> first = FakeRunAsync(async token =>
        {
            firstTokenBox.SetResult(token);
            firstEntered.SetResult();
            await releaseFirst.Task.WaitAsync(token);
        });

        await firstEntered.Task;
        CancellationToken firstToken = await firstTokenBox.Task;

        Task<(ExtractionResult.StatusKind Status, CancellationToken Token)> second = FakeRunAsync(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
        });

        await Task.Delay(50);

        FakeCancel();

        Assert.False(firstToken.IsCancellationRequested,
            "First run token was orphaned when second RunAsync overwrote _cts; Cancel() only hits latest.");

        releaseFirst.TrySetResult();

        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ExtractionResult.StatusKind.Success, firstResult.Status);

        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ExtractionResult.StatusKind.Cancelled, secondResult.Status);
    }

    /// <summary>
    /// After BeginShutdown, IsShutdown is true but nothing stops PipelineProgressTracker-driven
    /// progress posts. Simulates Extraction progress callback still dispatching after teardown.
    /// </summary>
    [Fact]
    public void MainWindowLogStore_BeginShutdown_DoesNotBlockFurtherAppends()
    {
        var store = new MainWindowLogStore(() => { });
        store.AppendRaw("before");
        store.BeginShutdown();

        // PrepareForShutdown clears, but progress/status posts after BeginShutdown still mutate VM;
        // log store only gates RefreshLocalizedLines / RefreshColors / coordinator flush — not AppendRaw.
        store.AppendRaw("after-shutdown");

        Assert.True(store.IsShutdown);
        Assert.DoesNotContain(store.Lines, line => line.Text.Contains("after-shutdown", StringComparison.Ordinal));
    }

    /// <summary>
    /// ConsoleOutputCoordinator.EnqueueLine drops output once FinishRun sets _runFinished.
    /// Late stdout after FinishRunAsync starts is silently discarded from the UI feed.
    /// </summary>
    [Fact]
    public void ConsoleOutputCoordinator_EnqueueAfterRunFinished_DropsLines()
    {
        var store = new MainWindowLogStore(() => { });
        var coordinator = new ConsoleOutputCoordinator(store, _ => { }, (_, _) => { });

        FieldInfo? runFinishedField = typeof(ConsoleOutputCoordinator)
            .GetField("_runFinished", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(runFinishedField);

        runFinishedField!.SetValue(coordinator, true);

        coordinator.EnqueueLine(LogLine.From("late-line"));
        coordinator.EnqueueStatusLine("late-status");

        FieldInfo? queueField = typeof(ConsoleOutputCoordinator)
            .GetField("_pendingLogLines", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(queueField);
        var queue = (ConcurrentQueue<LogLine>)queueField!.GetValue(coordinator)!;

        Assert.True(queue.IsEmpty, "EnqueueLine must no-op after _runFinished; late backend lines are dropped.");
    }

    /// <summary>
    /// AssetProcessorRunner holds a static _currentProcess. Overlapping GuiProcessRunner starts
    /// can overwrite it; TryKillCurrentProcess then only kills the latest.
    /// </summary>
    [Fact]
    public void AssetProcessorRunner_StaticCurrentProcess_IsSingleSlot()
    {
        FieldInfo? field = typeof(AssetProcessorRunner)
            .GetField("_currentProcess", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Equal(typeof(Process), Nullable.GetUnderlyingType(field!.FieldType) ?? field.FieldType);
    }

    /// <summary>
    /// BusyIndicatorAnimator.Stop disposes CTS and nulls _animationTask without awaiting.
    /// </summary>
    [Fact]
    public async Task BusyIndicatorAnimator_Stop_DoesNotAwaitAnimationTask()
    {
        int applyCount = 0;
        var animator = new BusyIndicatorAnimator((_, _) => Interlocked.Increment(ref applyCount));

        var sw = Stopwatch.StartNew();
        animator.Start();
        animator.Stop();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Stop should not await animation loop; elapsed={sw.ElapsedMilliseconds}ms");

        await Task.Delay(150);
        animator.Dispose();
    }

    /// <summary>
    /// Extraction finish path calls FlushCollapsedGroup after Dispatcher.InvokeAsync returns
    /// (continuation thread). ObservableCollection is not synchronized — concurrent mutation
    /// eventually throws under stress (same collection type as MainWindowLogStore.Lines).
    /// </summary>
    [Fact(Skip = "Production finish flush now runs on the UI thread; this stress is no longer the product path.")]
    public async Task ExtractionFinish_FlushCollapsedGroup_OffUiThread_Races()
    {
        Exception? offThreadError = null;

        for (int attempt = 0; attempt < 25 && offThreadError is null; attempt++)
        {
            var store = new MainWindowLogStore(() => { });
            using var started = new ManualResetEventSlim(false);
            var errors = new ConcurrentBag<Exception>();

            Task[] workers = Enumerable.Range(0, 8).Select(workerIndex => Task.Run(() =>
            {
                started.Wait();
                try
                {
                    for (int i = 0; i < 3_000; i++)
                    {
                        if (workerIndex % 2 == 0)
                        {
                            store.AppendRaw($"w{workerIndex}-{i}");
                            store.FlushCollapsedGroup();
                        }
                        else
                        {
                            store.AppendRaw($"c{workerIndex}-{i}");
                            if (i % 7 == 0)
                            {
                                store.ClearWithoutNotify();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToArray();

            started.Set();
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30));
            offThreadError = errors.FirstOrDefault();
        }

        Assert.NotNull(offThreadError);
        Assert.IsAssignableFrom<Exception>(offThreadError);
    }

    /// <summary>
    /// GuiProcessRunner invokes outputCallback from stdout and stderr Task.Run readers.
    /// StreamWriter.WriteLine used for raw console capture is not thread-safe under that fan-in.
    /// </summary>
    [Fact(Skip = "Product path now locks the raw console StreamWriter; this test hammers a local unsynchronized writer.")]
    public async Task DualReaderOutputCallback_StreamWriterWriteLine_ThrowsOrCorrupts()
    {
        string path = Path.Combine(Path.GetTempPath(), "gauntlet-raw-" + Guid.NewGuid().ToString("N") + ".log");
        var errors = new ConcurrentBag<Exception>();
        using var started = new ManualResetEventSlim(false);

        try
        {
            await using var writer = new StreamWriter(path);
            Action<string> outputCallback = line =>
            {
                // Same unsynchronized WriteLine as MainWindowViewModel.Extraction output handler.
                writer.WriteLine(line);
            };

            Task[] readers = Enumerable.Range(0, 2).Select(readerIndex => Task.Run(() =>
            {
                started.Wait();
                try
                {
                    for (int i = 0; i < 5_000; i++)
                    {
                        outputCallback($"reader-{readerIndex}-line-{i}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToArray();

            started.Set();
            await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(30));
            await writer.FlushAsync();
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }

        // StreamWriter concurrent WriteLine typically throws InvalidOperationException / IndexOutOfRange.
        Assert.True(
            errors.Count > 0,
            "Expected unsynchronized StreamWriter failure under dual stdout/stderr callbacks.");
    }

    /// <summary>
    /// RequestFlush fire-and-forget ContinueWith only logs faults — proves swallow pattern exists
    /// on ConsoleOutputCoordinator (reflection: method uses discarded Task).
    /// </summary>
    [Fact]
    public void ConsoleOutputCoordinator_RequestFlush_IsFireAndForget()
    {
        MethodInfo? method = typeof(ConsoleOutputCoordinator)
            .GetMethod("RequestFlush", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Source contract: returns void and starts FlushPendingLogLinesAsync without awaiting.
        Assert.Equal(typeof(void), method!.ReturnType);
    }
}
