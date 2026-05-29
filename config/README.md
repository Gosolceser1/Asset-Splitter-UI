# config - External Configuration Directory

## Overview

The `config` folder contains **all external configuration files** for Asset Processor. These files control how assets are extracted, processed, and organized without requiring code changes. The Avalonia UI, console runner, and shared config loader all use these files at runtime.

## Quick links

| Subfolder | README |
|-----------|--------|
| [01_Templates/](01_Templates/README.md) | Which asset templates to extract |
| [02_Processing_Rules/](02_Processing_Rules/README.md) | Fixlist (merge + dependency resolution) |
| [03_Regional_Ingredients/](03_Regional_Ingredients/README.md) | Anno 1800 regional ingredient mappings |
| [04_Comment_Whitelist/](04_Comment_Whitelist/README.md) | Properties eligible for GUID comments |
| [05_Console_Messages/](05_Console_Messages/README.md) | Console output **and** generated mod README strings |

Shipped next to `AssetSplitterUI.exe` / `AssetProcessor.exe` in release builds. Edit copies in the repo, then rebuild or republish to refresh release folders.

## Purpose

This directory provides:
- ✅ **Game-specific templates** - Which asset templates to extract (Anno 1800: 81, Anno 117: 67)
- ✅ **Priority fixlists** - Which templates get expensive dependency resolution (Anno 1800: 120, Anno 117: 29)
- ✅ **Regional mappings** - Anno 1800 Africa DLC ingredient replacements
- ✅ **Application settings** - Global processing parameters
- ✅ **Comment whitelists** - Which asset properties get translated GUID comments
- ✅ **Console messages** - Translated console output strings (11 languages bundled: EN, DE, ES, FR, IT, JA, KO, PL, RU, TW, ZH)

## File Organization

### Configuration Files

| File | Purpose | Format | Game |
|------|---------|--------|------|
| `01_Templates/Anno1800_Templates.txt` | Assets to extract | Text list (1 per line) | Anno 1800 |
| `01_Templates/Anno117_Templates.txt` | Assets to extract | Text list (1 per line) | Anno 117 |
| `02_Processing_Rules/Anno1800_Fixlist.txt` | Dependency resolution | Text list (1 per line) | Anno 1800 |
| `02_Processing_Rules/Anno117_Fixlist.txt` | Dependency resolution | Text list (1 per line) | Anno 117 |
| `app_settings.json` | Global settings | JSON object | Both |
| `03_Regional_Ingredients/regional_ingredients.json` | Africa DLC mappings | JSON object | Anno 1800 |
| `04_Comment_Whitelist/Anno1800_Comment_Whitelist.txt` | Commentable properties | Text list | Anno 1800 |
| `04_Comment_Whitelist/Anno117_Comment_Whitelist.txt` | Commentable properties | Text list | Anno 117 |
| `05_Console_Messages/console_en.json` | Console strings (English) | JSON object | Both |
| `05_Console_Messages/console_ru.json` | Console strings (Russian) | JSON object | Both |
| `README.md` | This documentation | Markdown | - |

### Comment Whitelist Directory

| Path | Purpose |
|------|---------|
| `04_Comment_Whitelist/` | Comment whitelist files |
| `04_Comment_Whitelist/Anno1800_Comment_Whitelist.txt` | Property names for -c flag (Anno 1800) |
| `04_Comment_Whitelist/Anno117_Comment_Whitelist.txt` | Property names for -c flag (Anno 117) |

## Detailed File Descriptions

### 1. 01_Templates/Anno1800_Templates.txt

**Purpose**: List of asset templates to **EXTRACT** from Anno 1800

**Format**: One template name per line (text file, UTF-8 encoded)

**Content**: 81 unique templates for Anno 1800

**Example**:
```
Skin
Slot
Street
FactoryBuilding7
ProductList
ResidenceBuilding
Road
```

**Processing Phase**: Phase 3 (Asset Extraction)

**Usage**:
- Backend loads this file during startup for Anno 1800 detection
- Only assets matching these templates are extracted to ModOp files
- If file missing: Uses hardcoded `templates_used[]` array as fallback

**CLI Override**: 
```powershell
AssetProcessor ... -u:config/custom_templates.txt
```

---

### 2. 01_Templates/Anno117_Templates.txt

**Purpose**: List of asset templates to **EXTRACT** from Anno 117

**Format**: One template name per line (text file, UTF-8 encoded)

**Content**: 67 unique templates for Anno 117

**Processing Phase**: Phase 3 (Asset Extraction)

**Usage**:
- Loaded when Anno 117 game detected
- Fewer templates than Anno 1800 (67 vs 81)
- Different extraction profile optimized for Anno 117

---

### 3. 02_Processing_Rules/Anno1800_Fixlist.txt

**Purpose**: Priority templates for **DEPENDENCY RESOLUTION** (Phase 4 - Template Merging)

**Format**: One template name per line (text file, UTF-8 encoded)

**Content**: 120 priority templates for Anno 1800

**Example**:
```
ActionPauseBuilding
AirShip
Animal
AssemblyLine
```

**Processing Phase**: Phase 4 (Template Merging)

**Usage**:
- Backend loads ONCE at startup (avoids infinite loop bugs)
- Only assets with templates in this list get expensive template merging
- Merges complete template properties from templates.xml
- Creates self-contained ModOp files without external references
- Processes properties of ~120 templates (significant performance cost)

**Performance Impact**:
- ⚡ **Fast**: Skip templates not in fixlist (avoids expensive merging)
- 🐢 **Slow**: Every template in fixlist processes completely (expensive operation)

**CLI Override**: 
```powershell
AssetProcessor ... -x:config/custom_fixlist.txt
```

---

### 4. 02_Processing_Rules/Anno117_Fixlist.txt

**Purpose**: Priority templates for **DEPENDENCY RESOLUTION** (Anno 117)

**Format**: One template name per line (text file, UTF-8 encoded)

**Content**: 29 priority templates for Anno 117

**Usage**:
- Applied only when Anno 117 game detected
- Much smaller fixlist than Anno 1800 (29 vs 120)
- Optimized for Anno 117's different asset structure

---

### 5. app_settings.json

**Purpose**: Global application defaults and processing parameters

**Format**: JSON object (UTF-8 encoded)

**Top-level structure**:
```json
{
  "description": "AssetProcessor application settings and default configurations",
  "version": "1.1",
  "settings": {
    "default_paths": { "anno1800": "...", "anno117": "..." },
    "file_processing": {
      "xml_extension": "*.xml",
      "progress_interval": 100,
      "max_console_output_lines": 2000,
      "stream_buffer_size": 8192,
      "processing_timeout_seconds": 300
    },
    "output_structure": {
      "source_xml_folder": "source_xml",
      "output_xml_folder": "output_xml",
      "baseasset_folder": "BaseAssetGUID",
      "test_extractions_folder": "AssetSplit_Output"
    },
    "fallback_behavior": {
      "use_built_in_defaults": true,
      "continue_on_missing_config": true,
      "log_missing_config_warnings": true
    },
    "supported_languages": ["english", "french", "german", ...],
    "required_files": { "anno1800": [...], "anno117": [...] },
    "regional_processing": {
      "anno1800_enabled": true,
      "anno117_enabled": false
    }
  }
}
```

**Key settings**:
- **processing_timeout_seconds**: Max seconds per operation (default: 300)
- **max_console_output_lines**: Log line cap in the UI (default: 2000)
- **fallback_behavior**: Controls whether missing config files cause hard failures or silent fallback to built-in defaults
- **supported_languages**: Languages available for extraction (UI and console)
- **regional_processing.anno1800_enabled**: Enables Africa DLC ingredient replacement for Anno 1800
- **regional_processing.anno117_enabled**: Disabled for Anno 117 (no regional ingredients)

**Usage**:
- Loaded at application startup
- If missing: app falls back to hardcoded defaults and continues normally

---

### 6. 03_Regional_Ingredients/regional_ingredients.json

**Purpose**: **ANNO 1800 ONLY** - Africa DLC ingredient GUID mappings

**Format**: JSON object with region-specific mappings (UTF-8 encoded)

**Top-level structure**:
```json
{
  "description": "Regional ingredient mappings for Anno 1800 cost replacement system",
  "version": "1.0",
  "games": {
    "anno1800": {
      "africa":    { "ingredients": { "ingredients": [{ "guid": "114356", "name": "Wanza Wood" }, ...] } },
      "default":   { "ingredients": { "ingredients": [{ "guid": "1010196", "name": "Timber" }, ...] } },
      "universal": { "ingredients": { "ingredients": [{ "guid": "1010017", "name": "Money" }, ...] } }
    },
    "anno117": { "enabled": false }
  }
}
```

**Processing Phase**: Phase 6 (Formatting & Cleanup)

**Ingredient Replacements**:

| Item | African GUID | Default GUID | Region |
|------|--------------|--------------|--------|
| Wood/Wanza | 114356 | 1010196 | Africa DLC |
| Bricks/Mud | 114402 | 1010205 | Africa DLC |
| Concrete | - | 1010202 | Default only |

**Usage**:
- Only applied if building folder path contains "Africa"
- Replaces ingredient GUIDs in cost calculations
- **Anno 117**: Processing disabled (no regional ingredients)

**Example Processing**:
```
Building: ResidenceBuilding_Africa_01.xml
Detection: Folder contains "Africa" → YES
Action: Replace ingredient GUIDs

Before: <Amount>1010196</Amount>  (Standard Timber)
After:  <Amount>114356</Amount>   (Wanza Wood - Africa variant)
```

---

### 7. 04_Comment_Whitelist/Anno1800_Comment_Whitelist.txt

**Purpose**: Property node names eligible for translated GUID comments (-c flag)

**Format**: One property name per line (text file, UTF-8 encoded)

**Content Example**:
```
BuildingStorage
ConstructionCosts
Cooldown
DaytimeProductivity
EffectHitRatePercentage
FactoryProductivity
```

**Processing Phase**: Phase 6 (Formatting) - when -c flag used

**Usage**:
- Backend reads during Phase 6 (Formatting)
- Creates `prop_result[]` list of whitelisted nodes
- When `-c` flag used: Adds translated GUID comments ONLY to whitelisted nodes
- Other XML nodes: Comments NOT added (prevents XML bloat)

**Example with Whitelist**:
```xml
<!-- Property IS whitelisted - comment added -->
<BuildingStorage>
  <Item>
    <GUID>1010196</GUID>  <!-- [Timber] -->
  </Item>
</BuildingStorage>

<!-- Property NOT whitelisted - comment NOT added -->
<UnknownProperty>
  <GUID>1010196</GUID>
</UnknownProperty>
```

---

### 8. 04_Comment_Whitelist/Anno117_Comment_Whitelist.txt

**Purpose**: Property node names eligible for translated GUID comments for Anno 117 (same role as the Anno 1800 file above)

**Format**: One property name per line (text file, UTF-8 encoded)

**Usage**:
- Applied when Anno 117 game detected
- Different whitelist than Anno 1800 — reflects Anno 117's different asset structure
- Prevents unintended comment insertion on Anno 117-specific properties

---

### 9. 05_Console_Messages/console_{lang}.json

**Purpose**: Localized strings for console (AssetProcessor.exe) output

**Format**: JSON key/value pairs (UTF-8 encoded)

**Bundled languages**: EN, DE, ES, FR, IT, JA, KO, PL, RU, TW, ZH (11 files)

**Usage**:
- Backend loads `console_{language}.json` matching the selected language
- Falls back to `console_en.json` if the requested locale file is missing
- Placeholders like `{0}` are preserved for string formatting at runtime
- To add a language: copy `console_en.json` to `console_{lang}.json` and translate the values (keep all keys unchanged)

---

## Loading Order (Priority)

Backend loads configuration in this order (first found wins):

```
1. Command-line override flags (-u, -x)
   │
   ├─ YES → Use custom file (stops here)
   └─ NO  → Continue
           ↓
2. External config files (config/01_Templates/Anno*_Templates.txt)
   │
   ├─ File exists → Load it
   └─ File missing → Continue
                     ↓
3. Hardcoded fallback arrays (built-in defaults)
   │
   └─ Always used
```

**Example**:
```powershell
AssetProcessor "C:\Anno1800" output english -u:config/custom.txt

# Processing:
# 1. Check command-line: -u:config/custom.txt
#    → FOUND "custom.txt" → Load and use it (STOP)
# (2 and 3 skipped)
```

---

## Fallback System (Graceful Degradation)

If configuration files are missing, backend uses **hardcoded defaults**:

```csharp
// If 01_Templates/Anno1800_Templates.txt missing:
public static string[] templates_used = new string[] { 
    "Skin", "Slot", "Street", ... // 81 templates
};

// If 02_Processing_Rules/Anno1800_Fixlist.txt missing:
public static string[] fixlist = new string[] { 
    "ActionPauseBuilding", "AirShip", ... // 120 templates
};

// If app_settings.json missing:
ProcessingTimeout = 300000;
SupportedLanguages = new[] { "english", "french", "german", ... };
```

**Benefits**:
- ✅ Application continues working if config files deleted/corrupted
- ✅ Users can delete config files to reset to defaults
- ✅ Development/testing with minimal configuration

---

## Game Detection & Config Selection

Backend detects game type automatically and selects correct configs:

```csharp
string gameType = DetectGameType(gamePath);

if (gameType == "anno1800") {
    templates = LoadFile("config/01_Templates/Anno1800_Templates.txt");
    fixlist = LoadFile("config/02_Processing_Rules/Anno1800_Fixlist.txt");
    comments = LoadFile("config/04_Comment_Whitelist/Anno1800_Comment_Whitelist.txt");
    regionalIngredients = LoadFile("config/03_Regional_Ingredients/regional_ingredients.json");
} 
else if (gameType == "anno117") {
    templates = LoadFile("config/01_Templates/Anno117_Templates.txt");
    fixlist = LoadFile("config/02_Processing_Rules/Anno117_Fixlist.txt");
    comments = LoadFile("config/04_Comment_Whitelist/Anno117_Comment_Whitelist.txt");
    // regionalIngredients not loaded (Anno 117 has no regions)
}
```

**Detection Logic**:
- Checks `assets.xml` for "Anno 117" or "Pax Romana" text
- Defaults to Anno 1800 if ambiguous
- Can be overridden by external config file

---

## Configuration Format Specifications

### Template Lists (*.txt files)

**Format Requirements**:
- One template name per line
- UTF-8 encoding
- Empty lines: Ignored
- Comments: NOT supported
- Whitespace: Trimmed before/after each line

**Valid Example** ✅:
```
Skin
Slot
Street
FactoryBuilding7
```

**Invalid Example** ❌:
```
Skin              # This is a comment - NOT supported!

Street            // C++ style comments not valid
```

### JSON Configuration Files

**Format Requirements**:
- Valid JSON 2.0 specification
- UTF-8 encoding
- Double quotes required (not single quotes)
- Trailing commas: NOT allowed
- Comments: NOT supported in JSON

**Valid Example** ✅:
```json
{
  "africanRegion": {
    "wanzaWood": "114356",
    "mudBricks": "114402"
  }
}
```

**Invalid Example** ❌:
```json
{
  "africanRegion": {
    "wanzaWood": "114356",  // Comments NOT allowed in JSON
    "mudBricks": "114402",  // Trailing comma also invalid
  }
}
```

---

## CLI processing flags

The GUI maps these to checkboxes; when running the console directly you can pass:

| Flag | Purpose |
|------|---------|
| `-c` | Add translated GUID comments (uses comment whitelist) |
| `-f` | Resolve BaseAssetGUID (full parent data) |
| `-t` | Organize output by template folder |
| `-y` | Overwrite existing output |
| `-d` | Debug / verbose output |
| `-u:path` | Custom template list file |
| `-x:path` | Custom fixlist file |
| `--no-modops-wrap` | Save raw `<Asset>` XML only (no ModOps/ModOp wrapper) |
| `--no-default-properties` | Do not fill missing properties from properties.xml when merging |
| `--split-templates` | Split templates.xml into one file per template in template-named folders |

By default (no flags): output is ModOps-wrapped, default properties are applied, templates.xml is not split.

---

## Usage Examples

### Standard Extraction (Using Configs)
```powershell
# Uses all config files with defaults
AssetProcessor "C:\Anno1800" "./output" english -c -f -t -y

# Automatically loads:
# - 01_Templates/Anno1800_Templates.txt (81 templates)
# - 02_Processing_Rules/Anno1800_Fixlist.txt (120 templates)
# - 04_Comment_Whitelist/Anno1800_Comment_Whitelist.txt
# - 03_Regional_Ingredients/regional_ingredients.json
```

### Custom Template List
```powershell
# Override template extraction list
AssetProcessor "C:\Anno1800" "./output" english -c -f -t -y -u:config/subset_templates.txt

# -u flag: Use "subset_templates.txt" instead of "01_Templates/Anno1800_Templates.txt"
# Result: Only templates in subset_templates.txt are extracted
```

### Custom Fixlist (Dependency Resolution)
```powershell
# Override priority templates for merging
AssetProcessor "C:\Anno1800" "./output" english -c -f -t -y -x:config/priority_fixlist.txt

# -x flag: Use "priority_fixlist.txt" instead of "02_Processing_Rules/Anno1800_Fixlist.txt"
# Result: Different templates processed for dependency resolution
```

### Debug Mode (Additional Logging)
```powershell
# Enable debug logging
AssetProcessor "C:\Anno1800" "./output" english -c -f -t -y -d

# Outputs: [INFO], [DEBUG], [EXTRACT], [MERGE], [FIX], [COMPLETE] messages
# Useful for troubleshooting extraction issues
```

---

## Best Practices

### ✅ DO

- **Keep backups**: Save original config files before modifying
- **Test incrementally**: Change one setting at a time
- **Validate JSON**: Use online JSON validator (jsonlint.com) for complex configs
- **Document changes**: Add notes about why you modified configs
- **Use version control**: Track config changes in Git
- **UTF-8 encoding**: Save text files with UTF-8 encoding

### ❌ DON'T

- **Don't edit while extraction running**: Close application and restart
- **Don't remove all templates**: Keep at least 1 template in extraction list
- **Don't use invalid JSON**: Causes silent failures and defaults applied
- **Don't forget UTF-8**: Some editors default to ANSI - verify encoding
- **Don't hardcode paths**: Keep paths relative to project root
- **Don't copy JSON without validation**: Errors silently use fallback

---

## Troubleshooting Config Issues

### Issue: "Extraction skipping assets"
**Causes & Solutions**:
1. ✅ Check `01_Templates/Anno*_Templates.txt` file exists
2. ✅ Verify asset templates are correctly listed (one per line)
3. ✅ Check for typos in template names (case-sensitive)
4. ✅ Ensure file is UTF-8 encoded (not ANSI)
5. ✅ Verify file not empty (should have ~81 or 67 templates)

### Issue: "Dependencies not resolving"
**Causes & Solutions**:
1. ✅ Check `02_Processing_Rules/Anno*_Fixlist.txt` file exists
2. ✅ Add missing templates to fixlist
3. ✅ Verify template names match exactly (case-sensitive)
4. ✅ Try with MORE templates in fixlist (might be too restrictive)
5. ✅ Run with `-d` debug flag to see which templates are processed

### Issue: "Comments not appearing"
**Causes & Solutions**:
1. ✅ Verify you're using `-c` flag (Add Comments)
2. ✅ Check `04_Comment_Whitelist/Anno1800_Comment_Whitelist.txt` or `Anno117_Comment_Whitelist.txt`
3. ✅ Verify property names in whitelist match your XML (case-sensitive)
4. ✅ Ensure GUI checkboxes are enabled (if using GUI)

### Issue: "Africa ingredients not replaced"
**Causes & Solutions**:
1. ✅ Check `03_Regional_Ingredients/regional_ingredients.json` file exists
2. ✅ Verify GUID values are correct
3. ✅ Ensure building folder name contains "Africa" (detection requirement)
4. ✅ Confirm file is valid JSON (use validator)
5. ✅ Note: Only applies to Anno 1800 (Anno 117 disabled)

### Issue: "Config file not found"
**Expected Behavior** ✅:
Backend will use hardcoded defaults - extraction continues successfully
This is **not an error**, just using fallback configuration.

---

## Configuration Examples

### Minimal Setup (All Defaults)
```
# No config files needed!
# Backend uses hardcoded arrays and defaults
# Extraction: ✅ Works perfectly
```

### Full Customization
```
config/
├── app_settings.json                 (global settings)
├── README.md                         (this file)
├── 01_Templates/
│   ├── Anno1800_Templates.txt        (81 templates)
│   └── Anno117_Templates.txt         (67 templates)
├── 02_Processing_Rules/
│   ├── Anno1800_Fixlist.txt          (120 templates)
│   └── Anno117_Fixlist.txt           (29 templates)
├── 03_Regional_Ingredients/
│   └── regional_ingredients.json     (Africa mappings)
├── 04_Comment_Whitelist/
│   ├── Anno1800_Comment_Whitelist.txt
│   └── Anno117_Comment_Whitelist.txt
└── 05_Console_Messages/
  ├── console_en.json
  └── console_ru.json
```

### Custom Workflow (Subset Extraction)
```
Extract only 10 specific templates:

1. Create: config/my_templates.txt with 10 template names
2. Run: AssetProcessor ... -u:config/my_templates.txt
3. Result: Only those 10 templates extracted (much faster!)

Useful for: Testing, specific asset categories, performance tuning
```

---

## File Statistics

| Category | Anno 1800 | Anno 117 |
|----------|-----------|----------|
| Extraction Templates | 81 | 67 |
| Fixlist Templates | 120 | 29 |
| Comment Whitelist Entries | ~50+ | ~50+ |
| Console Messages (bundled) | 11 languages | 11 languages |

---
