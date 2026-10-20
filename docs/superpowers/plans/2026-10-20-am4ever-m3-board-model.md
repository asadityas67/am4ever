# am4ever Milestone 3: Board Model and Build

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On top of the M2 ROM core, build the board model (AMD CPM tables in both formats, GPIO/GEVENT/DXIO/DDI edits, HD-Audio verb tables, SMBIOS identity, APCB SPD check), the typed profile/donor-adapter schema, the operation dispatcher, and an `am4ever build` command. The acceptance test applies the HP 285 G3 profile to the user's ASRock P10.43 + HP F.65 ROMs and reproduces the boot/PEI volume and the SMBIOS identity of `G:\HPBIOS\build\HP285G3_ComboAM4v2_T5.bin` semantically. This is the C# port of `G:\HPBIOS\tools\build_hp285g3.py`.

**Architecture:** Pure, UI-free code under `src/Am4ever.Core/Board/`, `src/Am4ever.Core/Profile/`, and `src/Am4ever.Core/Build/`, operating on the mutable `byte[]` buffers the M2 core already parses.
- **Board model** (`Board/`): parsers + editors for CPM tables, GPIO, DXIO/DDI, verb tables, SMBIOS and APCB. Each editor works on a span inside a decompressed volume and fixes nothing outside it.
- **Profiles** (`Profile/`): JSON schema `am4ever.profile/1` and the donor-adapter format, deserialized into records and validated. Profiles carry no firmware, cannot run code, and name operations by type.
- **Operations + build** (`Build/`): each operation checks preconditions, applies its edit, returns a report line, and declares a post-check. `BuildPipeline` runs them in a fixed order against the user's own two ROMs, recompresses, and verifies.

Tests are the two M2 kinds: **synthetic** unit tests build tiny structures in memory; **reference** tests (`[RomFact]`) run against the developer's local ROMs via `AM4EVER_TEST_ROMS` and skip when it is unset. ROM files are never committed.

**Tech Stack:**
- C# on .NET 8 (`net8.0`), building on the M2 types (`RomImage`, `Bin`, `AmdDirectory`, `FirmwareVolume`, `FfsFile`, `FvSection`, `PspZlib`, `EdkLzma`, `GuidedSection`, `FvEditor`).
- `System.Text.Json` for profile/adapter/DMI deserialization (no third-party JSON).
- xUnit 2.9.2.

**Spec:** `G:\am4ever\docs\design.md` §5.1 (CPM/APCB/verb/SMBIOS), §5.2 (operations), §5.3 (donor adapters), §7 (profiles), §10/§10.1 (verification, no firmware), §11 (reference test), §12 (first-release scope).

**Branch:** `m3-board-model`, cut from `m2-rom-core`. M1 and M2 are kept un-merged at the user's request.

## Scope boundary (what M3 does *not* do)

The `build_hp285g3.py` reference also adds `HpEcFanInit`, a generated x64 DXE driver assembled with Keystone (`hp_ecfan.py`). An assembler is a native library that belongs in `Am4ever.Emu`. Therefore:

- The `add_ec_init` operation, the Keystone/PE/FFS code generator, the emulator post-checks, and the **full** T5 reference test (with the EC-fan driver and an emulator run) are **Milestone 4**, not here.
- M3's reference test (Task 12) reproduces T5's **decompressed boot/PEI volume byte-for-byte** (all CPM/GPIO/GEVENT/DXIO/DDI/verb edits) and the **SMBIOS static table** inside the DXE volume. It explicitly does not add the EC-fan driver and does not assert the DXE volume as a whole.
- An M3 build therefore produces a bootable image with HP's board configuration and identity but without the EC fan hand-off (fans run full speed until M4). The build report says so.

## Global Constraints

- **Inherited:** `net8.0`, nullable enabled, warnings as errors, the SPDX header `// SPDX-License-Identifier: GPL-2.0-or-later` as the first line of every `.cs` file, no network access, no tracked file over 262144 bytes.
- **Signature-string policy (from the M1/M2 reviews):** C# source must not contain the five firmware ASCII strings the repo hygiene test scans for (the four AMD directory magics and the FV header). The CPM table tag `$Axx` and the CPM main tag are **not** in that set, but to stay clearly clear of the scan, write every 4-byte tag as a little-endian `uint` or as byte constants, each with a short comment that names it without spaces (e.g. `// A 1 0`). The CPM scan looks for the two bytes `0x24, 0x41` (`$`,`A`); write them as numeric constants.
- **No firmware bytes in profiles or adapters (§7, §10.1):** the only byte strings a profile/adapter JSON may carry are the short expected/replacement strings for `byte_patch` and `neutralise_override`, capped at **64 bytes** each. Profile/adapter loading rejects any byte string longer than that, and the no-firmware repo hygiene test already guards the committed JSON. OEM tables are always read from the user's own dump at build time, never stored.
- **No ROM bytes in tests.** Reference tests read `ASRock_AB350MPro4_P10.43.bin` and `HP_08434_F65_raw.bin` from `AM4EVER_TEST_ROMS` (locally `G:\HPBIOS\analysis`) via the existing `TestRoms`/`[RomFact]` helpers, and skip when a file is missing.
- **Editors never move data they do not own, never truncate, never grow a slot.** A table edit keeps the table's size (padding with its terminator); a verb/SMBIOS rewrite must fit the existing region (else `RomEditException`, image unchanged). This matches M2's editor contract.
- **Every inferred value carries evidence (§3, §7).** Operations that encode an inferred mapping (DXIO lane maps, DDI blocks for families the OEM never shipped) record `confidence: inferred` and an evidence string in their report line.

### Reference constants (from `build_hp285g3.py`, verified against the real ROMs)

| Thing | Value |
|---|---|
| ASRock compressed PEI blob | offset `0xE02000`, compressed length `0xD57C6`, BHD entry type `0x62`, entry size `0x400020` |
| FFS file wrapping the blob | starts `0xE01FD0`; GUID-defined section header at `+0x18` |
| FV inside decompressed blob | offset `0x20` |
| HP PEI firmware volume | offset `0xC00000` |
| Main DXE volume | offset `0x94F000`; file GUID `9e21fd93-9c72-4c15-8c4b-e77f1db2d792`; LZMA section GUID `ee4e5898-3914-4259-9d6e-dc7bd79403cf` |
| SMBIOS static data | file GUID `daf4bf89-ce71-4917-b522-c89d32fbc59f`; freeform-subtype section type `0x18`; data GUID `ab56dc60-0057-11da-a8db-000102eee626`; body at section `+0x14` |
| OEM CPM PEIMs (ASRock) | `AmdCpmOemInitPeim` (×2: Summit/Pinnacle, then Matisse/Vermeer), `AmdCpmOemRvInitPeim` (Raven/Picasso), `AmdCpmOemRnInitPeim` (Renoir/Cezanne) |
| OEM CPM PEIMs (HP) | `AmdCpmOemInitPeim`, `AmdCpmOemRvInitPeim` only |
| CPM table header | at tag `+4`: `UInt16 size`, `byte featureRevision`, `byte contentRevision`, `UInt32 platformMask`, `UInt32 attributes`; body at `+0x10` |
| DXIO descriptor | ComboAM4v2 `0x1A4` bytes, PinnaclePI `0x94` bytes; descriptors start at table `+0x14`; per descriptor: `byte engine@+4`, `byte laneStart@+6`, `byte laneEnd@+7`, `UInt32 port@+0xC` (present bit0, device bits3-7, function bits8-10, clkReq bits24-27), `byte type@+0x14` |
| DDI (display) block | after the 16 port descriptors: ComboAM4v2 `0x14 + 16*0x1A4`, PinnaclePI `0x14 + 16*0x94`; connectors at block `+0x0C`, 12 bytes each: `UInt32 flags@+0` (last-entry bit `0x80000000`), `byte connector@+4`, `byte aux@+5`, `byte hpd@+6` |
| HD-Audio codecs | ASRock `0x10EC0899`, `0x10EC0898`; HP ALC671 `0x10EC0671`; HP ROM mapped at `0xFF000000`; list/verb terminator `0xFFFFFFFF` |
| Override sites (offset into the OEM PEIM image section → expected bytes) | ZP `0x2D6`→`81ecc4000000`, MTS `0x344`→`8b018d94244cffffff`, RV `0x394`→`81ecc0000000`, RN `0x36C`→`8b018d942460ffffff`; replacement is `31 C0 C3` (`xor eax,eax; ret`) over the first 3 bytes |

## Review Focus

Most likely to bite first:

1. **A profile naming an operation, family or table that the user's dump does not contain.** Expected: the operation's precondition fails with a plain message naming what was missing; the build stops, the image is untouched, no partial write. Test `Build_MissingTable_StopsWithMessage` (Task 9).
2. **A profile or adapter JSON carrying a long byte string (smuggled firmware).** Expected: loading throws with a message naming the field and the 64-byte limit. Test `LoadProfile_RejectsOversizeByteString` (Task 8).
3. **A `byte_patch`/`neutralise_override` whose expected bytes do not match the dump.** Expected: `RomEditException` quoting the expected vs found bytes; nothing written. Test `Neutralise_WrongExpectedBytes_Throws` (Task 6).
4. **A verb or SMBIOS rewrite larger than the region it replaces.** Expected: `RomEditException` with both sizes; image byte-for-byte unchanged. Tests `ReplaceVerbs_TooBig_LeavesImageUntouched` (Task 4), `Smbios_TooBig_LeavesImageUntouched` (Task 5).
5. **A CPM tag whose size field is absurd (0, or larger than the region).** Expected: the scan rejects it (`12 <= size < 0x4000`) rather than reading past the buffer; no `IndexOutOfRangeException`. Test `CpmScan_IgnoresGarbageSizeTag` (Task 1).

---

### Task 1: CPM table model (both formats)

**Files:**
- Create: `src/Am4ever.Core/Board/Cpm/CpmTable.cs`, `src/Am4ever.Core/Board/Cpm/CpmImage.cs`
- Test: `tests/Am4ever.Tests/Board/CpmImageTests.cs`

**Interfaces:**
- Consumes: `Bin` (M2).
- Produces:
  - `public sealed record CpmTable(int Offset, string Tag, int Size, byte FeatureRevision, byte ContentRevision, uint PlatformMask, uint Attributes)` — `int BodyOffset => Offset + 0x10`; `int End => Offset + Size`.
  - `public static class CpmImage`:
    - `static IReadOnlyList<CpmTable> Tables(ReadOnlySpan<byte> img)` — scan for the two bytes `0x24,0x41` (`$`,`A`) followed by two bytes in `[0-9A-Z]`; read the header; accept only `12 <= size < 0x4000` and `End <= img.Length`.
    - `static CpmTable FindTable(ReadOnlySpan<byte> img, string tag, uint? platformMask = null, int? size = null)` — the single matching table, else `RomFormatException` naming the tag/mask/count (mirrors the Python `find_table`'s "N matches" error).

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class CpmImageTests
{
    [Fact] public void FindTable_ByTagAndMask_ReturnsTheOne()
    {
        var img = SyntheticCpm.WithTables(("$A10", 0xFFFF00u, 0x40), ("$A10", 0xFFu, 0x40), ("$A11", 0u, 0x20));
        var t = CpmImage.FindTable(img, "$A10", 0xFFu);
        Assert.Equal(0xFFu, t.PlatformMask);
        Assert.Equal(0x40, t.Size);
    }

    [Fact] public void FindTable_Ambiguous_Throws() =>
        Assert.Throws<RomFormatException>(() =>
            CpmImage.FindTable(SyntheticCpm.WithTables(("$A10", 0u, 0x40), ("$A10", 0u, 0x40)), "$A10"));

    [Fact] public void CpmScan_IgnoresGarbageSizeTag()
    {
        var img = SyntheticCpm.WithTables(("$A10", 0u, 0x40));
        SyntheticCpm.SetSizeField(img, "$A10", 0xFFFF);        // absurd size
        Assert.Empty(CpmImage.Tables(img));
    }

    [RomFact] public void FindsCpmTablesInAsrockOemPeim()
    {
        var img = ReferenceImages.AsrockOemPeim("AmdCpmOemInitPeim", 0);   // Summit/Pinnacle
        Assert.NotNull(CpmImage.FindTable(img, "$A10", 0xFFFF00u));
        Assert.NotNull(CpmImage.FindTable(img, "$A11"));
        Assert.NotNull(CpmImage.FindTable(img, "$A26", 0xBFBF00u));
    }
}
```

`SyntheticCpm` builds a byte array of concatenated CPM tables with the header layout above; `ReferenceImages` is a Task-1 test helper (see below) that loads a ROM, decompresses the PEI volume, and returns the Nth OEM PEIM image-section bytes by name.

- [ ] **Step 2:** Run `dotnet test --filter CpmImageTests` → FAIL (types missing).
- [ ] **Step 3:** Implement `CpmTable`, `CpmImage`, `SyntheticCpm` and `ReferenceImages`. `ReferenceImages.AsrockOemPeim(name, index)`: `RomImage.Load`, `PspZlib.Decompress` the blob at `0xE02000` (length `0xD57C6`), `new FirmwareVolume(plain, 0x20)`, pick the `index`-th file whose `Name()==name`, return `ImageSection()` bytes.
- [ ] **Step 4:** Run with and without `AM4EVER_TEST_ROMS` → all green / reference skipped.
- [ ] **Step 5:** Commit: `feat(core): CPM table scan and lookup for both CPM formats`.

### Task 2: GPIO table read, write and family-delta merge

**Files:**
- Create: `src/Am4ever.Core/Board/Cpm/GpioTable.cs`
- Test: `tests/Am4ever.Tests/Board/GpioTableTests.cs`

**Interfaces:**
- Consumes: `CpmTable`, `Bin`.
- Produces:
  - `public static class GpioTable`:
    - `static (Dictionary<ushort,ushort> Entries, List<ushort> Order) Read(ReadOnlySpan<byte> img, CpmTable t)` — entries of `{UInt16 pin, UInt16 state}` from `t.BodyOffset`, stopping at `pin == 0xFF`.
    - `static void Write(byte[] img, CpmTable t, IReadOnlyDictionary<ushort,ushort> entries, IReadOnlyList<ushort> order)` — writes the entries then the `0xFF,0` terminator into `[BodyOffset, End)`, padding with the leftover bytes; `RomEditException` if they do not fit.
    - `static (Dictionary<ushort,ushort> Entries, List<ushort> Order, IReadOnlyList<string> Notes) Merge(IReadOnlyDictionary<ushort,ushort> hp, IReadOnlyList<ushort> hpOrder, IReadOnlyDictionary<ushort,ushort> baseGpio, IReadOnlyDictionary<ushort,ushort> family)` — HP's table plus the donor's per-family deltas, keeping the HP value on any pin HP wires itself. Returns the human-readable notes for the report.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class GpioTableTests
{
    [Fact] public void WriteThenRead_RoundTrips()
    {
        var (img, t) = SyntheticCpm.OneGpioTable(size: 0x40);
        var entries = new Dictionary<ushort,ushort> { [0] = 0x1234, [5] = 0x00AB };
        var order = new List<ushort> { 0, 5 };
        GpioTable.Write(img, t, entries, order);
        var (back, backOrder) = GpioTable.Read(img, t);
        Assert.Equal(entries, back);
        Assert.Equal(order, backOrder);
    }

    [Fact] public void Merge_KeepsHpValueWhereHpWiresPin()
    {
        var hp     = new Dictionary<ushort,ushort> { [10] = 0x00AA };
        var baseG  = new Dictionary<ushort,ushort> { };                  // donor base: pin 10 unset
        var family = new Dictionary<ushort,ushort> { [10] = 0x00BB };    // donor family wants 0xBB
        var (merged, _, _) = GpioTable.Merge(hp, new List<ushort>{10}, baseG, family);
        Assert.Equal(0x00AA, merged[10]);                                // HP wins: HP wires it, base doesn't
    }

    [Fact] public void Merge_AppliesFamilyDeltaWhereHpDoesNot()
    {
        var hp     = new Dictionary<ushort,ushort> { };
        var baseG  = new Dictionary<ushort,ushort> { };
        var family = new Dictionary<ushort,ushort> { [12] = 0x00CC };
        var (merged, order, _) = GpioTable.Merge(hp, new List<ushort>(), baseG, family);
        Assert.Equal(0x00CC, merged[12]);
        Assert.Contains((ushort)12, order);
    }

    [Fact] public void Write_Overflow_Throws()
    {
        var (img, t) = SyntheticCpm.OneGpioTable(size: 0x18);            // room for ~1 entry + terminator
        var big = Enumerable.Range(0, 20).ToDictionary(i => (ushort)i, i => (ushort)i);
        Assert.Throws<RomEditException>(() => GpioTable.Write(img, t, big, big.Keys.ToList()));
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement. `Merge` follows `merge_gpio` in `build_hp285g3.py`: for each `family` pin, skip (keep HP) when the pin is in `hp` but not `baseGpio`; when `hp` and `base` both have it but differ, keep HP; otherwise take the family value, appending to `order` if new. Record a note per decision.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): GPIO table read/write and family-delta merge`.

### Task 3: DXIO descriptors and DDI display block

**Files:**
- Create: `src/Am4ever.Core/Board/Cpm/DxioTable.cs`, `src/Am4ever.Core/Board/Cpm/DdiBlock.cs`
- Test: `tests/Am4ever.Tests/Board/DxioTableTests.cs`

**Interfaces:**
- Consumes: `CpmTable`, `Bin`.
- Produces:
  - `public sealed class DxioTable` — `ctor(byte[] img, CpmTable t, int descriptorSize)` (0x1A4 or 0x94); `int Count`; `string Describe(int i)` (engine, lane start-end, present/device/function/clkReq, type); `void Set(int i, int? template = null, byte? engine = null, (byte start, byte end)? lanes = null, byte? device = null, byte? function = null, byte? clkReq = null)` — mirrors `Dxio.set`: a `template` copies another descriptor's body first (keeping this slot's 4 flag bytes), then fields overwrite; setting `device` also sets the port present bit.
  - `public static class DdiBlock` — `static string Describe(ReadOnlySpan<byte> block)`; `static void Copy(byte[] dstImg, CpmTable dst, ReadOnlySpan<byte> srcImg, CpmTable src, int dstDescSize, int srcDescSize)` — copies the source DDI block into the destination table tail, zero-padding to the destination length; `RomEditException` if the source block is longer than the destination tail.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class DxioTableTests
{
    [Fact] public void Set_LanesAndClkReq_UpdatesDescriptor()
    {
        var (img, t) = SyntheticCpm.OneDxioTable(descriptors: 6, descSize: 0x1A4);
        var dx = new DxioTable(img, t, 0x1A4);
        dx.Set(2, lanes: (0, 0), clkReq: 1);
        Assert.Contains("lanes 0-0", dx.Describe(2));
        Assert.Contains("clkreq=1", dx.Describe(2));
    }

    [Fact] public void Set_Template_CopiesThenOverrides()
    {
        var (img, t) = SyntheticCpm.OneDxioTable(descriptors: 6, descSize: 0x1A4);
        var dx = new DxioTable(img, t, 0x1A4);
        dx.Set(2, lanes: (0, 0), clkReq: 1);
        dx.Set(4, template: 2, engine: 1, lanes: (1, 1), device: 1, function: 2, clkReq: 2);
        Assert.Contains("present=1", dx.Describe(4));         // device set turns present on
        Assert.Contains("lanes 1-1", dx.Describe(4));
    }

    [Fact] public void DdiCopy_PadsToDestinationLength()
    {
        var (dstImg, dst) = SyntheticCpm.OneDxioTable(descriptors: 16, descSize: 0x1A4, ddiBytes: 0x60);
        var (srcImg, src) = SyntheticCpm.OneDxioTable(descriptors: 16, descSize: 0x94,  ddiBytes: 0x40);
        DdiBlock.Copy(dstImg, dst, srcImg, src, 0x1A4, 0x94);  // must not throw; pads 0x40 -> 0x60
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement. `Describe` reads `port@+0xC` and unpacks the bitfields exactly as `Dxio.describe`. `Set` packs them back with the documented masks (`device` → `(port & ~(0x1f<<3)) | (dev<<3) | 1`, etc.). DDI block starts at `0x14 + 16*descriptorSize`.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): DXIO descriptor editing and DDI display block`.

### Task 4: HD-Audio verb table extract and replace

**Files:**
- Create: `src/Am4ever.Core/Board/VerbTable.cs`
- Test: `tests/Am4ever.Tests/Board/VerbTableTests.cs`

**Interfaces:**
- Consumes: `FirmwareVolume`, `FfsFile`, `Bin`.
- Produces:
  - `public static class VerbTable`:
    - `static IReadOnlyList<uint> ExtractHp(byte[] hpRom, int peiFvOffset, uint codecId, uint romBase)` — walk `AmdSocAm4*` PEIM bodies for the `{UInt32 codecId; UInt64 verbTablePtr}` list entries, follow each pointer (`romBase <= ptr < 0x1_0000_0000`) to the `UInt32` verb list, read verbs until the first word whose top nibble (CAd) is non-zero; require exactly one distinct table; validate the subsystem-ID verbs (pins `0x720..0x723` → `0x34,0x84,0x3C,0x10`).
    - `static void Replace(byte[] img, uint[] oldCodecs, uint newCodecId, IReadOnlyList<uint> verbs)` — find the donor codec list (first `oldCodecs[0]` word, exactly once), walk its `{codecId, verbs..., 0xFFFFFFFF}` blocks to the list terminator, replace the whole list with one block `newCodecId, verbs..., 0xFFFFFFFF, 0xFFFFFFFF`, padding the rest with `0xFF`; `RomEditException` if the new block is larger than the old list.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class VerbTableTests
{
    [Fact] public void Replace_InPlace_PadsRemainderWithFf()
    {
        var img = SyntheticVerbs.List((0x10EC0899, new uint[]{1,2,3}), (0x10EC0898, new uint[]{4,5}));
        VerbTable.Replace(img, new uint[]{0x10EC0899, 0x10EC0898}, 0x10EC0671, new uint[]{9,9});
        Assert.Equal(0x10EC0671u, Bin.U32(img, SyntheticVerbs.ListStart));
        Assert.Equal(0xFFu, img[^1]);                         // tail padded
    }

    [Fact] public void ReplaceVerbs_TooBig_LeavesImageUntouched()
    {
        var img = SyntheticVerbs.List((0x10EC0899, new uint[]{1}), (0x10EC0898, new uint[]{2}));
        var before = (byte[])img.Clone();
        var huge = Enumerable.Range(0, 500).Select(i => (uint)i).ToArray();
        Assert.Throws<RomEditException>(() =>
            VerbTable.Replace(img, new uint[]{0x10EC0899, 0x10EC0898}, 0x10EC0671, huge));
        Assert.Equal(before, img);
    }

    [RomFact] public void ExtractsHpAlc671VerbTable()
    {
        var hp = File.ReadAllBytes(TestRoms.HpF65!);
        var verbs = VerbTable.ExtractHp(hp, 0xC00000, 0x10EC0671, 0xFF000000);
        Assert.NotEmpty(verbs);                               // validated subsystem ID inside ExtractHp
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement following `hp_verbs`/`patch_verbs`. `Replace` must compute the old list span exactly (to the terminator past the last block) and refuse to overrun it.
- [ ] **Step 4:** Run with/without the env var.
- [ ] **Step 5:** Commit: `feat(core): HD-Audio verb table extraction and in-place replace`.

### Task 5: SMBIOS identity

**Files:**
- Create: `src/Am4ever.Core/Board/Smbios/SmbiosTable.cs`, `src/Am4ever.Core/Board/Smbios/DmiIdentity.cs`
- Test: `tests/Am4ever.Tests/Board/SmbiosTableTests.cs`

**Interfaces:**
- Consumes: `FirmwareVolume`, `FfsFile`, `FvSection`, `Bin`.
- Produces:
  - `public sealed record DmiIdentity(...)` — the fields `build_hp285g3.py` sets: `SystemManufacturer`, `SystemProduct`, `SystemVersion`, `SystemSerial`, `SystemSku`, `SystemFamily`, `SystemUuid`, `BoardManufacturer`, `BoardProduct`, `BoardVersion`, `BoardSerial`, `BoardAssetTag`, `BoardLocation`, `ChassisManufacturer`, `ChassisVersion`, `ChassisSerial`, `ChassisAssetTag`, `ChassisSku`, `ChassisTypes`, `OemStrings` (all nullable); plus `static DmiIdentity HpDefault` (the generic HP identity) and `DmiIdentity Merge(DmiIdentity overrides)` (non-empty override fields win). A `static DmiIdentity FromJson(byte[] utf8)` reader keyed by the Python snake_case names, so `save_hp_dmi.ps1` output loads unchanged.
  - `public static class SmbiosTable`:
    - `static (int Start, int End) LocateStaticData(FirmwareVolume dxeFv)` — the `daf4bf89…` file's `0x18` section, data-GUID checked, body at `+0x14`.
    - `static void ApplyIdentity(byte[] img, int start, int end, DmiIdentity dmi, out IReadOnlyList<string> report)` — parse SMBIOS structures, rewrite the string fields per type (types 1/2/3), the type-1 UUID (`bytes_le`), the type-3 chassis-type byte, and the type-11 OEM strings; renumber strings in field order; `RomEditException` if the rebuilt table is larger than the region.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class SmbiosTableTests
{
    [Fact] public void ApplyIdentity_RewritesManufacturerString()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("ASRock", "AB350M");
        SmbiosTable.ApplyIdentity(img, start, end,
            new DmiIdentity { SystemManufacturer = "HP", SystemProduct = "HP 285 G3 MT Business PC" }, out _);
        Assert.Equal(("HP", "HP 285 G3 MT Business PC"), SyntheticSmbios.ReadType1(img, start));
    }

    [Fact] public void ApplyIdentity_SetsType1Uuid()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("x", "y");
        var g = Guid.NewGuid();
        SmbiosTable.ApplyIdentity(img, start, end, new DmiIdentity { SystemUuid = g.ToString() }, out _);
        Assert.Equal(g.ToByteArray(), SyntheticSmbios.ReadType1Uuid(img, start));   // SMBIOS 2.6+ byte order
    }

    [Fact] public void Smbios_TooBig_LeavesImageUntouched()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("x", "y");
        var before = (byte[])img.Clone();
        var huge = new string('Z', (end - start) + 100);
        Assert.Throws<RomEditException>(() =>
            SmbiosTable.ApplyIdentity(img, start, end, new DmiIdentity { SystemSerial = huge }, out _));
        Assert.Equal(before, img);
    }

    [Fact] public void FromJson_ReadsSnakeCaseFields()
    {
        var dmi = DmiIdentity.FromJson(System.Text.Encoding.UTF8.GetBytes(
            "{\"system_manufacturer\":\"HP\",\"board_product\":\"8434\"}"));
        Assert.Equal("HP", dmi.SystemManufacturer);
        Assert.Equal("8434", dmi.BoardProduct);
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement following `smbios_structs`/`set_strings`/`patch_smbios`. `STRING_FIELDS` per type: 0→(4,5,8), 1→(4,5,6,7,0x19,0x1A), 2→(4,5,6,7,8,0xA), 3→(4,6,7,8,0x15). Build to a scratch buffer first; only write back if it fits, padding the region with `0xFF`.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): SMBIOS identity rewrite and DMI model`.

### Task 6: Override neutralise and byte_patch

**Files:**
- Create: `src/Am4ever.Core/Board/BytePatch.cs`
- Test: `tests/Am4ever.Tests/Board/BytePatchTests.cs`

**Interfaces:**
- Consumes: `Bin`.
- Produces:
  - `public static class BytePatch`:
    - `static void Apply(byte[] img, int offset, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> replacement)` — require `img[offset..]` to equal `expected` (else `RomEditException` quoting expected vs found as hex), then overwrite the first `replacement.Length` bytes; `replacement.Length <= expected.Length`.
    - `static void NeutraliseOverride(byte[] img, int offset, ReadOnlySpan<byte> expected)` — `Apply` with replacement `31 C0 C3` (`xor eax,eax; ret`).

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class BytePatchTests
{
    [Fact] public void Neutralise_WritesXorRet()
    {
        var img = new byte[] { 0x81, 0xEC, 0xC4, 0x00, 0x00, 0x00, 0x90 };
        BytePatch.NeutraliseOverride(img, 0, new byte[] { 0x81, 0xEC, 0xC4, 0x00, 0x00, 0x00 });
        Assert.Equal(new byte[] { 0x31, 0xC0, 0xC3 }, img[..3]);
    }

    [Fact] public void Neutralise_WrongExpectedBytes_Throws()
    {
        var img = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        var before = (byte[])img.Clone();
        Assert.Throws<RomEditException>(() =>
            BytePatch.NeutraliseOverride(img, 0, new byte[] { 0x81, 0xEC, 0xC4, 0x00, 0x00, 0x00 }));
        Assert.Equal(before, img);
    }

    [Fact] public void Apply_ReplacementLongerThanExpected_Throws() =>
        Assert.Throws<RomEditException>(() =>
            BytePatch.Apply(new byte[4], 0, new byte[] { 0, 0 }, new byte[] { 1, 2, 3 }));
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): expected-bytes override neutralise and byte patch`.

### Task 7: APCB SPD-map parse and compatibility check

**Files:**
- Create: `src/Am4ever.Core/Board/Apcb.cs`
- Test: `tests/Am4ever.Tests/Board/ApcbTests.cs`

**Interfaces:**
- Consumes: `RomImage`, `AmdDirectory`, `Bin`.
- Produces:
  - `public static class Apcb`:
    - `static IReadOnlyList<int> FindAll(RomImage rom)` — offsets of APCB blocks (the ASCII `APCB` magic, written as bytes `0x41,0x50,0x43,0x42`, validated by the v2/v3 header: a 4-byte version word and a length that stays inside the image).
    - `static IReadOnlyList<byte> SpdAddressMap(ReadOnlySpan<byte> rom, int apcbOffset)` — the SPD/I2C address bytes from the type `0x0031` group (v2), or an empty list if absent.
    - `static bool SpdMapsCompatible(ReadOnlySpan<byte> oem, int oemApcb, ReadOnlySpan<byte> donor, int donorApcb, out string report)` — true when the two SPD address maps are equal; the report names the two maps either way.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class ApcbTests
{
    [RomFact] public void FindsApcbInBothRoms()
    {
        var asrock = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var hp     = RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!));
        Assert.NotEmpty(Apcb.FindAll(asrock));
        Assert.NotEmpty(Apcb.FindAll(hp));
    }

    [RomFact] public void HpAndAsrockSpdMapsAreCompatible()
    {
        var asrock = File.ReadAllBytes(TestRoms.AsrockP1043!);
        var hp     = File.ReadAllBytes(TestRoms.HpF65!);
        var a = Apcb.FindAll(RomImage.Load(asrock))[0];
        var h = Apcb.FindAll(RomImage.Load(hp))[0];
        Assert.True(Apcb.SpdMapsCompatible(hp, h, asrock, a, out var report));
        Assert.Contains("SPD", report);
    }
}
```

If the reference ROMs turn out to have incompatible SPD maps, the second test's expectation flips to `False` and the report is asserted to name both maps — the point is that the check reports truthfully, not that a particular pair matches. Decide from the real bytes during Step 3.

- [ ] **Step 2:** Run → FAIL (and, without the env var, both skip).
- [ ] **Step 3:** Implement. Scan for the `APCB` magic bytes on reasonable alignment, validate the header, read the type-0x0031 group. Confirm the real SPD maps during this step and set the Step-1 expectation accordingly.
- [ ] **Step 4:** Run with/without the env var.
- [ ] **Step 5:** Commit: `feat(core): APCB SPD address-map parse and compatibility check`.

### Task 8: Profile and donor-adapter schema

**Files:**
- Create: `src/Am4ever.Core/Profile/BoardProfile.cs`, `src/Am4ever.Core/Profile/DonorAdapter.cs`, `src/Am4ever.Core/Profile/ProfileLoader.cs`
- Test: `tests/Am4ever.Tests/Profile/ProfileLoaderTests.cs`

**Interfaces:**
- Consumes: nothing from the board model yet (pure JSON records).
- Produces:
  - `public sealed record BoardProfile(string Schema, BoardInfo Board, DonorRef Donor, IReadOnlyList<string> Authors, ProfileStatus Status, IReadOnlyList<OperationSpec> Operations, IdentitySpec? Identity)` with the nested records (`BoardInfo` incl. `Match`, `DonorRef`, `ProfileStatus` with `Tested`/`Untested`, `IdentitySpec`). `OperationSpec` is a record with one positional parameter `string Op` and the rest as init-only properties defaulting to null — `Family`, `From`, `Merge` (`string?`), `PlatformMask` (`uint?`), `Module` (`string?`), `Offset` (`int?`), `Expected`, `New` (`byte[]?`), `Confidence`, `Evidence`, `Notes` (`string?`), `Extra` (`JsonElement?`) — so `new OperationSpec("port_gpio") { Family = "MTS_VMR" }` compiles. It is a superset covering every operation's arguments; unknown JSON keys are collected into `Extra`, not an error.
  - `public sealed record DonorAdapter(string Id, DonorIdentity Identity, IReadOnlyList<ModuleEntry> Modules, IReadOnlyList<OverrideSig> Overrides, VolumeMap Volumes, IReadOnlyList<string> Hazards, string DownloadUrl)` — `OverrideSig` carries a pattern/expected bytes, `VolumeMap` the boot/DXE FV offsets and compression kinds.
  - `public static class ProfileLoader`:
    - `static BoardProfile LoadProfile(byte[] utf8)` / `static DonorAdapter LoadAdapter(byte[] utf8)` — deserialize, require `schema == "am4ever.profile/1"` (profiles), and enforce the no-firmware rule: any `Expected`/`New` byte string over 64 bytes throws `RomFormatException` naming the field and the limit.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class ProfileLoaderTests
{
    [Fact] public void LoadProfile_ParsesSchemaAndOperations()
    {
        var p = ProfileLoader.LoadProfile(SampleJson.MinimalHpProfile);
        Assert.Equal("am4ever.profile/1", p.Schema);
        Assert.Equal("8434", p.Board.BoardId);
        Assert.Contains(p.Operations, o => o.Op == "port_gpio");
    }

    [Fact] public void LoadProfile_WrongSchema_Throws() =>
        Assert.Throws<RomFormatException>(() =>
            ProfileLoader.LoadProfile(SampleJson.WithSchema("am4ever.profile/999")));

    [Fact] public void LoadProfile_RejectsOversizeByteString() =>
        Assert.Throws<RomFormatException>(() =>
            ProfileLoader.LoadProfile(SampleJson.WithBytePatch(expectedHexBytes: 128)));

    [Fact] public void LoadAdapter_ParsesModulesAndOverrides()
    {
        var a = ProfileLoader.LoadAdapter(SampleJson.AsrockAdapter);
        Assert.Equal("asrock-ab350m-pro4-p10.43", a.Id);
        Assert.NotEmpty(a.Overrides);
        Assert.Equal(0xE02000, a.Volumes.BootBlobOffset);
    }
}
```

`SampleJson` holds small inline JSON fixtures (well under 64-byte patch strings except the deliberately oversize one).

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement with `System.Text.Json` (`JsonSerializerOptions { PropertyNameCaseInsensitive = true }`, a snake_case naming policy, hex-string→`byte[]` converter that enforces the 64-byte cap).
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): board profile and donor adapter schema with no-firmware limits`.

### Task 9: Operation model and dispatcher

**Files:**
- Create: `src/Am4ever.Core/Build/IBoardOperation.cs`, `src/Am4ever.Core/Build/BuildContext.cs`, `src/Am4ever.Core/Build/Operations/*.cs` (one file per operation), `src/Am4ever.Core/Build/OperationFactory.cs`
- Test: `tests/Am4ever.Tests/Build/OperationTests.cs`

**Interfaces:**
- Consumes: Tasks 1-6 (board editors) and Task 8 (`OperationSpec`).
- Produces:
  - `public interface IBoardOperation { string Describe(); void Check(BuildContext ctx); IReadOnlyList<string> Apply(BuildContext ctx); }` — `Check` throws `RomEditException`/`RomFormatException` on a failed precondition; `Apply` performs the edit and returns report lines; a post-check is a method `Verify(BuildContext ctx)` returning `bool` with an out message.
  - `public sealed class BuildContext` — holds the donor PEI buffer and per-family OEM PEIM image-section spans, the HP source buffers (`hpRom`, HP PEI FV), the donor adapter, and the collected report. Operations read OEM tables from the HP buffer and write into the donor spans.
  - Operation classes implementing the §5.2 set available in M3: `NeutraliseOverrideOp`, `PortGpioOp`, `PortGeventOp`, `EmptyTablesOp`, `DxioLayoutOp`, `DdiLayoutOp`, `VerbTableOp`, `SmbiosIdentityOp`, `BytePatchOp`, `ApcbCheckOp`. (`AddEcInitOp` is M4.)
  - `public static class OperationFactory { static IBoardOperation Create(OperationSpec spec); }` — maps `spec.Op` to a class; an unknown op is `RomFormatException`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class OperationTests
{
    [Fact] public void Factory_UnknownOp_Throws() =>
        Assert.Throws<RomFormatException>(() => OperationFactory.Create(new OperationSpec("no_such_op")));

    [Fact] public void Build_MissingTable_StopsWithMessage()
    {
        var ctx = SyntheticBuild.ContextWithoutGpioTable();
        var op = OperationFactory.Create(new OperationSpec("port_gpio") { Family = "MTS_VMR" });
        var ex = Assert.Throws<RomFormatException>(() => op.Check(ctx));
        Assert.Contains("$A10", ex.Message);
    }

    [Fact] public void NeutraliseOp_AppliesAndPostChecksPass()
    {
        var ctx = SyntheticBuild.ContextWithOverrideSite(0x10, new byte[]{0x81,0xEC,0xC4,0,0,0});
        var op = OperationFactory.Create(new OperationSpec("neutralise_override") { Offset = 0x10,
                     Expected = new byte[]{0x81,0xEC,0xC4,0,0,0} });
        op.Check(ctx);
        var lines = op.Apply(ctx);
        Assert.NotEmpty(lines);
        Assert.True(op.Verify(ctx, out _));
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement each operation as a thin wrapper over the Task 1-6 editors. `SyntheticBuild` builds a `BuildContext` over small in-memory CPM/override fixtures.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): typed board operations and dispatcher`.

### Task 10: Build pipeline + ASRock adapter + HP profile data

**Files:**
- Create: `src/Am4ever.Core/Build/BuildPipeline.cs`, `src/Am4ever.Core/Build/BuildReport.cs`
- Create (data, shipped with the app): `donors/asrock-ab350m-pro4-p10.43.json`, `profiles/hp-285-g3-8434.json`
- Modify: `src/Am4ever.Core/Am4ever.Core.csproj` (embed `donors/` and `profiles/` as resources, or copy to output)
- Test: `tests/Am4ever.Tests/Build/BuildPipelineTests.cs`

**Interfaces:**
- Consumes: everything above.
- Produces:
  - `public static class BuildPipeline`:
    - `static BuildResult Build(byte[] oemRom, byte[] donorRom, BoardProfile profile, DonorAdapter adapter, DmiIdentity? dmiOverride)` — verifies the donor SHA-256 against the adapter, locates the boot blob from the BHD entries, decompresses the PEI volume, finds the OEM PEIMs, runs the profile's operations in fixed order per PEIM (neutralise → gpio → gevent → empty → dxio → ddi → verb), recompresses the PEI volume back into the FFS wrapper, applies the SMBIOS identity in the DXE volume (recompressing it), runs every post-check, and returns the new image plus the report. Never mutates its inputs (works on copies).
  - `public sealed record BuildResult(byte[] Image, BuildReport Report)`; `BuildReport.ToMarkdown()`.
  - The two JSON data files encode exactly what `build_hp285g3.py` hardcodes: the ASRock adapter (boot blob `0xE02000`, DXE FV `0x94F000`, the four override sites and expected bytes, the module→family map) and the HP profile (the operation list with the DXIO plans per family, each inferred lane/DDI mapping carrying `confidence`/`evidence`).

- [ ] **Step 1: Write the failing test**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class BuildPipelineTests
{
    [Fact] public void Build_WrongDonorHash_Throws()
    {
        var adapter = ProfileLoader.LoadAdapter(SampleJson.AsrockAdapter);   // expects a specific sha256
        var profile = ProfileLoader.LoadProfile(SampleJson.MinimalHpProfile);
        Assert.Throws<RomFormatException>(() =>
            BuildPipeline.Build(new byte[16<<20], new byte[16<<20], profile, adapter, null));
    }

    [Fact] public void BundledProfileAndAdapterLoad()
    {
        Assert.NotNull(BundledData.LoadProfile("hp-285-g3-8434"));
        Assert.NotNull(BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"));
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the pipeline and author the two JSON files. `BundledData` locates them next to the assembly or as embedded resources. The real end-to-end build is exercised by Task 12's reference test; here, cover input validation and that the shipped data loads and validates.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): build pipeline, ASRock adapter and HP 285 G3 profile`.

### Task 11: `am4ever build` command

**Files:**
- Create: `src/Am4ever.Cli/Commands/BuildCommand.cs`
- Modify: `src/Am4ever.Cli/CliApp.cs` (route `build`)
- Test: `tests/Am4ever.Tests/Cli/BuildCommandTests.cs`

**Interfaces:**
- Consumes: `BuildPipeline`, `ProfileLoader`, the M1 risk gate.
- Produces:
  - `public static class BuildCommand` — `static int Run(BuildArgs args, IConsoleIo io)` where `BuildArgs` has `OemPath`, `DonorPath`, `ProfilePath` (or a bundled profile id), `OutPath`, `DmiPath?`, `IUnderstandTheRisk`. Returns `0` on a built+verified image (writing the `.bin` and a `<out>.report.md` next to it), `1` on a build/verify failure (message to `Err`, nothing written), `64` on a usage error, and refuses with a non-zero code and the risk text if `--i-understand-the-risk` is absent (per §6.0).
  - `CliApp.Run` routes `build …` to it.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class BuildCommandTests
{
    [Fact] public void Build_WithoutRiskFlag_Refuses()
    {
        var io = FakeIo.Create();
        var rc = CliApp.Run(new[] { "build", "oem.bin", "donor.bin", "--profile", "hp-285-g3-8434", "--out", "o.bin" }, io);
        Assert.NotEqual(0, rc);
        Assert.Contains("risk", io.ErrText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Build_MissingArgs_UsageError()
    {
        var io = FakeIo.Create();
        Assert.Equal(64, CliApp.Run(new[] { "build" }, io));
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement argument parsing and routing; reuse the M1 `RiskWarning` text for the refusal.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(cli): build command with risk gate and build report`.

### Task 12: HP 285 G3 semantic reference test (PEI volume + SMBIOS)

**Files:**
- Create: `tests/Am4ever.Tests/Reference/Hp285G3ReferenceTests.cs`
- Test helper: `tests/Am4ever.Tests/Reference/T5Reference.cs` (reads `HP285G3_ComboAM4v2_T5.bin` from `AM4EVER_TEST_ROMS`, skips if absent)

**Interfaces:**
- Consumes: `BuildPipeline`, the bundled HP profile and ASRock adapter, M2 decompression.

- [ ] **Step 1: Write the failing test**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class Hp285G3ReferenceTests
{
    [RomFact] public void ReproducesT5BootVolumeAndSmbios()
    {
        var oem    = File.ReadAllBytes(TestRoms.HpF65!);
        var donor  = File.ReadAllBytes(TestRoms.AsrockP1043!);
        var t5      = File.ReadAllBytes(T5Reference.Path!);   // skips if missing

        var result = BuildPipeline.Build(donor,                 // donor is the ASRock base
            oem, BundledData.LoadProfile("hp-285-g3-8434"),
            BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"), dmiOverride: null);

        // 1. decompressed boot/PEI volume identical to T5's
        Assert.Equal(DecompressPei(t5), DecompressPei(result.Image));
        // 2. SMBIOS static table identical to T5's (generic HP identity, no --dmi)
        Assert.Equal(SmbiosBytes(t5), SmbiosBytes(result.Image));
        // 3. all AMD directory checksums valid in the built image
        Assert.All(AmdDirectory.FindAll(RomImage.Load(result.Image)), d => Assert.True(d.ChecksumValid));
    }
}
```

`DecompressPei` decompresses the `0xE02000` blob and returns the FV bytes; `SmbiosBytes` locates and returns the SMBIOS static-data region. Both are small local helpers over the M2 core.

- [ ] **Step 2:** Run → FAIL (pipeline not yet producing a match).
- [ ] **Step 3:** Make it pass. The PEI volume is reproduced byte-for-byte (the edits are deterministic); compressed bytes may differ (§11) but the decompressed volume must be identical. If a diff appears, bisect by operation: compare each OEM PEIM image section between the built image and T5 and find the first differing table. Do not weaken the assertion to make it pass.
- [ ] **Step 4:** Run the whole suite `dotnet test` with and without `AM4EVER_TEST_ROMS`. Both green (reference tests run / skip).
- [ ] **Step 5:** Commit: `test(core): HP 285 G3 semantic reference test (PEI + SMBIOS)`.
- [ ] **Step 6:** Hand over: report the test counts for both modes; note that the EC-fan driver (`add_ec_init`), the emulator post-checks, and the full T5 reference test are Milestone 4, and that an M3-built image carries HP's configuration and identity but not yet the EC fan hand-off.
