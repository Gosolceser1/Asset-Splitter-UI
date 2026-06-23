using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AssetSplitterUI.Services;

/// <summary>
/// Main-window placement following Microsoft Windows UX guidelines for top-level utility
/// windows (center on default monitor, persist geometry, validate against <see cref="Screen.WorkingArea"/>).
/// See: https://learn.microsoft.com/en-us/windows/win32/uxguide/win-window-mgt
/// </summary>
public static class WindowPlacementHelper
{
    public const double DefaultWidth = 980;
    public const double DefaultHeight = 820;
    public const double MinWidth = 780;
    public const double MinHeight = 700;

    /// <summary>Microsoft recommends utility windows not open near-maximized.</summary>
    private const double DefaultWidthFraction = 0.58;
    private const double DefaultHeightFraction = 0.62;
    private const double MaxDefaultWidthDip = 1080;
    private const double MaxDefaultHeightDip = 860;
    private const double MaxWorkAreaFraction = 0.85;

    /// <summary>45% above / 55% below — Microsoft "centered" vertical bias for utilities.</summary>
    private const double VerticalTopBias = 0.45;

    private const int TitleBarPhysicalHeightEstimate = 40;

    public static void Apply(Window window, WindowPlacementSettings settings)
    {
        IReadOnlyList<Screen> screens = window.Screens?.All ?? [];
        if (screens.Count == 0)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;

        Screen? defaultScreen = GetDefaultMonitor(window, screens);
        if (defaultScreen is null)
        {
            return;
        }

        if (settings.StartMaximized)
        {
            ApplyMaximized(window, ResolveTargetMonitor(screens, settings, defaultScreen) ?? defaultScreen);
            return;
        }

        if (TryRestoreSavedPlacement(screens, settings, defaultScreen, out var restored))
        {
            window.Width = restored.WidthDip;
            window.Height = restored.HeightDip;
            window.Position = restored.Position;
            window.WindowState = WindowState.Normal;
            return;
        }

        ApplyDefaultUtilityPlacement(window, defaultScreen);
    }

    /// <summary>Re-clamps the window when monitors are added/removed while the app is running.</summary>
    public static void ClampToVisibleScreen(Window window)
    {
        if (window.WindowState is WindowState.Maximized or WindowState.FullScreen)
        {
            return;
        }

        IReadOnlyList<Screen> screens = window.Screens?.All ?? [];
        if (screens.Count == 0)
        {
            return;
        }

        Screen? current = window.Screens?.ScreenFromWindow(window);
        double scale = current?.Scaling ?? window.DesktopScaling;
        PixelRect work = current?.WorkingArea
            ?? window.Screens?.Primary?.WorkingArea
            ?? screens[0].WorkingArea;

        double maxWidthDip = ToDip(work.Width, scale) * MaxWorkAreaFraction;
        double maxHeightDip = ToDip(work.Height, scale) * MaxWorkAreaFraction;

        if (window.Width > maxWidthDip)
        {
            window.Width = maxWidthDip;
        }

        if (window.Height > maxHeightDip)
        {
            window.Height = maxHeightDip;
        }

        int frameWidthPx = ToPhysical(window.Width, scale);
        int frameHeightPx = ToPhysical(window.Height, scale) + TitleBarPhysicalHeightEstimate;
        var frame = new PixelRect(window.Position, new PixelSize(frameWidthPx, frameHeightPx));
        int frameArea = Math.Max(1, frameWidthPx * frameHeightPx);

        foreach (Screen screen in screens)
        {
            if (IntersectionArea(frame, screen.WorkingArea) >= frameArea * 0.35)
            {
                PixelRect fitted = FitIntoWorkArea(frame, screen.WorkingArea);
                window.Position = fitted.Position;
                return;
            }
        }

        Screen? primary = window.Screens?.Primary ?? screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        window.Position = CenterUtilityWindow(
            primary.WorkingArea,
            frameWidthPx,
            frameHeightPx);
    }

    public static WindowPlacementSettings Capture(Window window)
    {
        Screen? screen = window.Screens?.ScreenFromWindow(window);
        string? displayName = screen?.DisplayName;

        if (window.WindowState == WindowState.Maximized)
        {
            return new WindowPlacementSettings
            {
                StartMaximized = true,
                SavedDisplayName = displayName
            };
        }

        return new WindowPlacementSettings
        {
            SavedX = window.Position.X,
            SavedY = window.Position.Y,
            SavedWidth = window.Width,
            SavedHeight = window.Height,
            SavedDisplayName = displayName,
            StartMaximized = false
        };
    }

    private static void ApplyMaximized(Window window, Screen target)
    {
        PixelRect work = target.WorkingArea;
        window.Position = new PixelPoint(work.X + 40, work.Y + 40);
        window.WindowState = WindowState.Maximized;
    }

    private static void ApplyDefaultUtilityPlacement(Window window, Screen screen)
    {
        double scale = screen.Scaling;
        PixelRect work = screen.WorkingArea;

        (double width, double height) = GetDefaultUtilitySize(work, scale);
        window.Width = width;
        window.Height = height;
        window.Position = CenterUtilityWindow(work, ToPhysical(width, scale), ToPhysical(height, scale) + TitleBarPhysicalHeightEstimate);
        window.WindowState = WindowState.Normal;
    }

    private static bool TryRestoreSavedPlacement(
        IReadOnlyList<Screen> screens,
        WindowPlacementSettings settings,
        Screen defaultScreen,
        out RestoredPlacement restored)
    {
        restored = default;
        if (!settings.HasSavedPosition)
        {
            return false;
        }

        Screen? target = ResolveTargetMonitor(screens, settings, defaultScreen);
        if (target is null)
        {
            return false;
        }

        double scale = target.Scaling;
        PixelRect work = target.WorkingArea;
        double maxWidthDip = ToDip(work.Width, scale) * MaxWorkAreaFraction;
        double maxHeightDip = ToDip(work.Height, scale) * MaxWorkAreaFraction;

        double width = settings.SavedWidth >= MinWidth ? settings.SavedWidth : DefaultWidth;
        double height = settings.SavedHeight >= MinHeight ? settings.SavedHeight : DefaultHeight;
        width = Math.Clamp(width, MinWidth, maxWidthDip);
        height = Math.Clamp(height, MinHeight, maxHeightDip);

        int frameWidthPx = ToPhysical(width, scale);
        int frameHeightPx = ToPhysical(height, scale) + TitleBarPhysicalHeightEstimate;

        var savedRect = new PixelRect(settings.SavedX, settings.SavedY, frameWidthPx, frameHeightPx);
        PixelRect fitted = FitIntoWorkArea(savedRect, work);

        if (!IsFullyWithinWorkArea(fitted, work))
        {
            fitted = new PixelRect(work.X, work.Y, Math.Min(frameWidthPx, work.Width), Math.Min(frameHeightPx, work.Height));
            fitted = FitIntoWorkArea(fitted, work);
        }

        if (!IsFullyWithinWorkArea(fitted, work))
        {
            return false;
        }

        restored = new RestoredPlacement(width, height, fitted.Position);
        return true;
    }

    private static Screen? ResolveTargetMonitor(
        IReadOnlyList<Screen> screens,
        WindowPlacementSettings settings,
        Screen defaultScreen)
    {
        if (!string.IsNullOrEmpty(settings.SavedDisplayName))
        {
            Screen? byName = screens.FirstOrDefault(s =>
                string.Equals(s.DisplayName, settings.SavedDisplayName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        if (settings.HasSavedPosition)
        {
            Screen? byPoint = screens.FirstOrDefault(s =>
                s.WorkingArea.Contains(new PixelPoint(settings.SavedX, settings.SavedY)));
            if (byPoint is not null)
            {
                return byPoint;
            }
        }

        return defaultScreen;
    }

    private static Screen? GetDefaultMonitor(Window window, IReadOnlyList<Screen> screens) =>
        window.Screens?.Primary ?? screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault();

    private static PixelPoint CenterUtilityWindow(PixelRect work, int frameWidthPx, int frameHeightPx)
    {
        int freeHeight = Math.Max(0, work.Height - frameHeightPx);
        int freeWidth = Math.Max(0, work.Width - frameWidthPx);
        return new PixelPoint(
            work.X + freeWidth / 2,
            work.Y + (int)Math.Round(freeHeight * VerticalTopBias));
    }

    private static PixelRect FitIntoWorkArea(PixelRect window, PixelRect work)
    {
        int width = Math.Min(window.Width, work.Width);
        int height = Math.Min(window.Height, work.Height);
        int x = Math.Clamp(window.X, work.X, Math.Max(work.X, work.X + work.Width - width));
        int y = Math.Clamp(window.Y, work.Y, Math.Max(work.Y, work.Y + work.Height - height));
        return new PixelRect(x, y, width, height);
    }

    private static bool IsFullyWithinWorkArea(PixelRect frame, PixelRect work) =>
        frame.X >= work.X
        && frame.Y >= work.Y
        && frame.X + frame.Width <= work.X + work.Width
        && frame.Y + frame.Height <= work.Y + work.Height;

    private static (double Width, double Height) GetDefaultUtilitySize(PixelRect work, double scale)
    {
        double workWidthDip = ToDip(work.Width, scale);
        double workHeightDip = ToDip(work.Height, scale);

        double width = Math.Clamp(workWidthDip * DefaultWidthFraction, MinWidth, MaxDefaultWidthDip);
        double height = Math.Clamp(workHeightDip * DefaultHeightFraction, MinHeight, MaxDefaultHeightDip);
        return (width, height);
    }

    private static double ToDip(int physicalPixels, double scale) =>
        scale > 0 ? physicalPixels / scale : physicalPixels;

    private static int ToPhysical(double dip, double scale) =>
        (int)Math.Round(dip * scale);

    private static int IntersectionArea(PixelRect a, PixelRect b)
    {
        int left = Math.Max(a.X, b.X);
        int top = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        return (right - left) * (bottom - top);
    }

    private readonly record struct RestoredPlacement(double WidthDip, double HeightDip, PixelPoint Position);
}

public sealed class WindowPlacementSettings
{
    public const int UnsetCoordinate = int.MinValue;

    public int SavedX { get; init; } = UnsetCoordinate;
    public int SavedY { get; init; } = UnsetCoordinate;
    public double SavedWidth { get; init; }
    public double SavedHeight { get; init; }
    public string? SavedDisplayName { get; init; }
    public bool StartMaximized { get; init; }

    public bool HasSavedPosition => SavedX != UnsetCoordinate && SavedY != UnsetCoordinate;
}
