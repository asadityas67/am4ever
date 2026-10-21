# am4ever Milestone 7: PEI Emulator Harness

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run each AMD CPM OEM PEIM (32-bit TE) from a built image under every supported CPU family in the emulator, and report what it publishes — the CPM table list and its tags, GPIO pin counts, whether the neutralised override routines are no-ops, and whether it writes outside its image/stack/heap. This completes the emulator (M5 did the DXE/EC half) and lets the build verify the PEI edits by running them, not just by structure. The C# port of the `Pei` class and `run_pei` in `G:\HPBIOS\tools\emulate_modules.py`.

**Architecture:** Extend the M5 native host with a `pei` subcommand (the whole 32-bit PEI harness runs in C inside `uc_host`, because the hooks — CPUID per family, PEI-service stubs, PPI stubs, IN/OUT, memory — fire during emulation). `Am4ever.Emu` gains a `PeiEmulator` wrapper that invokes it and parses the result. Reuses all the M5 plumbing (native host, build scripts, `[EmuFact]` gating, binaries not committed).

**Tech Stack:** C (uc_host, Unicorn 2.1.x), .NET 8 (`Am4ever.Emu`), xUnit 2.9.2. No new dependencies.

**Spec:** `G:\am4ever\docs\design.md` §5.4 (PEI: run each CPM OEM PEIM under every CPU family, fake PEI services, PPIs, notify dispatch, walk the CPM table list, decode GPIO/DXIO; flag writes outside image/stack/heap). Byte/behaviour reference: the `Pei` class, `walk_cpm_tables`, `decode_gpio`, `decode_dxio`, `find_table_list`, `dispatch`, and `run_pei` in `G:\HPBIOS\tools\emulate_modules.py` (~lines 62–420), and the eight CPU families in `CPUS`. The expected output is `G:\HPBIOS\build\T5_emulation.txt` (the PEI section).

**Branch:** `m7-pei-harness`, cut from `m6-wizard`. Earlier milestones stay un-merged.

## Global Constraints
- Inherited (.NET): `net8.0`, nullable, warnings-as-errors, SPDX header on every `.cs`, no network, no tracked file over 262144 bytes, no firmware committed.
- `uc_host.c` stays GPL-2.0-or-later; the host is a plain non-CET exe (M5). Native binaries never committed.
- The eight families (name → CPUID Fn0000_0001/8000_0001 EAX) are exactly `CPUS` in `emulate_modules.py`: Summit 0x00800F11, Pinnacle 0x00800F82, Raven 0x00810F10, Picasso 0x00810F81, Matisse 0x00870F10, Renoir 0x00860F01, Vermeer 0x00A20F10, Cezanne 0x00A50F00. CPUID leaf 1/0x80000001 returns the family EAX; 0x80000001 EBX PkgType = 0x20000000 (AM4); leaf 0 = AuthenticAMD.
- A module that returns `0x80000003` (EFI_UNSUPPORTED) for a family is "not this module's family" — not a failure.
- PEI env addresses (from the reference): services table 0x00F10000, PeiServices** at 0x00F11000, IDT 0x00F12000 (pointer at IDT-4), STUB 0x00F00000, heap 0x01000000, stack top 0x00E00000, RET_MAGIC 0x00F0FFF0 (HLT), PPI stubs 0x00F08000, PPI objects 0x00F13000; PCD PPI GUID 06e81c58-4ad7-44bc-8390-f10265f72480.

## Review Focus
1. A module run under a family it does not serve returns EFI_UNSUPPORTED → reported "not handled", not a crash/failure. Test `Pei_UnsupportedFamily_NotAFailure` (Task 2).
2. A module that writes outside image/stack/heap → the write is flagged and the run is not "clean". Test `Pei_FlagsStrayWrites` (Task 1, via a crafted check or the real modules showing none). 
3. The CPM table list cannot be found (a module that never installs it) → reported "no CPM table list", not a hang/crash. Test `Pei_NoTableList_Reported` (Task 2).
4. An override offset that is NOT neutralised (plain image) → the override check reports it is not a no-op. Test `Pei_OverrideCheck_DetectsLiveRoutine` (Task 2).
5. The instruction limit is hit (runaway module) → reported "did not return", bounded. Covered by the host's `count` limit; asserted indirectly by the real-module runs returning cleanly.

---

### Task 1: uc_host `pei` subcommand — the 32-bit PEI harness
**Files:** Modify `native/uc_host/uc_host.c`, `native/README.md`
**Interfaces:** `uc_host pei --te <file> --cpuid <hex> [--override <hex>]` — load the TE image, build the fake PEI env, call the entry `(FileHandle=0x1000, PeiServices**)`, run the notify callbacks, find the CPM table list among the installed PPIs, and print: `entry=0x<hex>`, `ppis=<n>`, `notifies=<n>`, `tables=<sig>[/<pmask>]…`, `gpio[<pmask>]=<pins>`, `a26[<pmask>]=<lane summary>`, `writes_outside=<n>`, and (when `--override` given) `override[0x<off>]=ok|live` (ok = returns 0 with no LocatePpi and no stray write). Ports `Pei`, `walk_cpm_tables`, `find_table_list`, `decode_gpio`, `decode_dxio`, `dispatch`, and the override check of `run_pei`.
- [ ] **Step 1:** Port the harness into `uc_host.c` (new `pei` command; reuse the `Sim`-style user struct for installed/notified/located lists, the PCD PPI, and the hooks). TE header parse: `VZ` magic, machine 0x14C, base = imagebase+stripped-40, entry = imagebase+entry.
- [ ] **Step 2: Build + run on the real ASRock OEM PEIMs.** Rebuild the host (`native/build`), then for `AmdCpmOemInitPeim`#0 extracted from the donor PEI volume, run `pei --te <file> --cpuid 0x00800F82` (Pinnacle) and `--override 0x2D6`.
  Expected: `tables=` lists `$CPM` and several `$Axx` incl. `$A10`/`$A26`; `writes_outside=0`; `override[0x2D6]=ok`. Cross-check the tags/GPIO against `T5_emulation.txt`. (Scripted verification — automated gate is Task 3.)
- [ ] **Step 3: Commit** `feat(emu): uc_host PEI harness`.

### Task 2: PeiEmulator .NET wrapper
**Files:** Create `src/Am4ever.Emu/PeiEmulator.cs`; Test `tests/Am4ever.Tests/Emu/PeiEmulatorTests.cs`
**Interfaces:** Consumes the `pei` host command (Task 1), `EcEmulator.HostPath` (M5, reuse the locator). Produces `public sealed record PeiResult(int Entry, int PpiCount, int NotifyCount, IReadOnlyList<string> Tables, int WritesOutside, bool? OverrideOk)`; `public static class PeiEmulator` — `static PeiResult Run(byte[] teImage, uint cpuid, int? overrideOffset = null)` (writes the TE to a temp file, runs `uc_host pei`, parses; throws `EmulatorException` like `EcEmulator`). Refactor the shared host-locate/invoke helper out of `EcEmulator` into an internal `UcHost` type both use.
- [ ] **Step 1: failing tests** — `Pei_UnsupportedFamily_NotAFailure`, `Pei_NoTableList_Reported`, `Pei_OverrideCheck_DetectsLiveRoutine`: these are `[EmuFact]` and build on the real donor modules extracted via the M5 `ReferenceImages`-style helper (a module under a wrong family returns EFI_UNSUPPORTED → `PeiResult` with empty `Tables`, no throw; a plain (un-neutralised) override offset → `OverrideOk == false`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement `UcHost` + `PeiEmulator`. **Step 4:** Run with/without the host. **Step 5:** Commit `feat(emu): PeiEmulator wrapper`.

### Task 3: PEI verification over the built image
**Files:** Create `tests/Am4ever.Tests/Emu/PeiVerificationTests.cs`
**Interfaces:** Consumes `BuildPipeline` (build the real HP image), `PeiEmulator`, the eight families.
- [ ] **Step 1: test** — `[EmuFact]`+`[RomFact]` `BuiltImage_OemPeimsPublishTablesUnderServedFamilies`: build the HP image from the real ROMs, extract each CPM OEM PEIM, run it under each family it serves, and assert: it publishes a CPM table list including `$A10` and `$A26`; `WritesOutside == 0`; and each neutralised override offset reports `OverrideOk == true`. Values cross-checked against `T5_emulation.txt`.
- [ ] **Step 2:** Run → with the host built this passes; without it skips. **Step 3:** Run the whole suite both modes; both green. **Step 4:** Commit `test(emu): PEI harness verification over the built image`. **Step 5:** Hand over: counts both modes; note the emulator is now complete (PEI + DXE), and Board Lab + docs remain.
```
