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
    private void UpdateResponsiveLayout(Size clientSize)
    {
        double mainHeight = MainContentGrid.Bounds.Height;
        if (mainHeight <= 0)
        {
            mainHeight = Math.Max(0, clientSize.Height - 118);
        }

        MainWindowLayoutHelper.Apply(
            MainContentGrid,
            ConfigColumn,
            ConsoleColumn,
            ToggleOptionsGrid,
            CoreProcessingGroup,
            OutputStructureGroup,
            clientSize,
            mainHeight);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateResponsiveLayout(e.NewSize);
    }
}
