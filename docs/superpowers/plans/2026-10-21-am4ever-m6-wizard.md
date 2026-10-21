# am4ever Milestone 6: Beginner Wizard

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The beginner's six-step wizard (design §6): dump → profile → donor → identity → build+checks → flash checklist, so someone with a profiled board and a programmer goes from dump to a verified flashable image without firmware knowledge. The logic is a UI-agnostic flow (plain-xUnit tested) that reuses the Core pipeline; a lean Avalonia view hosts it on the M1 shell.

**Architecture:** `src/Am4ever.App/Wizard/WizardFlow.cs` — a UI-free state machine (`WizardStep` enum, `Next`/`Back` with per-step gating, build inputs, invokes Core). It has no Avalonia dependency, so it is tested with plain xUnit. `src/Am4ever.App/Wizard/WizardView.axaml(.cs)` — a `UserControl` with one panel per step, Next/Back, bound to the flow; hosted in `MainWindow`. The risk gate (M1) still runs first.

**Tech Stack:** C#/.NET 8, Avalonia 11.2.3 (view only), the Core pipeline (`RomImage`, `AmdDirectory`, `BundledData`, `ProfileLoader`, `BuildPipeline`, `DmiIdentity`), `System.Security.Cryptography.SHA256`. xUnit 2.9.2; Avalonia.Headless.XUnit for the view smoke test.

**Spec:** `G:\am4ever\docs\design.md` §6 (wizard steps 1–6), §10 (beginners get no override on a failed check), §3 (inferred/untested shown plainly). Reuses M3 `BuildPipeline`.

**Branch:** `m6-wizard`, cut from `m5-emu-echost`. Earlier milestones stay un-merged.

## Global Constraints
- Inherited: `net8.0`, nullable, warnings-as-errors, SPDX header on every `.cs`, no network, no tracked file over 262144 bytes.
- `WizardFlow` has **no Avalonia using** — pure logic, plain-xUnit testable. The view depends on the flow, never the reverse.
- **No "build anyway" for beginners** (§10): when a step's validation fails, `Next` is refused; the build step surfaces any failure and does not write an image.
- Inputs are never modified; the output is a new file the user names.

## Review Focus
1. Two dumps that differ byte-for-byte → step 1 refuses to advance, naming the mismatch. Test `Step1_TwoDumpsDiffer_Blocks` (Task 2).
2. A non-AM4 / blank / wrong-size dump → step 1 rejects it with a plain reason, no crash. Test `Step1_RejectsNonAm4AndBlank` (Task 2).
3. A donor whose SHA-256 does not match the profile's adapter → step 3 blocks with the expected-vs-actual. Test `Step3_WrongDonorHash_Blocks` (Task 3).
4. `Next` pressed on a step whose inputs are incomplete/invalid → always refused; `Back` never loses earlier entries. Tests `Next_GatedByValidation`, `Back_PreservesState` (Task 1).
5. A build/verify failure at step 5 → no image written, failure shown, no override offered. Test `Step5_BuildFailure_NoImageNoOverride` (Task 4).

---

### Task 1: Wizard step state machine

**Files:** Create `src/Am4ever.App/Wizard/WizardFlow.cs`; Test `tests/Am4ever.Tests/Wizard/WizardFlowTests.cs`

**Interfaces:**
- Produces: `public enum WizardStep { Dump, Profile, Donor, Identity, Build, Flash }`; `public sealed class WizardFlow` — `WizardStep Step { get; }`; `bool CanAdvance(out string? reason)` (per-step validation); `bool Next()` (advances only if `CanAdvance`; returns moved); `bool Back()` (never below Dump; preserves all state); and the input properties filled by later tasks. Steps 1–6 map to the enum in order.

- [ ] **Step 1: failing tests** — `Next_GatedByValidation` (on a fresh flow, `Next()` returns false and `Step` stays `Dump` because no dump is set); `Back_PreservesState` (set a dump, advance where possible, `Back()` to Dump, the dump path is still set); `Steps_InOrder` (the enum order is Dump,Profile,Donor,Identity,Build,Flash).
- [ ] **Step 2:** `dotnet test --filter WizardFlowTests` → FAIL.
- [ ] **Step 3:** Implement the enum + navigation; `CanAdvance` delegates to per-step checks added in later tasks (Dump returns false until Task 2 fills it — for now gate on "a dump path is set").
- [ ] **Step 4:** Run → PASS. **Step 5:** Commit `feat(app): wizard step state machine`.

### Task 2: Step 1 — your BIOS (dump validation)

**Files:** Modify `WizardFlow.cs`; Test `WizardFlowTests.cs`

**Interfaces:**
- Consumes: `RomImage.Load`, `AmdDirectory.FindAll`.
- Produces on `WizardFlow`: `void SetDump(byte[] primary, byte[]? secondRead = null)`; the Dump-step `CanAdvance` passes only when: size is 8/16/32 MiB and `RomImage.Load` succeeds; at least one AMD directory is found (`AmdDirectory.FindAll` non-empty — an AMI Aptio AM4 image); the image is not blank (not all `0x00`/`0xFF`); and, when a second read is given, it equals the primary byte-for-byte. The failing reason is specific.

- [ ] **Step 1: failing tests** — `Step1_TwoDumpsDiffer_Blocks` (two 16 MiB arrays differing in one byte → `CanAdvance` false, reason mentions "match"); `Step1_RejectsNonAm4AndBlank` (a 4 MiB array and an all-0xFF 16 MiB array each → false with a size / blank reason); `Step1_ValidDump_Advances` (a `[RomFact]`-style: the real HP F.65 dump → `CanAdvance` true). Use the existing `TestRoms`/`[RomFact]` for the real-dump case.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement `SetDump` + the Dump `CanAdvance`. **Step 4:** Run with/without `AM4EVER_TEST_ROMS`. **Step 5:** Commit `feat(app): wizard step 1 dump validation`.

### Task 3: Steps 2–3 — profile match and donor verify

**Files:** Modify `WizardFlow.cs`; Test `WizardFlowTests.cs`

**Interfaces:**
- Consumes: `BundledData.LoadProfile`, bundled profile list, `ProfileLoader`, `BundledData.LoadAdapter`, `SHA256`.
- Produces: `IReadOnlyList<BoardProfile> MatchingProfiles()` (bundled profiles whose `Board.Match.RomSize` equals the dump size; first release has the HP profile); `void SelectProfile(BoardProfile)`; `void SetDonor(byte[] donor)`; the Profile-step `CanAdvance` passes when a profile is selected; the Donor-step `CanAdvance` passes when the donor's SHA-256 matches the selected profile's adapter (`DonorAdapter.Identity.Sha256`), else a reason with the first bytes of expected vs actual.

- [ ] **Step 1: failing tests** — `Step2_OffersBundledProfileForSize` (a 16 MiB dump → `MatchingProfiles()` contains `hp-285-g3-8434`); `Step3_WrongDonorHash_Blocks` (select the HP profile, `SetDonor(zeros)` → Donor `CanAdvance` false, reason mentions sha256); `Step3_CorrectDonorHash_Advances` (`[RomFact]`: the real ASRock donor → true).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run both modes. **Step 5:** Commit `feat(app): wizard steps 2-3 profile match and donor verify`.

### Task 4: Steps 4–6 — identity, build+checks, flash checklist

**Files:** Modify `WizardFlow.cs`; Create `src/Am4ever.App/Wizard/FlashChecklist.cs`; Test `WizardFlowTests.cs`

**Interfaces:**
- Consumes: `DmiIdentity`, `BuildPipeline.Build`, `BuildReport`.
- Produces: `enum IdentityChoice { Generic, FromDmiFile }`; `void SetIdentity(IdentityChoice, byte[]? dmiJson = null)` (FromDmiFile parses via `DmiIdentity.FromJson`, else generic HP default); `BuildOutcome Build(string outPath)` where `BuildOutcome(bool Ok, string? Error, string? ReportPath)` — runs `BuildPipeline.Build(donor, oem, profile, adapter, dmi)`, writes the image + `<out>.report.md` only on success, returns `Ok=false` with the error and writes nothing on failure (no override); the Build-step `CanAdvance` is false until a successful `Build`. `FlashChecklist.Text` returns the §6.6 checklist (CH341A 3.3 V, back up original, erase/write/verify, clear CMOS, 1–2 min first boot, recovery) as plain text.

- [ ] **Step 1: failing tests** — `Step4_GenericIdentity_IsDefault`; `Step5_BuildFailure_NoImageNoOverride` (a bad donor → `Build` returns `Ok=false`, no file at `outPath`, Build-step `CanAdvance` stays false); `FlashChecklist_MentionsVoltageAndBackup` (text contains "3.3" and "back up"). A `[RomFact]` `Step5_RealBuild_WritesImageAndReport` builds from the real ROMs into a temp path and asserts both files exist and the image is 16 MiB.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run both modes. **Step 5:** Commit `feat(app): wizard steps 4-6 identity, build, flash checklist`.

### Task 5: Avalonia wizard view

**Files:** Create `src/Am4ever.App/Wizard/WizardView.axaml`, `WizardView.axaml.cs`; Modify `MainWindow.axaml(.cs)` to host it; Test `tests/Am4ever.Tests/App/WizardViewTests.cs`

**Interfaces:**
- Consumes: `WizardFlow` (Tasks 1–4), the WinForms theme (M1).
- Produces: a `UserControl` with a panel per `WizardStep` (file pickers for dumps/donor/dmi, a profile list showing author/tested/⚠-inferred per §6.2, Back/Next, a build-progress/result area, the flash checklist), showing one panel at a time driven by `WizardFlow.Step`; Next/Back call the flow and refuse with the `CanAdvance` reason. `MainWindow` hosts `WizardView` as its content.

- [ ] **Step 1: failing test** — `WizardView_StartsOnDumpStep_NextDisabledUntilValid` (Avalonia.Headless: construct the view, assert the Dump panel is visible and Next is disabled with no dump). Follow the existing `App/` headless test pattern (`TestAppBuilder`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement the view + host it in `MainWindow`; keep the WinForms look. **Step 4:** Run the whole suite `dotnet test` with and without `AM4EVER_TEST_ROMS`; both green. **Step 5:** Commit `feat(app): beginner wizard view`. **Step 6:** Hand over: report counts both modes; note the wizard is functional on the shell and the Board-Lab expert UI, the emulator PEI harness, and the docs are the remaining first-release milestones.
```
