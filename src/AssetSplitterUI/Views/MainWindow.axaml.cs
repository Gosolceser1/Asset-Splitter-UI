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

/// <summary>
/// Main application window: hosts game path, output path, language, options, run button, and log.
/// <see cref="DataContext"/> is <see cref="MainWindowViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private const int DeveloperUnlockTapCount = 7;
    private static readonly TimeSpan DeveloperUnlockWindow = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan LogAutoScrollInterval = TimeSpan.FromMilliseconds(120);
    private int _developerUnlockTaps;
    private DateTime _firstDeveloperUnlockTapUtc;
    private DateTime _lastLogAutoScrollUtc;
    private EventHandler? _screensChangedHandler;

/// <summary>Builds UI, wires ViewModel, and registers lifecycle handlers.</summary>
    public MainWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Opacity = 0;
        MainContentGrid.SizeChanged += (_, _) => UpdateResponsiveLayout(ClientSize);

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
        {
            WindowsTitleBarTheme.Apply(this, vm.SelectedTheme);
        }
    }
}
