using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace AssetSplitterUI.Services;

internal static class WindowsTitleBarTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public static void Apply(Window window, string themeMode)
    {
        if (!OperatingSystem.IsWindows()) return;

        IPlatformHandle? handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero) return;
        IntPtr hwnd = handle.Handle;

        bool isLight = ResolveIsLight(themeMode);
        Color caption = isLight ? Color.Parse("#1D222C") : Color.Parse("#07130F");
        Color text = Color.Parse("#FFF7E8");

        int useDarkMode = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));

        int captionColor = ToColorRef(caption);
        int textColor = ToColorRef(text);

        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref textColor, sizeof(int));
    }

    private static bool ResolveIsLight(string themeMode) =>
        themeMode == "Light"
        || (themeMode == "Auto" && Application.Current?.ActualThemeVariant == ThemeVariant.Light);

    private static int ToColorRef(Color color) =>
        color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
