using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AssetSplitterUI.Services;
using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Converters;

/// <summary>
/// Maps a <see cref="LogLineKind"/> value to a <see cref="SolidColorBrush"/>
/// for color-coded terminal output. Uses the app's forest/gold palette.
/// </summary>
public sealed class LogLineKindConverter : IValueConverter
{
    private const string ProgressMetricPart = "ProgressMetric";
    private const string ProgressSeparatorPart = "ProgressSeparator";
    private const string ProgressOperationPart = "ProgressOperation";
    private const string ProgressGuidPart = "ProgressGuid";
    private const string ProgressAssetPart = "ProgressAsset";
    private const string ProgressTemplatePart = "ProgressTemplate";

    private static readonly IBrush D_Progress = new SolidColorBrush(Color.Parse("#58B9AD"));
    private static readonly IBrush D_Processing = new SolidColorBrush(Color.Parse("#CDD8C7"));
    private static readonly IBrush D_Command = new SolidColorBrush(Color.Parse("#C0A7FF"));
    private static readonly IBrush D_Header = new SolidColorBrush(Color.Parse("#F0C75A"));
    private static readonly IBrush D_Separator = new SolidColorBrush(Color.Parse("#456759"));
    private static readonly IBrush D_Phase = new SolidColorBrush(Color.Parse("#F0C75A"));
    private static readonly IBrush D_Subsystem = new SolidColorBrush(Color.Parse("#82A9D8"));
    private static readonly IBrush D_Summary = new SolidColorBrush(Color.Parse("#D1D9C7"));
    private static readonly IBrush D_Trace = new SolidColorBrush(Color.Parse("#4D5B52"));
    private static readonly IBrush D_OptionOn = new SolidColorBrush(Color.Parse("#D4A843"));
    private static readonly IBrush D_OptionOff = new SolidColorBrush(Color.Parse("#7F8A7A"));
    private static readonly IBrush D_Info = new SolidColorBrush(Color.Parse("#AAB8AA"));
    private static readonly IBrush D_Success = new SolidColorBrush(Color.Parse("#72D98A"));
    private static readonly IBrush D_Error = new SolidColorBrush(Color.Parse("#F16D6D"));
    private static readonly IBrush D_Warning = new SolidColorBrush(Color.Parse("#E5A93A"));
    private static readonly IBrush D_Debug = new SolidColorBrush(Color.Parse("#59685E"));
    private static readonly IBrush D_Normal = new SolidColorBrush(Color.Parse("#AEB9AB"));
    private static readonly IBrush D_ProgressSeparator = new SolidColorBrush(Color.Parse("#59685E"));
    private static readonly IBrush D_ProgressOperation = new SolidColorBrush(Color.Parse("#D7B34A"));
    private static readonly IBrush D_ProgressGuid = new SolidColorBrush(Color.Parse("#F2A65A"));
    private static readonly IBrush D_ProgressAsset = new SolidColorBrush(Color.Parse("#AFC0AC"));
    private static readonly IBrush D_ProgressTemplate = new SolidColorBrush(Color.Parse("#6FA1C8"));

    // Light palette: darker, saturated hues for ~#EAF0E8 console background (WCAG-friendly).
    private static readonly IBrush L_Progress = new SolidColorBrush(Color.Parse("#007067"));
    private static readonly IBrush L_Processing = new SolidColorBrush(Color.Parse("#1A2E22"));
    private static readonly IBrush L_Command = new SolidColorBrush(Color.Parse("#4A2A82"));
    private static readonly IBrush L_Header = new SolidColorBrush(Color.Parse("#6B4700"));
    private static readonly IBrush L_Separator = new SolidColorBrush(Color.Parse("#5C6658"));
    private static readonly IBrush L_Phase = new SolidColorBrush(Color.Parse("#5C3D00"));
    private static readonly IBrush L_Subsystem = new SolidColorBrush(Color.Parse("#0F4F7A"));
    private static readonly IBrush L_Summary = new SolidColorBrush(Color.Parse("#1A2E22"));
    private static readonly IBrush L_Trace = new SolidColorBrush(Color.Parse("#4A554C"));
    private static readonly IBrush L_OptionOn = new SolidColorBrush(Color.Parse("#6B4700"));
    private static readonly IBrush L_OptionOff = new SolidColorBrush(Color.Parse("#525C52"));
    private static readonly IBrush L_Info = new SolidColorBrush(Color.Parse("#2A3D30"));
    private static readonly IBrush L_Success = new SolidColorBrush(Color.Parse("#1A6B2E"));
    private static readonly IBrush L_Error = new SolidColorBrush(Color.Parse("#A01C24"));
    private static readonly IBrush L_Warning = new SolidColorBrush(Color.Parse("#7A4F00"));
    private static readonly IBrush L_Debug = new SolidColorBrush(Color.Parse("#4E5850"));
    private static readonly IBrush L_Normal = new SolidColorBrush(Color.Parse("#1E2A22"));
    private static readonly IBrush L_ProgressSeparator = new SolidColorBrush(Color.Parse("#5C6658"));
    private static readonly IBrush L_ProgressOperation = new SolidColorBrush(Color.Parse("#7A5200"));
    private static readonly IBrush L_ProgressGuid = new SolidColorBrush(Color.Parse("#9A4200"));
    private static readonly IBrush L_ProgressAsset = new SolidColorBrush(Color.Parse("#1E2A22"));
    private static readonly IBrush L_ProgressTemplate = new SolidColorBrush(Color.Parse("#0F5A8F"));

    private static readonly IBrush Empty = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = ApplicationThemeService.IsLightTheme;
        if (parameter is string part)
        {
            return ConvertProgressPart(part, isLight);
        }

        if (value is not LogLineKind kind)
        {
            return isLight ? L_Normal : D_Normal;
        }

        return kind switch
        {
            LogLineKind.Progress => isLight ? L_Progress : D_Progress,
            LogLineKind.Processing => isLight ? L_Processing : D_Processing,
            LogLineKind.Command => isLight ? L_Command : D_Command,
            LogLineKind.Header => isLight ? L_Header : D_Header,
            LogLineKind.Separator => isLight ? L_Separator : D_Separator,
            LogLineKind.Phase => isLight ? L_Phase : D_Phase,
            LogLineKind.Subsystem => isLight ? L_Subsystem : D_Subsystem,
            LogLineKind.Summary => isLight ? L_Summary : D_Summary,
            LogLineKind.Trace => isLight ? L_Trace : D_Trace,
            LogLineKind.OptionOn => isLight ? L_OptionOn : D_OptionOn,
            LogLineKind.OptionOff => isLight ? L_OptionOff : D_OptionOff,
            LogLineKind.Info => isLight ? L_Info : D_Info,
            LogLineKind.Success => isLight ? L_Success : D_Success,
            LogLineKind.Error => isLight ? L_Error : D_Error,
            LogLineKind.Warning => isLight ? L_Warning : D_Warning,
            LogLineKind.Debug => isLight ? L_Debug : D_Debug,
            LogLineKind.Empty => Empty,
            _ => isLight ? L_Normal : D_Normal,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush ConvertProgressPart(string part, bool isLight) =>
        part switch
        {
            ProgressMetricPart => isLight ? L_Progress : D_Progress,
            ProgressSeparatorPart => isLight ? L_ProgressSeparator : D_ProgressSeparator,
            ProgressOperationPart => isLight ? L_ProgressOperation : D_ProgressOperation,
            ProgressGuidPart => isLight ? L_ProgressGuid : D_ProgressGuid,
            ProgressAssetPart => isLight ? L_ProgressAsset : D_ProgressAsset,
            ProgressTemplatePart => isLight ? L_ProgressTemplate : D_ProgressTemplate,
            _ => isLight ? L_Normal : D_Normal
        };
}
