# UI Console Animation Loops — Agent Brief

**Audience:** AI implementer working in `src/AssetSplitterUI`  
**Scope:** Console / status / progress feedback during Extract & Process  
**Do not:** Redesign layout, theme system, log pipeline, or first-run onboarding UX

---

## 1. Purpose of the console during Extract / Process

The console panel is the **live run surface**: phase/status text, progress bar, and scrolling log of backend output. While `IsProcessing` is true, the UI is interaction-locked (inputs disabled, dropdowns closed). Animation loops exist only to signal **“work is ongoing”**, **“run finished”**, or **“run failed/cancelled”** — not decoration.

Primary state gate: `MainWindowViewModel.IsProcessing`  
- `true` → start busy loops, show shimmer, enable Cancel  
- `false` → stop busy loops, hide shimmer, re-enable controls  

Related: `IsComplete`, `StatusIsError`, `Progress`, `LogVersion`, cancel via `CancelExtractionCommand`.

---

## 2. What already exists (do not reinvent)

| Name | Where | Behavior | Starts | Stops |
|---|---|---|---|---|
| **BusyIndicatorAnimator** | `ViewModels/BusyIndicatorAnimator.cs` | Cycles Braille spinner frames (`⠋⠙…`) into `SpinnerIcon` and ` ·` / ` ··` / ` ···` into `Ellipsis` every **80 ms** on UI thread | `OnIsProcessingChanged(true)` | `OnIsProcessingChanged(false)` → `Stop()` cancels CTS, clears both strings; also `Dispose()` on VM teardown |
| **SpinnerIcon / Ellipsis bindings** | `MainWindow.axaml` status stacks | Spinner on legacy + structured status; ellipsis only on **legacy** status line (`ShowLegacyStatusLine`) | Visible when `IsProcessing` | Hidden / cleared when not processing |
| **Progress shimmer** | `MainWindow.axaml` `Border.shimmer` | Infinite linear `TranslateTransform.X` sweep (−150 → 1300), gold/teal gradient stripe, **1.7 s** | `IsVisible="{Binding IsProcessing}"` | Binding hides border when processing ends (style animation may keep running while class present — prefer removing from tree / clearing class on stop if fixing) |
| **ProgressBarColor** | VM property | Amber `#D4A843` running; green `#5DBE6E` success; red `#D94545` error | Color binding | Static color change (not a loop) |
| **CompletionFlash** | `Window.Resources` + `ViewModelBindings` | Soft teal overlay opacity pulses (~**5 s**, three diminishing breaths) on `ProgressFlashOverlay` | `IsComplete == true` (success **or** error) | One-shot; ends at opacity 0 |
| **StatusFadeIn** | `Window.Resources` + `ViewModelBindings` | Opacity 0.3 → 1.0 over **0.6 s** on `StatusTextBlock` | When `StatusText` changes **and** `!IsProcessing` (also clipboard / developer unlock) | One-shot; **intentionally skipped** during processing to avoid strobing |
| **Log auto-scroll** | `MainWindow.ViewModelBindings` / `MainWindow.axaml.cs` | On `LogVersion` bump, `ScrollIntoView` last line; throttled to **120 ms** while processing; skipped if user has selection | Each log flush batch | N/A (not a continuous timer loop) |
| **ConsoleOutputCoordinator** | `ViewModels/ConsoleOutputCoordinator.cs` | Timer flush every **100 ms**, batches up to **250** lines — keeps log “live” without per-line UI thrash | `IsProcessing` true | `Stop()` / discard on cancel-close |
| **FirstRunGlow / FirstRunStepHint.Active** | `MainWindow.axaml` styles | Infinite opacity (and box-shadow) pulse **1.35 s** alternate — **onboarding only**, not console-run feedback | First-run step classes while `!IsProcessing` | Stops when processing starts (first-run bindings gate on `!IsProcessing`) |
| **BannerSlideIn / CheckmarkBounce** | Resources | Phase-2 banner entrance — **not** console busy loops | Phase banner show | One-shot |

**Not present today:** dedicated cancel fade; error-colored flash (error reuses teal `CompletionFlash`); ellipsis on structured status; progress “pulse” separate from shimmer; per-line log fade-in; `DispatcherTimer`-based UI loops (busy uses `Task.Delay` + `Dispatcher.UIThread.Post`).

---

## 3. Recommended loops / improvements

Implement only what improves run clarity. Prefer extending existing hooks (`IsProcessing`, `IsComplete`, `StatusIsError`, cancel result path) over new global timers.

### Priority A — must be solid (fix / complete existing)

1. **Busy spinner + ellipsis lifecycle**  
   - Keep Braille/` ·` approach (no emoji).  
   - Ensure cancel and error paths always hit `IsProcessing = false` so `Stop()` clears frames (no stuck spinner).  
   - Optional: show ellipsis next to structured phase title the same way as legacy line.

2. **Shimmer clean stop**  
   - When `IsProcessing` becomes false (success, error, **cancel**), shimmer must not keep moving off-screen or burn CPU.  
   - Prefer `IsVisible` + remove animation participation, or reset transform; verify after cancel.

3. **Cancel visual**  
   - On `StatusKind.Cancelled`: short fade/dim of progress track or status (reuse `StatusFadeIn` pattern; **no** success green flash).  
   - Progress already resets to 0 — optional brief amber→muted fade, then idle.  
   - Must not leave `CompletionFlash` or shimmer running.

### Priority B — polish (small, Avalonia-friendly)

4. **Success vs error flash**  
   - Success: keep/adjust current teal `CompletionFlash`.  
   - Error: same timing, overlay tinted with error red (`#D94545` / `Error` brush), triggered when `IsComplete && StatusIsError`.  
   - Cancel: **no** flash (or a single soft dim only).

5. **Progress idle vs active**  
   - While processing and progress is stalled (optional, low priority): very subtle opacity pulse on bar fill **or** rely on shimmer alone — do **not** add both aggressive pulses.  
   - Prefer one motion: shimmer (existing) is enough unless product asks for more.

6. **Log “live” feel**  
   - **Do not** animate every log line (thousands of lines = jank).  
   - Keep batch flush + throttled auto-scroll.  
   - Optional only: one-shot opacity nudge on the **status** row when phase title changes — never on `ListBox` items.

### Priority C — out unless requested

- Cursor blink in log, particle effects, gradient theme redesign, web-style skeleton loaders, sound.

---

## 4. Constraints

| Constraint | Detail |
|---|---|
| Theme | Dark green panels, gold/amber accent (`#D4A843`), teal accent (`#2E9A8E` / `AccentBrush`). Match existing brushes; no new palette. |
| No emoji | Spinner stays Braille/ASCII. No 🎉 / ❌ in UI chrome. |
| Performance | Log can grow to thousands of lines. No per-item Storyboards. No high-frequency layout invalidation. Coalesce UI posts. |
| `IsProcessing` | All continuous loops start/stop with this flag. |
| Cancel | Must stop animator, shimmer, and any new loops cleanly; no orphan `Task`/`Timer`/`Animation` after cancel. |
| Processing status updates | Do **not** re-trigger full `StatusFadeIn` on every progress tick (already documented in view). |
| Avalonia | Use existing `Animation` resources + `RunAsync`, style `IterationCount="Infinite"` with visibility gates, or the existing CTS/`Task.Delay` pattern. Avoid WPF-only APIs. |

---

## 5. Implementation touchpoints (read first)

```
src/AssetSplitterUI/ViewModels/BusyIndicatorAnimator.cs
src/AssetSplitterUI/ViewModels/MainWindowViewModel.Settings.cs   # OnIsProcessingChanged
src/AssetSplitterUI/ViewModels/MainWindowViewModel.ExtractionResult.cs
src/AssetSplitterUI/Views/MainWindow.axaml                     # resources, shimmer, status, progress
src/AssetSplitterUI/Views/MainWindow.ViewModelBindings.cs      # PlayAnimation, scroll, flash triggers
src/AssetSplitterUI/Views/MainWindow.axaml.cs                  # LogAutoScrollInterval
src/AssetSplitterUI/ViewModels/ConsoleOutputCoordinator.cs    # flush timer (not visual, but “live” feel)
```

---

## 6. Acceptance criteria

- [ ] Start Extract/Process → spinner frames change; ellipsis cycles; shimmer visible and moving.
- [ ] Cancel mid-run → within one UI frame cycle: spinner/ellipsis cleared, shimmer gone/stopped, no flash green/teal success pulse, status shows cancelled (fade OK).
- [ ] Success → amber bar → green; `CompletionFlash` (or success variant) plays once; busy loops stopped.
- [ ] Error → bar red; error flash (if implemented) or existing flash once; busy loops stopped; no stuck spinner.
- [ ] Long run with heavy log output → UI stays responsive; auto-scroll throttled; no per-line animations.
- [ ] First-run glow does not fight console busy state (already gated; do not reintroduce glow during `IsProcessing`).
- [ ] Theme tokens only; no emoji; no whole-window redesign.

---

## 7. Out of scope

- Redesigning MainWindow layout, fonts, or section structure  
- Changing backend console message format or localization keys (unless a new status string is required for cancel)  
- Reworking `ConsoleOutputCoordinator` batch sizes except if required to stop a visual bug  
- First-run onboarding animation redesign (`FirstRunGlow` / step hints)  
- Light-theme-only experiments or new animation libraries  
- Implementing animations in this doc task itself (doc-only unless a follow-up asks to implement)

---

## 8. Suggested implementer order

1. Audit cancel/error/success paths → confirm `IsProcessing` false always stops `BusyIndicatorAnimator` + shimmer.  
2. Add cancel fade (status or track) if missing visual feedback.  
3. Split success/error flash colors.  
4. Optional: ellipsis on structured status.  
5. Manual check against §6 with a long verbose run + cancel mid-flight.
)
