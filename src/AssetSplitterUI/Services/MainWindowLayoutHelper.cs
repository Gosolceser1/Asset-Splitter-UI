using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AssetSplitterUI.Services;

/// <summary>
/// Responsive main-window layout following Avalonia star/auto sizing and
/// Microsoft utility-window guidance: one resizable region (console), fixed/auto sidebar,
/// breakpoint-driven structure changes, and minimum functional sizes.
/// </summary>
internal static class MainWindowLayoutHelper
{
    public const double CompactBreakpoint = 900;
    public const double WideBreakpoint = 1200;
    public const double ToggleStackBreakpoint = 440;

    private const double ConfigMinWidth = 380;
    private const double ConfigPreferredWidth = 440;
    private const double ConsoleMinHeightStacked = 220;
    private const double StackedConfigMaxFraction = 0.55;

    private static MainWindowLayoutPhase _lastPhase = (MainWindowLayoutPhase)(-1);
    private static bool _lastStackToggles;

    public static void Apply(
        Grid mainGrid,
        Border configColumn,
        Control consoleColumn,
        Grid toggleGrid,
        Control coreProcessingGroup,
        Control outputStructureGroup,
        Size clientSize,
        double mainContentHeight)
    {
        var phase = ResolvePhase(clientSize.Width);
        bool stackToggles = phase == MainWindowLayoutPhase.Stacked
            || EstimateConfigWidth(clientSize.Width, phase) < ToggleStackBreakpoint;

        if (phase != _lastPhase)
        {
            ApplyStructure(mainGrid, configColumn, consoleColumn, phase);
            _lastPhase = phase;
        }

        if (stackToggles != _lastStackToggles)
        {
            ApplyToggleGroups(toggleGrid, coreProcessingGroup, outputStructureGroup, stackToggles);
            _lastStackToggles = stackToggles;
        }

        ApplyConstraints(configColumn, consoleColumn, phase, mainContentHeight);
    }

    private static MainWindowLayoutPhase ResolvePhase(double width) =>
        width > 0 && width < CompactBreakpoint
            ? MainWindowLayoutPhase.Stacked
            : width >= WideBreakpoint
                ? MainWindowLayoutPhase.SideBySideWide
                : MainWindowLayoutPhase.SideBySide;

    private static double EstimateConfigWidth(double clientWidth, MainWindowLayoutPhase phase)
    {
        const double horizontalChrome = 36;
        double inner = Math.Max(0, clientWidth - horizontalChrome);

        return phase switch
        {
            MainWindowLayoutPhase.Stacked => inner,
            MainWindowLayoutPhase.SideBySideWide => ConfigPreferredWidth,
            _ => inner * (1.05 / (1.05 + 1.45))
        };
    }

    private static void ApplyStructure(
        Grid grid,
        Control configColumn,
        Control consoleColumn,
        MainWindowLayoutPhase phase)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (phase == MainWindowLayoutPhase.Stacked)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star) { MinHeight = ConsoleMinHeightStacked });

            Grid.SetColumn(configColumn, 0);
            Grid.SetRow(configColumn, 0);
            Grid.SetColumnSpan(configColumn, 1);
            Grid.SetRowSpan(configColumn, 1);

            Grid.SetColumn(consoleColumn, 0);
            Grid.SetRow(consoleColumn, 1);
            Grid.SetColumnSpan(consoleColumn, 1);
            Grid.SetRowSpan(consoleColumn, 1);
            return;
        }

        grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

        if (phase == MainWindowLayoutPhase.SideBySideWide)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(ConfigPreferredWidth, GridUnitType.Pixel)
            {
                MinWidth = ConfigMinWidth,
                MaxWidth = 480
            });
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1.05, GridUnitType.Star) { MinWidth = ConfigMinWidth });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        Grid.SetRow(configColumn, 0);
        Grid.SetRow(consoleColumn, 0);
        Grid.SetColumn(configColumn, 0);
        Grid.SetColumn(consoleColumn, 1);
        Grid.SetColumnSpan(configColumn, 1);
        Grid.SetColumnSpan(consoleColumn, 1);
        Grid.SetRowSpan(configColumn, 1);
        Grid.SetRowSpan(consoleColumn, 1);
    }

    private static void ApplyToggleGroups(
        Grid toggleGrid,
        Control coreGroup,
        Control outputGroup,
        bool stackVertically)
    {
        toggleGrid.ColumnDefinitions.Clear();
        toggleGrid.RowDefinitions.Clear();

        if (stackVertically)
        {
            toggleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            toggleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(coreGroup, 0);
            Grid.SetRow(coreGroup, 0);
            Grid.SetColumnSpan(coreGroup, 1);

            Grid.SetColumn(outputGroup, 0);
            Grid.SetRow(outputGroup, 1);
            Grid.SetColumnSpan(outputGroup, 1);
            return;
        }

        toggleGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        toggleGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        Grid.SetRow(coreGroup, 0);
        Grid.SetRow(outputGroup, 0);
        Grid.SetColumn(coreGroup, 0);
        Grid.SetColumn(outputGroup, 1);
        Grid.SetColumnSpan(coreGroup, 1);
        Grid.SetColumnSpan(outputGroup, 1);
    }

    private static void ApplyConstraints(
        Border configColumn,
        Control consoleColumn,
        MainWindowLayoutPhase phase,
        double mainContentHeight)
    {
        double mainHeight = Math.Max(0, mainContentHeight);

        consoleColumn.VerticalAlignment = VerticalAlignment.Stretch;
        consoleColumn.HorizontalAlignment = HorizontalAlignment.Stretch;

        configColumn.HorizontalAlignment = HorizontalAlignment.Stretch;
        configColumn.MaxWidth = double.PositiveInfinity;

        if (phase == MainWindowLayoutPhase.Stacked)
        {
            // Auto-height row: panel hugs content; cap height and scroll when window is short.
            configColumn.VerticalAlignment = VerticalAlignment.Top;
            configColumn.MaxHeight = mainHeight > 0
                ? Math.Max(ConfigMinWidth, mainHeight * StackedConfigMaxFraction)
                : double.PositiveInfinity;
            return;
        }

        // Side-by-side: sidebar hugs content (no dead space inside the card); console fills height.
        configColumn.VerticalAlignment = VerticalAlignment.Top;
        configColumn.MaxHeight = mainHeight > 0 ? mainHeight : double.PositiveInfinity;
    }

    /// <summary>Resets cached breakpoint state (tests / design surface).</summary>
    internal static void ResetCache()
    {
        _lastPhase = (MainWindowLayoutPhase)(-1);
        _lastStackToggles = false;
    }

    private enum MainWindowLayoutPhase
    {
        Stacked,
        SideBySide,
        SideBySideWide
    }
}
