using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AssetSplitterUI.Services;

public static class ApplicationThemeService
{
    private static bool _themeWatcherAttached;

    /// <summary>Last theme mode passed to <see cref="ApplyTheme"/> (Light, Dark, or Auto).</summary>
    public static string CurrentThemeMode { get; private set; } = "Auto";

    /// <summary>Fired after theme brushes and <see cref="CurrentThemeMode"/> are updated.</summary>
    public static event Action? ThemeChanged;

    /// <summary>Whether console log colors should use the light palette.</summary>
    public static bool IsLightTheme =>
        CurrentThemeMode == "Light"
        || (CurrentThemeMode == "Auto" && Application.Current?.ActualThemeVariant == ThemeVariant.Light);

    public static void ApplyTheme(string themeMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        EnsureThemeWatcher();
        CurrentThemeMode = themeMode;

        Application.Current.RequestedThemeVariant = themeMode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        ReplaceThemeGradientBrushes(themeMode);
        ThemeChanged?.Invoke();
    }

    private static void EnsureThemeWatcher()
    {
        if (_themeWatcherAttached || Application.Current is null)
        {
            return;
        }

        Application.Current.ActualThemeVariantChanged += (_, _) =>
        {
            if (CurrentThemeMode != "Auto")
            {
                return;
            }

            ReplaceThemeGradientBrushes(CurrentThemeMode);
            ThemeChanged?.Invoke();
        };
        _themeWatcherAttached = true;
    }

    private static void ReplaceThemeGradientBrushes(string themeMode)
    {
        bool isLight = themeMode == "Light"
            || (themeMode == "Auto" && Application.Current?.ActualThemeVariant == ThemeVariant.Light);
        var res = Application.Current!.Resources;

        res["HeaderChromeBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#1D222C") : Color.Parse("#07130F"), 0),
                new GradientStop(isLight ? Color.Parse("#263225") : Color.Parse("#10231B"), 0.35),
                new GradientStop(isLight ? Color.Parse("#263225") : Color.Parse("#10231B"), 0.72),
                new GradientStop(isLight ? Color.Parse("#1A241D") : Color.Parse("#0A1712"), 1),
            }
        };

        res["SectionHeaderGlowBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#8B5E00") : Color.Parse("#D4A843"), 0),
                new GradientStop(isLight ? Color.Parse("#809F7618") : Color.Parse("#88D4A843"), 0.42),
                new GradientStop(isLight ? Color.Parse("#223E6F4C") : Color.Parse("#222E9A8E"), 0.72),
                new GradientStop(isLight ? Color.Parse("#00367D48") : Color.Parse("#005DBE6E"), 1),
            }
        };

        res["SectionGoldBorderBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#8B5E00") : Color.Parse("#D4A843"), 0),
                new GradientStop(isLight ? Color.Parse("#8B5E00") : Color.Parse("#D4A843"), 0.003),
                new GradientStop(isLight ? Color.Parse("#367D48") : Color.Parse("#2E9A8E"), 0.005),
                new GradientStop(isLight ? Color.Parse("#B8C2B5") : Color.Parse("#334A3F"), 1),
            }
        };

        res["SectionDiagonalBgBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#FBFCF8") : Color.Parse("#16261F"), 0),
                new GradientStop(isLight ? Color.Parse("#F4F8F2") : Color.Parse("#10221B"), 0.52),
                new GradientStop(isLight ? Color.Parse("#E8EFE4") : Color.Parse("#0D1B15"), 1),
            }
        };

        res["ProgressTrackGlowBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#B0BAB0") : Color.Parse("#20352A"), 0),
                new GradientStop(isLight ? Color.Parse("#9DAA9A") : Color.Parse("#182C22"), 0.5),
                new GradientStop(isLight ? Color.Parse("#B0BAB0") : Color.Parse("#20352A"), 1),
            }
        };

        res["HeaderTextGlowBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#C8A044") : Color.Parse("#E9C86A"), 0),
                new GradientStop(Color.Parse("#FFF7E8"), 0.7),
            }
        };

        res["ActionBarBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#F3F7F0") : Color.Parse("#16261F"), 0),
                new GradientStop(isLight ? Color.Parse("#DDE7D8") : Color.Parse("#10231B"), 1),
            }
        };

        res["ConsoleWellBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#DDE7DA") : Color.Parse("#0A1712"), 0),
                new GradientStop(isLight ? Color.Parse("#F1F6EF") : Color.Parse("#07130F"), 1),
            }
        };

        res["ConsolePanelBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(isLight ? Color.Parse("#EDF4EA") : Color.Parse("#07130F"), 0),
                new GradientStop(isLight ? Color.Parse("#E5EEE2") : Color.Parse("#0D1B15"), 0.55),
                new GradientStop(isLight ? Color.Parse("#F7FAF4") : Color.Parse("#10231B"), 1),
            }
        };

        res["GoldLineBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#00D4A843"), 0),
                new GradientStop(isLight ? Color.Parse("#908B5E00") : Color.Parse("#AAD4A843"), 0.35),
                new GradientStop(isLight ? Color.Parse("#50367D48") : Color.Parse("#55F0D060"), 0.65),
                new GradientStop(Color.Parse("#00D4A843"), 1),
            }
        };
    }
}
