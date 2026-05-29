using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace AssetSplitterUI.ViewModels;

internal sealed class BusyIndicatorAnimator(Action<string, string> applyFrame) : IDisposable
{
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private static readonly string[] DotStates = [" ·", " ··", " ···"];

    private readonly Action<string, string> _applyFrame = applyFrame;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _animationTask;

    public void Start()
    {
        Stop();

        _cancellationTokenSource = new CancellationTokenSource();
        _animationTask = AnimateAsync(_cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _animationTask = null;
        _applyFrame("", "");
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task AnimateAsync(CancellationToken cancellationToken)
    {
        var frameIndex = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string spinner = SpinnerFrames[frameIndex];
                string ellipsis = DotStates[frameIndex % DotStates.Length];

                Dispatcher.UIThread.Post(() => _applyFrame(spinner, ellipsis));
                frameIndex = (frameIndex + 1) % SpinnerFrames.Length;

                await Task.Delay(80, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
