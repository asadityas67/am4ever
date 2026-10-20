# am4ever Milestone 2: ROM Core

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A tested C# library in `Am4ever.Core` that reads, edits and rewrites AMD AM4 AMI Aptio V ROM images: AMD directories, the compressed boot volume, UEFI firmware volumes, files and sections. It also adds a read-only `am4ever inspect <rom>` command. This is the C# port of the Python `amdrom` helpers used for the HP 285 G3 work. Milestone 3 builds board operations on top of it.

**Architecture:** Pure, UI-free code under `src/Am4ever.Core/Rom/`, operating on a mutable `byte[]` image.
- **Parsers** return small record types holding offsets into that buffer.
- **Editors** rewrite the buffer in place and fix every checksum they disturb.
- **Compression** sits behind two codecs.

Tests are of two kinds:
- **Synthetic unit tests** build tiny structures in memory.
- **Reference tests** run against the developer's local ROM files, found through the `AM4EVER_TEST_ROMS` environment variable, and are skipped when it is not set. ROM files are never committed.

**Tech Stack:**
- C# on .NET 8 (`net8.0`)
- `System.IO.Compression.ZLibStream` for zlib
- SharpCompress `0.38.0` (MIT licence) for LZMA encode/decode
- xUnit 2.9.2

**Spec:** `G:\am4ever\docs\design.md` §5.1 (Core ROM model), §10 (verification), §10.1 (no firmware in repo), §11 (reference test).

**Branch:** `m2-rom-core`, cut from `m1-shell`. Milestone 1 is kept un-merged at the user's request.

## Global Constraints

- **Inherited from M1:** `net8.0`, nullable enabled, warnings as errors, the SPDX header on every `.cs` file, no network access, and no tracked file over 262144 bytes.
- **Signature-string policy (new, from the M1 review):** C# source must not contain the five ASCII strings the repo hygiene test scans for. Write each magic as a little-endian `uint` constant, with a short comment naming it. Values, each the 4 ASCII bytes read little-endian:

  | Magic | uint constant |
  |---|---|
  | AMD PSP dir | `0x50535024` |
  | AMD PSP L2 dir | `0x324C5024` |
  | AMD BIOS dir | `0x44484224` |
  | AMD BIOS L2 dir | `0x324C4224` |
  | FV header | `0x4856465F` |

- **No ROM bytes in tests.** Reference tests read files named `ASRock_AB350MPro4_P10.43.bin` and `HP_08434_F65_raw.bin` from the folder in `AM4EVER_TEST_ROMS` (locally `G:\HPBIOS\analysis`), and skip with a reason when a file is missing.
- **Editors never move data they do not own.** In-place edits keep the same size, or grow into existing free space. If something does not fit, they throw `RomEditException` with a plain message and leave the image unchanged. They never truncate.
- **Checksums:** FFS header checksum is an 8-bit sum with the state byte treated as zero; FFS data checksum is an 8-bit sum when attribute bit `0x40` is set, else the byte `0xAA`; the AMD directory checksum is Fletcher-32 over all bytes after the checksum field.
- **LZMA format:** the EDK2 "LZMA alone" layout — 5 property bytes, then the uncompressed size as a little-endian `UInt64`, then the stream. Encoding uses property byte `0x5D`, dictionary size `0x01000000`, and the true uncompressed size (never `-1`).

## Review Focus

Most likely first:

1. **A truncated or non-AM4 file** (a 4 MB image, random bytes). Expected: `RomImage.Load` and the parsers throw `RomFormatException` with a useful message; never an `IndexOutOfRangeException` and never garbage. Test `Load_TruncatedImage_ThrowsFormatError` (Task 1).
2. **A corrupt AMD directory** (bad checksum or an absurd entry count). Expected: it is reported invalid and skipped by `FindAll`, not trusted. Test `FindAll_SkipsDirectoryWithBadChecksum` (Task 2).
3. **A volume whose last file runs past the volume end, or a file whose size field is 0.** Expected: enumeration stops with `RomFormatException`; no infinite loop. Test `Files_StopsOnZeroSizeFile` (Task 4).
4. **Recompressed data that no longer fits its slot.** Expected: `RomEditException` naming the region and the two sizes; the image is left byte-for-byte unchanged. Test `ReplaceSection_TooBig_LeavesImageUntouched` (Task 6).
5. **Appending a file where free space is too small or not all 0xFF.** Expected: `RomEditException`, nothing written. Test `AppendFile_NoRoom_Throws` (Task 5).

---

### Task 1: ROM image, errors and reference-ROM test support

**Files:**
- Create: `src/Am4ever.Core/Rom/RomImage.cs`, `src/Am4ever.Core/Rom/RomExceptions.cs`, `src/Am4ever.Core/Rom/Bin.cs`
- Create: `tests/Am4ever.Tests/Rom/RomFactAttribute.cs`, `tests/Am4ever.Tests/Rom/TestRoms.cs`
- Test: `tests/Am4ever.Tests/Rom/RomImageTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class RomImage` — `static RomImage Load(byte[] data)` accepting only 8/16/32 MiB; `byte[] Data { get; }` (the mutable edit target); `int Size { get; }`; `int ToOffset(ulong location)` mapping flat offsets and `0xFFxxxxxx`-style addresses to file offsets (location masked with `0x3FFF_FFFF_FFFF_FFFF`), throwing `RomFormatException` if the result is outside the image.
  - `public class RomFormatException : Exception` and `public class RomEditException : Exception`.
  - `public static class Bin`: little-endian helpers over `byte[]` with bounds checks that throw `RomFormatException` — `U8 U16 U24 U32 U64 Guid(byte[],int)` and `W16 W24 W32 W64`.
  - Test side: `TestRoms.AsrockP1043` and `TestRoms.HpF65` (full path or null); `[RomFact]`, a `FactAttribute` that sets `Skip` when `AM4EVER_TEST_ROMS` or either file is missing.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class RomImageTests
{
    [Fact] public void Load_TruncatedImage_ThrowsFormatError() =>
        Assert.Throws<RomFormatException>(() => RomImage.Load(new byte[4 * 1024 * 1024 + 7]));

    [Fact] public void ToOffset_MapsHighAddressesIntoImage()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        Assert.Equal(0xE02000, rom.ToOffset(0xFFE02000));
        Assert.Equal(0xE02000, rom.ToOffset(0xE02000));
        Assert.Throws<RomFormatException>(() => rom.ToOffset(0x8000_0000));
    }

    [Fact] public void Bin_ReadPastEnd_ThrowsFormatError() =>
        Assert.Throws<RomFormatException>(() => Bin.U32(new byte[3], 0));

    [RomFact] public void ReferenceRomsLoad()
    {
        Assert.Equal(16 * 1024 * 1024, RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!)).Size);
        Assert.Equal(16 * 1024 * 1024, RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!)).Size);
    }
}
```

- [ ] **Step 2:** Run `dotnet test --filter RomImageTests` → FAIL (types missing).
- [ ] **Step 3:** Implement the types above. `ToOffset`: mask the address; if `>= 0xFF000000` return `location - (0x100000000 - Size)`; else `location & (Size-1)`; range-check the result.
- [ ] **Step 4:** With `AM4EVER_TEST_ROMS=G:\HPBIOS\analysis` → 4 passed. Without it → 3 passed, 1 skipped. Confirm both.
- [ ] **Step 5:** Commit: `feat(core): ROM image, binary readers, reference-ROM test support`.

### Task 2: AMD PSP/BIOS directories

**Files:**
- Create: `src/Am4ever.Core/Rom/Amd/AmdDirectory.cs`, `src/Am4ever.Core/Rom/Amd/Fletcher32.cs`
- Test: `tests/Am4ever.Tests/Rom/AmdDirectoryTests.cs`

**Interfaces:**
- Consumes: `RomImage`, `Bin` (Task 1).
- Produces:
  - `public static class Fletcher32 { public static uint Compute(ReadOnlySpan<byte> data); }` — two 16-bit running sums seeded at `0xFFFF`, folded per the AMD scheme, result `(c1 << 16) | c0`.
  - `public enum AmdDirKind { Psp, PspL2, Bios, BiosL2 }`.
  - `public sealed record AmdEntry(int Index, int EntryOffset, byte Type, byte SubProgram, byte RegionId, uint Flags, uint Size, ulong Location)` — the BIOS directories use 24-byte entries (type/region/flags/sub bytes, then size, location, destination); the PSP directories use 16-byte entries (type uint, size, location). `IsBios` selects the layout.
  - `public sealed class AmdDirectory` — `ctor(RomImage rom, int offset)` parsing the header there (throwing `RomFormatException` if the magic is not one of the four); `AmdDirKind Kind`; `int DirOffset`; `int Count`; `bool ChecksumValid`; `IReadOnlyList<AmdEntry> Entries`; `void FixChecksum(RomImage rom)` recomputing the Fletcher-32 over the body; `static IReadOnlyList<AmdDirectory> FindAll(RomImage rom)` scanning on 0x100-byte boundaries, accepting only directories whose count is 1..63 and whose checksum is valid.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class AmdDirectoryTests
{
    [Fact] public void FixChecksum_RoundTripsThroughValidation()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        WriteDir(rom, 0x1000, PspMagic, count: 3, goodChecksum: false);
        var dir = new AmdDirectory(rom, 0x1000);
        Assert.False(dir.ChecksumValid);
        dir.FixChecksum(rom);
        Assert.True(new AmdDirectory(rom, 0x1000).ChecksumValid);
    }

    [Fact] public void FindAll_SkipsDirectoryWithBadChecksum()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        WriteDir(rom, 0x1000, PspMagic, count: 2, goodChecksum: true);
        WriteDir(rom, 0x2000, PspMagic, count: 2, goodChecksum: false);
        var dirs = AmdDirectory.FindAll(rom);
        Assert.Single(dirs);
        Assert.Equal(0x1000, dirs[0].DirOffset);
    }

    [RomFact] public void Asrock_HasComboDirectories()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var kinds = AmdDirectory.FindAll(rom).Select(d => d.Kind).ToList();
        Assert.Contains(AmdDirKind.Psp, kinds);
        Assert.Contains(AmdDirKind.Bios, kinds);
        Assert.True(AmdDirectory.FindAll(rom).Count >= 10);   // combo image: many directories
    }
}
```

`WriteDir` and `PspMagic` are a local test helper and the `0x50535024` constant; `goodChecksum:false` writes a deliberately wrong checksum word.

- [ ] **Step 2:** Run `dotnet test --filter AmdDirectoryTests` → FAIL.
- [ ] **Step 3:** Implement `Fletcher32`, `AmdDirectory`, `AmdEntry`. The known ASRock image has 14 directories; the HP image has 9 (reference values for the `[RomFact]`).
- [ ] **Step 4:** Run with and without `AM4EVER_TEST_ROMS` → all green / reference ones skipped.
- [ ] **Step 5:** Commit: `feat(core): AMD PSP/BIOS directory parsing with Fletcher-32`.

### Task 3: AMD zlib boot-volume codec

**Files:**
- Create: `src/Am4ever.Core/Rom/Compression/PspZlib.cs`
- Test: `tests/Am4ever.Tests/Rom/PspZlibTests.cs`

**Interfaces:**
- Produces:
  - `public static class PspZlib` — `static (byte[] Header, byte[] Plain) Decompress(ReadOnlySpan<byte> blob)` reading the 0x100-byte header, taking the compressed length from offset 0x14, and inflating the zlib stream; `static byte[] Compress(byte[] header, ReadOnlySpan<byte> plain)` deflating and writing the new length back into a copy of the header at 0x14.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class PspZlibTests
{
    [Fact] public void RoundTrip_PreservesPlainBytes()
    {
        var header = new byte[0x100];
        var plain = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
        var blob = PspZlib.Compress(header, plain);
        var (_, back) = PspZlib.Decompress(blob);
        Assert.Equal(plain, back);
    }

    [RomFact] public void DecompressesAsrockBootVolume()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var blob = rom.Data.AsSpan(0xE02000, 0xD57C6).ToArray();
        var (_, pei) = PspZlib.Decompress(blob);
        // The PEI firmware volume sits 0x20 bytes into the decompressed blob.
        Assert.Equal(0x4856465Fu, Bin.U32(pei, 0x20 + 40));   // FV signature
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement with `ZLibStream`. The known boot blob at `0xE02000` has compressed length `0xD57C6` and decompresses to `0x997010` bytes (reference values).
- [ ] **Step 4:** Run with/without the env var.
- [ ] **Step 5:** Commit: `feat(core): AMD PSP zlib boot-volume codec`.

### Task 4: Firmware volumes, FFS files and sections

**Files:**
- Create: `src/Am4ever.Core/Rom/Fv/FirmwareVolume.cs`, `src/Am4ever.Core/Rom/Fv/FfsFile.cs`, `src/Am4ever.Core/Rom/Fv/FvSection.cs`
- Test: `tests/Am4ever.Tests/Rom/FirmwareVolumeTests.cs`

**Interfaces:**
- Consumes: `RomImage`, `Bin`.
- Produces:
  - `public sealed class FirmwareVolume` — `ctor(byte[] buffer, int offset)` validating the FV signature at offset+40; `long Length`; `int End`; `IEnumerable<FfsFile> Files()` (stops at the first all-0xFF header, throws `RomFormatException` on a zero-size or past-end file); `FfsFile FindByGuid(Guid)`; `IReadOnlyList<FfsFile> FindByName(string)`.
  - `public sealed class FfsFile` — `Guid Guid`; `byte Type`; `byte Attributes`; `long Size`; `int HeaderLength` (24, or 32 for the large-file attribute); `int DataOffset`; `int End`; `string Name()` (UI section text, else `""`); `IReadOnlyList<FvSection> Sections()`; `(int start, int end) ImageSection()` (the PE32/TE body); `void FixChecksums(byte[] buffer)` recomputing header and, when bit 0x40 is set, data checksums.
  - `public sealed record FvSection(int Offset, byte Type, int Size, int HeaderLength, int DataOffset, int End, Guid? GuidName, int? DataOffsetInGuid)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class FirmwareVolumeTests
{
    [Fact] public void Files_StopsOnZeroSizeFile()
    {
        var buf = SyntheticFv.WithOneFile(out var fileGuid);
        SyntheticFv.CorruptFirstFileSizeToZero(buf);
        var fv = new FirmwareVolume(buf, 0);
        Assert.Throws<RomFormatException>(() => fv.Files().ToList());
    }

    [Fact] public void FixChecksums_MakesHeaderChecksumValid()
    {
        var buf = SyntheticFv.WithOneFile(out _);
        var fv = new FirmwareVolume(buf, 0);
        var file = fv.Files().First();
        buf[file.DataOffset] ^= 0xFF;          // disturb the body
        file.FixChecksums(buf);
        Assert.True(SyntheticFv.HeaderChecksumValid(buf, file));
    }

    [RomFact] public void FindsOemModulesInHpPeiVolume()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!));
        var fv = new FirmwareVolume(rom.Data, 0xC00000);
        var names = fv.Files().Select(f => f.Name()).Where(n => n.StartsWith("AmdCpmOem")).ToList();
        Assert.Contains("AmdCpmOemInitPeim", names);
        Assert.Contains("AmdCpmOemRvInitPeim", names);
    }
}
```

`SyntheticFv` is a test helper that builds a minimal valid FFSv2 volume with a single raw file.

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the three types. Section walking is 4-byte aligned; the extended-size form uses `0xFFFFFF` then a uint. The HP PEI volume at `0xC00000` has 123 files and holds `AmdCpmOemInitPeim` and `AmdCpmOemRvInitPeim` (reference values).
- [ ] **Step 4:** Run with/without the env var.
- [ ] **Step 5:** Commit: `feat(core): firmware volume, FFS file and section parsing`.

### Task 5: FFS editing — in-place replace and append

**Files:**
- Create: `src/Am4ever.Core/Rom/Fv/FvEditor.cs`
- Test: `tests/Am4ever.Tests/Rom/FvEditorTests.cs`

**Interfaces:**
- Consumes: `FirmwareVolume`, `FfsFile`.
- Produces:
  - `public static class FvEditor` — `static void ReplaceFileBody(byte[] buffer, FfsFile file, ReadOnlySpan<byte> body)` requiring the same length and fixing checksums; `static void AppendRawFile(byte[] buffer, FirmwareVolume fv, Guid guid, ReadOnlySpan<byte> body)` placing a new file in the trailing free space, which must be all 0xFF and large enough, else `RomEditException`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class FvEditorTests
{
    [Fact] public void AppendFile_ThenReParseFindsIt()
    {
        var buf = SyntheticFv.WithFreeSpace(0x4000, out _);
        var fv = new FirmwareVolume(buf, 0);
        var g = Guid.NewGuid();
        FvEditor.AppendRawFile(buf, fv, g, new byte[] { 1, 2, 3, 4 });
        Assert.NotNull(new FirmwareVolume(buf, 0).FindByGuid(g));
    }

    [Fact] public void AppendFile_NoRoom_Throws()
    {
        var buf = SyntheticFv.WithFreeSpace(0x20, out _);
        var fv = new FirmwareVolume(buf, 0);
        Assert.Throws<RomEditException>(() => FvEditor.AppendRawFile(buf, fv, Guid.NewGuid(), new byte[0x1000]));
    }

    [Fact] public void ReplaceFileBody_WrongLength_Throws()
    {
        var buf = SyntheticFv.WithOneFile(out _);
        var file = new FirmwareVolume(buf, 0).Files().First();
        Assert.Throws<RomEditException>(() => FvEditor.ReplaceFileBody(buf, file, new byte[9999]));
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement. Append writes a 24-byte FFS header plus body, 8-byte aligned, and fixes checksums; it verifies the destination span is all 0xFF first.
- [ ] **Step 4:** Run.
- [ ] **Step 5:** Commit: `feat(core): in-place FFS replace and append into free space`.

### Task 6: LZMA section codec and compressed-volume replace

**Files:**
- Create: `src/Am4ever.Core/Rom/Compression/EdkLzma.cs`, `src/Am4ever.Core/Rom/Fv/GuidedSection.cs`
- Modify: `src/Am4ever.Core/Am4ever.Core.csproj` (add lzma-sdk 22.1.1)
- Test: `tests/Am4ever.Tests/Rom/EdkLzmaTests.cs`

**Interfaces:**
- Produces:
  - `public static class EdkLzma` — `static byte[] Decompress(ReadOnlySpan<byte> lzma)` reading the 5 property bytes and the UInt64 size, then decoding; `static byte[] Compress(ReadOnlySpan<byte> plain)` emitting props `0x5D`, dictionary `0x01000000`, the true size, and the stream.
  - `public static class GuidedSection` — `static byte[] DecompressLzmaGuided(byte[] buffer, FvSection section)`; `static void ReplaceLzmaGuided(byte[] buffer, FfsFile containerFile, FvSection section, ReadOnlySpan<byte> newPlain)` recompressing, checking the new section fits inside the container file's slot (else `RomEditException` with both sizes, image untouched), writing it, padding the remainder with 0xFF, and fixing the container file's checksums.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class EdkLzmaTests
{
    [Fact] public void RoundTrip_PreservesPlainBytes()
    {
        var plain = Enumerable.Range(0, 20000).Select(i => (byte)(i ^ (i >> 3))).ToArray();
        Assert.Equal(plain, EdkLzma.Decompress(EdkLzma.Compress(plain)));
    }

    [Fact] public void Compress_WritesTrueSizeNotMinusOne()
    {
        var plain = new byte[1234];
        var lzma = EdkLzma.Compress(plain);
        Assert.Equal((ulong)plain.Length, Bin.U64(lzma, 5));
    }

    [Fact] public void ReplaceSection_TooBig_LeavesImageUntouched()
    {
        var buf = SyntheticLzma.ContainerWithPlain(new byte[64], slot: 0x200, out var file, out var section);
        var before = (byte[])buf.Clone();
        var huge = Enumerable.Range(0, 0x4000).Select(i => (byte)i).ToArray();
        Assert.Throws<RomEditException>(() => GuidedSection.ReplaceLzmaGuided(buf, file, section, huge));
        Assert.Equal(before, buf);
    }

    [RomFact] public void DecompressesAsrockDxeVolume()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var fv = new FirmwareVolume(rom.Data, 0x94F000);
        var file = fv.FindByGuid(Guid.Parse("9e21fd93-9c72-4c15-8c4b-e77f1db2d792"));
        var section = file.Sections()[0];
        var plain = GuidedSection.DecompressLzmaGuided(rom.Data, section);
        Assert.True(plain.Length > 0x900000);     // the DXE volume is ~9 MiB
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Add lzma-sdk 22.1.1 and implement. The ASRock DXE container (file GUID `9e21fd93-…`, LZMA section GUID `ee4e5898-…` at `0x94F000`) decodes to about 9 MiB (reference value). Re-encoded bytes need not match the original exactly (§11: the check is semantic).
- [ ] **Step 4:** Run with/without the env var.
- [ ] **Step 5:** Commit: `feat(core): EDK LZMA codec and guided-section replace`.

### Task 7: `am4ever inspect` command

**Files:**
- Create: `src/Am4ever.Cli/Commands/InspectCommand.cs`
- Modify: `src/Am4ever.Cli/CliApp.cs` (route `inspect`)
- Test: `tests/Am4ever.Tests/Cli/InspectCommandTests.cs`

**Interfaces:**
- Consumes: the whole ROM core.
- Produces:
  - `public static class InspectCommand` — `static int Run(string romPath, IConsoleIo io)` printing, to `Out`: the size, the AMD directory list (kind, offset, entry count), and the AGESA string if found; returning `0` on success, `1` if the file is missing or not a valid AM4 image (a caught `RomFormatException`, message to `Err`).
  - `CliApp.Run` routes `inspect <path>` to it. `inspect` reads only; it prints the risk banner like every command but needs no acknowledgement.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class InspectCommandTests
{
    [Fact] public void Inspect_MissingFile_ReturnsOne()
    {
        var io = FakeIo.Create();
        Assert.Equal(1, CliApp.Run(new[] { "inspect", "does-not-exist.bin" }, io));
    }

    [Fact] public void Inspect_RandomBytes_ReportsNotValid()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[1024]);
        var io = FakeIo.Create();
        Assert.Equal(1, CliApp.Run(new[] { "inspect", path }, io));
        Assert.Contains("not a valid", io.ErrText);
    }

    [RomFact] public void Inspect_AsrockRom_ListsDirectories()
    {
        var io = FakeIo.Create();
        Assert.Equal(0, CliApp.Run(new[] { "inspect", TestRoms.AsrockP1043! }, io));
        Assert.Contains("Bios", io.OutText);
        Assert.Contains("16 MiB", io.OutText);
    }
}
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement; `inspect` with no path is a usage error (64).
- [ ] **Step 4:** Run the whole suite: `dotnet test`. With `AM4EVER_TEST_ROMS` set, every reference test runs; without it, they skip. Both must be green.
- [ ] **Step 5:** Commit: `feat(cli): read-only inspect command`.
- [ ] **Step 6:** Hand over: report the test counts for both modes, and note that the reference tests need `AM4EVER_TEST_ROMS` pointed at a folder of the user's own ROM files.
