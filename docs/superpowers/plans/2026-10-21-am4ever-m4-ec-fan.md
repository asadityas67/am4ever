# am4ever Milestone 4: EC Fan Hand-off Driver

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the HP 285 G3 modded image by adding "HpEcFanInit", the small x64 DXE driver that performs HP's EC fan hand-off (so the fans run their own curve instead of full speed). After this, an `am4ever build` reproduces the T5 reference image's DXE volume in full — the C# port of `G:\HPBIOS\tools\hp_ecfan.py` plus the `add_ec_fan_driver` step of `build_hp285g3.py`.

**Architecture:** The driver's machine code is a fixed, already-assembled 199-byte blob (the output of the Keystone assembler on the position-independent sequence in `hp_ecfan.py`, kept verbatim as a documented constant). C# builds the PE32+ image and the FFS file around it with pure byte construction — no assembler at runtime. A new `add_ec_init` operation inserts that FFS into the DXE firmware volume's trailing free space, in the same decompress→edit→recompress pass as the SMBIOS edit.

**Scope note:** A *general* EC-init composer (assembling arbitrary EC sequences with Keystone) and the Unicorn emulator that verifies the driver are the **next** milestone. This milestone ships only the HP 285 G3 driver, as a fixed template, and regenerating the blob from source needs Keystone (not bundled yet).

**Tech Stack:**
- C# on .NET 8, building on M2 (`FirmwareVolume`, `FfsFile`, `FvEditor`, `GuidedSection`) and M3 (`BuildPipeline`, the operation model).
- No new dependencies.
- xUnit 2.9.2.

**Spec:** `G:\am4ever\docs\design.md` §5.2 (`add_ec_init`), §5.4 (DXE EC sim — the *verification* is M5), §7 (profiles carry generated code, not firmware), §11 (reference test). The byte-level reference is `G:\HPBIOS\tools\hp_ecfan.py` (PE + FFS construction) and `add_ec_fan_driver`/`patch_dxe` in `G:\HPBIOS\tools\build_hp285g3.py` (insertion + single recompression).

**Branch:** `m4-ec-fan`, cut from `m3-board-model`. M1–M3 stay un-merged.

## Global Constraints

- **Inherited:** `net8.0`, nullable, warnings-as-errors, SPDX header on every `.cs` file, no network, no tracked file over 262144 bytes, the five firmware signature strings never appear as ASCII in source (the driver blob is stored as a hex *string*, which cannot contain them).
- **The EC-fan driver is am4ever's own generated code, not firmware** (§7 permits generated EC-init code). It is built in `Am4ever.Core`, never read from a profile, and never contains OEM/AMD/vendor bytes.
- **Editors never move data they do not own, never truncate.** Inserting the driver only writes into trailing free space that is already all `0xFF`; if it does not fit, nothing is written and `RomEditException` is thrown.
- **The driver is inserted exactly once.** A second `add_ec_init` on an image that already has the driver (same file GUID) is refused.
- **DXE is edited in one pass.** SMBIOS identity then EC-fan insertion happen on the same decompressed DXE blob, followed by a single recompression (matches `patch_dxe`).

### Reference constants (from `hp_ecfan.py`, verified against T5)

| Thing | Value |
|---|---|
| Driver file GUID | `6f9a1c2e-5b7d-4e41-9c3a-8534a0e7f285` |
| Driver UI name | `HpEcFanInit` |
| Assembled code | 199 bytes, hex `53b844a3008066baf80cef66bafc0ced0d00000040efb17eb3e7e856000000b180e87b000000720b24fd88c3b180e84200000031c05bc341b800000200e466a802740741ffc875f5f9c3f8c341b800000200e466a801750741ffc875f5f9c3f8c341b800010000e466a8017407e46241ffc875f3c3e8e7ffffffe8b8ffffff721fb081e666e8adffffff721488c8e662e8a2ffffff720988d8e662e897ffffffc3e8bbffffffe88cffffff7219b080e666e881ffffff720e88c8e662e88bffffff7203e462f8c3` |
| FFS sections | DXE-depex (type `0x13`, body `06 08` = TRUE END), PE32 (type `0x10`), UI (type `0x15`, `HpEcFanInit\0` UTF-16LE), 4-byte aligned |
| FFS file | type `0x07` (driver), header checksum + `0xAA` data checksum + state `0xF8`; total **1088 bytes** |
| DXE container | FV at `0x94F000`, file GUID `9e21fd93-9c72-4c15-8c4b-e77f1db2d792`, LZMA section → a type-`0x17` FV-image section wrapping the inner DXE volume |
| Insertion point | `(last file end + 7) & ~7` inside the inner volume; destination span must be all `0xFF` |

## Review Focus

Most likely first:

1. **The driver does not fit the DXE free space** (a donor whose DXE volume is nearly full). Expected: `RomEditException` naming the shortfall; the decompressed blob unchanged; no recompression attempted. Test `InsertFile_NoRoom_Throws` (Task 2).
2. **The driver is already present** (`add_ec_init` run twice, or a donor that already carries it). Expected: refused with a plain message; not inserted twice. Test `AddEcInit_AlreadyPresent_Refuses` (Task 3).
3. **The PE/FFS builder drifts from the reference** (a future edit changes a header field). Expected: the built FFS still equals the 1088-byte golden, or the test fails loudly. Test `Build_EqualsGoldenFfs` (Task 1).

---

### Task 1: HpEcFanInit PE + FFS builder

**Files:**
- Create: `src/Am4ever.Core/Board/Ec/EcFanDriver.cs`
- Test: `tests/Am4ever.Tests/Board/EcFanDriverTests.cs`

**Interfaces:**
- Consumes: nothing (pure byte construction).
- Produces:
  - `public static class EcFanDriver` — `static Guid FileGuid { get; }` = `6f9a1c2e-5b7d-4e41-9c3a-8534a0e7f285`; `const string Name = "HpEcFanInit"`; `static byte[] Build()` returning the complete FFS file (driver type `0x07`, DXE-depex + PE32 + UI sections, checksums fixed). The PE32+ is built around the fixed code blob (one RX `.text` section at RVA 0x1000, subsystem 11 EFI_BOOT_SERVICE_DRIVER, no relocations). Ports `build_pe`/`build_ffs`/`section` from `hp_ecfan.py`.

- [ ] **Step 1: Write the failing test**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class EcFanDriverTests
{
    [Fact] public void Build_EqualsGoldenFfs()
    {
        var golden = Convert.FromHexString(EcFanGolden.Ffs);   // 1088-byte reference FFS
        Assert.Equal(golden, EcFanDriver.Build());
    }

    [Fact] public void Build_HasDriverNameAndGuid()
    {
        var fv = new FirmwareVolume(WrapInVolume(EcFanDriver.Build(), out var at), at);  // helper puts the FFS in a minimal FV
        var file = fv.FindByGuid(EcFanDriver.FileGuid);
        Assert.Equal("HpEcFanInit", file.Name());
    }
}
```

`EcFanGolden.Ffs` is a test constant: the 1088-byte FFS hex from the Global Constraints table's source (captured from `hp_ecfan.build_ffs()`); store it in `tests/Am4ever.Tests/Board/EcFanGolden.cs`. `WrapInVolume` is a tiny helper that drops the FFS into an otherwise-empty FFSv2 volume so `FirmwareVolume` can parse it (reuse `SyntheticFv`-style construction, or build an FV header + the file + 0xFF tail).

- [ ] **Step 2:** Run `dotnet test --filter EcFanDriverTests` → FAIL (type missing).
- [ ] **Step 3:** Implement `EcFanDriver`. Embed the 199-byte code as a hex constant with the `hp_ecfan.py` ASM reproduced in a comment above it (so a reader can see what it is and regenerate it with Keystone later). Build the PE and FFS exactly as `hp_ecfan.py` does; the golden test is the oracle.
- [ ] **Step 4:** Run → PASS (both tests).
- [ ] **Step 5:** Commit: `feat(core): HpEcFanInit EC fan hand-off DXE driver`.

### Task 2: Insert a prebuilt FFS file into a volume's free space

**Files:**
- Modify: `src/Am4ever.Core/Rom/Fv/FvEditor.cs`
- Test: `tests/Am4ever.Tests/Rom/FvEditorTests.cs`

**Interfaces:**
- Consumes: `FirmwareVolume`, `FfsFile`.
- Produces:
  - `FvEditor.InsertFile(byte[] buffer, FirmwareVolume fv, ReadOnlySpan<byte> ffsFile)` — place a complete, already-built FFS file at `(last file end + 7) & ~7`; the destination span must be inside the volume and all `0xFF`, else `RomEditException` (image unchanged). Returns the insertion offset. (Distinct from `AppendRawFile`, which *builds* a header; `InsertFile` copies a file that already has one.)

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
[Fact] public void InsertFile_ThenReParseFindsIt()
{
    var buf = SyntheticFv.WithFreeSpace(0x4000, out _);
    var fv = new FirmwareVolume(buf, 0);
    var g = Guid.NewGuid();
    var file = SyntheticFv.RawFile(g, new byte[] { 1, 2, 3, 4 });   // a complete FFS file
    FvEditor.InsertFile(buf, fv, file);
    Assert.NotNull(new FirmwareVolume(buf, 0).FindByGuid(g));
}

[Fact] public void InsertFile_NoRoom_Throws()
{
    var buf = SyntheticFv.WithFreeSpace(0x20, out _);
    var before = (byte[])buf.Clone();
    var fv = new FirmwareVolume(buf, 0);
    Assert.Throws<RomEditException>(() => FvEditor.InsertFile(buf, fv, SyntheticFv.RawFile(Guid.NewGuid(), new byte[0x1000])));
    Assert.Equal(before, buf);
}
```

`SyntheticFv.RawFile(guid, body)` builds a minimal valid FFS raw file (24-byte header, type `0x19`, fixed checksums) — factor it out of the existing `AppendRawFile` test support if present, else add it next to `SyntheticFv`.

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `InsertFile`: compute the aligned end-of-files, verify `offset + ffsFile.Length <= fv.End` and the destination is all `0xFF`, then copy. No checksum fix-up (the file arrives valid).
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): insert a prebuilt FFS file into volume free space`.

### Task 3: `add_ec_init` operation and DXE-phase wiring

**Files:**
- Create: `src/Am4ever.Core/Build/Operations/AddEcInitOp.cs` (or add to `Operations.cs`)
- Modify: `src/Am4ever.Core/Build/OperationFactory.cs` (route `add_ec_init`), `src/Am4ever.Core/Build/BuildContext.cs` (add `int DxeInnerFvOffset { get; init; }`), `src/Am4ever.Core/Build/BuildPipeline.cs` (`ApplySmbios` → a DXE phase that also inserts the driver), `src/Am4ever.Cli/Commands/BuildCommand.cs` (drop the "not yet EC fan" note), `profiles/hp-285-g3-8434.json` (add the op)
- Test: `tests/Am4ever.Tests/Build/AddEcInitTests.cs`

**Interfaces:**
- Consumes: `EcFanDriver` (Task 1), `FvEditor.InsertFile` (Task 2), the M3 operation model.
- Produces:
  - `public sealed class AddEcInitOp : BoardOperation` — `Check`: `ctx.DxeVolume` present and the driver GUID not already in the inner volume (`new FirmwareVolume(ctx.DxeVolume, ctx.DxeInnerFvOffset)`), else `RomFormatException`/`RomEditException`. `Apply`: `FvEditor.InsertFile(ctx.DxeVolume, innerFv, EcFanDriver.Build())`; returns a report line (bytes added, insertion offset relative to the inner volume).
  - `OperationFactory.Create` maps `"add_ec_init"` → `AddEcInitOp`.
  - `BuildPipeline`: the DXE phase runs when the profile has `smbios_identity` **or** `add_ec_init`; it decompresses once, locates the inner FV (offset → `DxeInnerFvOffset`), runs the DXE ops in profile order (SMBIOS edits the static-data region, then `add_ec_init` inserts the driver), fixes the SMBIOS file checksums if it was touched, and recompresses once via `GuidedSection.ReplaceLzmaGuided`.
  - `profiles/hp-285-g3-8434.json`: an `{ "op": "add_ec_init" }` entry after `smbios_identity`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
[Fact] public void AddEcInit_InsertsDriver()
{
    var (dxe, innerFv) = SyntheticDxe.WithInnerVolume(freeSpace: 0x1000);   // dec blob + inner FV offset
    var ctx = new BuildContext(new(), new()) { DxeVolume = dxe, DxeInnerFvOffset = innerFv };
    var op = OperationFactory.Create(new OperationSpec("add_ec_init"));
    op.Check(ctx);
    op.Apply(ctx);
    Assert.NotNull(new FirmwareVolume(ctx.DxeVolume!, ctx.DxeInnerFvOffset).FindByGuid(EcFanDriver.FileGuid));
}

[Fact] public void AddEcInit_AlreadyPresent_Refuses()
{
    var (dxe, innerFv) = SyntheticDxe.WithInnerVolume(freeSpace: 0x1000);
    var ctx = new BuildContext(new(), new()) { DxeVolume = dxe, DxeInnerFvOffset = innerFv };
    OperationFactory.Create(new OperationSpec("add_ec_init")).Apply(ctx);     // first insert
    Assert.Throws<RomEditException>(() => OperationFactory.Create(new OperationSpec("add_ec_init")).Check(ctx));
}
```

`SyntheticDxe.WithInnerVolume(freeSpace)` builds a decompressed-DXE-shaped buffer: a type-0x17 FV-image section wrapping a small inner FFSv2 volume with one filler file and `freeSpace` bytes of `0xFF` tail; returns the buffer and the inner FV offset. The driver is 1088 bytes, so `freeSpace: 0x1000` admits it and `0x200` would not.

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the op, factory route, context field, and pipeline DXE-phase change; add the profile op; remove the CLI note. The 1088-byte driver must fit — the real HP DXE volume has room (T5 added it at `+0x996700`).
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): add_ec_init operation inserts the EC fan driver into the DXE volume`.

### Task 4: T5 reference test — full DXE volume

**Files:**
- Modify: `tests/Am4ever.Tests/Reference/Hp285G3ReferenceTests.cs`

**Interfaces:**
- Consumes: the M3 reference-test helpers, now that the build produces the EC-fan driver too.

- [ ] **Step 1: Strengthen the assertion**

Replace the SMBIOS-region check with a full decompressed-DXE-volume comparison (the EC-fan driver is added in trailing free space, so the whole decompressed DXE blob is now byte-identical to T5's):

```csharp
Assert.Equal(DecompressPei(t5), DecompressPei(result.Image));     // boot/PEI volume identical
Assert.Equal(DecompressDxe(t5), DecompressDxe(result.Image));     // full DXE volume identical (SMBIOS + EC fan)
Assert.All(AmdDirectory.FindAll(RomImage.Load(result.Image)), d => Assert.True(d.ChecksumValid));
```

`DecompressDxe(image)` returns the LZMA-decompressed DXE blob (the `9e21fd93…` file's guided section). Keep `DecompressPei`.

- [ ] **Step 2:** Run `AM4EVER_TEST_ROMS=… dotnet test --filter Hp285G3ReferenceTests` → the full-DXE equality must hold. If it differs, bisect: compare the inner volume file lists and the EC-fan insertion offset against T5; do not weaken the assertion.
- [ ] **Step 3:** Run the whole suite with and without `AM4EVER_TEST_ROMS`; both green (reference tests run / skip).
- [ ] **Step 4:** Commit: `test(core): reference test reproduces the full T5 DXE volume`.
- [ ] **Step 5:** Hand over: report both test-mode counts; note that an M4-built image is now a complete HP 285 G3 port (CPU support + identity + fan control), and that the Unicorn emulator (which *runs* this driver against a simulated EC to verify it) is the next milestone.
