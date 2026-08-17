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
    /// <summary>Copies the developer report to the clipboard.</summary>
    private async void CopyDeveloperReport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm || vm.IsProcessing)
            {
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            await clipboard.SetTextAsync(vm.BuildDeveloperReport());
            vm.SetLocalizedStatusText("developerTools.reportCopied");
            PlayAnimation("StatusFadeIn", StatusTextBlock);
        }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(MainWindow), "Failed to copy developer report");
            UILogger.Debug(nameof(MainWindow), ex);
            if (DataContext is MainWindowViewModel vm2)
            {
                vm2.SetLocalizedStatusText("statusMessages.clipboardCopyFailed");
            }
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
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SetLocalizedStatusText("statusMessages.clipboardCopyFailed");
            }
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
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SetLocalizedStatusText("statusMessages.clipboardCopyFailed");
            }
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
        if (e.Key != Key.C || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        e.Handled = true;
        _ = CopyConsoleToClipboard(selectedOnly: true);
    }

    /// <summary>Clears log selection when clicking on empty area (not on a list item).</summary>
    private void LogListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Control source)
        {
            return;
        }

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
        if (clipboard is null)
        {
            return;
        }

        string text;
        if (selectedOnly && LogListBox.SelectedItems is { Count: > 0 } selectedItems)
        {
            var lines = selectedItems
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

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch
        {
            // Ignore clipboard errors (e.g. another app has it open)
        }
    }
}
