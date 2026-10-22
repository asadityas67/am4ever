# Generic engine (remove HP/ASRock hardcodes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the three HP/ASRock hardcodes (Board Lab offsets, verb-table SSID check, identity extractor gate) so a new OEM board works without code changes — while keeping the HP 285 G3 reference build byte-for-byte identical.

**Architecture:** Three small, output-neutral changes: an optional SSID check in `VerbTable`, a profile-driven identity resolver used by `BuildPipeline`, and auto-detected OEM/adapter-driven load offsets in `BoardAnalysis`/Board Lab, plus de-gating the composer's `smbios_identity`.

**Tech Stack:** C#/.NET 8, Avalonia, xUnit.

**Spec:** docs/superpowers/specs/2026-10-22-generic-engine-design.md

## Global Constraints
- GPL-2.0-or-later header on new files.
- **The HP 285 G3 reference build stays byte-identical** — `Hp285G3ReferenceTests` must keep passing. Any change that alters the T5 bytes is a failure.
- No new donor-adapter authoring (item 4, later). No profile-schema required-field changes; `identity.extractor` stays an optional hint.
- UI-free cores are plain xUnit; full-build/ROM tests are `[RomFact]`/`[T5Fact]`.

## Review Focus
- **HP output drift:** the single biggest risk — any of these changes silently altering the HP image. Task 2 and Task 5 both run `Hp285G3ReferenceTests`; it must stay green (with ROMs).
- **FV scan picks the wrong volume:** `LoadOem` must select the PEI FV that holds the CPM OEM PEIMs, not another FV — Task 3 asserts the discovered module list matches the known-offset load.
- **Non-HP identity correctness:** a board with no known extractor must get its vendor/model/board-id into the System/Board/Chassis fields — Task 2 asserts the derived identity's fields.
- **Verb extract on a non-HP subsystem:** the SSID check must not run when not requested — Task 1 tests the check in isolation (pass + throw) and that the build path passes `null`.
- **Composer de-gate:** `smbios_identity` must no longer be gated off for a fresh board — Task 4 asserts `IdentitySupported` is true without an extractor.

---

### Task 1: Core — optional verb-table subsystem-ID check

**Files:**
- Modify: `src/Am4ever.Core/Board/VerbTable.cs`, `src/Am4ever.Core/Build/BuildPipeline.cs`
- Test: `tests/Am4ever.Tests/Board/VerbTableTests.cs` (add one test)

**Interfaces:**
- Produces: `VerbTable.Extract(byte[] rom, int peiFvOffset, uint codecId, uint romBase, uint[]? expectedSsid = null)` — runs the subsystem-ID validation only when `expectedSsid` is non-null; otherwise returns the verbs unchecked. The returned verbs are identical either way.
- Produces: `VerbTable.ExtractHp(...)` stays as a wrapper passing the HP SSID `{0x34,0x84,0x3C,0x10}` (callers/tests unchanged).
- Produces (testable seam): `public static void CheckSubsystemId(IReadOnlyList<uint> verbs, uint[] expected)` — throws `RomFormatException` on mismatch (public to avoid depending on `InternalsVisibleTo`).
- Consumes: `BuildPipeline` calls `Extract(..., expectedSsid: null)`.

- [ ] **Step 1: Write the failing test**

```csharp
// one SSID verb word = (NID 0x01 << 20) | (reg << 8) | value
static uint Ssid(int reg, byte val) => (0x01u << 20) | ((uint)reg << 8) | val;

[Fact] public void CheckSubsystemId_PassesHp_ThrowsOnMismatch()
{
    var hp = new[] { Ssid(0x720, 0x34), Ssid(0x721, 0x84), Ssid(0x722, 0x3C), Ssid(0x723, 0x10) };
    VerbTable.CheckSubsystemId(hp, new uint[] { 0x34, 0x84, 0x3C, 0x10 });   // no throw
    var wrong = new[] { Ssid(0x720, 0x99), Ssid(0x721, 0x84), Ssid(0x722, 0x3C), Ssid(0x723, 0x10) };
    Assert.Throws<RomFormatException>(() => VerbTable.CheckSubsystemId(wrong, new uint[] { 0x34, 0x84, 0x3C, 0x10 }));
}
```

- [ ] **Step 2: Run it to verify it fails** — Run: `dotnet test --filter FullyQualifiedName~VerbTableTests` · Expected: FAIL (`CheckSubsystemId` missing / not accessible).
- [ ] **Step 3: Implement** — extract the SSID loop into `public static CheckSubsystemId`; add `Extract(..., expectedSsid = null)` that calls it only when non-null; make `ExtractHp` delegate to `Extract` with the HP SSID; change `BuildPipeline`'s `VerbTable.ExtractHp(...)` call to `VerbTable.Extract(..., expectedSsid: null)`.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter FullyQualifiedName~VerbTableTests` · Expected: PASS (incl. the existing `[RomFact]` ExtractHp test).
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.Core/Board/VerbTable.cs src/Am4ever.Core/Build/BuildPipeline.cs tests/Am4ever.Tests/Board/VerbTableTests.cs
git commit -m "feat(core): make the verb-table subsystem-ID check optional"
```

---

### Task 2: Core — profile-driven identity resolver

**Files:**
- Modify: `src/Am4ever.Core/Board/Smbios/DmiIdentity.cs`
- Create: `src/Am4ever.Core/Board/Smbios/IdentityResolver.cs`
- Modify: `src/Am4ever.Core/Build/BuildPipeline.cs` (ApplyDxe base identity)
- Test: `tests/Am4ever.Tests/Board/IdentityResolverTests.cs`

**Interfaces:**
- Produces: `DmiIdentity.FromBoard(string vendor, string model, string boardId)` — `SystemManufacturer`/`BoardManufacturer`/`ChassisManufacturer` = vendor, `SystemProduct` = model, `BoardProduct` = boardId (the generic analogue of `HpDefault`).
- Produces: `IdentityResolver.BaseFor(BoardProfile p)` — a known built-in extractor name (`"hp-8434"`) → `DmiIdentity.HpDefault`; otherwise `DmiIdentity.FromBoard(p.Board.Vendor, p.Board.Model, p.Board.BoardId)`.
- Consumes: `BuildPipeline.ApplyDxe` uses `IdentityResolver.BaseFor(profile).Merge(dmiOverride ?? …)` instead of `DmiIdentity.HpDefault`.

- [ ] **Step 1: Write the failing test**

```csharp
public class IdentityResolverTests
{
    [Fact] public void BaseFor_HpProfile_IsTheHpIdentity()
    {
        var id = IdentityResolver.BaseFor(BundledData.LoadProfile("hp-285-g3-8434"));
        Assert.Equal("HP", id.SystemManufacturer);
        Assert.Equal(DmiIdentity.HpDefault.SystemProduct, id.SystemProduct);   // exact HP strings preserved
    }

    [Fact] public void BaseFor_UnknownBoard_DerivesFromProfileMetadata()
    {
        var draft = new Am4ever.Core.Profile.ProfileDraft { Vendor = "Lenovo", Model = "M715p", BoardId = "3134", IdentityExtractor = null };
        var id = IdentityResolver.BaseFor(draft.ToProfile());
        Assert.Equal("Lenovo", id.SystemManufacturer);
        Assert.Equal("M715p", id.SystemProduct);
        Assert.Equal("3134", id.BoardProduct);
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (`IdentityResolver`/`FromBoard` missing).
- [ ] **Step 3: Implement** `FromBoard`, `IdentityResolver.BaseFor`, and switch `ApplyDxe`'s base identity. Keep the merge-with-`--dmi` behaviour (today `HpDefault.Merge(dmiOverride)`); now `BaseFor(profile).Merge(dmiOverride)` when an override is given, else `BaseFor(profile)`.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter FullyQualifiedName~IdentityResolverTests` · Expected: PASS.
- [ ] **Step 5: Prove the HP output is unchanged** — Run: `dotnet test --filter FullyQualifiedName~Hp285G3ReferenceTests` · Expected: PASS (ROMs) / SKIP — the resolver returns `HpDefault` for the HP profile, so the DXE bytes are identical.
- [ ] **Step 6: Commit**
```bash
git add src/Am4ever.Core/Board/Smbios/DmiIdentity.cs src/Am4ever.Core/Board/Smbios/IdentityResolver.cs src/Am4ever.Core/Build/BuildPipeline.cs tests/Am4ever.Tests/Board/IdentityResolverTests.cs
git commit -m "feat(core): resolve SMBIOS identity from the profile, HP exact by extractor"
```

---

### Task 3: App — auto-detect OEM PEI volume; adapter-driven donor load

**Files:**
- Modify: `src/Am4ever.App/Lab/BoardAnalysis.cs`, `src/Am4ever.App/Lab/BoardLabView.axaml.cs`
- Test: `tests/Am4ever.Tests/Lab/BoardAnalysisTests.cs` (add one test)

**Interfaces:**
- Produces: `BoardAnalysis.LoadOem(byte[] rom)` — scans for the FV signature (`0x4856465F` at FV-header `+0x28`) and loads the FV whose files include an `AmdCpmOem*` PEIM; throws `RomFormatException` if none.
- Produces: `BoardAnalysis.LoadDonor(byte[] rom, DonorAdapter adapter)` — uses `adapter.Volumes` (the compressed boot blob or uncompressed FV).
- Consumes: `BoardLabView.Open` uses `LoadOem` for the OEM and `LoadDonor` (with the composer's chosen adapter) for the donor; no more `0xC00000`/`0xE02000` constants.

- [ ] **Step 1: Write the failing test**

```csharp
[RomFact] public void LoadOem_FindsThePeiVolume_WithoutHardcodedOffset()
{
    var oem = BoardAnalysis.LoadOem(File.ReadAllBytes(TestRoms.HpF65!));
    Assert.Contains(oem.Modules(), m => m.Name.StartsWith("AmdCpmOem"));
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (`LoadOem` missing) / project builds.
- [ ] **Step 3: Implement** the FV scan + `LoadOem`/`LoadDonor`; rework `BoardLabView.Open` to call them (donor load requires an adapter to be chosen — disable/guard otherwise). Keep the existing `Load(rom, pei, boot)` for internal reuse.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter "FullyQualifiedName~BoardAnalysisTests|FullyQualifiedName~BoardLabViewTests"` · Expected: PASS (ROM test skips without ROMs; the headless view tests still pass).
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/BoardAnalysis.cs src/Am4ever.App/Lab/BoardLabView.axaml.cs tests/Am4ever.Tests/Lab/BoardAnalysisTests.cs
git commit -m "feat(app): auto-detect the OEM PEI volume; donor load from the adapter"
```

---

### Task 4: App — de-gate smbios_identity in the composer

**Files:**
- Modify: `src/Am4ever.App/Lab/ComposerCore.cs`, `src/Am4ever.App/Lab/ProfileDiscovery.cs`
- Test: `tests/Am4ever.Tests/Lab/ComposerCoreTests.cs` (add one test)

**Interfaces:**
- Produces: `ComposerCore.IdentitySupported` is always `true` (identity now resolves generically from the profile), so nothing gates `smbios_identity`.
- Consumes: `ProfileDiscovery`'s `smbios_identity` suggestion note drops the "needs a Core identity extractor" wording (now: identity comes from the board metadata or `--dmi`).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] public void IdentitySupported_IsAlwaysTrue_ForAFreshBoard()
{
    Assert.True(new ComposerCore().IdentitySupported);
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (today it is false without an extractor).
- [ ] **Step 3: Implement** — `IdentitySupported => true`; drop the now-stale `KnownExtractors` gate (or keep the set unused/removed); update the discovery note string.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter FullyQualifiedName~ComposerCoreTests` · Expected: PASS.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/ComposerCore.cs src/Am4ever.App/Lab/ProfileDiscovery.cs tests/Am4ever.Tests/Lab/ComposerCoreTests.cs
git commit -m "feat(app): smbios_identity works for any board (generic identity)"
```

---

### Task 5: Docs + full suite

**Files:**
- Modify: `docs/manual/board-lab.md` (the SMBIOS-identity note), `docs/manual/reference.md` if it states the HP-only identity limit.
- Test: whole solution.

- [ ] **Step 1: Update the manual** — the composer's identity note: `smbios_identity` now works for any board (identity from the profile's board metadata, or `--dmi`); a known extractor only pins exact strings for boards that ship one (HP).
- [ ] **Step 2: Release build** — `dotnet build -c Release` · Expected: succeeds.
- [ ] **Step 3: Full suite** — `dotnet test` · Expected: all pass; ROM/T5/emulator tests skip without fixtures. Record counts in the ledger.
- [ ] **Step 4: Commit**
```bash
git add docs/manual/board-lab.md docs/manual/reference.md
git commit -m "docs: generic identity in the Board Lab manual"
```
