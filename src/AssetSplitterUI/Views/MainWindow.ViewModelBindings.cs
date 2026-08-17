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

        if (e.PropertyName == nameof(MainWindowViewModel.IsGameLanguageSelectionEnabled)
            && DataContext is MainWindowViewModel { IsGameLanguageSelectionEnabled: false })
        {
            Dispatcher.UIThread.Post(() => GameLanguageBox.IsDropDownOpen = false);
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
        {
            ScrollLogToTail(lvm);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.StatusText))
        {
            // During active processing the status line refreshes every ~100 ms.
            // Restarting a fade-from-zero animation that frequently causes rapid
            // blinking. Only animate for key moments (start / complete / error).
            if (DataContext is not MainWindowViewModel { IsProcessing: true })
            {
                PlayAnimation("StatusFadeIn", StatusTextBlock);
            }
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsComplete)
            && DataContext is MainWindowViewModel { IsComplete: true })
        {
            PlayAnimation("CompletionFlash", ProgressFlashOverlay);
        }
    }

    /// <summary>Looks up a resource animation by key and runs it on <paramref name="target"/>.</summary>
    private void PlayAnimation(string key, Control target, CancellationToken ct = default)
    {
        if (Resources[key] is Animation anim)
        {
            _ = anim.RunAsync(target, ct);
        }
    }

    private void ScrollLogToTail(MainWindowViewModel viewModel)
    {
        if (LogListBox.SelectedItems?.Count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (viewModel.IsProcessing && now - _lastLogAutoScrollUtc < LogAutoScrollInterval)
        {
            return;
        }

        _lastLogAutoScrollUtc = now;
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel { LogLines.Count: > 0 } latestViewModel
                && LogListBox.SelectedItems?.Count == 0)
            {
                LogListBox.ScrollIntoView(latestViewModel.LogLines[^1]);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Clears keyboard focus when the user clicks on any non-input area (label, border, panel, etc.).
    /// Without this, a TextBox keeps its caret/focus until another focusable control is clicked.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Source is not (TextBox or AutoCompleteBox or Button or ComboBox or CheckBox or ToggleButton))
        {
            TopLevel.GetTopLevel(this)?.Focus();
        }
    }

    private void DeveloperUnlockTarget_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.DeveloperOptionsUnlocked || vm.IsProcessing)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_developerUnlockTaps == 0 || now - _firstDeveloperUnlockTapUtc > DeveloperUnlockWindow)
        {
            _firstDeveloperUnlockTapUtc = now;
            _developerUnlockTaps = 0;
        }

        _developerUnlockTaps++;
        if (_developerUnlockTaps < DeveloperUnlockTapCount)
        {
            return;
        }

        vm.UnlockDeveloperOptions();
        _developerUnlockTaps = 0;
        if (!vm.IsProcessing)
        {
            vm.SetStatusText("statusMessages.developerOptionsUnlocked");
            PlayAnimation("StatusFadeIn", StatusTextBlock);
        }

        e.Handled = true;
    }
}
