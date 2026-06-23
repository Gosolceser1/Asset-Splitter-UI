namespace AssetSplitterUI.Services;

/// <summary>
/// Game asset language (<c>texts_*.xml</c>) is only required when GUID comments (-c) is enabled.
/// Every other flag (-f resolve parents, -t template folders, ModOps, default properties,
/// split templates, asset mods, single GUID, debug) runs with <c>language=none</c>.
/// </summary>
internal static class GameLanguageRunPolicy
{
    public static bool RequiresGameLanguage(bool addComments) => addComments;

    public static bool IsSelectionEnabled(bool addComments, bool hasGameLanguages) =>
        addComments && hasGameLanguages;

    public static string ResolveBackendLanguage(bool addComments, string? selectedLanguage) =>
        addComments && !string.IsNullOrWhiteSpace(selectedLanguage)
            ? selectedLanguage.Trim().ToLowerInvariant()
            : "none";

    public static bool TryValidate(bool addComments, bool hasGameLanguages, string? selectedLanguage, out string validationKey)
    {
        if (!addComments)
        {
            validationKey = "";
            return true;
        }

        if (!hasGameLanguages)
        {
            validationKey = "dialogs.noLanguagesForComments";
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedLanguage))
        {
            validationKey = "dialogs.selectLanguageForComments";
            return false;
        }

        validationKey = "";
        return true;
    }

    public static string GetTooltipLocalizationKey(bool addComments) =>
        addComments ? "tooltips.languageWhenOn" : "tooltips.languageWhenOff";
}
