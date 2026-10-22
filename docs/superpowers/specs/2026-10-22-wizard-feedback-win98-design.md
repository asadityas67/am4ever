# Wizard feedback — Win98 progress bars and toasts

## Intent & why
The beginner wizard works but gives almost no interaction feedback. The build — the single most
important action in the app — currently shows a 0.8-opacity grey subtext line that changes from
"Building and checking…" to "Built. Report saved: …", with the action button greyed for a few
seconds. Users don't notice it happened; it "just kinda happens." The app needs **game feel**: the
operations that take a beat should *visibly* take it, and every discrete success/failure should
*arrive*. The chosen idiom is **Windows 98**: chunky segmented progress bars and small beveled
toast pop-ups, matching the app's existing classic-WinForms theme.

## Goals
- The **build** shows a real, honest segmented progress bar that advances as the build's actual
  phases complete, and ends in an unmissable success (or failure) state.
- **Loading a dump and a donor** (each a 16 MiB read + validation/SHA) shows a segmented progress
  bar while it works.
- Every discrete event in the wizard — dump loaded, profile selected, donor verified, identity set,
  build done/failed — raises a **Win98-style toast** that auto-dismisses.
- Everything is drawn from the **existing** classic theme (`am4.*` brushes, the nested-border bevel
  idiom); no second style system.
- New controls and wiring are **headless-testable** like the rest of the app (the wizard is
  deliberately code-built, not XAML, "to stay small and headless-testable").

## Non-goals
- No sound effects (the Win98 *ding* is annoying and platform-flaky).
- No changes to the CLI, its output, or `inspect`/`build` behaviour beyond an optional progress hook
  that existing callers can ignore.
- No changes to Board Lab (expert mode) — this is the **wizard** only.
- No new animation framework; a simple fade/slide at most, and the bar's advance is driven by real
  progress, never a timer-interpolated fake.
- No change to what the build produces — the output image and report are byte-for-byte unchanged.

## Anti-goals (these make it a failure even if it "works")
- A progress bar that lies — jumps to 100% instantly, animates while nothing happens, or
  interpolates fake intermediate values. If the bar moves, a real phase completed.
- Toasts that pile up without bound, block the Back/Next buttons, or can't be dismissed.
- Regressing the "no build-anyway on a failed check" guarantee, or the existing test suite.

## Constraints
- GPL-2.0-or-later header on every new file.
- win-x64 and linux-x64 portable builds must both still work; no new native dependency.
- Avalonia (existing version), SimpleTheme + the app's `WinFormsTheme.axaml` overlay.
- Must not break the existing suite (159 pass with ROMs+host; 132 + skips without).
- "Keep it simple — not going to be maintained": prefer the smallest honest implementation; no
  configurability knobs beyond what the goals need.

## How it's built (decisions made)

### 1. Core — honest build phase progress
`BuildPipeline.Build(...)` gains an **optional** progress reporter:
`IProgress<BuildProgress>? progress = null` as a trailing parameter, so the CLI and all existing
callers compile and behave unchanged. `BuildProgress` is a small record `(int Done, int Total,
string Phase)`. The pipeline reports, in order: donor SHA check → PEI decompress → one tick per
operation as it is applied (the bulk, and the honest granularity) → DXE apply (`add_ec_init`) →
recompress + round-trip verify → directory/FFS checksums. `Total` is known up front from the
operation count plus the fixed phases. Reports are monotonic and the final report is `Done == Total`.
This is the only core change.

### 2. App — `SegmentedProgressBar` control
A small code-built control rendering the classic Win98 bar: a **sunken** trough (the bevel idiom
reversed — light-light on the bottom-right, dark on the top-left) holding a row of fixed-width navy
blocks separated by gaps. Input is a fraction `0.0–1.0` (or Done/Total); the number of lit blocks is
`floor(fraction × blockCount)`. Navy fill is a new theme brush `am4.ProgressFill` (`#000080` light,
a lighter navy for dark mode). Headless-testable: setting the value sets a readable `FilledSegments`
count. No indeterminate mode (the fill is always honest).

### 3. App — toast service + `Toast` control
An `IToastService` with `Show(string message, ToastKind kind)` where `ToastKind` is
`Success | Error | Info`. A `ToastHost` overlay lives in `MainWindow` (a `Panel` pinned
bottom-right) and renders each toast as a **raised** beveled gray box with a leading glyph
(`✓` / `✕` / `ℹ`), the message, auto-dismiss after ~3.5 s via a `DispatcherTimer`, and click-to-
dismiss. Toasts stack upward, newest at the bottom, capped at a small number (oldest dropped).
The host never intercepts input to the wizard beneath it. Headless-testable: `Show` adds a live
toast to the host's collection; dismiss (timer elapsed or click) removes it — tests drive the model,
not the pixels.

### 4. Wizard wiring (`WizardView`)
- The build step hosts a `SegmentedProgressBar`. `Build` runs off the UI thread (as now) with an
  `IProgress<BuildProgress>` marshaling to the UI thread; the bar advances per phase and a phase
  label shows beside it. On success: bar full, a **sunken "✓ Done" panel** showing the output and
  report paths, and a `Success` toast "✓ BIOS built!". On failure: an `Error` toast and the failure
  reason in a red-tinted panel; bar stops where it was.
- Dump and donor loading run their read+validate off-thread with a progress bar (bytes copied, then
  a verify tick), then a `Success` toast ("✓ Dump loaded — 16 MiB, valid AM4" / "✓ Donor verified").
- Profile select and identity set raise their toasts immediately (fast, no bar).
- The existing faint `Reason` line stays for the per-step "why Next is disabled" hint; the new
  feedback is additive, not a rewrite of the step machine.

### 5. Theme additions
Add `am4.ProgressFill` to `WinFormsColors.axaml` (light + dark) and style classes for the sunken
trough, the raised toast box, and the done/error panels to `WinFormsTheme.axaml`, all using existing
`am4.*` bevel brushes.

## What's left to the builder
- Exact block width/gap/count of the segmented bar, toast dismiss duration and max-stack count, the
  done/error panel wording and padding — pick tasteful Win98-authentic values.
- Whether `SegmentedProgressBar` is a `TemplatedControl` or a `UserControl` drawing in code — either
  is fine as long as it's headless-testable and themed from `am4.*`.
- The precise `BuildProgress` phase labels and where in `BuildPipeline` each tick is emitted, as long
  as they are honest, monotonic, and end at `Done == Total`.
- Fade/slide-in for toasts is optional polish; skip it if it complicates headless testing.

## Testing approach (TDD)
- Core: a test asserts `Build` reports monotonic progress, one tick per operation, final
  `Done == Total`, and that passing `null` leaves behaviour identical (output image unchanged — the
  existing T5 reference test already pins the bytes).
- `SegmentedProgressBar`: value → `FilledSegments` mapping (0, partial, 1.0, clamping).
- Toast service: `Show` adds; timer/click removes; stack cap drops oldest.
- Wizard: an `[AvaloniaFact]` drives a build with a fake/one-shot flow and asserts the bar reaches
  full and a success toast is raised; a failed build raises an error toast and writes no image.
