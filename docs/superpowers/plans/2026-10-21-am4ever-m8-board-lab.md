# am4ever Milestone 8: Board Lab (expert analysis)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A first expert "Board Lab" (design §8): open an OEM ROM and a donor, and see the analysis the HP work produced — ROM/AGESA summary for both, the OEM PEI module list, each CPM OEM PEIM's published CPM tables, a byte-compare of a module/region OEM-vs-donor, and a one-click emulator run on a chosen module+family. The analysis is a UI-agnostic core (unit-tested); a lean Avalonia view hosts it, reachable from a View-menu mode switch alongside the wizard.

**Scope boundary:** Analyse + a basic report + the emulator console. NOT in this milestone (noted as future): the hex editor, Capstone disassembly, GUI table editors, the EC-init composer, and profile export — each is its own large piece.

**Architecture:** `src/Am4ever.App/Lab/BoardAnalysis.cs` — UI-free: loads two images, reuses Core (`RomImage`, `AmdDirectory`, `PspZlib`, `FirmwareVolume`, `CpmImage`) + `PeiEmulator`, exposes summaries/module list/tables/compare/emulate. `src/Am4ever.App/Lab/BoardLabView.axaml.cs` — a code-only `UserControl` (same approach as the wizard) showing the analysis; `MainWindow` gains a View ▸ mode switch between Wizard and Board Lab.

**Tech Stack:** C#/.NET 8, Avalonia 11.2.3 (view), Core + `Am4ever.Emu`. xUnit 2.9.2; Avalonia.Headless for the view smoke test.

**Spec:** `G:\am4ever\docs\design.md` §8.1 Analyse (PSP/AGESA, CPM tables per family, GPIO/lane comparison, audio codec, SMBIOS), §8.2 Report, §8.3 Workbench (module browser, byte-compare, emulator console — the subset above), §3 (inferred items shown). Reuses M3 (`CpmImage`) and M7 (`PeiEmulator`).

**Branch:** `m8-board-lab`, cut from `m7-pei-harness`.

## Global Constraints
- Inherited: `net8.0`, nullable, warnings-as-errors, SPDX header on every `.cs`, no network, no tracked file over 262144 bytes, no firmware committed.
- `BoardAnalysis` has no Avalonia dependency (plain-xUnit testable). The view depends on it, never the reverse.
- Reuses, never duplicates, the Core parsers and the emulator wrapper. Read-only on the ROMs the user opens.

## Review Focus
1. A non-AM4 / truncated ROM opened → `BoardAnalysis.Load` surfaces a plain error, no crash. Test `Load_NonAm4_Throws` (Task 1).
2. A module name/index that isn't present → the tables/compare methods report "not found", not an exception escape. Test `Tables_UnknownModule_Reported` (Task 1).
3. Byte-compare of two different-length regions → a clear "sizes differ" result, no out-of-range read. Test `Compare_DifferentModules_ReportsDiff` (Task 2).
4. Emulate requested but the native host isn't built → the UI reports it's unavailable, not an unhandled throw. Test `Emulate_NoHost_Reported` (Task 2).

---

### Task 1: BoardAnalysis — summaries, modules, CPM tables
**Files:** Create `src/Am4ever.App/Lab/BoardAnalysis.cs`; Test `tests/Am4ever.Tests/Lab/BoardAnalysisTests.cs`
**Interfaces:** Consumes `RomImage`, `AmdDirectory`, `PspZlib`, `FirmwareVolume`, `CpmImage`.
Produces: `sealed record RomSummary(long Size, int AmdDirectories, string? Agesa)`; `sealed record OemModule(string Name, Guid Guid)`; `sealed class BoardAnalysis` — `static BoardAnalysis Load(byte[] oem, int peiFvOffset, int bootBlobOffset)` (throws `RomFormatException` on a non-AM4/truncated image); `RomSummary Summary()`; `IReadOnlyList<OemModule> Modules()` (the decompressed PEI volume's FFS files with a UI name); `IReadOnlyList<string> CpmTables(string moduleName, int index)` (the `$Axx`/`$CPM` tags via `CpmImage` on that PEIM's image section, or an empty list reported for an unknown module). The HP PEI FV is at 0xC00000; the ASRock boot blob at 0xE02000 — the caller passes the offsets (so it works for either ROM).
- [ ] **Step 1: failing tests** — `Load_NonAm4_Throws` (a 4 MiB array → `RomFormatException`); `Tables_UnknownModule_Reported` (a real ROM via `[RomFact]`: `CpmTables("NoSuchPeim", 0)` is empty); `Oem_ListsCpmPeims` (`[RomFact]`: HP ROM `Modules()` contains `AmdCpmOemInitPeim`, and `CpmTables("AmdCpmOemInitPeim", 0)` contains `$A10` and `$A26`).
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** both modes. **Step 5:** Commit `feat(app): Board Lab analysis — summaries, modules, CPM tables`.

### Task 2: Byte-compare and emulator run
**Files:** Modify `BoardAnalysis.cs`; Test `BoardAnalysisTests.cs`
**Interfaces:** Consumes `PeiEmulator`, `EcEmulator.HostPath`.
Produces: `sealed record Diff(bool SameLength, int FirstDiff, int DiffCount)`; `Diff CompareModule(BoardAnalysis other, string moduleName, int index)` (compare this PEIM's image-section bytes to the other ROM's same-named module; `SameLength=false`/`FirstDiff=-1` when sizes differ; `FirstDiff` = first differing offset or -1 if identical); `bool EmulatorAvailable => PeiEmulator.HostPath is not null`; `PeiResult? Emulate(string moduleName, int index, uint cpuid, int? overrideOffset = null)` (returns null when the host is unavailable, else runs the PEIM).
- [ ] **Step 1: failing tests** — `Compare_DifferentModules_ReportsDiff` (`[RomFact]`: comparing the OEM's AmdCpmOemInitPeim#0 against the donor's reports a diff with a valid FirstDiff or a sizes-differ result, no throw); `Emulate_NoHost_Reported` (when `EmulatorAvailable` is false, `Emulate(...)` returns null) and `Emulate_Runs` (`[EmuRomFact]`: a real module under Pinnacle returns a non-null `PeiResult` with tables).
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** both modes. **Step 5:** Commit `feat(app): Board Lab byte-compare and emulator run`.

### Task 3: Board Lab view + mode switch
**Files:** Create `src/Am4ever.App/Lab/BoardLabView.axaml.cs`; Modify `MainWindow.axaml(.cs)` (View ▸ mode switch Wizard/Board Lab); Test `tests/Am4ever.Tests/App/BoardLabViewTests.cs`
**Interfaces:** Consumes `BoardAnalysis`.
Produces: a code-only `UserControl` — buttons to open the OEM ROM and a donor (storage provider), a tree/list of modules, a panel showing the selected module's CPM tables + a byte-compare line vs the donor, and a "Run in emulator" button (disabled when `!EmulatorAvailable`) showing the `PeiResult`. `MainWindow` hosts either the wizard or Board Lab, switched from a new `View ▸ Board Lab` / `View ▸ Wizard` menu pair; the WinForms look is kept.
- [ ] **Step 1: failing test** — `BoardLabView_StartsEmpty_OpenOemEnabled` (`[AvaloniaFact]`: construct the view, assert the "Open OEM ROM" button exists/enabled and no module is shown yet).
- [ ] **Step 2:** FAIL. **Step 3:** Implement the view + the MainWindow mode switch. **Step 4:** whole suite `dotnet test` both modes; both green. **Step 5:** Commit `feat(app): Board Lab view and mode switch`. **Step 6:** Hand over: counts both modes; note Board Lab ships Analyse + report + emulator console, and the hex editor / disassembly / table editors / EC-init composer / profile export remain as future expert-mode work.
```
