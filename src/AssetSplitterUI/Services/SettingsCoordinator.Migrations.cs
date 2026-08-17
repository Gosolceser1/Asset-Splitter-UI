using System.Collections.ObjectModel;

namespace AssetSplitterUI.Services;

public partial class SettingsCoordinator
{
    private bool ApplyModOpsWrapMigration()
    {
        if (_settings is null)
        {
            return false;
        }

        if (_settings.NoModOpsWrapLegacy.HasValue)
        {
            _settings.ModOpsWrap = !_settings.NoModOpsWrapLegacy.Value;
            _settings.NoModOpsWrapLegacy = null;
        }
        return _settings.ModOpsWrap;
    }

    private bool ApplyIncludeDefaultPropertiesMigration()
    {
        if (_settings is null)
        {
            return false;
        }

        if (!_settings.IncludeDefaultProperties.HasValue && _settings.NoDefaultPropertiesLegacy.HasValue)
        {
            _settings.IncludeDefaultProperties = !_settings.NoDefaultPropertiesLegacy.Value;
        }

        return _settings.IncludeDefaultProperties ?? false;
    }
}
