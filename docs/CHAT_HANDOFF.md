# Chat handoff — Asset Splitter UI

**Last updated:** 2026-05-27  
**Use this:** Paste into a new Cursor chat, or `@docs/CHAT_HANDOFF.md` so the agent has context without old screenshots.

---

## Project

Desktop app **Asset Splitter** (Anno 1800 & 117): Avalonia UI (`src/AssetSplitterUI`) + pipeline (`src/AssetSplitter` / `AssetProcessor.dll`). Extracts game assets to XML, optional per-asset Mod Loader mod folders.

**Workspace:** `C:\Users\vadim\Desktop\RANDOM PROJECTS\Asset Splitter UI`

**Build / run:**
```powershell
dotnet build AssetSplitter.sln -c Debug
dotnet run --project src\AssetSplitterUI\AssetSplitterUI.csproj -c Debug
```

---

## Work completed (recent sessions)

### 1. Console coloring — mixed gold / grey on `[DEBUG]` lines (fixed)

**Symptom:** In verbose mod export, `[DEBUG] Creating mod package…` was gold; `[DEBUG] Mod package created…` was muted green — same prefix, different colors.

**Cause:** `ConsoleLineClassifier.IsPhaseOrStepHeader` uses `Contains("PHASE")`. Asset names like `[ Mid Game Phase Roman ]` matched before `[DEBUG]` was checked → `LogLineKind.Phase` (gold) instead of `Debug`.

**Fix:** In `src/AssetSplitterUI/Services/ConsoleLineClassifier.cs` — classify `[DEBUG]` / trace **before** phase heuristics; same in `[…]` switch branch. `LogLine.WithText` and `MainWindowLogStore.RefreshColors` now re-classify.

### 2. Phase 7 crash — `FormatException` in `ModReadmeWriter` (fixed)

**Symptom:** Extraction failed at first mod package with:
`Index must be greater than or equal to zero and less than the size of the argument list`  
Stack: `ModReadmeWriter.WriteShortReadme` → `AssetModPackageExporter.TryCreatePackage`.

**Cause:** `readmeShortStep3` used `{1}` but code only passes one arg:
```csharp
string.Format(t("readmeShortStep3"), assetPath)
```

**Fix:** `{1}` → `{0}` in `config/05_Console_Messages/console_en.json` and `src/AssetSplitter/ConsoleMessages.cs`.

**Note:** Introduced when tiered mod READMEs were added (not by the color fix). Build does not catch `string.Format` arity mismatches.

### 3. Tiered mod documentation (done)

**New:** `src/AssetSplitter/ModReadmeWriter.cs`

| Output | Purpose |
|--------|---------|
| `{export}/MODDING-GUIDE.md` | Full modding guide once per export |
| `{export}/README.md` | Export summary |
| `{mod}/README.md` | Short per-mod readme + link to guide |
| Template `INDEX.md` | Browse assets (existing pattern) |

**Wiring:** `AssetModPackageExporter.cs` calls `ModReadmeWriter` instead of huge inline `WriteReadme`.

**User impact:** Old runs wrote ~150-line README per mod (~31k folders). Re-export needed to replace old READMEs.

### 4. Developer report & console alerts (done)

- `ConsoleLineClassifier`: removed broad `"failed"` substring (popup asset names were false reds); `IsReportableAlert` for `[ERROR]`/`[WARN]` only.
- `MainWindowViewModel.Extraction.cs`: re-wired `ExtractionIssueSummaryAppender` after partial-class split.
- `PipelineIssueReporter`: empty `issues_*.json` in debug when zero issues.
- `MainWindowViewModel.DeveloperTools.cs`: console alert section in copied developer report.
- Missing UI strings: `consoleMessages.outputFolders*`, `issueSummary.noneRecorded`, etc. (EN + manual mirror across locale files).

### 5. Project README audit (done)

Updated source READMEs under `docs/`, `src/`, `config/`, `Localization/`, `scripts/`, RDA — not copies under `publish/`, `bin/`, `debug-output/`.

### 6. UI tweak

Removed `ToolTip.Tip` on console log in `MainWindow.axaml` (was noisy).

---

## Key files

| Area | Path |
|------|------|
| Pipeline entry | `src/AssetSplitter/PipelineOrchestrator.cs` |
| Phase 7 mods | `src/AssetSplitter/AssetModPackageExporter.cs` |
| Mod READMEs | `src/AssetSplitter/ModReadmeWriter.cs` |
| Console strings | `config/05_Console_Messages/console_en.json` |
| Embedded defaults | `src/AssetSplitter/ConsoleMessages.cs` |
| Log colors | `src/AssetSplitterUI/Services/ConsoleLineClassifier.cs` |
| Log brushes | `src/AssetSplitterUI/Converters/LogLineKindConverter.cs` |
| Issues JSON | `src/AssetSplitter/Issues/PipelineIssueReporter.cs` |
| UI strings | `Localization/Languages/Strings.json` |

---

## User environment (typical)

- Game: **Anno 117 - Pax Romana** (Steam path under Program Files x86).
- Output: often `C:\Users\vadim\Desktop\AnnoAssets\...`
- Options often on: verbose log, GUID comments, resolve parents, ModOps XML, template folders, split templates, **asset mod folders**.
- Large export: ~31,565 mod packages in Phase 7.

---

## Known issues / quirks

- **UI exit code `3221226525`:** Windows abort after long sessions or abrupt close — not necessarily a build failure.
- **File lock on build:** If UI or AssetProcessor is running, DLL copy can fail — close app and rebuild.
- **Cursor chat lag:** Long threads with many 4K screenshots — use **new chat**, paste **text logs**, `@docs/CHAT_HANDOFF.md`, avoid huge images.
- **readme strings:** Other languages may fall back to English for new `readme*` keys if not synced to all `console_*.json`.

---

## Suggested verification (after pull/build)

1. Build solution, run UI.
2. Full extraction with **asset mod folders** + **verbose log**.
3. Phase 7 completes without `FormatException`.
4. Console: all `[DEBUG]` mod lines same muted color (names with “Phase” no longer gold).
5. Spot-check one mod folder: short `README.md`, link to `MODDING-GUIDE.md`.

---

## Duplicate console lines (fixed 2026-05-27)

- **Double `[100%] Creating asset mods`:** Loop emitted on last file *and* post-loop block did the same — removed `created == files.Length` from loop; single final `OutputFixer` in `AssetModPackageExporter`.
- **Double “No structured issues…” in debug:** Backend `PipelineIssueReporter` + UI `ExtractionIssueSummaryAppender` — backend skips human `SUMMARY` lines when `Console.IsOutputRedirected` (GUI); UI keeps bordered developer block.

---

## Not done / optional follow-ups

- Unit test: validate every `readme*` key used in `ModReadmeWriter` has correct `{0}`/`{1}` arity vs `string.Format` calls.
- Sync full `readme*` keys to all `console_*.json` languages (not only EN).
- `.cursorignore` for huge export folders (`output_xml_*`, `_mods`) to reduce IDE indexing lag.

---

## Git

Many modified files; **commits only when user asks.** Do not force-push or amend without explicit request.
