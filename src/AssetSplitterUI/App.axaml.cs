using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AssetSplitterUI.Views;

namespace AssetSplitterUI;

/// <summary>Avalonia application entry. Loads XAML and sets MainWindow as the classic desktop lifetime window.</summary>
public partial class App : Application
{
    /// <summary>Loads XAML resources and registers the application.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Sets MainWindow as the desktop lifetime window and runs the app.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
