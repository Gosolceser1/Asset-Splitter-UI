# DLC01 "Prophecies of Ash" — Triggers Reference

> **Game**: Anno 117 — Pax Romana  
> **Source archives**: `config.rda` (assets.xml), `script.rda` (Lua datasets + bindings)  
> **Status**: Research complete — all trigger types enumerated

---

## Quick Lookup: What You Can Trigger

| What you want | How to trigger it |
|---|---|
| Jump to campaign chapter | `Quests.CampaignHandlerMutable:SetActiveChapter(QuestCampaignChapter.Act_2_Chapter_4)` |
| Cycle next chapter (cheat) | `Quests:CheatCycleCampaignChaptersNet()` |
| Start a storyline by GUID | `Quests:CheatStartStoryLineForCurrentPlayerNet(114730)` |
| Start a quest component by GUID | `Quests:CheatStartQuestComponentForCurrentPlayerNet(115012)` |
| Fire a named campaign special action | embedded in a `QuestComponent` via `ActionCampaignSpecialActions` |

---

## 1. Campaign Chapter Triggers

**Enum**: `datasets.QuestCampaignChapter` — 3 Acts × 3 Chapters + Completed  
**File**: `data/script/types/generated/datasets/quest_campaign_chapter.lua`

| Value | Enum Name | Description |
|---|---|---|
| -1 | `Invalid` | No chapter |
| 0 | `Act_1_Chapter_1` | Opening, arrival in Latium |
| 1 | `Act_1_Chapter_2` | Establishing footholds |
| 2 | `Act_1_Chapter_3` | Voada's first threat |
| 3 | `Act_2_Chapter_4` | Ben-Baalion's counsel; island unlocked |
| 4 | `Act_2_Chapter_5` | Voada ambush |
| 5 | `Act_2_Chapter_6` | Resolution / Prophecy discovered |
| 6 | `Act_3_Chapter_7` | Volcano eruption onset |
| 7 | `Act_3_Chapter_8` | Obsidian harvest + Lucius confrontation |
| 8 | `Act_3_Chapter_9` | Final choices, Latium settling |
| 9 | `CampaignCompleted` | Epilogue festival |

**API call** (Lua, in-game console / debug page):
```lua
Quests.CampaignHandlerMutable:SetActiveChapter(datasets.QuestCampaignChapter.Act_3_Chapter_7)
-- or cycle to next:
Quests:CheatCycleCampaignChaptersNet()
```

### Chapter-Related Side Info from CQuestCampaignHandler

```lua
---@field VoadaTradeRightTendency integer   -- Voada's current trade-rights reasoning
---@field VoadaCeaseFireTendency integer    -- Voada's current cease-fire reasoning
```

---

## 2. Campaign Special Actions (CampaignSpecialActions)

**Enum**: `datasets.CampaignSpecialActions`  
**File**: `data/script/types/generated/datasets/campaign_special_actions.lua`

These are scripted one-shot events placed inside `ActionCampaignSpecialActions` nodes in `assets.xml` quest components. They are not directly callable by Lua — they fire when the hosting quest component is reached.

| Value | Enum Name | When it Fires | Notes |
|---|---|---|---|
| 0 | `KillLucius` | Emperor Lucius is killed by slave revolt | Story turning point — Act 3 pivot |
| 1 | `CreateCampaignBackupSavegame` | Checkpoint after major decisions | Fires at Act 2 and Act 3 boundaries |
| 2 | `EpilogueFestival` | Campaign completed | Triggers epilogue festival visuals/music |
| 3 | `Act2IslandUnblocked` | Act 2 starts | Unlocks second island on the province map |
| 4 | `VoadaAmbushStart` | Voada launches an ambush | Starts hostile encounter cutscene/objective |
| 5 | `VoadaAmbushEnd` | Ambush resolves | Returns province to normal diplomatic state |
| 6 | `Act3LatiumSettlingUnblocked` | Act 3 chapter transition | Unlocks Latium as a settable province area |

**How they appear in assets.xml** (example node):
```xml
<Action>
  <Template>ActionCampaignSpecialActions</Template>
  <Values>
    <ActionCampaignSpecialActions>
      <SpecialAction>VoadaAmbushStart</SpecialAction>
    </ActionCampaignSpecialActions>
  </Values>
</Action>
```

Evidence of all 7 enum values confirmed in `assets.xml` via `CampaignSpecialAction` and `VoadaAmbush` matches.

---

## 3. StoryLine Assets and Their Triggers

StoryLines are story graph roots — each has a GUID, a `StartConnector` pointing to the first `QuestComponent` GUID, and optional `QuestPool` membership.

### DLC01-Related StoryLines

| GUID | Name | First Component GUID | Triggered By |
|---|---|---|---|
| **114730** | `VoadaIslandSetup` | 115012 | Campaign chapter transition (Act 2) |
| **140444** | `SchaafusStoryline` | 140445 | "Decisions Uprising" QuestPool |
| **83248** | `PressVersionIntro` | 83249 | Game launch / Press build |

### Base-Game StoryLines Referenced by Trigger Assets

| GUID | Name | Trigger GUID | Notes |
|---|---|---|---|
| 98984 | (Province Roman entry) | 64748 | Fires when player enters Roman province |
| 99234 | (Province Celtic entry) | 64749 | Fires when player enters Celtic province |
| 108257 | `Achievement_Colosseum` | 121325 | Achievement storyline |
| 108705 | `Achievement_409_WarPeace` | 121325 | Achievement storyline |
| 49942 | (unnamed) | unnamed trigger | AllowAllLocations |
| 38415 | (unnamed) | unnamed trigger | FixLocations |
| 38407 | (unnamed) | unnamed trigger | FixLocations |

### Named Trigger Assets

| GUID | Name | Starts StoryLine |
|---|---|---|
| **64748** | Start Enter Province Roman Storyline Trigger | 98984 |
| **64749** | Start Enter Province Celtic Storyline Trigger | 99234 |
| **121325** | Achievement_Storylines_Trigger | 108257, 108705 |

---

## 4. QuestPool Membership

DLC01 storylines are associated with these pools:

| Pool Name | Storyline GUIDs |
|---|---|
| Decisions Inferno | 77893, 77961, 88183, 90065 |
| Decisions Uprising | 77916, 77978, 89099, 89250, 90085, **140444** (Schaafu) |
| Decisions Plague | 77938, 77984, 88997, 89547, 90119 |

> **SchaafusStoryline (140444)** is pooled under "Decisions Uprising" with `IsTopLevel=1`, meaning it can be the top-level selected storyline from that pool.

---

## 5. Volcano System Triggers (DCVolcano Datasets)

**Lua API**: `Volcano` global (alias of `rdgs.CVolcanoEruptionManager`)  
**File**: `data/script/types/generated/rdgs/cvolcanoeruptionmanagerluabindings.lua`

```lua
Volcano:GetCurrentOverdriveFactor(participantGuid)   -- float: current eruption intensity
Volcano:GetMaximumOverdriveFactor(participantGuid)   -- float: cap intensity
Volcano:isValid()                                    -- bool
```

### Volcano State Datasets

| Dataset | Enum Name | Purpose |
|---|---|---|
| `DCVolcanoEruptionActive` | `datasets.DCVolcanoEruptionActive` | Is eruption currently active? |
| `DCVolcanoIncidents` | `datasets.DCVolcanoIncidents` | Incident type triggered during eruption |
| `DCVolcanoObsidianDeposits` | `datasets.DCVolcanoObsidianDeposits` | Deposit spawn type |
| `DCVolcanoObsidianDrops` | `datasets.DCVolcanoObsidianDrops` | Drop event type |
| `DCVolcanoSchedulerEruptionLength` | `datasets.DCVolcanoSchedulerEruptionLength` | Duration tier for eruptions |
| `DCVolcanoSchedulerIntervalDuration` | `datasets.DCVolcanoSchedulerIntervalDuration` | Dormancy interval tier |

### Volcano Lifecycle Phases (from ambient XMLs)

| Phase | Config File | Ash | Snow | Glow |
|---|---|---|---|---|
| Prelude | `amb_roman_dlc01_prelude_01.xml` | 0 | 0 | — |
| Base Ash-Rain | `amb_roman_dlc01_01.xml` | 1 | 0.999 | 50 |
| Eruption | `amb_roman_dlc01_eruption_01.xml` | 1 | 0 | 270.8 |
| Volcanic Winter | `amb_roman_dlc01_volcwinter_01.xml` | 1 | 0.999 | 270.8 |
| Bloom | `amb_roman_dlc01_bloom_01.xml` | 1 | 0 | 270 |

**Phase selection** is controlled by the `DCVolcanoEruptionActive` dataset value and the eruption scheduler.

---

## 6. Quest Component Type Taxonomy

**File**: `data/script/types/generated/datasets/quest_component_type.lua`

```lua
datasets.QuestComponentType = {
    Invalid = -1,
    Storyline = 0,    -- Root node / story graph container
    Questline = 1,    -- Sub-chain of objectives
    Objective = 2,    -- Single task for the player
    Function = 3,     -- Script function call node
    Sequence = 4,     -- Ordered sequence of components
    Starter = 5,      -- Entry-point trigger
    Decision = 6,     -- Branch point (player choice)
    ComplexCombination = 7,
    StateChecker = 8,
    DecisionRoot = 9,
}
```

### Related Type Enums

| Enum | Key Values |
|---|---|
| `QuestState` | Triggered(0), Active(1), Reachable(2), Reached(3), Failed(4), AbortedAutomatically(5), AbortedManually(6) |
| `QuestScope` | Storyline(0), Global(1) |
| `QuestSystemType` | Quests(0), GovernorDecisions(1), Contracts(2) |
| `SpecialLogicQuestComponentType` | Success(0), Failure(1), Loop(2), Exit(3), DenyAndExit(4) |
| `FirstPersonQuestType` | Sidequest(0), Fighter(1), Mage(2), Thieve(3) |
| `QuestEntryType` | Campaign(0), Default(1) |
| `CampaignBanner` | Generic(0), Chapter01-09(1-9), LastBanner(10) |

---

## 7. DLC01 Key Characters (Quest Participants)

### Ben-Baalion (Enslaved Advisor / Merchant)
- **Portrait GUIDs**: 54569, 54570, 54571, 54572, 54573, 54574  
- **Quest type**: Core campaign advisor — present across all 3 Acts  
- **Notable quest GUIDs**: 42630, 55248, 78070, 88524, 94130, 87501

### Voada (Celtic Rival Queen)
- **Portrait GUIDs**: 83480, 83481, 83482, 83483, 43022, 64802, 64803, 64804, 145477, 95450  
- **StoryLine**: `VoadaIslandSetup` = **114730** (first component: 115012)  
- **Special Actions**: `VoadaAmbushStart` (4), `VoadaAmbushEnd` (5)  
- **Quest components**: GUIDs in 93989–95450 range + 114xxx range  
- **Diplomatic fields**: `VoadaTradeRightTendency`, `VoadaCeaseFireTendency`

### Schaafu (Celtic Noble / Storyline)
- **StoryLine**: `SchaafusStoryline` = **140444** (first component: 140445)  
- **Character assets**: 140446, 140447, 140448, 140460, 140461  
- **Pool**: "Decisions Uprising" (IsTopLevel = 1)  
- **Schaafu** is an animal (`schaafu` in graphics/library/wildlife path) — possibly Schaafu is a named creature or character  

### Caecilia (Blind Oracle / Trader)
- Referenced in DLC activation art: `img_dlc01_volcano_midground_trader_caecilia.png`  
- Referenced in quest icon: `icon_2d_quest_volcano*`  
- Character likely linked to the Caecilia Lighthouse building  

---

## 8. Province Template Expansion (DLC01 Map Content)

All 9 province layout templates expanded by `<EnlargeDLC>67902</EnlargeDLC>`:

| Province Template | Variants |
|---|---|
| `roman_province_campaign_01` | `dlc01expanded` |
| `roman_province_default_01` | easy / medium / hard `dlc01expanded` |
| `roman_province_donut_01` | easy / medium / hard `dlc01expanded` |
| `roman_province_rift_01` | easy / medium / hard `dlc01_expanded` |
| `roman_province_corners_01` | easy / medium / hard `dlc01expanded` |
| `roman_province_chain_01` | easy / medium / hard `dlc01expanded` |

---

## 9. Full Quests API Reference

### CQuestManager (`Quests` global)

```lua
-- Start a full storyline for current player:
Quests:CheatStartStoryLineForCurrentPlayerNet(storyLineGUID)

-- Start a single quest component:
Quests:CheatStartQuestComponentForCurrentPlayerNet(componentGUID)

-- Cycle campaign chapter forward:
Quests:CheatCycleCampaignChaptersNet()

-- Quest pool control:
Quests:EnableQuestPoolForCurrentPlayer(poolGUID, true/false)
Quests:CheatResetPoolEntriesNet(poolGUID)
Quests:CheatEndPoolCooldownNet(poolGUID)
Quests:CheatEndQuestBlockingNet(poolGUID, questGUID)
Quests:ResetTutorialQuestsNet()

-- Debug:
Quests:SetDebugQuestGUID(questGUID)
Quests:SetDebugParticipant(participantGUID)

-- Access campaign handler:
Quests.CampaignHandlerMutable:SetActiveChapter(chapterEnum)

-- Fields on CampaignHandlerMutable:
Quests.CampaignHandlerMutable.VoadaTradeRightTendency  -- integer
Quests.CampaignHandlerMutable.VoadaCeaseFireTendency   -- integer
```

### CQuestComponentHelper (module, also aliased as global)

```lua
-- Start storyline via smart router (auto-selects correct manager):
CheatStartStoryLineForCurrentPlayerNet(storyLineGUID)

-- Get a live quest component object:
GetQuestComponent(componentID)        -- returns rdgs.CQuestComponent

-- Get location info for a component:
GetLocationInfo(componentID)          -- returns rdgs.CLocationInfo|nil
```

### CQuestComponent (returned by GetQuestComponent)

```lua
component.GUID   -- integer: the component's GUID
component:isValid()  -- boolean
```

---

## 10. Cheat / Debug Quick-Start Commands

```lua
-- Jump to Act 3 (volcano), Chapter 7:
Quests.CampaignHandlerMutable:SetActiveChapter(6)

-- Start Voada island setup storyline:
Quests:CheatStartStoryLineForCurrentPlayerNet(114730)

-- Start Schaafu's storyline:
Quests:CheatStartStoryLineForCurrentPlayerNet(140444)

-- Start province entry storylines:
Quests:CheatStartStoryLineForCurrentPlayerNet(98984)  -- Roman province
Quests:CheatStartStoryLineForCurrentPlayerNet(99234)  -- Celtic province

-- Check volcano state:
Volcano:GetCurrentOverdriveFactor(participantGuid)
Volcano:GetMaximumOverdriveFactor(participantGuid)
```

---

## Search Commands (RDA Agent)

```powershell
# Reload for future sessions:
. .\scripts\agent-tools\RDA-Agent.ps1; Use-Anno117

# Find more quest components by name:
rda-grep "QuestComponent|StoryLine" "assets"

# Find Voada quest components in detail:
rda-grep "Voada" "assets"

# List all quest dataset enums:
$scriptList | Where-Object { $_ -match 'quest_|storyline|campaign' }

# Read a specific enum:
rda-read script "data/script/types/generated/datasets/quest_campaign_chapter.lua"
```

---

*Research date: April 2026 | Data source: Anno 117 Pax Romana `maindata/*.rda`*
