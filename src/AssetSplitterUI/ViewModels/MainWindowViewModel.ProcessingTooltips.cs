using AssetSplitterUI.Localization;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    public string CommentsToolTipText => ToggleTip(AddComments, "tooltips.commentsWhenOff", "tooltips.commentsWhenOn");
    public string DependenciesToolTipText => ToggleTip(FixDependencies, "tooltips.dependenciesWhenOff", "tooltips.dependenciesWhenOn");
    public string IncludeDefaultPropertiesToolTipText => ToggleTip(IncludeDefaultProperties, "tooltips.includeDefaultPropertiesWhenOff", "tooltips.includeDefaultPropertiesWhenOn");
    public string ModOpsWrapToolTipText => ToggleTip(ModOpsWrap, "tooltips.modOpsWrapWhenOff", "tooltips.modOpsWrapWhenOn");
    public string TemplateFoldersToolTipText
    {
        get
        {
            if (IsSingleGuidMode)
            {
                return StringResourceManager.Instance.GetString("tooltips.templatesIgnoredInSingleGuid");
            }

            return ToggleTip(CreateTemplateFolders, "tooltips.templatesWhenOff", "tooltips.templatesWhenOn");
        }
    }

    public string SplitTemplatesToolTipText
    {
        get
        {
            if (IsSingleGuidMode)
            {
                return StringResourceManager.Instance.GetString("tooltips.splitTemplatesIgnoredInSingleGuid");
            }

            return ToggleTip(SplitTemplates, "tooltips.splitTemplatesWhenOff", "tooltips.splitTemplatesWhenOn");
        }
    }
    public string CreateAssetModsToolTipText => ToggleTip(CreateAssetMods, "tooltips.createAssetModsWhenOff", "tooltips.createAssetModsWhenOn");
    public string DebugModeToolTipText => ToggleTip(DebugMode, "tooltips.debugModeWhenOff", "tooltips.debugModeWhenOn");
    public string ForceFullRebuildToolTipText => ToggleTip(ForceFullRebuild, "tooltips.forceFullRebuildWhenOff", "tooltips.forceFullRebuildWhenOn");

    private static string ToggleTip(bool isOn, string offKey, string onKey) =>
        StringResourceManager.Instance.GetString(isOn ? onKey : offKey);

    private void NotifyProcessingTooltipsChanged()
    {
        OnPropertyChanged(nameof(CommentsToolTipText));
        OnPropertyChanged(nameof(DependenciesToolTipText));
        OnPropertyChanged(nameof(IncludeDefaultPropertiesToolTipText));
        OnPropertyChanged(nameof(ModOpsWrapToolTipText));
        OnPropertyChanged(nameof(TemplateFoldersToolTipText));
        OnPropertyChanged(nameof(SplitTemplatesToolTipText));
        OnPropertyChanged(nameof(CreateAssetModsToolTipText));
        OnPropertyChanged(nameof(DebugModeToolTipText));
        OnPropertyChanged(nameof(ForceFullRebuildToolTipText));
    }
}
