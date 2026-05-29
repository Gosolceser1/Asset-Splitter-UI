# RDA Agent Knowledge

Copied from verified Anno 117 / Anno 1800 archive research notes and committed DLC research files.

Verified by live inspection of actual installed game archives in April 2026.

## Critical: Both games store RDAs in `maindata` subfolder

- Anno 117 path: `<game>\maindata\*.rda`
- Anno 1800 path: `<game>\maindata\*.rda`

## Archive Model

### Anno 117

- Named archives (not numbered): each archive serves a distinct role
- `zz_patchfiles_*.rda` — higher number = newer patch; **overrides** files from all named archives
- DLC archives (`dlc01_*`, `cdlc01_*`) are additive — new content, no base-file overrides
- `cdlc01_*` (data/cdlc01/) and `dlc01_*` (data/dlc01/) coexist for DLC01 — different internal paths

### Patch Files Content

| Archive | Files | MB | Sample content |
|---|---|---|---|
| zz_patchfiles_150.rda | 6,480 | 2,562 | Config overrides, DLC configs (dlc01–dlc12), updated Lua scripts, GUI texts, game data |
| zz_patchfiles_151.rda | 55 | 37 | Incremental patch — small targeted overrides |

### New DLC Archives (vs original cdlc01_*)

| Archive | Files | MB | Internal path | Content |
|---|---|---|---|---|
| cdlc01_graphics.rda | 1,862 | 2,293 | data/cdlc01/graphics/ | Ornaments, plazas, wall system, mosaic skins (Celtic+Roman) |
| dlc01_graphics.rda | 1,586 | 3,224 | data/dlc01/graphics/ | Roman buildings, ornaments, ground plazas, wall system |
| dlc01_provinces.rda | 8,837 | 1,400 | data/dlc01/provinces/ | DLC01 province terrain, maps, .prp files (Latium region) |

### Anno 117 Full Archive List (29 archives — verified live May 2026)

| Archive | Files | MB | Compressed | Contents |
|---|---|---|---|---|
| config.rda | 171 | 120 | — | All game config XML, texts, engine.ini, console config |
| shared_configs.rda | 32,314 | 2,545 | — | Shared/base graphics configs (.bfg/.cfg/.ifo), shared/cdlc01 graphics |
| script.rda | 1,031 | 9 | — | data/script/ — all Lua scripts, types, modules, cheats |
| infotips.rda | 1 | 2 | — | data/infotips/export.bin — single binary export |
| ui.rda | 17,866 | 7,800 | — | UI layout, assets, images, 2K/4K textures |
| graphics_roman.rda | 20,925 | 26,778 | — | Roman building models, textures, rdm/bfg |
| graphics_celtic.rda | 8,702 | 7,044 | — | Celtic building models, textures |
| graphics_roman_celtic.rda | 2,473 | 8,125 | — | Shared Roman+Celtic combined models |
| graphics_library.rda | 20,919 | 8,682 | — | Shared graphics library (clouds, ambient, common textures) |
| graphics_portrait.rda | 1,004 | 7,108 | — | Character portraits (specialists, NPCs) |
| graphics_skins.rda | 1,676 | 1,387 | — | Building skin variants |
| graphics_engine.rda | 177 | 81 | — | Engine-level graphics |
| graphics_ui.rda | 1,894 | 687 | — | UI-specific graphics and textures |
| graphics_misc.rda | 42 | 11 | — | Miscellaneous graphics |
| cdlc01_graphics.rda | 1,862 | 2,293 | — | DLC01 graphics (Celtic/Roman ornaments, wall system, mosaic skins) |
| dlc01_graphics.rda | 1,586 | 3,224 | — | DLC01 graphics (`data/dlc01/` — Roman ornaments, plazas, wall system) |
| dlc01_provinces.rda | 8,837 | 1,400 | — | DLC01 province data (Latium/Volcano region) |
| shaders.rda | 30,662 | 12,217 | — | HLSL shaders (DX12) |
| provinces_roman.rda | 29,545 | 5,191 | — | Roman province data (.prp files, terrain) |
| provinces_celtic.rda | 30,810 | 4,442 | — | Celtic province data (.prp files, terrain) |
| video.rda | 135 | 17,698 | — | Cutscene videos (.bk2 Bink format) |
| sound.rda | 9,704 | 680 | — | Audio files (.wem Wwise format) |
| file_browse_patterns.rda | 138 | 8 | — | File browser pattern definitions |
| en_us0.rda | 12,835 | 460 | — | English localization (audio + text) |
| de_de0.rda | 13,957 | 510 | — | German localization |
| fr_fr0.rda | 13,954 | 496 | — | French localization |
| zh_cn0.rda | 21,308 | 709 | — | Chinese localization |
| zz_patchfiles_150.rda | 6,480 | 2,562 | — | Patch v150 — override files (config, scripts, DLC configs) |
| zz_patchfiles_151.rda | 55 | 37 | — | Patch v151 — small incremental update overlay |

**Archive Override Model (Anno 117):**
- Named archives are content-partitioned (no numeric override like Anno 1800)
- `zz_patchfiles_*.rda` — higher number = newer patch; these OVERRIDE files from named archives
- DLC archives (`dlc01_*`, `cdlc01_*`) are additive — they add new content without overriding base files
- `cdlc01_*` and `dlc01_*` — both serve DLC01 content but from different internal paths (`data/cdlc01/` vs `data/dlc01/`)

### Anno 1800

- 36 archives total: `data0.rda` through `data33.rda` + `en_us0.rda` + `ru_ru0.rda`
- Higher number = newer patch/DLC, **overrides** files from lower-numbered archives
- Authoritative version of any file is always in the **highest-numbered** archive that contains it
- Locale archives (`en_us0`, `ru_ru0`) contain only voice/sound banks

### Anno 1800 Archive Content Map (verified live May 2026)

| Archive | MB | Files | Content tags | Primary content |
|---------|-----|-------|-------------|----------------|
| data0   | 218 | 5,640 | assets,templates,texts,props,scripts,pypredef | Base game: assets, templates, texts, Python scripts, config |
| data1   | 8,809 | 57,000 | — | Shaders (HLSL) |
| data2   | 2,134 | 42,008 | — | UI 2K images (console port icons) |
| data3   | 8,672 | 65 | — | UI video (pre-rendered cutscenes) |
| data4   | 43 | 32 | — | Blacklist data |
| data5   | 25 | 213 | — | Session data |
| data6   | 8,749 | 83,487 | — | Session graphics (models/effects) |
| data7   | 140 | 859 | — | Sessions + benchmark config |
| data8   | 11,075 | 34,177 | — | Graphics (models, effects, textures) |
| data9   | 856 | 391 | — | Sound (base audio) |
| data10  | 511 | 3,536 | assets,templates,texts,props | DLC content batch 1 |
| data11  | 2,450 | 12,255 | assets,templates,texts,props | DLC content batch 2 |
| data12  | 2,299 | 7,323 | assets,templates,texts,props | DLC content batch 3 |
| data13  | 5,943 | 22,658 | assets,templates,texts,props,scripts | DLC + benchmark scripts |
| data14  | 1,048 | 4,457 | assets,templates,texts,props,scripts | DLC + scripts |
| data15  | 1,003 | 4,249 | assets,templates,texts,props | DLC content |
| data16  | 6,478 | 27,478 | assets,templates,texts,props | DLC content |
| data17  | 1,513 | 3,581 | assets,templates,texts,props | DLC content |
| data18  | 2,075 | 19,611 | assets,templates,texts,props | DLC content |
| data19  | 2,076 | 20,451 | assets,templates,texts,props | DLC content |
| data20  | 523 | 1,876 | assets,templates,texts,props | DLC content |
| data21  | 3,000 | 16,644 | assets,templates,texts,props,scripts | DLC + scripts |
| data22  | 1,971 | 16,115 | assets,templates,texts,props,scripts | DLC + scripts |
| data23  | 389 | 1,073 | assets,texts,props | DLC content |
| data24  | 5,601 | 33,071 | assets,templates,texts,props,scripts | DLC + scripts |
| data25  | 5,888 | 34,072 | assets,templates,texts,props | DLC content |
| data26  | 726 | 861 | assets,templates,texts,props | DLC content |
| data27  | 1,542 | 6,831 | assets,templates,texts,props | DLC content |
| data28  | 785 | 981 | assets,datasets,texts | Latest patches; **datasets.xml** starts here |
| data29  | 817 | 2,204 | assets,datasets,texts | Latest patches |
| data30  | 888 | 1,504 | assets,datasets,texts | Latest patches |
| data31  | 946 | 1,473 | assets,datasets,texts,props | Latest patches |
| data32  | 940 | 1,612 | assets,datasets,texts | Latest patches |
| data33  | 942 | 1,793 | assets,datasets,texts | **Newest patch** — authoritative for most config |
| en_us0  | 1,092 | 17,723 | — | English US voice/sound banks |
| ru_ru0  | 1,010 | 17,693 | — | Russian voice/sound banks |

## Key File Paths

### Anno 117 — key paths inside archives

All in `config.rda`:

- `data/base/config/export/assets.xml`
- `data/base/config/export/templates.xml`
- `data/base/config/export/properties.xml`
- `data/base/config/export/properties-meta.xml`
- `data/base/config/game/datasets.xml`
- `data/base/config/gui/texts_english.xml`
- `data/base/config/console/console.xml`
- `data/base/config/console/balancing/balancing.xml`
- `data/base/config/game/engine.ini`
- `data/base/config/online/storm.ini`

### Anno 1800 — key paths inside archives

Spread across multiple data*.rda (highest-numbered wins):

- `data/config/export/main/asset/assets.xml`        ← in data0+data10-data33
- `data/config/export/main/asset/templates.xml`     ← in data0+data10-data27
- `data/config/export/main/asset/properties.xml`    ← in data0+data10-data25+
- `data/config/export/main/asset/datasets.xml`      ← in data28-data33
- `data/config/gui/texts_english.xml`               ← in data0+data10-data33
- `data/script/predefs/anno6.pypredef`

### Comparison: config paths Anno 117 vs Anno 1800

| File | Anno 117 path | Anno 1800 path |
|---|---|---|
| assets.xml | `data/base/config/export/assets.xml` | `data/config/export/main/asset/assets.xml` |
| templates.xml | `data/base/config/export/templates.xml` | `data/config/export/main/asset/templates.xml` |
| properties.xml | `data/base/config/export/properties.xml` | `data/config/export/main/asset/properties.xml` |
| datasets.xml | `data/base/config/game/datasets.xml` | `data/config/export/main/asset/datasets.xml` |
| texts_english.xml | `data/base/config/gui/texts_english.xml` | `data/config/gui/texts_english.xml` |

## Script Systems

### Anno 1800 script system

- Uses **Python** (CPython 3.5) embedded in engine
- Base scripts in `data0.rda\data/script/`
- Subdirs: `benchmark/`, `flow/`, `lib/` (stdlib), `predefs/`, `pydevd/` (debugger), `scripts/`, `support/`, `system/`
- `data/script/lib/` = full Python stdlib — **exclude from cheat searches**
- `data/script/pydevd/` = PyCharm remote debugger — **exclude from cheat searches**
- `data/script/predefs/anno6.pypredef` = game API stubs:
  - `Console` class: `.autoComplete()`, `.bind(shortcut, script)`, `.print()`
  - `Game` class: `.activateCheat(cheatEnum, activate)` — **THE** cheat activation
  - `Cheat` class: `NO_BUILDCOSTS = 0` — only 1 documented cheat constant
  - `Session` class: `.createDebugWalker()`, `.load()`, `.save()`, `.setDebugPPHandle()`
- DLC data archives do NOT contain updated pypredef — data0 is definitive

### Anno 117 script system (COMPLETE)

- Uses **Lua** (not Python) — complete redesign from Anno 1800
- All scripts in `script.rda\data/script/`
- **1013 total files** decomposed as:
  - `core/core.lua` — Lua engine bootstrap: overrides `dofile`, `require`, `print`, `coroutine.create`, adds `dir()`, `inspect()`, `getTypeInfo()`, `resultToString()`
  - `core/scriptsystem.lua` — `system.wait(ms)`, `system.waitPP(ms)`, `system.waitUILoaded()`, traceback helpers
  - `modules/json.lua` — JSON encode/decode
  - `modules/sessions.lua` — all Session GUIDs: `Base_Province_Roman_Italia=3245`, `Base_Province_Celtic_Britannia=6627`, benchmark sessions, test maps
  - `modules/specialguids.lua` — **280+ special GUIDs** for all singleton assets (SpecialGUIDs table)
  - `modules/testhelper.lua` — automated test harness: `startNewGameBlocking()`, `saveGameBlocking()`, `startNewMPGameBlocking()`, `takeScreenshot()`, DTest integration
  - `types/events.lua` — event declarations: `OnSessionLoaded`, `OnSessionEnter`, `OnMetaGameUnloaded`, `OnCameraSequenceEnd`, `OnLeaveUIState`, `OnGameSetupLoaded`
  - `types/rdgs.lua` — rdgs type stubs: `rdgs.ScriptEvent` (Add/Remove/RemoveByName/RemoveAll), `rdgs.CAsyncFunction`, type aliases
  - `types/generated/datasets/` — **~300 dataset enum Lua files** (one per dataset type)
  - `types/generated/rdgs/` — **430 Lua binding files** (one per C++ class exposed to Lua)
  - `content/cheats/` — 4 cheat scripts

### Anno 117 global Lua objects (verified)

- `World`
- `GameSession`
- `Island`
- `AreaManager`
- `AreaPopulation`
- `Economy`
- `AreaEconomy`
- `Participants`
- `Cheat`
- `Cheats`
- `Diplomacy`
- `Quests`
- `Unlock`
- `Selection`
- `Volcano`

## Search Patterns — copied from verified notes

### "Find cheats"

- **Anno 1800**: `rda-find` in data0 for `anno6.pypredef`; look at `class Cheat:` and `Game.activateCheat()`
- **Anno 117**: `rda-find "cheat"` across `script.rda` only; all cheat files are in `data/script/content/cheats/` and `data/script/types/generated/rdgs/c*cheat*`

### "Find assets/game data"

- **Anno 1800**: `assets.xml` in data0 (base) + any data10–data33 (DLC/patches override)
- **Anno 117**: `config.rda` only → `data/base/config/export/assets.xml`

### "Find game balance/settings"

- **Anno 1800**: `data/config/export/main/asset/datasets.xml` in data28–data33 (newest = most authoritative)
- **Anno 117**: `config.rda` → `data/base/config/game/datasets.xml` and `data/base/config/console/balancing/balancing.xml`

### "Find texts/localisation"

- **Anno 1800**: `data/config/gui/texts_english.xml` in data0, overridden by data10+
- **Anno 117**: `config.rda` → `data/base/config/gui/texts_english.xml`

### "Find scripts/game logic"

- **Anno 1800**: Python in `data0.rda\data/script/` (exclude `lib/` and `pydevd/`)
- **Anno 117**: Lua in `script.rda\data/script/` — `content/` = game logic, `types/generated/` = API stubs

### "Find graphics/models for X"

- **Anno 1800**: data8 (base graphics), data1/data6 (DLC graphics), search with `rda-find "X"`
- **Anno 117**: `graphics_roman.rda` (Roman content), `graphics_celtic.rda` (Celtic), `shared_configs.rda` (shared)

## Noise Patterns — copied from verified notes

**These are automatically suppressed in `rda-find` and `rda-grep` for Anno 1800 unless `-IncludeNoise` is used.**

- `data/ui/2kimages/` = 100k+ Xbox/PS/PC UI icon textures (DDS)
- `data/ui/studio/` = rendered UI scene images
- `data/ui/backgrounds/` = background DDS images
- `data/graphics/` = 3D graphics/normal-map textures
- `data/script/lib/` = Python stdlib
- `data/script/pydevd/` = PyCharm debugger
- `data/sound/` / `en_us0.rda` / `ru_ru0.rda` = audio
- `data/shaders/` = shader cache
- `*.dds` = any remaining DDS image

Anno 117 has no comparable heavy noise problem because archives are separated by purpose.

## DLC01 Example: Prophecies of Ash

Copied from committed research file [../../docs/rda-research/dlc01-prophecies-of-ash.md](../../docs/rda-research/dlc01-prophecies-of-ash.md):

- **GUID**: `67902` (`DLC01_Prophecies_of_Ash` in `specialguids.lua`)
- Main content archives:
  - `cdlc01_graphics.rda`
  - `config.rda`
  - `script.rda`
  - `graphics_library.rda`
  - `shared_configs.rda`
  - `ui.rda`
  - `video.rda`
- Volcano Lua binding:
  - `data/script/types/generated/rdgs/cvolcanoeruptionmanagerluabindings.lua`
- Volcano datasets:
  - `data/script/types/generated/datasets/dcvolcano_eruption_active.lua`
  - `data/script/types/generated/datasets/dcvolcano_incidents.lua`
  - `data/script/types/generated/datasets/dcvolcano_obsidian_deposits.lua`
  - `data/script/types/generated/datasets/dcvolcano_obsidian_drops.lua`
  - `data/script/types/generated/datasets/dcvolcano_scheduler_eruption_length.lua`
  - `data/script/types/generated/datasets/dcvolcano_scheduler_interval_duration.lua`

For deeper DLC-specific notes, use the committed files in `docs/rda-research/`.

## Core Commands

```powershell
. .\RDA-Agent.ps1

Use-Anno117
Use-Anno1800

rda-all
rda-info config
rda-list config "assets"
rda-read config "data/base/config/export/assets.xml"
rda-search config "Volcano"
rda-find "datasets"
rda-grep "Voada|Volcano|Caecilia"
rda-extract ui "data/ui/.../icon_0.dds"
rda-image ui "data/ui/.../icon_0.dds" ".\icon.png"
```

## Important Limitation

This file is committed with the repository. Copilot local repo memory such as `/memories/repo/rda-knowledge.md` is **not** part of Git and will not travel with a push, clone, or download. Durable archive knowledge should be copied into committed files like this one or into `docs/rda-research/`.
    achievements/achievements.xml           — achievement definitions
    balancing/balancing.xml                 — gamepad mappings + balancing (~43 KB)
    bloomberg/_bloombergconfig.bat          — internal config tool
    bloomberg/bloomberg_settings.ini        — internal balancing tool settings
    console.xml                             — developer console groups + gamepad vibration effects
    ui/infotips.xml, scenes.xml, ui.xml     — developer console UI scenes
  engine/
    ambientsettings/amb_*.xml               — 30+ ambient light presets (roman, celtic, DLC01, benchmark, portrait, UI)
    animations.db                           — animation database (binary)
    enginesettings/default.xml, extended.xml — graphics engine settings
    globals.xml                             — renderer global constants (light factors, moon, rain, fog, wetness)
    imageinfos.dat                          — binary image metadata
    posteffects/*.pfx                       — 11 post-FX: blur, depth_of_field, fisheye, mirage, motionblur, etc.
    prop_sets.xml                           — prop set definitions
    unit_scales.xml                         — unit scale factors
  export/
    assets.xml                              — main asset database (~300 MB)
    assets.xml.checksum                     — integrity check
    audio_generated.xml                     — auto-generated audio asset references
    audio_generated.xml.checksum
    properties-meta.xml                     — property metadata (field types/descriptions)
    properties.xml                          — default property values
    properties.xml.checksum
    templates.xml                           — template definitions
    templates.xml.checksum
  file_browser_patterns.xml                 — file browser configuration
  game/
    camera.xml                              — camera parameters
    datasets.xml                            — game balancing datasets
    engine.ini                              — main engine config (JSON format)
    feedbackconfigdata.xml                  — feedback/animation config
    vanilla.txt                             — DLC access rules (+ enable / - disable paths) supports up to dlc12
  gui/
    .p4ignore                               — source control ignore file
    credits.txt                             — game credits
    licenses.md                             — third-party licenses
    texts_*.xml                             — 13 language files: english, german, french, spanish, italian,
                                              japanese, korean, polish, russian, simplified_chinese,
                                              traditional_chinese, brazilian, texts_metadata.xml
  luna/game/engine.ini                      — Luna (streaming/cloud) engine config
  online/storm.ini, storm_debug.ini         — multiplayer Storm SDK config
  pc/bloomberg/bloomberg_settings.ini       — PC-specific balancing tool
  portraitcam/*.portraitcam                 — 25 portrait camera definitions (characters, diplomacy, quests, etc.)
```

### Anno 117 engine.ini (JSON, key settings)
```json
{
  "FileSystem": { "InitFileVersion": 19, "PreferLocalFiles": false },
  "RenderEngine": {
    "QualitySetting": "Custom",
    "Custom": { "Water": 2, "Lighting": 3, "Shadow": 4, "Terrain": 2, "Texture": 2, "AA": 0, "Object": 4, "ViewDistance": 3, "Raytracing": 0 },
    "Upscaling": { "DLSS": 3, "XeSS": 4, "FSR": 3 },
    "EnableHDR": false, "HDRMaxLuminance": 1100
  },
  "Window": { "FullscreenType": 2, "VSync": 1 },
  "Debugging": { "SimulateConsole": false }
}
```

### Anno 117 vanilla.txt — DLC access rules
- Syntax: `+path` = allow, `-path` = deny
- Base game enables: `data/dlc01/config/...` through `data/dlc12/config/...` (up to 12 DLCs planned)
- Currently released: DLC01 (Prophecies_of_Ash, GUID 67902), DLC02 (The_Hippodrome, 67903), DLC03 (Dawn_of_Delta, 67904)

### Anno 117 script system (COMPLETE)
- Uses **Lua** (not Python) — complete redesign from Anno 1800
- All scripts in `script.rda\data/script/`
- **1013 total files** decomposed as:
  - `core/core.lua` — Lua engine bootstrap: overrides `dofile`, `require`, `print`, `coroutine.create`, adds `dir()`, `inspect()`, `getTypeInfo()`, `resultToString()`
  - `core/scriptsystem.lua` — `system.wait(ms)`, `system.waitPP(ms)`, `system.waitUILoaded()`, traceback helpers
  - `modules/json.lua` — JSON encode/decode
  - `modules/sessions.lua` — all Session GUIDs: `Base_Province_Roman_Italia=3245`, `Base_Province_Celtic_Britannia=6627`, benchmark sessions, test maps
  - `modules/specialguids.lua` — **280+ special GUIDs** for all singleton assets (SpecialGUIDs table)
  - `modules/testhelper.lua` — automated test harness: `startNewGameBlocking()`, `saveGameBlocking()`, `startNewMPGameBlocking()`, `takeScreenshot()`, DTest integration
  - `types/events.lua` — event declarations: `OnSessionLoaded`, `OnSessionEnter`, `OnMetaGameUnloaded`, `OnCameraSequenceEnd`, `OnLeaveUIState`, `OnGameSetupLoaded`
  - `types/rdgs.lua` — rdgs type stubs: `rdgs.ScriptEvent` (Add/Remove/RemoveByName/RemoveAll), `rdgs.CAsyncFunction`, type aliases
  - `types/generated/datasets/` — **~300 dataset enum Lua files** (one per dataset type)
  - `types/generated/rdgs/` — **430 Lua binding files** (one per C++ class exposed to Lua)
  - `content/cheats/` — 4 cheat scripts (see cheat section)

### Anno 117 cheat system — COMPLETE API

**CCheatManager** (global: `Cheat`) — `data/script/types/generated/rdgs/ccheatmanagerluabindings.lua`:
- `Cheat.GlobalCheats` → `CGlobalCheats`
- `Cheat.AICheats` → `CAIGlobalCheatHandler`
- `Cheat:TriggerCheatByName(name)`, `TriggerCheat(id)`, `TriggerCheatByNameWithArgs(name, args)`, `TriggerCheatWithArgs(id, args)`
- `Cheat:IncreaseCheatCount(id)`, `GetCheatCount(name) → int`
- `Cheat:SetCheatCategory(category)`, `ToggleInGameDebugCheatPage()`
- `Cheat:AddCheat(category, name, command, shortcut, comment, worksInMultiplayer[, quickAccessCheatOverlay[, contexts]]) → int`

**CGlobalCheats** (via `Cheat.GlobalCheats`) — `cglobalcheatsluabindings.lua`:
Properties (bool): `IgnoreBuildingCosts`, `IgnoreFertilities`, `IgnoreThreats`, `IsUndiscoveredDisabled`, `IncidentsDisabled`, `SuperShipSpeedEnabled`, `ConstructionAIEnabled`, `DeferExpensiveEconomy`, `IsProductivityCheated`, `IsAttackDebugView`, `IsLOSDebugView`, `IsWinLoseConditionsDisabled`, `IsAllPerfSamplesUploaded`
Properties (number): `EconomySpeed`
Toggles: `ToggleIgnoreBuildingCosts`, `ToggleIgnoreFertilities`, `ToggleIgnoreThreats`, `DisableUndiscovered`, `ToggleIncidents`, `ToggleSuperShipSpeed`, `ToggleProductivity`, `ToggleUnlockAllForHumans`, `ToggleUpgradeCheck`, `ToggleHappyDaysDisabled`, `DisableWinLoseConditions`, `ToggleConstructionAIEnabled`, `ToggleDeferExpensiveEconomy`, `ToggleDeferExpensiveQuestSystem`, `ToggleSessionSwitchUISkipped`, `ToggleLetterboxingDisabled`, `ToggleBlockBannerNotifications`
Profiling: `ToggleMemoryTracking`, `ToggleGearPerfProfiler`, `StartAutoProfiler`, `StopAutoProfiler`, `CapturePAPSample()`, `LoadLivePP`, `SelectPerformanceGameTiming(gameTiming)`
Visual debug: `ToggleAttackDebugView`, `ToggleLOSDebugView`, `ToggleExtendedDebugging(type)`, `ToggleSuppressDebugDrawings`, `EnableVegetationDebugging`, `EnableTrailerMovieHacks`, `ToggleUseDebugCameraZoomLevels`

**CCheatBindings** (session-scoped: `Cheats`) — `ccheatbindingsluabindings.lua`:
- `SessionGuid` (int)
- Unit: `DamageSelectedObjects(pct[, dealer[, countAsUnitAttack]])`, `HealSelectedObjects(pct)`, `SetSelectedObjectsInvincible(bool)`, `DestroySelectedObjects()`, `SpawnForces()`, `SpawnGameObject(guid[, owner[, spawnAtPickPos[, pos]]])`
- UI debug: `ToggleTextSourceResolving()`, `ShowOnScreenText(textGuid)`, `ShowExplainerPopup(guid)`, `ShowTextPopup(guid)`, `ShowNotification(guid)`, `ShowCharacterNotification(subtitleId)`, `ToggleConsole()`, `ToggleScriptConsole()`, `ToggleDebugUI()`, `ToggleBuildInformation()`, `ToggleStateVisibility(name)`, `ToggleStateUpdates(name)`, `ToggleVersionLabel()`, `ToggleUiPlayground()`, `ToggleHighlightDynamicButtonPrompts()`, `ReloadUI()`, `CopyTextUnderMouse()`
- Dev tools: `OpenDebugInfoPage()`, `ToggleAIDebug()`, `OpenSelectionInBob()`, `OpenSelectionInBob2()`, `OpenInBob2(filename)`, `OpenSelectionInT9()`, `OpenInT9(guid)`, `OpenInfoTip()`, `OpenInInfoTip(guid)`, `OpenInAnnoEditor(filename)`
- Camera: `ToggleFpsView()`, `SetWindDirection(dir[, keepForTenMinutes])`
- Game objects: `ExecuteActions(actionGuid)`, `StartBuildMode(guid)`, `ReplaceSelected(guid)`, `UpgradeSelected(...)` (via SelectionManager)
- Meta: `GainAchievement(guid) → bool`, `GainRandomAchievement()`, `ShowAllCollectables(show)`, `CopyBuildStringToClipboard()`, `UnloadSession(guid)`, `OpenDebugInfoPage()`, `WriteMemorySnapshot()`, `SetMemorySnapshotSampling(period)`, `StartPerformancefProfileStream(fileName)`, `StopPerformanceProfileStream()`, `ToggleTelemetry()`, `ToggleFrameCaptureMode()`

**Actual cheat Lua scripts** (`script.rda\data/script/content/cheats/`):
- `cheat_diplomacy_reset_all_action_cooldowns.lua` → `Participants:GetParticipant(Selection.Object.Owner).Diplomacy:ResetAllActionCooldownsNet()`
- `cheat_emperor_shorten_all_quest_intervals.lua` → `Participants.ActiveEmperorProperty:ShortenAllEmperorQuestTimerIntervals()`
- `cheat_incidentscheduler_set_incident_intervals_to_shorter_cooldown.lua` → `Static.Incident.ShortenIncidentSchedulerIntervalForMajorIncidents(0)`
- `cheat_questmanager_set_all_sidequest_pool_cooldowns_to_shorter_cooldown.lua` → `Quests:CheatSetAllPoolCountdownEndtimersToNewTime()`

### Anno 117 global Lua objects (rdgs binding aliases, all verified)
These are C++ engine objects exposed as Lua globals via `= ClassName` in binding files:
```lua
-- Game world
World          = rdgs.CWorldManager         -- World.AmbientLocalWater
GameSession    = rdgs.CGameSessionManager   -- .AreaFromContext, .Current, .SessionGUID, .RegionGUID
Island         = rdgs.CIslandManager        -- .AllIslandIDs; .GetIsland(id|x,y), .GetNearestIsland, .GetIslandByLabel
AreaManager    = rdgs.CAreaManager          -- .GetAreaManagerByIslandGUID(guid), .GetAreaManagerByID(id)
GetAreaManagerByIslandGUID = rdgs.CAreaManager
GetAreaManagerByID         = rdgs.CAreaManager
AreaPopulation = rdgs.CAreaPopulationManager -- .GetPopulationCount, .CityStatus, .DominantPopulation...

-- Economy
Economy        = rdgs.CEconomyManager       -- .MetaStorage, .GetRegisteredDeltaProduction/Consumption
AreaEconomy    = rdgs.CAreaEconomy          -- .GetStorageAmount/Capacity/Trend, .AddAmount, .GetSatisfaction
-- (MetaEconomy): Economy.MetaStorage → .Knowledge, .Belief, .Prestige, .AddAmount

-- Participants / Players / AI
Participants   = rdgs.CParticipantManager   -- .GetCurrentParticipantGUID, .Current, .Human0-3ParticipantGUID
                                             -- .ActiveEmperor, .ActiveEmperorProperty
                                             -- .GetParticipant(guid), .RemoveParticipant, .SetCurrentParticipant

-- Cheats
Cheat          = rdgs.CCheatManager         -- see cheat section
Cheats         = rdgs.CCheatBindings        -- see cheat section

-- Diplomacy / Quests / Unlock
Diplomacy      = rdgs.CDiplomacyManager     -- .GetRelationIfModified, .GetAllies/NonAllies/DefensiveAllies
                                             -- CheatTotalWar/Peace/Alliance/Trade
Quests         = rdgs.CQuestManager         -- .CheatStartStoryLineForCurrentPlayerNet, .CheatStartQuestComponentForCurrentPlayerNet
                                             -- .CheatResetPoolEntriesNet, .CheatSetAllPoolCountdownEndtimersToNewTime
Unlock         = rdgs.CUnlockManager        -- .Unlock(guid), .IsUnlocked(guid), .RelockNet(guid), .IsVisible(guid)
Selection      = rdgs.CSelectionManager     -- .Picked, .Objects, .Object, .SelectByID, .AddToSelectionByID, .ClearSelection

-- Static helpers
Static.GetKnowledgeRequirement = rdgs.VirtualSpaceTechHandler
Static.GetTechUnlockRewards = rdgs.VirtualSpaceTechHandler
-- (many more Static.* from VirtualSpace* bindings)
```

### Anno 117 event system (`types/events.lua`)
```lua
events.OnSessionLoaded   -- listener receives rdgs.SessionLoadedContext
events.OnSessionEnter    -- listener receives SessionEnteredContext  
events.OnMetaGameUnloaded -- listener receives int (unused)
events.OnCameraSequenceEnd -- listener receives CameraSequenceEventIdentifier
events.OnLeaveUIState    -- listener receives rdui.StateID
events.OnGameSetupLoaded -- listener receives bool (true=savegame, false=new game)
-- Usage: events.OnSessionLoaded:Add(function(ctx) ... end, "groupName")
--        events.OnSessionLoaded:Remove(listener)
--        events.OnSessionLoaded:RemoveByName("groupName")
```

### Anno 117 rdgs.lua type system
- `rdgs.ScriptEvent` — Add(listener, group?), Remove(listener), RemoveByName(group), RemoveAll()
- `rdgs.CAsyncFunction` — Check() → bool, PushResult() → ...
- Type classes: `rdgs.ProductGUID`, `rdgs.ProductAmount`, `rdgs.BuildingGUID`, `rdgs.CraftableGUID`, `rdgs.AreaID`, `rdgs.IslandID`, `rdgs.ParticipantGUID`, `rdgs.SessionTime`, `rdgs.TradeRouteID`, `rdgs.NotificationID`, `rdgs.MetaGameObjectID`

### Anno 117 rdgs binding files — key categories (430 total in `types/generated/rdgs/`)
Area managers: `careamanagerluabindings`, `careaeconomyluabindings`, `careapopulationmanagerluabindings`, `careamoneymanagerluabindings`, `careaincidentmanagerluabindings`, `careatakeovermanagerluabindings`, `careawallmanagerluabindings`, `careaworkforcemanagerluabindings`, `careastreetmanagerluabindings`, `careafestivalmanagerluabindings`, `careafetchmanagerluabindings`, `careaobjectmanagerluabindings`, `careareligionmanagerluabindings`, `careaneedattributemanagerluabindings`, `careabuildabilitygridmanagerluabindings`
Session managers: `cgamesessionmanagerluabindings`, `csessionparticipantmanagerluabindings`, `csessionunitmanagerluabindings`, `csessionlandcombatmanagerluabindings`, `csessiontransfermanagerluabindings`, `csessiontraderouteluabindings`, `csessioneffectmanagerluabindings`
World/Island: `cworldmanagerluabindings`, `cislandmanagerluabindings`, `cislandluabindings`
Economic: `ceconomymanagerluabindings`, `ceconomystatisticmanagerluabindings`, `cmetaeconomyluabindings`, `cactivetrade*`, `ctraderoutemanagerluabindings`
Players: `cparticipantmanagerluabindings`, `cmetaproperty*` (diplomacy, island owner, tech handler, emperor)
Quest/Tech: `cquestmanagerluabindings`, `cquestcampaignhandlerluabindings`, `cunlockmanagerluabindings`, `virtualspacetechhandlerluabindings`
VirtualSpace (static helper namespaces): `virtualspaceshipsluabindings`, `virtualspaceeconomy*`, `virtualspaceattributes*`, `virtualspacemilitary*`, `virtualspacetech*`, `virtualspacevillaluabindings`, `virtualspacewarehouseluabindings`, etc.
AI: `caicheathandlerluabindings`, `caiglobalcheathandlerluabindings`, `caiconstructionmanagerluabindings`, `caiunitmanagerluabindings`
UI/Debug: `cautomatedtestmanagerluabindings`, `cbenchmarkmanagerluabindings`, `cassertmanagerluabindings`

---

### "Find cheats"
- **Anno 1800**: `rda-find` in data0 for `anno6.pypredef`; look at `class Cheat:` and `Game.activateCheat()`
- **Anno 117**: `rda-find "cheat"` across `script.rda` only; all cheat files are in `data/script/content/cheats/` and `data/script/types/generated/rdgs/c*cheat*`

### "Find assets/game data"
- **Anno 1800**: `assets.xml` in data0 (base) + any data10–data33 (DLC/patches override)
- **Anno 117**: `config.rda` only → `data/base/config/export/assets.xml`

### "Find game balance/settings"
- **Anno 1800**: `data/config/export/main/asset/datasets.xml` in data28–data33 (newest = most authoritative)
- **Anno 117**: `config.rda` → `data/base/config/game/datasets.xml` and `data/base/config/console/balancing/balancing.xml`

### "Find texts/localisation"
- **Anno 1800**: `data/config/gui/texts_english.xml` in data0, overridden by data10+
- **Anno 117**: `config.rda` → `data/base/config/gui/texts_english.xml`

### "Find scripts/game logic"
- **Anno 1800**: Python in `data0.rda\data/script/` (exclude `lib/` and `pydevd/`)
- **Anno 117**: Lua in `script.rda\data/script/` — `content/` = game logic, `types/generated/` = API stubs

### "Find graphics/models for X"
- **Anno 1800**: data8 (base graphics), data1/data6 (DLC graphics), search with `rda-find "X"`
- **Anno 117**: `graphics_roman.rda` (Roman content), `graphics_celtic.rda` (Celtic), `shared_configs.rda` (shared)

---

## NOISE PATTERNS — False positives to exclude

**These are now automatically suppressed in `rda-find` and `rda-grep` for Anno 1800.**
Use `-IncludeNoise` switch to override. Example: `rda-find "console" -IncludeNoise`

Anno 1800 noise (built into RDA-Agent.ps1):
- `data/ui/2kimages/` = 100k+ Xbox/PS/PC UI icon textures (DDS) — EXCLUDED
- `data/ui/studio/` = rendered UI scene images — EXCLUDED
- `data/ui/backgrounds/` = background DDS images — EXCLUDED
- `data/graphics/` = 3D graphics/normal-map textures — EXCLUDED
- `data/script/lib/` = Python stdlib — EXCLUDED
- `data/script/pydevd/` = PyCharm debugger test files — EXCLUDED
- `data/sound/` / `en_us0.rda` / `ru_ru0.rda` = audio — EXCLUDED
- `data/shaders/` = shader cache — EXCLUDED
- `*.dds` = any remaining DDS image — EXCLUDED

Anno 117: no heavy noise (named archives are content-specific, easy to navigate)

---

## Anno 117 DLC01 — "Prophecies of Ash" (GUID 67902) — COMPREHENSIVE

### Overview
- First DLC for Anno 117; GUID `67902` (`DLC01_Prophecies_of_Ash` in specialguids.lua)
- Theme: **active volcano in Latium**, volcanic prophecies, new resource (Obsidian), barter trader
- Dedicated graphics archive: `cdlc01_graphics.rda` (2293 MB, 1862 files)

### Story & Characters
- **Setting**: "The forgotten senatorial province of Latium" — site of a dangerous volcano. Game starts "In the fortieth year of Pax" with the player newly appointed governor after the last eruption destroyed the previous settlement.
- **Emperor Lucius (Augustus Florianus)** — aging emperor who instituted PAX; dies mid-campaign (stabbed by a slave); his debts and unfinished projects (Amphitheatre, Tomb) create the main political tension
- **Julia Augusta** — Empress, Lucius' wife/widow; takes over imperial politics after his death
- **Ben-Baalion** — player's sardonic enslaved advisor (formally named by Emperor Lucius upon entering service); deeply loyal despite dry wit; freed later in the story
- **Caecilia** — blind oracle/Sibyl, survivor of the last eruption, located in the north of Latium; new barter trader; player barters **obsidian** for her acolytes
- **Caeso Syracus** — Latium's resident Raider (slave trader); can be Active or Dormant (player choice)
- **Diana** — Roman noble, doesn't get along with her father Lucius (her siblings died in infancy and Lucius never forgave her for surviving); has "Diana's Entourage" campaign objective
- **Titanius** — enemy known by sight, wanted by the Emperor; a fugitive governor who abused imperial power
- **Ma Licia** — Chinese trader from "Daqin" (Chinese name for Rome), exotic merchant

### New Mechanics
1. **Volcano System** — Latium's volcano has a lifecycle with 4 phases + governor decisions at each transition:
   - **Prelude/Beginning** — calm before the storm, no ash fall, clear skies
   - **Bloom** — early volcanic signs, glowing ash effects begin (UseAsh=1), clear atmosphere
   - **Eruption** — full eruption, intense glowing ash (270.8), eruption cloud texture, no ash snow fall (volcano emitting up not raining down)
   - **Volcanic Winter** — aftermath, heavy ash fall (Snow=0.999) + intense glow, reuses eruption cloud texture
2. **Obsidian** — new resource; byproduct of quarries/pits; used as currency to barter with Caecilia for acolytes; icon: `icon_3d_obsidian_goods_0.dds`; icon: `icon_2d_currency_obsidian_0.dds`
3. **Caecilia's Acolytes** — barter obsidian for "resource specialists" who can mitigate volcano effects
4. **Volcano Overdrive** — `Volcano.GetCurrentOverdriveFactor(participant)` / `GetMaximumOverdriveFactor(participant)` — tension/stress level mechanic
5. **Governor Decisions** — fullscreen UI screens at each volcano phase transition
6. **Volcano Incidents**: Tremors (`icon_2d_incident_tremor_0`), Volcano Rocks (`icon_2d_incident_volcano_rock_0`)
7. **Deity: Vulcanus** — new religion element, god of the forge and volcano
8. **New luxury goods**: Boardgames, Idols (in addition to Obsidian)

### Volcano Datasets (all from `script.rda`)
- `DCVolcanoEruptionActive` — `Off=0, On=1`
- `DCVolcanoIncidents` — `Easy=0, Hard=1`
- `DCVolcanoObsidianDeposits` — `Plenty=0, Medium=1, Sparse=2`
- `DCVolcanoObsidianDrops` — `Plenty=0, Medium=1, Off=2`
- `DCVolcanoSchedulerEruptionLength` — `Short=0, Medium=1, Sparse=2`
- `DCVolcanoSchedulerIntervalDuration` — `Long=0, Medium=1, Inferno=2`

### Lua API: CVolcanoEruptionManager (global: `Volcano`)
- `Volcano:GetCurrentOverdriveFactor(participantGuid) → integer`
- `Volcano:GetMaximumOverdriveFactor(participantGuid) → integer`
- `Volcano.isValid() → boolean`

### Ambient Phases — visual comparison
| Phase | XML name | UseAsh | Ash Snow | AshGlowBrightness | MieStrength | LUT |
|---|---|---|---|---|---|---|
| Prelude | `amb_roman_dlc01_prelude_01` | 0 | 0 | 270 | 3e-6 (clear) | roman_01_hdr |
| Bloom | `amb_roman_dlc01_bloom_01` | 1 | 0 | 270 | 3e-6 (clear) | roman_01_hdr |
| Base/Ash rain | `amb_roman_dlc01_01` | 1 | 0.999 | 50 | 3.75e-5 (hazy) | lut_neutral |
| Eruption | `amb_roman_dlc01_eruption_01` | 1 | 0 | 270.837 | 1e-5 | lut_neutral |
| Volcanic Winter | `amb_roman_dlc01_volcwinter_01` | 1 | 0.999 | 270.837 | 3.75e-5 (hazy) | lut_neutral |
- Interior variants exist for: base, bloom, eruption, prelude (`*_int_01`), plus `amb_roman_dlc01_default_int_01`
- `PuddleColor` = (0.15, 0.1, 0.05) across all phases — volcanic mud / dirty puddles
- "Ash" is the snow particle system recolored to grey (0.23, 0.23, 0.23)

### Graphics / Ornaments (cdlc01_graphics.rda)
Two main categories:
1. **Building Skins** (`skin01_mosaics`): Mosaic skin for Roman Forum (2 variants), Roman Baths (2 variants)
2. **Ornaments** (Celtic + Roman):
   - Celtic: Mosaic plazas (01_01, 01_02, 02_01, 02_02, 03_01, 03_02, 03_03, 04_01 + 3x3 variants), Stone plazas (01, 03), Natural plaza, Celtic Wall system, Celtic Gate
   - Roman: Mosaic plazas (01_01, 01_02, 02_01, 02_02, 03_01, 03_03, 3x3 variants), Stone plazas (01–07), Roman Wall (01, 02), Roman Gate
   - Props: Flower pots, ornament bases, watcher statues, hanging bushes (12 variants), top bushes (4), town crier on box (celebration)

### UI Content (dlc01/ in ui.rda)
- **DLC activation**: Volcano foreground/midground images; with deity Vulcanus, obsidian goods, trader Caecilia
- **Fullscreen governor decisions**: beginning, bloom, eruption, winter (×2: governor + event)
- **Tech tree icons**: 7 techs: Ashen Concrete, Better Boardgames, Better Idols, Export Boardgames, Export Idols, Fire Safety Precautions, Larger Obsidian Loads
- **Campaign icons**: Diana's Entourage, Fresh Ash, Ominous Obsidian Offering, Philosophers, Rainbow Obsidian, Vulcanus Temple Grounds
- **Specialist icons**: 14 female + 16 male + 1 trader (Caecilia) portraits
- **Incidents**: Tremor, Volcano Rock
- **Status icons**: Farms Flourishing, Farms in Ash, Finding Obsidian, Food Rationing, Mines Collapsing
- **Achievements**: Set 12, items 01–09
- **Religion**: Vulcanus deity artwork (1248px + 88px)
- **Infrastructure**: Caecilia's Lighthouse (`icon_3d_lighthouse_caecilia_0`)
- **Video**: `campaignbanner_volcano_1440x1440_a.bk2`

### Config — cloud textures (graphics_library.rda)
`data/base/graphics/library/ambient/clouds/cloud_unique/roman/roman_dlc01_*.dds`: base, bloom, bloom_int, default_int, eruption, eruption_int, prelude, prelude_int, volcwinter

### Key GUIDs from specialguids.lua
- `DLC01_Prophecies_of_Ash = 67902`
- `LatiumVolcanoTemple = 145427`
- `VolcanoDefaultProjectile = 145812`
- `LatiumKontor1/2/3 = 3402/3403/3406`

---

## IMPORTANT NOTES
- Anno 117's `console/` config (`config.rda/data/base/config/console/`) is unrelated to Xbox/PS; it's the game's developer console/settings system
- Anno 117 uses **Lua** extensively; Anno 1800 uses **Python** for scripting
- Anno 117's `infotips.rda` contains only ONE file: `data/infotips/export.bin`
- Anno 117's `vanilla.txt` is at `data/base/config/game/vanilla.txt` in `config.rda`

---

## RDA-Agent.ps1 — Tool Commands

### Load
```powershell
. scripts/agent-tools/RDA-Agent.ps1
# Auto-detects game; if both present, says "Both detected. Run Use-Anno117 or Use-Anno1800"
```

### Smart Game-Aware Commands
| Alias | What it does |
|-------|-------------|
| `rda-cheats` | Show full cheat API for active game (Python API for 1800, Lua for 117) |
| `rda-assets` | Read authoritative assets.xml (auto-picks newest archive for 1800) |
| `rda-texts [lang]` | Read localisation texts_<lang>.xml (default: english) |
| `rda-scripts [filter]` | List game logic scripts (excludes stdlib/debugger for 1800) |
| `rda-datasets` | Read balancing datasets.xml |

### Cross-Archive Investigation
| Alias | What it does |
|-------|-------------|
| `rda-find <pattern>` | Which archive(s) contain a matching path? (noise-filtered by default) |
| `rda-find <p> -IncludeNoise` | Same but show all 100k+ noise files too |
| `rda-grep <pattern>` | Text search inside all archives (slow; noise-filtered) |

### Single-Archive Explore
| Alias | What it does |
|-------|-------------|
| `rda-all` | List all archives with file/block counts |
| `rda-info <n>` | Archive metadata |
| `rda-list <n> [filter]` | List files in archive |
| `rda-read <n> <file>` | Print file contents |
| `rda-search <n> <text>` | Text search in one archive |
| `rda-extract <n> <file>` | Extract file to disk |
| `rda-image <n> <file>` | DDS → PNG via texconv |
