using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
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

/// <summary>
/// Main application window: hosts game path, output path, language, options, run button, and log.
/// <see cref="DataContext"/> is <see cref="MainWindowViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private const int DeveloperUnlockTapCount = 7;
    private static readonly TimeSpan DeveloperUnlockWindow = TimeSpan.FromSeconds(2.5);
    private CancellationTokenSource? _bannerAnimCts;
    private int _developerUnlockTaps;
    private DateTime _firstDeveloperUnlockTapUtc;

    /// <summary>Builds UI, wires ViewModel, and registers lifecycle handlers.</summary>
    public MainWindow()
    {
        InitializeComponent();

        // Defer heavy work until after the window is shown so double-click feels instant.
        Opened += OnWindowOpened;

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var viewModel = new MainWindowViewModel(new PlatformServices(this));
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        GameLanguageBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == ComboBox.IsDropDownOpenProperty
                && GameLanguageBox.IsDropDownOpen
                && DataContext is MainWindowViewModel vm)
            {
                _ = vm.RefreshAvailableLanguagesForDropdownAsync();
            }
        };

        StringResourceManager.Instance.PropertyChanged += StringResourceManager_PropertyChanged;

        if (DataContext is MainWindowViewModel vm)
            WindowsTitleBarTheme.Apply(this, vm.SelectedTheme);
    }

    private void StringResourceManager_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not ("LanguageVersion" or null)) return;

        // The localization system (LocalizeExtension + LanguageVersion) handles rebinding automatically.
        // We only need to close dropdowns so stale localized content doesn't remain visible.
        Dispatcher.UIThread.Post(CloseAllDropdowns, DispatcherPriority.Background);
    }

    private static void OnProcessExit(object? sender, EventArgs e) =>
        AssetProcessorRunner.TryKillCurrentProcess();

    private static void OnDomainUnload(object? sender, EventArgs e) =>
        AssetProcessorRunner.TryKillCurrentProcess();

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        AssetProcessorRunner.TryKillCurrentProcess();

    /// <summary>Runs once when the window is shown: loads settings and closes dropdowns.</summary>
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;
        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadSettings();
            // Restore saved window size (ViewModel only sets Saved* when size is >= MinWidth/MinHeight)
            if (vm.SavedWindowWidth >= MinWidth && vm.SavedWindowHeight >= MinHeight)
            {
                Width = vm.SavedWindowWidth;
                Height = vm.SavedWindowHeight;
            }
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
        ThemeBox.IsDropDownOpen = false;
        DetectedGamesBox.IsDropDownOpen = false;
        GameLanguageBox.IsDropDownOpen = false;
        GamePathBox.IsDropDownOpen = false;
        OutputPathBox.IsDropDownOpen = false;
    }

    /// <summary>Handles ViewModel property changes: close dropdowns when processing starts, scroll log, run animations.</summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsProcessing)
            && DataContext is MainWindowViewModel { IsProcessing: true })
        {
            // Close every dropdown and remove keyboard focus so the UI is fully "locked"
            // while extraction runs. IsEnabled bindings disable interaction but don't
            // automatically close open dropdowns or blur an active TextBox.
            Dispatcher.UIThread.Post(() =>
            {
                CloseAllDropdowns();
                TopLevel.GetTopLevel(this)?.Focus();
            });
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTheme)
            && DataContext is MainWindowViewModel themeVm)
        {
            Dispatcher.UIThread.Post(
                () => WindowsTitleBarTheme.Apply(this, themeVm.SelectedTheme),
                DispatcherPriority.Background);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.LogVersion)
            && DataContext is MainWindowViewModel { LogLines.Count: > 0 } lvm)
            Dispatcher.UIThread.Post(
              () => LogListBox.ScrollIntoView(lvm.LogLines[^1]),
              DispatcherPriority.Background);

        if (e.PropertyName == nameof(MainWindowViewModel.StatusText))
        {
            // During active processing the status line refreshes every ~100 ms.
            // Restarting a fade-from-zero animation that frequently causes rapid
            // blinking. Only animate for key moments (start / complete / error).
            if (DataContext is not MainWindowViewModel { IsProcessing: true })
                PlayAnimation("StatusFadeIn", StatusTextBlock);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ShowPhase2Banner)
            && DataContext is MainWindowViewModel { ShowPhase2Banner: true })
        {
            _bannerAnimCts?.Cancel();
            _bannerAnimCts?.Dispose();
            _bannerAnimCts = new CancellationTokenSource();
            Phase2Banner.Opacity = 0; // ensure clean start if banner was previously shown
            PlayAnimation("BannerSlideIn", Phase2Banner, _bannerAnimCts.Token);
            PlayAnimation("CheckmarkBounce", BannerCheckmark, _bannerAnimCts.Token);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsComplete)
            && DataContext is MainWindowViewModel { IsComplete: true })
            PlayAnimation("CompletionFlash", ProgressFlashOverlay);
    }

    /// <summary>Looks up a resource animation by key and runs it on <paramref name="target"/>.</summary>
    private void PlayAnimation(string key, Control target, CancellationToken ct = default)
    {
        if (Resources[key] is Animation anim)
            _ = anim.RunAsync(target, ct);
    }

    /// <summary>
    /// Clears keyboard focus when the user clicks on any non-input area (label, border, panel, etc.).
    /// Without this, a TextBox keeps its caret/focus until another focusable control is clicked.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Source is not (TextBox or AutoCompleteBox or Button or ComboBox or CheckBox or ToggleButton))
            TopLevel.GetTopLevel(this)?.Focus();
    }

    private void DeveloperUnlockTarget_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.DeveloperOptionsUnlocked)
            return;

        var now = DateTime.UtcNow;
        if (_developerUnlockTaps == 0 || now - _firstDeveloperUnlockTapUtc > DeveloperUnlockWindow)
        {
            _firstDeveloperUnlockTapUtc = now;
            _developerUnlockTaps = 0;
        }

        _developerUnlockTaps++;
        if (_developerUnlockTaps < DeveloperUnlockTapCount)
            return;

        vm.UnlockDeveloperOptions();
        _developerUnlockTaps = 0;
        if (!vm.IsProcessing)
        {
            vm.SetStatusText("statusMessages.developerOptionsUnlocked");
            PlayAnimation("StatusFadeIn", StatusTextBlock);
        }

        e.Handled = true;
    }

    /// <summary>Copies the developer report to the clipboard.</summary>
    private async void CopyDeveloperReport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;

            await clipboard.SetTextAsync(vm.BuildDeveloperReport());
            vm.StatusText = StringResourceManager.Instance.GetString("developerTools.reportCopied");
            PlayAnimation("StatusFadeIn", StatusTextBlock);
        }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(MainWindow), "Failed to copy developer report");
            UILogger.Debug(nameof(MainWindow), ex);
            if (DataContext is MainWindowViewModel vm2)
                vm2.StatusText = StringResourceManager.Instance.GetString("statusMessages.clipboardCopyFailed");
        }
    }

    /// <summary>Copies selected log lines to the clipboard.</summary>
    private async void CopySelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try { await CopyConsoleToClipboard(selectedOnly: true); }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(MainWindow), "Failed to copy selected log lines");
            UILogger.Debug(nameof(MainWindow), ex);
            if (DataContext is MainWindowViewModel vm) vm.StatusText = StringResourceManager.Instance.GetString("statusMessages.clipboardCopyFailed");
        }
    }

    /// <summary>Copies the full log to the clipboard.</summary>
    private async void CopyAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try { await CopyConsoleToClipboard(selectedOnly: false); }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(MainWindow), "Failed to copy full log");
            UILogger.Debug(nameof(MainWindow), ex);
            if (DataContext is MainWindowViewModel vm) vm.StatusText = StringResourceManager.Instance.GetString("statusMessages.clipboardCopyFailed");
        }
    }

    /// <summary>Handles Ctrl+C (copy selection) and Escape (clear selection) in the log list.</summary>
    private void LogListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            LogListBox.SelectedItems?.Clear();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.C || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        e.Handled = true;
        _ = CopyConsoleToClipboard(selectedOnly: true);
    }

    /// <summary>Clears log selection when clicking on empty area (not on a list item).</summary>
    private void LogListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Control source) return;
        var hit = source.FindAncestorOfType<ListBoxItem>();
        if (hit is null)
        {
            LogListBox.SelectedItems?.Clear();
            e.Handled = true;
        }
    }

    /// <summary>Copies log lines to the system clipboard (selected items or full log).</summary>
    private async Task CopyConsoleToClipboard(bool selectedOnly)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        string text;
        if (selectedOnly && LogListBox.SelectedItems?.Count > 0)
        {
            var lines = LogListBox.SelectedItems!
              .OfType<ViewModels.LogLine>()
              .Select(l => l.Text);
            text = string.Join(Environment.NewLine, lines);
        }
        else if (DataContext is MainWindowViewModel vm)
        {
            text = string.Join(Environment.NewLine, vm.LogLines.Select(l => l.Text));
        }
        else
        {
            return;
        }

        if (string.IsNullOrEmpty(text)) return;
        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch
        {
            // Ignore clipboard errors (e.g. another app has it open)
        }
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetWindowSizeForSave(Width, Height);
            vm.SaveSettings();
            vm.Dispose();
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        StringResourceManager.Instance.PropertyChanged -= StringResourceManager_PropertyChanged;
        if (DataContext is MainWindowViewModel currentViewModel)
            currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _bannerAnimCts?.Cancel();
        _bannerAnimCts?.Dispose();
        _bannerAnimCts = null;

        base.OnClosing(e);
    }
}
