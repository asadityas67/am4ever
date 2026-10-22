# Donor-adapter authoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Board Lab tool that auto-detects a donor adapter's fields from a donor ROM and lets an expert finish the override sites, exporting a shareable donor-adapter JSON.

**Architecture:** An `AdapterWriter` (serialize `DonorAdapter`), an `AdapterDraft` (mutable authoring model), an `AdapterDetector` (reuse the analysis code to fill what it can), and a Board Lab UI — the donor-side analogue of the profile composer.

**Tech Stack:** C#/.NET 8, Avalonia, System.Text.Json, xUnit.

**Spec:** docs/superpowers/specs/2026-10-22-adapter-authoring-design.md

## Global Constraints
- GPL-2.0-or-later header on new files.
- Output adapters carry no firmware: only offsets/GUIDs/SHA and the ≤64-byte override `expected` strings `ProfileLoader` caps.
- The composer must only emit adapters `ProfileLoader.LoadAdapter` accepts.
- No `DonorAdapter` schema change; no change to how builds consume adapters.
- Detection is best-effort and honest: an undetermined field is blank/marked, never guessed silently.
- UI-free cores are plain xUnit; views `[AvaloniaFact]`; donor-ROM detection `[RomFact]`.

## Review Focus
- **Unloadable adapter:** every draft the UI can produce must round-trip through `ProfileLoader.LoadAdapter` — Task 1 round-trip + Task 2 ToAdapter→LoadAdapter.
- **Override hex parse:** a bad `expected` hex string must surface as an error, not a crash or a silent empty patch — Task 2 tests an invalid hex throws on `ToAdapter`.
- **Detector honesty:** when the boot blob can't be found, the volume fields are left blank, not zero-pretending-detected — Task 3 asserts a non-donor buffer yields a draft with the SHA set and volumes blank (no throw).
- **Detector correctness on the real donor:** `[RomFact]` — detects amd-zlib, the boot-blob offset and DXE GUID matching the bundled adapter.
- **Export of a half-filled draft:** a draft with empty overrides still exports loadable JSON (overrides are optional) — Task 2/Task 4.

---

### Task 1: Core — AdapterWriter

**Files:**
- Create: `src/Am4ever.Core/Profile/AdapterWriter.cs`
- Test: `tests/Am4ever.Tests/Profile/AdapterWriterTests.cs`

**Interfaces:**
- Produces: `public static class AdapterWriter { public static byte[] Write(DonorAdapter adapter); }` — UTF-8 JSON, indented, nulls skipped, `SnakeCaseLower`, the shared `HexBytesConverter`; parses back through `ProfileLoader.LoadAdapter`.

- [ ] **Step 1: Write the failing test**

```csharp
public class AdapterWriterTests
{
    [Fact] public void Write_RoundTripsThroughLoader()
    {
        var original = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var json = AdapterWriter.Write(original);
        var reloaded = ProfileLoader.LoadAdapter(json);
        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(original.Identity.Sha256, reloaded.Identity.Sha256);
        Assert.Equal(original.Overrides.Select(o => o.Offset), reloaded.Overrides.Select(o => o.Offset));
        Assert.Equal(original.Volumes.DxeFileGuid, reloaded.Volumes.DxeFileGuid);
        Assert.Equal(json, AdapterWriter.Write(reloaded));   // idempotent
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Run: `dotnet test --filter FullyQualifiedName~AdapterWriterTests` · Expected: FAIL (`AdapterWriter` missing).
- [ ] **Step 3: Implement** — same options as `ProfileWriter` (`SnakeCaseLower`, `WriteIndented`, `WhenWritingNull`, `new HexBytesConverter()`), `SerializeToUtf8Bytes(adapter, options)`.
- [ ] **Step 4: Run tests to verify they pass** — Expected: PASS.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.Core/Profile/AdapterWriter.cs tests/Am4ever.Tests/Profile/AdapterWriterTests.cs
git commit -m "feat(core): AdapterWriter serialises a donor adapter to loadable JSON"
```

---

### Task 2: Core — AdapterDraft

**Files:**
- Create: `src/Am4ever.Core/Profile/AdapterDraft.cs`
- Test: `tests/Am4ever.Tests/Profile/AdapterDraftTests.cs`

**Interfaces:**
- Produces: `public sealed record OverrideDraft(string Family, int Offset, string ExpectedHex);`
- Produces: `public sealed class AdapterDraft` with mutable `Id`, `Vendor`, `Board`, `Version`, `Sha256`, `List<ModuleEntry> Modules`, `List<OverrideDraft> Overrides`, `string? Compression`, `int? BootBlobOffset`, `int? BootBlobLength`, `int? BootFvOffset`, `int? DxeFvOffset`, `string? DxeFileGuid`, `List<string> Hazards`, `string? DownloadUrl`; `DonorAdapter ToAdapter()` (overrides → `OverrideSig(Family, Offset, Convert.FromHexString(ExpectedHex))`; volumes from the nullable ints defaulting 0); `static AdapterDraft FromAdapter(DonorAdapter a)`. `ToAdapter` throws `RomFormatException` on an invalid `ExpectedHex`.

- [ ] **Step 1: Write the failing test**

```csharp
public class AdapterDraftTests
{
    [Fact] public void FromAdapter_ToAdapter_PreservesOverridesAndVolumes()
    {
        var a = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var round = AdapterDraft.FromAdapter(a).ToAdapter();
        Assert.Equal(a.Overrides.Select(o => o.Offset), round.Overrides.Select(o => o.Offset));
        Assert.Equal(a.Volumes.DxeFileGuid, round.Volumes.DxeFileGuid);
        Assert.Equal(a.Volumes.BootBlobOffset, round.Volumes.BootBlobOffset);
    }

    [Fact] public void ToAdapter_InvalidOverrideHex_Throws()
    {
        var d = new AdapterDraft { Id = "x" };
        d.Overrides.Add(new OverrideDraft("ZP", 10, "zzzz"));
        Assert.ThrowsAny<System.Exception>(() => d.ToAdapter());
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (`AdapterDraft` missing).
- [ ] **Step 3: Implement** `OverrideDraft`, `AdapterDraft`, `ToAdapter` (wrap `Convert.FromHexString` failure in `RomFormatException`), `FromAdapter` (override bytes → `Convert.ToHexString`).
- [ ] **Step 4: Run tests to verify they pass** — Expected: PASS.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.Core/Profile/AdapterDraft.cs tests/Am4ever.Tests/Profile/AdapterDraftTests.cs
git commit -m "feat(core): mutable AdapterDraft with DonorAdapter round-trip"
```

---

### Task 3: App — AdapterDetector

**Files:**
- Create: `src/Am4ever.App/Lab/AdapterDetector.cs`
- Test: `tests/Am4ever.Tests/Lab/AdapterDetectorTests.cs`

**Interfaces:**
- Produces: `public static class AdapterDetector { public static AdapterDraft Detect(byte[] donorRom); }` — sets `Sha256`; best-effort `volumes` (compression + boot-blob offset/length + boot-FV offset via an uncompressed-FV-or-decompress-scan; `dxe_fv_offset`/`dxe_file_guid` by scanning FVs for an LZMA GUID-defined container); `modules` (CPM OEM PEIM names + indices + a suggested family); identity vendor/board/version from SMBIOS if readable. Undetermined fields stay null; `overrides` stays empty. Reuses `AmdDirectory`/`PspZlib`/`FirmwareVolume`/`GuidedSection`/`SmbiosTable`.

- [ ] **Step 1: Write the failing test**

```csharp
public class AdapterDetectorTests
{
    [Fact] public void Detect_NonDonor_SetsShaLeavesVolumesBlank()
    {
        var d = AdapterDetector.Detect(new byte[16 * 1024 * 1024]);
        Assert.False(string.IsNullOrEmpty(d.Sha256));
        Assert.Null(d.DxeFileGuid);              // nothing found → blank, not guessed
        Assert.Empty(d.Overrides);
    }

    [RomFact] public void Detect_RealAsrockDonor_MatchesBundledAdapter()
    {
        var want = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var got = AdapterDetector.Detect(File.ReadAllBytes(TestRoms.AsrockP1043!)).ToAdapter();
        Assert.Equal(want.Identity.Sha256, got.Identity.Sha256);
        Assert.Equal(want.Volumes.DxeFileGuid, got.Volumes.DxeFileGuid);
        Assert.Equal(want.Volumes.BootBlobOffset, got.Volumes.BootBlobOffset);
        Assert.Contains(got.Modules, m => m.Name.StartsWith("AmdCpmOem"));
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (`AdapterDetector` missing) / builds.
- [ ] **Step 3: Implement** `Detect`. Boot blob: if an FV sits at a scanned offset use it uncompressed; else scan candidate offsets and accept the one whose `PspZlib.Decompress` yields an FV containing a CPM OEM PEIM (set compression `amd-zlib`, offsets, length). DXE: scan FV signatures, pick the FV whose container file has an LZMA GUID-defined section. Modules + family suggestion per the design. Leave unknowns null. **Ruling allowed:** if a field proves infeasible to detect reliably, leave it null and relax that assertion with a ledgered ruling.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter FullyQualifiedName~AdapterDetectorTests` · Expected: PASS (ROM test skips without ROMs).
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/AdapterDetector.cs tests/Am4ever.Tests/Lab/AdapterDetectorTests.cs
git commit -m "feat(app): auto-detect donor-adapter fields from a donor ROM"
```

---

### Task 4: App — Board Lab adapter authoring UI

**Files:**
- Modify: `src/Am4ever.App/Lab/BoardLabView.axaml.cs`
- Test: `tests/Am4ever.Tests/App/BoardLabViewTests.cs` (add tests)

**Interfaces:**
- Consumes: `AdapterDetector`, `AdapterDraft`, `AdapterWriter`, the loaded donor ROM bytes, toasts.
- Produces: `BoardLabView` gains an Adapter Authoring section with public controls for tests — `public Button DetectAdapterButton { get; }`, `public Button ExportAdapterButton { get; }`, `public Button AddOverrideButton { get; }` — and an adapter JSON preview. `DetectAdapterButton` is disabled until a donor ROM is loaded.

- [ ] **Step 1: Write the failing tests**

```csharp
[AvaloniaFact] public void AdapterAuthoring_DetectDisabledUntilDonorLoaded()
{
    var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
    Assert.NotNull(view.DetectAdapterButton);
    Assert.False(view.DetectAdapterButton.IsEnabled);
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (members missing).
- [ ] **Step 3: Implement** the Adapter Authoring section: capture the donor bytes on donor load (enable Detect); **Detect from donor** → `AdapterDetector.Detect` populates an `AdapterDraft`; a form for id/identity/volumes; the modules list with editable family; an overrides editor (`AddOverrideButton` adds a family/offset/expected row); a JSON preview via `AdapterWriter.Write(draft.ToAdapter())` (catch + show parse errors); **Export/Import** via file pickers with a toast. Keep the existing analyse/compose/inspection UI.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter FullyQualifiedName~BoardLabViewTests` · Expected: PASS.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/BoardLabView.axaml.cs tests/Am4ever.Tests/App/BoardLabViewTests.cs
git commit -m "feat(app): donor-adapter authoring UI in Board Lab"
```

---

### Task 5: Docs + full suite

**Files:**
- Modify: `docs/manual/board-lab.md`
- Test: whole solution.

- [ ] **Step 1: Document** the adapter authoring flow in board-lab.md (detect from donor → fill overrides → export), and that it unlocks new donors (overrides are the expert's part).
- [ ] **Step 2: Release build** — `dotnet build -c Release` · Expected: succeeds.
- [ ] **Step 3: Full suite** — `dotnet test` · Expected: all pass; ROM tests skip without fixtures. Record counts in the ledger.
- [ ] **Step 4: Commit**
```bash
git add docs/manual/board-lab.md
git commit -m "docs: donor-adapter authoring in the Board Lab manual"
```
