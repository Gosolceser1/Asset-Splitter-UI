using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;
using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Views;

public partial class MainWindow : Window
{
    private int _closeStarted;

    private void StringResourceManager_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not ("LanguageVersion" or null))
        {
            return;
        }

        // The localization system (LocalizeExtension + LanguageVersion) handles rebinding automatically.
        // We only need to close dropdowns so stale localized content doesn't remain visible.
        Dispatcher.UIThread.Post(CloseAllDropdowns, DispatcherPriority.Background);
    }

    private static void OnProcessExit(object? sender, EventArgs e) =>
        AssetProcessorRunner.TryKillCurrentProcess(maxWaitMilliseconds: 2000);

    private static void OnDomainUnload(object? sender, EventArgs e) =>
        AssetProcessorRunner.TryKillCurrentProcess(maxWaitMilliseconds: 2000);

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        AssetProcessorRunner.TryKillCurrentProcess(maxWaitMilliseconds: 500);

    /// <summary>Runs once when the window is shown: loads settings and closes dropdowns.</summary>
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;
        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadSettings();
            WindowPlacementHelper.Apply(this, new WindowPlacementSettings
            {
                SavedX = vm.SavedWindowLeft,
                SavedY = vm.SavedWindowTop,
                SavedWidth = vm.SavedWindowWidth,
                SavedHeight = vm.SavedWindowHeight,
                SavedDisplayName = vm.SavedWindowDisplayName,
                StartMaximized = vm.SavedWindowMaximized
            });
            Opacity = 1;
            UpdateResponsiveLayout(ClientSize);
            AttachScreenChangeHandler();
        }

        // Prevent path/theme dropdowns from staying open after ItemsSource is populated.
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            Dispatcher.UIThread.Post(CloseAllDropdowns, DispatcherPriority.Background);
        });
    }

    /// <summary>Closes all open dropdowns (theme, game path, output path, etc.) so the UI state is consistent.</summary>
    private void CloseAllDropdowns()
    {
        if (Volatile.Read(ref _closeStarted) != 0)
        {
            return;
        }

        ThemeBox.IsDropDownOpen = false;
        DetectedGamesBox.IsDropDownOpen = false;
        GameLanguageBox.IsDropDownOpen = false;
        GamePathBox.IsDropDownOpen = false;
        OutputPathBox.IsDropDownOpen = false;
    }

    private void AttachScreenChangeHandler()
    {
        if (Screens is null || _screensChangedHandler is not null)
        {
            return;
        }

        _screensChangedHandler = (_, _) =>
            Dispatcher.UIThread.Post(() => WindowPlacementHelper.ClampToVisibleScreen(this));
        Screens.Changed += _screensChangedHandler;
    }

    private void DetachScreenChangeHandler()
    {
        if (Screens is null || _screensChangedHandler is null)
        {
            return;
        }

        Screens.Changed -= _screensChangedHandler;
        _screensChangedHandler = null;
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Stop log-driven UI work before teardown. Avalonia close cost scales with bound item
        // count and visual subscribers (AvaloniaUI/Avalonia#6660, #3622).
        Interlocked.Exchange(ref _closeStarted, 1);
        StringResourceManager.Instance.PropertyChanged -= StringResourceManager_PropertyChanged;
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        LogListBox.ItemsSource = null;
        DetachScreenChangeHandler();

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PrepareForShutdown();
            viewModel.SetWindowPlacementForSave(WindowPlacementHelper.Capture(this));
            viewModel.SaveSettings();
            viewModel.Dispose();
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

        base.OnClosing(e);
    }
}
