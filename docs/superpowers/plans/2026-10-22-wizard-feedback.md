# Wizard feedback (Win98 progress bars + toasts) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the beginner wizard game-feel feedback — honest Win98 segmented progress bars for the slow operations and toast pop-ups for discrete events — built on the app's existing classic-WinForms theme.

**Architecture:** One optional `IProgress<BuildProgress>` hook in `BuildPipeline.Build` (existing callers unaffected, output bytes unchanged) carries real per-phase progress. The App gains a `SegmentedProgressBar` control, a `ToastService`/`ToastHost`, and a pure `WizardToasts` message mapper. `WizardView` wires them: a progress bar on the build and on the 16 MiB dump/donor read+verify, and a toast per step event. All styling reuses the `am4.*` bevel brushes.

**Tech Stack:** C#/.NET 8, Avalonia (existing version) over SimpleTheme + `WinFormsTheme.axaml`, xUnit with `Avalonia.Headless.XUnit`.

**Spec:** docs/superpowers/specs/2026-10-22-wizard-feedback-win98-design.md

## Global Constraints
- GPL-2.0-or-later header (`// SPDX-License-Identifier: GPL-2.0-or-later`) on every new file.
- No sound; no new native dependency; win-x64 and linux-x64 must both still build.
- No change to the built image or report bytes — the existing `Hp285G3ReferenceTests` pins them.
- No changes to the CLI, `inspect`/`build` behaviour, or Board Lab.
- The build-anyway-on-failure guarantee stays: a failed build writes no image.
- "Keep it simple, unmaintained": smallest honest implementation; no config knobs beyond what a task needs.
- UI-free logic is plain xUnit; controls/views are `[AvaloniaFact]` headless; full-build tests are `[RomFact]` (skip without `AM4EVER_TEST_ROMS`).

## Review Focus
- **Cross-thread progress:** the build runs on a background thread; progress must reach the UI safely. The `Progress<BuildProgress>` must be constructed on the UI thread so its callback posts to the UI synchronization context — Task 7's view test constructs it in the `[AvaloniaFact]` (UI) context and asserts the bar updates without a cross-thread throw.
- **Build fails mid-phase:** progress stops partway and the bar must not imply success; the error toast still fires and no image is written — Task 7 tests a failed `BuildOutcome` path raises an `Error` toast and leaves the bar un-full.
- **Toast timer after teardown:** an auto-dismiss firing after the host/view is gone must be a no-op — Task 3 tests dismissing an already-removed toast is safe and double-dismiss is idempotent.
- **Toast flood:** rapid events beyond `MaxVisible` drop the oldest with no unbounded growth — Task 3 tests the cap.
- **Degenerate bar values:** `Value` of NaN/∞/negative/>1 must clamp so `FilledSegments` stays in `[0, SegmentCount]` — Task 2 tests clamping.

---

### Task 1: Core — honest build phase progress

**Files:**
- Create: `src/Am4ever.Core/Build/BuildProgress.cs`
- Create: `tests/Am4ever.Tests/SyncProgress.cs`
- Modify: `src/Am4ever.Core/Build/BuildPipeline.cs`
- Test: `tests/Am4ever.Tests/Build/BuildProgressTests.cs`

**Interfaces:**
- Produces: `public sealed record BuildProgress(int Done, int Total, string Phase);`
- Produces: `BuildPipeline.Build(byte[] donorRom, byte[] oemRom, BoardProfile profile, DonorAdapter adapter, DmiIdentity? dmiOverride, IProgress<BuildProgress>? progress = null)` — the trailing optional parameter keeps every existing caller source-compatible and behaviourally identical when omitted/null.
- Produces (test helper): `sealed class SyncProgress<T>(Action<T> onReport) : IProgress<T> { public void Report(T v) => onReport(v); }` in `tests/Am4ever.Tests/SyncProgress.cs` — a synchronous `IProgress` used by every progress test so reports are complete when the reporting call returns.

- [ ] **Step 1: Write the failing test**

```csharp
// BuildProgressTests.cs — needs the real ROMs, like the reference test.
public class BuildProgressTests
{
    [RomFact] public void Build_ReportsMonotonicProgressEndingAtTotal()
    {
        var oem = File.ReadAllBytes(TestRoms.HpF65!);
        var donor = File.ReadAllBytes(TestRoms.AsrockP1043!);
        var items = new List<BuildProgress>();
        BuildPipeline.Build(donor, oem, BundledData.LoadProfile("hp-285-g3-8434"),
            BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"), null,
            new SyncProgress<BuildProgress>(items.Add));

        Assert.NotEmpty(items);
        Assert.All(items, p => Assert.False(string.IsNullOrWhiteSpace(p.Phase)));
        Assert.All(items, p => Assert.InRange(p.Done, 1, p.Total));
        for (int i = 1; i < items.Count; i++) Assert.True(items[i].Done >= items[i - 1].Done);
        Assert.Equal(items[^1].Total, items[^1].Done);        // ends full
    }
}
```

> Use the synchronous `SyncProgress<T>`, **not** `new Progress<T>(...)`: `Progress<T>.Report` marshals to the captured `SynchronizationContext` (or the thread pool when there is none), so its callbacks can arrive after `Build` returns and make the assertions race. The real view (Task 6) uses `Progress<T>` on purpose — there the UI context is what should receive the callback.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~BuildProgressTests`
Expected: FAIL to compile — `Build` has no 6th parameter. (With ROMs set it then runs; without, `[RomFact]` skips — so also confirm the project builds.)

- [ ] **Step 3: Implement the hook**

Add `BuildProgress`. In `Build`, compute `total` up front from the profile: `1` (donor SHA) `+ apcbOps.Count + 1` (PEI decompress) `+ peiOps.Count + 1` (boot repack) `+ (hasDxe ? dxeOps.Count + 1 : 0)` where `hasDxe = Operations.Any(o => o.Op is "smbios_identity" or "add_ec_init")`. Keep a `done` counter and a local `void Tick(string phase) => progress?.Report(new(++done, total, phase))`. Call `Tick` **after** each unit completes: after the SHA check (`"Checking donor"`), after `RunApcbChecks` per apcb op (`"Checking memory map"`), after PEI decompress (`"Reading boot volume"`), after each PEI op's apply (`"Porting <op.Op>"` — tick inside the apply loop), after `RepackBootVolume` (`"Recompressing boot volume"`), and for the DXE phase pass the reporter/tick into `ApplyDxe`→`ApplyDxeOperations` so each DXE op ticks (`"Writing <op>"`) plus one after recompression (`"Recompressing DXE volume"`). Report is monotonic; final `done == total`. Thread `progress`/`Tick` only where those loops already are; do not reorder existing work.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~BuildProgressTests`
Expected: PASS (with ROMs) / SKIP (without), project builds.

- [ ] **Step 5: Run the reference test to prove bytes are unchanged**

Run: `dotnet test --filter FullyQualifiedName~Hp285G3ReferenceTests`
Expected: PASS (with ROMs) / SKIP — the output image is byte-identical; the hook is side-effect-free.

- [ ] **Step 6: Commit**

```bash
git add src/Am4ever.Core/Build/BuildProgress.cs src/Am4ever.Core/Build/BuildPipeline.cs tests/Am4ever.Tests/SyncProgress.cs tests/Am4ever.Tests/Build/BuildProgressTests.cs
git commit -m "feat(core): optional build progress reporter"
```

---

### Task 2: App — SegmentedProgressBar control

**Files:**
- Create: `src/Am4ever.App/Controls/SegmentedProgressBar.cs`
- Modify: `src/Am4ever.App/Theme/WinFormsColors.axaml`, `src/Am4ever.App/Theme/WinFormsTheme.axaml`
- Test: `tests/Am4ever.Tests/App/SegmentedProgressBarTests.cs`

**Interfaces:**
- Produces: `SegmentedProgressBar : TemplatedControl` with `double Value` (styled property, clamped to `[0,1]`, NaN/∞→0), `int SegmentCount` (styled property, default 20), and read-only `int FilledSegments => (int)Math.Floor(Clamp(Value) * SegmentCount)` clamped to `[0, SegmentCount]`. Renders a sunken trough holding `SegmentCount` blocks; the first `FilledSegments` use `am4.ProgressFill`, the rest are empty. Set `Value` from a `BuildProgress` via `(double)Done/Total` at the call site (Task 7).

- [ ] **Step 1: Write the failing test**

```csharp
public class SegmentedProgressBarTests
{
    [AvaloniaFact] public void FilledSegments_MapsAndClampsValue()
    {
        var bar = new SegmentedProgressBar { SegmentCount = 20 };
        bar.Value = 0.0;   Assert.Equal(0, bar.FilledSegments);
        bar.Value = 1.0;   Assert.Equal(20, bar.FilledSegments);
        bar.Value = 0.5;   Assert.Equal(10, bar.FilledSegments);
        bar.Value = -3;    Assert.Equal(0, bar.FilledSegments);
        bar.Value = 2;     Assert.Equal(20, bar.FilledSegments);
        bar.Value = double.NaN; Assert.Equal(0, bar.FilledSegments);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~SegmentedProgressBarTests`
Expected: FAIL — `SegmentedProgressBar` does not exist.

- [ ] **Step 3: Implement the control and its style**

Implement `SegmentedProgressBar` with the styled properties and `FilledSegments`. Give it a control template (in `WinFormsTheme.axaml`, selector `SegmentedProgressBar`) that is a sunken bevel (outer border `am4.ControlLightLight` on bottom/right, inner `am4.ControlDark`/`am4.ControlDarkDark` on top/left — the reverse of the raised Button idiom) wrapping an `ItemsControl`/`UniformGrid` of blocks. Add `am4.ProgressFill` to `WinFormsColors.axaml`: `#000080` (Light) and a lighter navy e.g. `#4A6CD4` (Dark). The simplest honest rendering: bind the blocks' brushes off `FilledSegments` (a value converter or a tiny code-built child panel rebuilt on property change). Keep rendering in the control if a pure-XAML template is awkward — the test only pins `FilledSegments`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~SegmentedProgressBarTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.App/Controls/SegmentedProgressBar.cs src/Am4ever.App/Theme/WinFormsColors.axaml src/Am4ever.App/Theme/WinFormsTheme.axaml tests/Am4ever.Tests/App/SegmentedProgressBarTests.cs
git commit -m "feat(app): Win98 segmented progress bar control"
```

---

### Task 3: App — toast service, model, and host

**Files:**
- Create: `src/Am4ever.App/Feedback/ToastKind.cs`, `src/Am4ever.App/Feedback/Toast.cs`, `src/Am4ever.App/Feedback/IToastService.cs`, `src/Am4ever.App/Feedback/ToastService.cs`, `src/Am4ever.App/Feedback/ToastHost.cs`
- Modify: `src/Am4ever.App/Theme/WinFormsTheme.axaml`
- Test: `tests/Am4ever.Tests/App/ToastServiceTests.cs`

**Interfaces:**
- Produces: `enum ToastKind { Success, Error, Info }`
- Produces: `sealed record Toast(string Message, ToastKind Kind) { public string Glyph => Kind switch { ToastKind.Success => "✓", ToastKind.Error => "✕", _ => "ℹ" }; }`
- Produces: `interface IToastService { void Show(string message, ToastKind kind); }`
- Produces: `sealed class ToastService : IToastService` with `ObservableCollection<Toast> Toasts { get; }`, `int MaxVisible { get; init; } = 4`, `void Show(string, ToastKind)` (adds; if over `MaxVisible`, removes `Toasts[0]`; schedules auto-dismiss via an injectable `Action<Toast, Action> _scheduleDismiss` defaulting to a `DispatcherTimer` of ~3.5 s), and `void Dismiss(Toast t)` (removes if present; a no-op otherwise).
- Produces: `ToastHost : TemplatedControl` bound to a `ToastService`, pinned bottom-right, stacking newest at the bottom, each toast a raised beveled box (glyph + message) that calls `Dismiss` on click. `IsHitTestVisible` false on the host container, true on each toast, so it never blocks the wizard beneath.

- [ ] **Step 1: Write the failing test**

```csharp
public class ToastServiceTests
{
    [Fact] public void Show_AddsToast_AndManualDismissRemoves()
    {
        var s = new ToastService { ScheduleDismiss = (_, _) => { } };   // no auto-dismiss in the test
        s.Show("built", ToastKind.Success);
        var t = Assert.Single(s.Toasts);
        Assert.Equal("✓", t.Glyph);
        s.Dismiss(t);
        Assert.Empty(s.Toasts);
    }

    [Fact] public void Dismiss_IsIdempotent_AfterAlreadyRemoved()
    {
        var s = new ToastService { ScheduleDismiss = (_, _) => { } };
        s.Show("x", ToastKind.Info);
        var t = s.Toasts[0];
        s.Dismiss(t); s.Dismiss(t);       // second is a safe no-op
        Assert.Empty(s.Toasts);
    }

    [Fact] public void Show_OverCap_DropsOldest()
    {
        var s = new ToastService { MaxVisible = 2, ScheduleDismiss = (_, _) => { } };
        s.Show("a", ToastKind.Info); s.Show("b", ToastKind.Info); s.Show("c", ToastKind.Info);
        Assert.Equal(new[] { "b", "c" }, s.Toasts.Select(t => t.Message));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ToastServiceTests`
Expected: FAIL — the `Feedback` types do not exist.

- [ ] **Step 3: Implement the model, service, host, and style**

Implement the four non-UI types and `ToastHost`. Make `ScheduleDismiss` a settable `Action<Toast, Action>` (default wraps a `DispatcherTimer` that fires once then calls the dismiss callback and stops); the test overrides it to a no-op so timing is deterministic. `Dismiss` guards with `Toasts.Contains(t)`. Add a `ToastHost` style + a raised-bevel toast item template to `WinFormsTheme.axaml` using `am4.*` brushes (a subtle left colour bar per kind is fine; no new colour resource required — reuse `am4.Highlight` for info, and plain text glyphs carry success/error).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ToastServiceTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.App/Feedback/ src/Am4ever.App/Theme/WinFormsTheme.axaml tests/Am4ever.Tests/App/ToastServiceTests.cs
git commit -m "feat(app): Win98 toast service and host"
```

---

### Task 4: App — WizardToasts message mapper (pure)

**Files:**
- Create: `src/Am4ever.App/Wizard/WizardToasts.cs`
- Test: `tests/Am4ever.Tests/Wizard/WizardToastsTests.cs`

**Interfaces:**
- Consumes: `ToastKind` (Task 3), `BoardProfile` (core), `BuildOutcome` (existing `WizardFlow.cs`).
- Produces: static `WizardToasts` with `(string Message, ToastKind Kind) ForDump(int byteLength)`, `ForProfile(BoardProfile p)`, `ForDonor()`, `ForIdentity(IdentityChoice c)`, `ForBuild(BuildOutcome o)`. Pure, no Avalonia — so it unit-tests without a headless app and without ROMs.

- [ ] **Step 1: Write the failing test**

```csharp
public class WizardToastsTests
{
    [Fact] public void ForBuild_SuccessAndFailure()
    {
        Assert.Equal(ToastKind.Success, WizardToasts.ForBuild(new BuildOutcome(true, null, "x.report.md")).Kind);
        var fail = WizardToasts.ForBuild(new BuildOutcome(false, "donor sha mismatch", null));
        Assert.Equal(ToastKind.Error, fail.Kind);
        Assert.Contains("donor sha mismatch", fail.Message);
    }

    [Fact] public void ForDump_StatesSizeInMiB()
    {
        var (msg, kind) = WizardToasts.ForDump(16 << 20);
        Assert.Equal(ToastKind.Success, kind);
        Assert.Contains("16 MiB", msg);
    }

    [Fact] public void ForProfile_NamesTheBoard()
    {
        var p = BundledData.LoadProfile("hp-285-g3-8434");
        Assert.Contains("285", WizardToasts.ForProfile(p).Message);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~WizardToastsTests`
Expected: FAIL — `WizardToasts` does not exist.

- [ ] **Step 3: Implement the mapper**

Write the five functions returning concise Win98-flavoured lines: dump → `"✓ Dump loaded — {len/1MiB} MiB, valid AM4"`; profile → `"✓ Profile selected: {Vendor} {Model}"`; donor → `"✓ Donor verified"`; identity → generic/DMI wording; build → `o.Ok ? ("✓ BIOS built!", Success) : ($"✕ Build failed: {o.Error}", Error)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~WizardToastsTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.App/Wizard/WizardToasts.cs tests/Am4ever.Tests/Wizard/WizardToastsTests.cs
git commit -m "feat(app): wizard toast message mapping"
```

---

### Task 5: App — WizardFlow.Build forwards progress

**Files:**
- Modify: `src/Am4ever.App/Wizard/WizardFlow.cs`
- Test: `tests/Am4ever.Tests/Wizard/WizardFlowTests.cs` (add one test)

**Interfaces:**
- Consumes: `BuildPipeline.Build(..., IProgress<BuildProgress>?)` (Task 1).
- Produces: `WizardFlow.Build(string outPath, IProgress<BuildProgress>? progress = null)` — forwards `progress` to `BuildPipeline.Build`; the no-arg overload behaviour is unchanged.

- [ ] **Step 1: Write the failing test**

```csharp
[RomFact] public void Build_ForwardsProgressToPipeline()
{
    var flow = new WizardFlow();
    flow.SetDump(File.ReadAllBytes(TestRoms.HpF65!));
    flow.SelectProfile(BundledData.LoadProfile("hp-285-g3-8434"));
    flow.SetDonor(File.ReadAllBytes(TestRoms.AsrockP1043!));
    var items = new List<BuildProgress>();
    var progress = new SyncProgress<BuildProgress>(items.Add);   // synchronous; see note
    var outPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    try
    {
        var outcome = flow.Build(outPath, progress);
        Assert.True(outcome.Ok, outcome.Error);
        Assert.Equal(items[^1].Total, items[^1].Done);
    }
    finally { File.Delete(outPath); File.Delete(outPath + ".report.md"); }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~WizardFlowTests.Build_ForwardsProgressToPipeline`
Expected: FAIL to compile — `Build` takes one argument.

- [ ] **Step 3: Implement the forward**

Add the optional parameter and pass it into `BuildPipeline.Build(...)`. No other change.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~WizardFlowTests`
Expected: PASS (progress test with ROMs / skip without); existing flow tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.App/Wizard/WizardFlow.cs tests/Am4ever.Tests/Wizard/WizardFlowTests.cs
git commit -m "feat(app): thread build progress through the wizard flow"
```

---

### Task 6: App — wire the bar and toasts into the view and window

**Files:**
- Modify: `src/Am4ever.App/Wizard/WizardView.axaml.cs`, `src/Am4ever.App/MainWindow.axaml`, `src/Am4ever.App/MainWindow.axaml.cs`
- Test: `tests/Am4ever.Tests/App/WizardViewTests.cs` (add tests)

**Interfaces:**
- Consumes: `SegmentedProgressBar` (Task 2), `IToastService`/`ToastService`/`ToastHost` (Task 3), `WizardToasts` (Task 4), `WizardFlow.Build(string, IProgress<BuildProgress>?)` (Task 5).
- Produces: `WizardView(WizardFlow flow, IToastService toasts)` — the new constructor parameter; keep a convenience `WizardView(WizardFlow)` that makes a throwaway `ToastService` so existing `[AvaloniaFact]` tests and any other callers still compile. Exposes `public SegmentedProgressBar BuildProgressBar { get; }`.

- [ ] **Step 1: Write the failing tests**

```csharp
[AvaloniaFact] public void BuildStep_HasAProgressBar()
{
    var view = new WizardView(new WizardFlow(), new ToastService { ScheduleDismiss = (_, _) => { } });
    Assert.NotNull(view.BuildProgressBar);
}

[AvaloniaFact] public void FailedBuildOutcome_RaisesErrorToast_AndBarNotFull()
{
    var toasts = new ToastService { ScheduleDismiss = (_, _) => { } };
    var view = new WizardView(new WizardFlow(), toasts);
    view.ReportBuildOutcome(new BuildOutcome(false, "no inputs", null));   // internal seam the view also calls after a real build
    var t = Assert.Single(toasts.Toasts);
    Assert.Equal("✕", t.Glyph);
    Assert.True(view.BuildProgressBar.Value < 1.0);
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~WizardViewTests`
Expected: FAIL — the two-arg constructor, `BuildProgressBar`, and `ReportBuildOutcome` do not exist.

- [ ] **Step 3: Implement the wiring**

In `WizardView`: add the `IToastService` field and the two-arg constructor (+ the one-arg convenience overload). Add a `SegmentedProgressBar BuildProgressBar` into the build step's panel (visible on `WizardStep.Build`). Extract `internal void ReportBuildOutcome(BuildOutcome o)` that sets the bar (full on success, left as-is on failure), swaps the sunken done/error panel, and raises `_toasts.Show(WizardToasts.ForBuild(o).Message, …Kind)`. In `OnAction` for the build step: construct the `Progress<BuildProgress>` **on the UI thread**, set `p => BuildProgressBar.Value = (double)p.Done/p.Total`, run `_flow.Build(path, progress)` on the background `Task.Run`, then call `ReportBuildOutcome`. For dump/donor: after a successful `SetDump`/`SetDonor` (and its `CanAdvance` validation), raise the matching `WizardToasts` toast; drive `BuildProgressBar`/a read bar off the stream copy if trivial, otherwise show the bar during the off-thread read. Raise `ForProfile`/`ForIdentity` toasts from the existing profile-selection and identity handlers. In `MainWindow`: wrap the `DockPanel` in a root `Panel`, create one `ToastService`, add a `ToastHost` bound to it as the top child (bottom-right), and pass the service into `new WizardView(flow, service)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~WizardViewTests`
Expected: PASS.

- [ ] **Step 5: Run the full App + Wizard suite**

Run: `dotnet test --filter "FullyQualifiedName~App|FullyQualifiedName~Wizard"`
Expected: PASS (ROM/T5 tests skip without fixtures); no regressions in existing wizard/view tests.

- [ ] **Step 6: Commit**

```bash
git add src/Am4ever.App/Wizard/WizardView.axaml.cs src/Am4ever.App/MainWindow.axaml src/Am4ever.App/MainWindow.axaml.cs tests/Am4ever.Tests/App/WizardViewTests.cs
git commit -m "feat(app): wire progress bar and toasts into the wizard"
```

---

### Task 7: Full suite and manual smoke

**Files:**
- Test: whole solution.

- [ ] **Step 1: Build portable for both RIDs**

Run: `dotnet build -c Release`
Expected: succeeds for the solution (win-x64 + linux-x64 publish settings unchanged).

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test`
Expected: all pass; ROM/T5/emulator tests skip when their fixtures/host are absent (same baseline as before this plan). Record the pass/skip counts in the ledger.

- [ ] **Step 3: Manual smoke (noted, not automated)**

Launch the app, run a build with the real ROMs, and confirm by eye: the segmented bar advances during build, a "✓ BIOS built!" toast appears with the done panel, and dump/donor loads toast. Record the observation in the ledger (a human may do this; do not block the branch on it).

- [ ] **Step 4: Commit any fixups**

```bash
git add -A && git commit -m "test: full-suite pass for wizard feedback"
```
