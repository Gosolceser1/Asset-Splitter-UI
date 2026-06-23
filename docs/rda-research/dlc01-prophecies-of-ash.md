# DLC01 — Prophecies of Ash

**GUID**: `67902` (`DLC01_Prophecies_of_Ash` in `specialguids.lua`)  
**Game**: Anno 117 — Pax Romana  
**Researched**: April 2026 (live archive inspection)

---

## Archives Containing DLC01 Content

| Archive | What's in it |
|---|---|
| `cdlc01_graphics.rda` | 2293 MB, 1862 files — all DLC01 graphics, ornaments, skins |
| `config.rda` | 10 ambient XML files (`amb_roman_dlc01_*.xml`), volcano cloud textures |
| `script.rda` | Volcano Lua API + 6 dataset enum files |
| `graphics_library.rda` | DLC01 cloud DDS textures, ambient light library |
| `shared_configs.rda` | Shared `.cfg` files for DLC01 building skins and ornaments |
| `ui.rda` | All UI assets: icons, fullscreen images, tech tree, portraits, achievements |
| `video.rda` | `campaignbanner_volcano_1440x1440_a.bk2` |

---

## Story & Characters

**Setting**: "The forgotten senatorial province of Latium" — built around an active volcano. The game starts *"In the fortieth year of Pax"* with the player appointed as new governor after the previous settlement was destroyed by the last eruption.

| Character | Role |
|---|---|
| **Emperor Lucius Augustus Florianus** | Aging emperor who instituted PAX. Dies mid-campaign (stabbed by a slave). Leaves behind debts, an unfinished Tomb, and an Amphitheatre law. |
| **Julia Augusta** | Empress, Lucius' wife. Takes over imperial politics after his death. |
| **Ben-Baalion** | Player's sardonic enslaved advisor, named by Lucius upon entering service. Deeply loyal despite dry wit. Eventually freed. |
| **Caecilia** | Blind oracle/Sibyl, survivor of the last eruption, found in northern Latium. New **barter trader** — player trades obsidian for her acolytes. |
| **Caeso Syracus** | Latium's resident Raider and slave trader. Player can set him Active or Dormant. |
| **Diana** | Roman noble, hostile to her father Lucius (her siblings died in infancy; Lucius blamed her for surviving). Has her own campaign objectives. |
| **Titanius** | Rogue former governor, wanted by the Emperor; known by sight to all of Latium. |
| **Ma Licia** | Exotic Chinese merchant from "Daqin" (Chinese name for Rome). |

**The DLC's title** refers to *Sybilline prophecies* — ancient volcanic oracles excavated from ash and ruin by Caecilia. These become a plot device throughout the campaign.

---

## Core Mechanic: The Volcano Lifecycle

The volcano has a scripted lifecycle with 4 phases. Each phase has:
- A distinct fullscreen governor-decision UI screen
- A unique ambient environment (see below)
- Gameplay consequences (incidents, production modifiers)

### Phase Timeline
```
Prelude → Bloom → Eruption → Volcanic Winter → (next cycle)
```

### Governor Decision UI Images (`ui.rda/data/ui/4k/dlc01/features/images_fullscreen/`)
```
img_dlc01_volcano_beginning_0.dds          — event art for each phase
img_dlc01_volcano_bloom_0.dds
img_dlc01_volcano_eruption_0.dds
img_dlc01_volcano_winter_0.dds
img_dlc01_governor_decision_volcano_beginning_0.dds  — governor decision variants
img_dlc01_governor_decision_volcano_bloom_0.dds
img_dlc01_governor_decision_volcano_eruption_0.dds
img_dlc01_governor_decision_volcano_winter_0.dds
```

---

## Ambient Phases — Visual Comparison

All ambient XMLs are in `config.rda` at `data/base/config/engine/ambientsettings/`.

| Phase | XML file | UseAsh | Ash Snowfall | AshGlowBrightness | Atmosphere (Mie) | LUT |
|---|---|:---:|:---:|---|---|---|
| Prelude | `amb_roman_dlc01_prelude_01` | 0 | 0 | 270 | `3e-6` clear | `roman_01_hdr` |
| Bloom | `amb_roman_dlc01_bloom_01` | 1 | 0 | 270 | `3e-6` clear | `roman_01_hdr` |
| Base / ash rain | `amb_roman_dlc01_01` | 1 | **0.999** | 50 | `3.75e-5` hazy | `lut_neutral` |
| Eruption | `amb_roman_dlc01_eruption_01` | 1 | 0 | **270.837** | `1e-5` | `lut_neutral` |
| Volcanic Winter | `amb_roman_dlc01_volcwinter_01` | 1 | **0.999** | **270.837** | `3.75e-5` hazy | `lut_neutral` |

Interior variants exist for each main phase: `*_int_01` (e.g. `amb_roman_dlc01_bloom_int_01`), plus `amb_roman_dlc01_default_int_01`.

**Notes:**
- "Ash" is the snow particle system recoloured grey: `Color (0.23, 0.23, 0.23)`
- `PuddleColor = (0.15, 0.1, 0.05)` throughout all phases — volcanic mud / dirty puddles
- `AshGlowChance` ranges from 0.198 to 0.3 (probability per particle of glowing embers)
- Cloud textures (DDS): `roman_dlc01_{phase}_01_0.dds` in `graphics_library.rda`

---

## Volcano Scripting API

### Lua Global: `Volcano`
File: `script.rda/data/script/types/generated/rdgs/cvolcanoeruptionmanagerluabindings.lua`

```lua
---@class rdgs.CVolcanoEruptionManager
Volcano = CVolcanoEruptionManager

Volcano:GetCurrentOverdriveFactor(participantGuid)  → integer
Volcano:GetMaximumOverdriveFactor(participantGuid)  → integer
Volcano.isValid()                                   → boolean
```

**Overdrive** is a per-participant tension/stress meter — higher = closer to eruption.

---

## Volcano Datasets (Game Setup Sliders)

All in `script.rda/data/script/types/generated/datasets/`:

| Dataset | Values |
|---|---|
| `DCVolcanoEruptionActive` | `Off=0`, `On=1` |
| `DCVolcanoIncidents` | `Easy=0`, `Hard=1` |
| `DCVolcanoObsidianDeposits` | `Plenty=0`, `Medium=1`, `Sparse=2` |
| `DCVolcanoObsidianDrops` | `Plenty=0`, `Medium=1`, `Off=2` |
| `DCVolcanoSchedulerEruptionLength` | `Short=0`, `Medium=1`, `Sparse=2` |
| `DCVolcanoSchedulerIntervalDuration` | `Long=0`, `Medium=1`, `Inferno=2` |

---

## New Resources & Economy

| Resource | Description |
|---|---|
| **Obsidian** | Byproduct of quarries and pits. Used as the barter currency with Caecilia for acolytes. |
| **Boardgames** | New DLC luxury good. |
| **Idols** | New DLC luxury good. |

**Caecilia's Acolytes** — barter obsidian with Caecilia to acquire "resource specialists" who can mitigate volcano-phase effects on production.

---

## Tech Tree (7 DLC01 Techs)

From `ui.rda/data/ui/4k/dlc01/icon_content/techtree/`:

| Tech | Icon |
|---|---|
| Ashen Concrete | `icon_3d_techtree_ashen_concrete_0` |
| Better Boardgames | `icon_3d_techtree_better_boardgames_0` |
| Better Idols | `icon_3d_techtree_better_idols_0` |
| Export Boardgames | `icon_3d_techtree_export_boardgames_0` |
| Export Idols | `icon_3d_techtree_export_idols_0` |
| Fire Safety Precautions | `icon_3d_techtree_fire_safety_precautions_0` |
| Larger Obsidian Loads | `icon_3d_techtree_larger_obsidian_loades_0` |

---

## Incidents

| Incident | Icon |
|---|---|
| Tremor | `icon_2d_incident_tremor_0.dds` |
| Volcano Rock | `icon_2d_incident_volcano_rock_0.dds` |

Status effect icons: Farms Flourishing, Farms in Ash, Finding Obsidian, Food Rationing, Mines Collapsing.

---

## Religion: Deity Vulcanus

- `icon_2d_deity_vulcanus_0.dds` — 2D icon
- `artwork_deity_vulkan_1248_0.dds` — full artwork (1248px wide)
- `artwork_deity_vulkan_small_88_0.dds` — thumbnail
- Tech category icon: `deco_techtree_techcategory_mosaic_dlc01_444_0.dds`

---

## Ornaments (cdlc01_graphics.rda)

### Celtic Ornaments
- **Mosaic Plazas**: `mosaic_01_01`, `01_02`, `02_01`, `02_02`, `03_01`, `03_03`, `04_01`, `3x3_01`, `3x3_02`
- **Stone Plazas**: `stone_plaza_01`, `stone_plaza_03`
- **Natural Plaza**: `natural_plaza_01`
- **Wall System**: `wall_01` — full set: straight (2/5/7/10/14/20/28/30/40 tiles), T, X, Y, corner_360, gate, gate_02
- **Celtic Gate**: `gate_celtic_01`

### Roman Ornaments
- **Mosaic Plazas**: `mosaic_01_01`, `01_02`, `02_01`, `02_02`, `03_01`, `03_03`, `04_01`, `3x3_01`, `3x3_02`
- **Stone Plazas**: `stone_plaza_01` through `stone_plaza_07`
- **Wall System**: `wall_roman_01`, `wall_roman_02`
- **Roman Gate**: `gate_roman_01`

### Building Skins (`skin01_mosaics`)
- Roman Forum × 2 variants — mosaic-textured skin with marble details
- Roman Baths × 2 variants — mosaic-textured skin with marble details

### Props Library
- Flower pots, ornament bases, watcher statues
- Hanging bush (12 variants: `hanging_bushl_01` – `12`)
- Top bush (4 variants)
- Town Crier on Box (celebration ornament)

---

## UI: Specialist Portraits

- `icon_3d_dlc01_female_01` – `14` (14 female specialists)
- `icon_3d_dlc01_male_01` – `16` (16 male specialists)
- `icon_3d_dlc01_trader_caecilia_0` (Caecilia's trader portrait)
- `icon_3d_trader_caecilia_0` (Caecilia's diplomacy portrait)

## UI: Campaign Icons

| Icon | Subject |
|---|---|
| `icon_3d_campaign_dianas_entourage_0` | Diana's questline |
| `icon_3d_campaign_fresh_ash_0` | Ash event |
| `icon_3d_campaign_ominous_obsidian_offering_0` | Obsidian offering quest |
| `icon_3d_campaign_philosophers_0` | Philosophers questline |
| `icon_3d_campaign_rainbow_obsidian_0` | Rare obsidian find |
| `icon_3d_campaign_vulcanus_temple_grounds_0` | Vulcanus temple |

---

## Achievements

9 achievements in set 12: `achievement_set12_01_0.dds` – `achievement_set12_09_0.dds`

---

## Key GUIDs

```lua
DLC01_Prophecies_of_Ash = 67902
LatiumVolcanoTemple     = 145427
VolcanoDefaultProjectile= 145812
LatiumKontor1           = 3402
LatiumKontor2           = 3403
LatiumKontor3           = 3406
```

---

## How to Search DLC01 Content

```powershell
. scripts/agent-tools/RDA-Agent.ps1
Use-Anno117

rda-find "dlc01"                                      # all DLC01 file paths
rda-grep "67902|dlc01" "assets"                       # GUID in assets.xml
rda-grep "prophecy|obsidian|volcano|caecilia" "texts_english"  # lore text
rda-list cdlc01_graphics                              # full graphics file list
rda-read script "data/script/types/generated/rdgs/cvolcanoeruptionmanagerluabindings.lua"
rda-read config "data/base/config/engine/ambientsettings/amb_roman_dlc01_eruption_01.xml"
```
