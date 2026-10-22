# Profile composer (expert mode) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An expert-mode workbench that authors and tests a shareable board-profile JSON against an already-supported donor, with lightweight read-only hex/disassembly views and a warning-gated editable table view.

**Architecture:** A UI-free `ComposerCore` holds a mutable `ProfileDraft`, discovers suggested operations from the loaded OEM + donor + adapter (`ProfileDiscovery` over the existing `BoardAnalysis`), renders JSON with a new `ProfileWriter` (the loader's `System.Text.Json` options in reverse), and test-builds via `BuildPipeline`. The Board Lab view is a thin shell that reuses the Win98 segmented bar and toast service. Inspection views (hex, Iced disassembly, parsed tables) have pure-logic cores.

**Tech Stack:** C#/.NET 8, Avalonia, `System.Text.Json`, the `Iced` managed x86 disassembler, xUnit + `Avalonia.Headless.XUnit`.

**Spec:** docs/superpowers/specs/2026-10-22-profile-composer-design.md

## Global Constraints
- GPL-2.0-or-later header on every new file.
- Profiles carry no firmware: only operations and the ≤64-byte `expected`/`new` strings `ProfileLoader` already caps.
- The composer must only emit JSON that `ProfileLoader.LoadProfile` accepts.
- Scope: author profiles against a **bundled** donor adapter; no new-adapter authoring.
- `smbios_identity` is offered only when a Core identity extractor exists for the board (today `hp-8434`); otherwise omitted with a note.
- Inspection views never mutate ROM bytes; the table view's edits produce/update profile **operations**.
- The table-edit warning shows on **every** toggle-on and is not permanently dismissable.
- win-x64 + linux-x64 must both build; `Iced` is managed (no native dep). No change to existing build output bytes (`Hp285G3ReferenceTests` still pins them).
- UI-free cores are plain xUnit; views are `[AvaloniaFact]`; full-build tests are `[RomFact]`.

## Review Focus
- **Composer emits an unloadable profile:** every draft the UI can produce must round-trip through `ProfileLoader` — Task 1's round-trip test and Task 4's "compose → ToJson → LoadProfile succeeds" test cover it.
- **Test-build on a broken draft:** an op referencing a missing table/family must surface as an error with no file written — Task 4 tests a deliberately broken draft returns `Ok=false` and writes nothing.
- **Table-edit warning suppression:** the warning must fire on every enable — Task 4 counts invocations across two enables (=2) and that a declined warning leaves editing off.
- **Import of foreign/malformed JSON:** must show an error and leave the current draft intact, not half-apply — Task 4 tests importing garbage throws/returns error and the draft is unchanged.
- **Disassembly of non-code bytes:** the Iced wrapper must not throw on arbitrary bytes (it renders `(bad)`/db) — Task 5 tests a garbage slice returns a list without throwing.

---

### Task 1: Core — ProfileWriter (JSON round-trip)

**Files:**
- Create: `src/Am4ever.Core/Profile/HexBytesConverter.cs` (moved out of `ProfileLoader`, made `internal`)
- Create: `src/Am4ever.Core/Profile/ProfileWriter.cs`
- Modify: `src/Am4ever.Core/Profile/ProfileLoader.cs` (use the moved converter; no behaviour change)
- Test: `tests/Am4ever.Tests/Profile/ProfileWriterTests.cs`

**Interfaces:**
- Produces: `internal sealed class HexBytesConverter : JsonConverter<byte[]>` (the existing one, relocated).
- Produces: `public static class ProfileWriter { public static byte[] Write(BoardProfile profile); }` — UTF-8 JSON, indented, nulls skipped, `SnakeCaseLower`, the shared hex converter; the output parses back through `ProfileLoader.LoadProfile`.

- [ ] **Step 1: Write the failing test**

```csharp
public class ProfileWriterTests
{
    [Fact] public void Write_RoundTripsThroughLoader()
    {
        var original = BundledData.LoadProfile("hp-285-g3-8434");
        var json = ProfileWriter.Write(original);
        var reloaded = ProfileLoader.LoadProfile(json);
        // compare by shape, not record identity (record lists compare by reference)
        Assert.Equal(original.Operations.Select(o => o.Op), reloaded.Operations.Select(o => o.Op));
        Assert.Equal(original.Board.BoardId, reloaded.Board.BoardId);
        Assert.Equal(original.Donor.Adapter, reloaded.Donor.Adapter);
        // idempotent: writing the reloaded profile yields identical bytes
        Assert.Equal(json, ProfileWriter.Write(reloaded));
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Run: `dotnet test --filter FullyQualifiedName~ProfileWriterTests` · Expected: FAIL (`ProfileWriter` missing).
- [ ] **Step 3: Implement** — move `HexBytesConverter` to its own `internal` file; have `ProfileLoader` keep using it; add `ProfileWriter.Write` building `JsonSerializerOptions { PropertyNamingPolicy = SnakeCaseLower, WriteIndented = true, DefaultIgnoreCondition = WhenWritingNull, Converters = { new HexBytesConverter() } }` and `JsonSerializer.SerializeToUtf8Bytes(profile, options)`.
- [ ] **Step 4: Run tests to verify they pass** — Run: `dotnet test --filter FullyQualifiedName~ProfileWriterTests` · Expected: PASS.
- [ ] **Step 5: Reload-safety for the whole suite** — Run: `dotnet test --filter FullyQualifiedName~ProfileLoaderTests` · Expected: PASS (loader behaviour unchanged by the converter move).
- [ ] **Step 6: Commit**
```bash
git add src/Am4ever.Core/Profile/HexBytesConverter.cs src/Am4ever.Core/Profile/ProfileWriter.cs src/Am4ever.Core/Profile/ProfileLoader.cs tests/Am4ever.Tests/Profile/ProfileWriterTests.cs
git commit -m "feat(core): ProfileWriter serialises a profile back to loadable JSON"
```

---

### Task 2: Core — ProfileDraft (mutable authoring model)

**Files:**
- Create: `src/Am4ever.Core/Profile/ProfileDraft.cs`
- Test: `tests/Am4ever.Tests/Profile/ProfileDraftTests.cs`

**Interfaces:**
- Consumes: `BoardProfile`, `OperationSpec`, `BoardInfo`, `BoardMatch`, `DonorRef`, `IdentitySpec` (Core).
- Produces: `public sealed class ProfileDraft` with mutable `Vendor`, `Model`, `BoardId`, `Ssid`, `BiosVersions` (`List<string>`), `AdapterId`, `IdentityExtractor` (`string?`), `Untested` (`List<string>`), and `Operations` (`List<OperationSpec>`); `BoardProfile ToProfile()` (Schema = `ProfileLoader.ProfileSchema`, `Donor = new DonorRef(AdapterId, null)`, `match.rom_size` from a settable `RomSize`); `static ProfileDraft FromProfile(BoardProfile p)`.

- [ ] **Step 1: Write the failing test**

```csharp
public class ProfileDraftTests
{
    [Fact] public void FromProfile_ToProfile_PreservesOperationSequence()
    {
        var hp = BundledData.LoadProfile("hp-285-g3-8434");
        var draft = ProfileDraft.FromProfile(hp);
        Assert.Equal(hp.Operations.Select(o => o.Op), draft.ToProfile().Operations.Select(o => o.Op));
        Assert.Equal(hp.Board.BoardId, draft.ToProfile().Board.BoardId);
    }

    [Fact] public void AddOperation_AppearsInProfile()
    {
        var d = new ProfileDraft { Vendor = "Acme", Model = "X", BoardId = "1", AdapterId = "asrock-ab350m-pro4-p10.43", RomSize = 16 << 20 };
        d.Operations.Add(new OperationSpec("apcb_check"));
        Assert.Equal("apcb_check", Assert.Single(d.ToProfile().Operations).Op);
        Assert.Equal(ProfileLoader.ProfileSchema, d.ToProfile().Schema);
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (`ProfileDraft` missing).
- [ ] **Step 3: Implement** `ProfileDraft`, `ToProfile`, `FromProfile`.
- [ ] **Step 4: Run tests to verify they pass** — `dotnet test --filter FullyQualifiedName~ProfileDraftTests` · Expected: PASS.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.Core/Profile/ProfileDraft.cs tests/Am4ever.Tests/Profile/ProfileDraftTests.cs
git commit -m "feat(core): mutable ProfileDraft with BoardProfile round-trip"
```

---

### Task 3: App — ProfileDiscovery (suggested operations)

**Files:**
- Create: `src/Am4ever.App/Lab/ProfileDiscovery.cs`
- Modify: `src/Am4ever.App/Lab/BoardAnalysis.cs` (expose the OEM audio codec id, if not already derivable)
- Test: `tests/Am4ever.Tests/Lab/ProfileDiscoveryTests.cs`

**Interfaces:**
- Consumes: `BoardAnalysis` (OEM + donor), `DonorAdapter` (`Modules`, `Overrides`, `Volumes`), `OperationSpec`.
- Produces: `public static class ProfileDiscovery { public static IReadOnlyList<OperationSpec> Suggest(BoardAnalysis oem, BoardAnalysis donor, DonorAdapter adapter); }` — per adapter family: a `neutralise_override` (from `OverrideSig` Family/Offset/Expected), `port_gpio`/`port_gevent` (family + the OEM `$A10` platform mask), the standard `empty_tables`, `dxio_layout`/`ddi_layout` templates seeded from the donor (marked `Confidence = "inferred"`), `verb_table` (OEM codec id), then `apcb_check` and `add_ec_init`.

- [ ] **Step 1: Write the failing test**

```csharp
public class ProfileDiscoveryTests
{
    [RomFact] public void Suggest_IncludesGpioAndVerbForTheDonorFamilies()
    {
        var adapter = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var profile = BundledData.LoadProfile("hp-285-g3-8434");
        var oem = BoardAnalysis.Load(File.ReadAllBytes(TestRoms.HpF65!), profile.OemPeiFvOffset!.Value, 0);
        var donor = BoardAnalysis.Load(File.ReadAllBytes(TestRoms.AsrockP1043!), 0, adapter.Volumes.BootBlobOffset);
        var ops = ProfileDiscovery.Suggest(oem, donor, adapter);
        Assert.Contains(ops, o => o.Op == "port_gpio");
        Assert.Contains(ops, o => o.Op == "verb_table" && o.NewCodec is > 0);
        Assert.Contains(ops, o => o.Op == "neutralise_override");
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — Expected: FAIL (`ProfileDiscovery` missing) / project builds.
- [ ] **Step 3: Implement** `Suggest` using `BoardAnalysis.Modules`/`CpmTables`, the adapter's `Overrides`/`Modules`, and the OEM codec id (add a `BoardAnalysis.AudioCodec()` if needed, reusing `VerbTable`). Parse the platform mask out of the `"$A10/0x…"` strings `CpmTables` returns.
- [ ] **Step 4: Run tests to verify they pass** — `dotnet test --filter FullyQualifiedName~ProfileDiscoveryTests` · Expected: PASS (ROMs) / SKIP.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/ProfileDiscovery.cs src/Am4ever.App/Lab/BoardAnalysis.cs tests/Am4ever.Tests/Lab/ProfileDiscoveryTests.cs
git commit -m "feat(app): discover suggested profile operations from a loaded board"
```

---

### Task 4: App — ComposerCore (orchestration)

**Files:**
- Create: `src/Am4ever.App/Lab/ComposerCore.cs`
- Test: `tests/Am4ever.Tests/Lab/ComposerCoreTests.cs`

**Interfaces:**
- Consumes: `ProfileDraft`, `ProfileWriter`, `ProfileLoader`, `ProfileDiscovery`, `BoardAnalysis`, `BuildPipeline`, `BundledData`, `DonorAdapter`, `BuildProgress`.
- Produces: `public sealed class ComposerCore` with:
  - `void SetOemDump(byte[] rom)` (validates AM4 via `RomImage.Load`), `void SetAdapterId(string id)` (loads the bundled adapter + donor expectations), `void SetDonor(byte[] rom)`;
  - `ProfileDraft Draft { get; }`, `IReadOnlyList<OperationSpec> Discover()`, `void AddOperation(OperationSpec op)`, `void RemoveOperation(int i)`, `void MoveOperation(int from, int to)`;
  - `byte[] ToJson()` → `ProfileWriter.Write(Draft.ToProfile())`;
  - `ComposerTestResult TestBuild(string? outPath = null, IProgress<BuildProgress>? progress = null)` — builds with the chosen bundled adapter + loaded OEM/donor, writes the image only if `outPath` is given **and** the build succeeded;
  - `void Import(byte[] json)` → `ProfileDraft.FromProfile(ProfileLoader.LoadProfile(json))`, replacing the draft only on success;
  - `bool IdentitySupported { get; }` (whether a Core extractor exists; gates `smbios_identity`);
  - table-edit gate: `Func<bool>? ConfirmTableEdit { get; set; }`, `bool TableEditEnabled { get; private set; }`, `bool EnableTableEdit()` (calls `ConfirmTableEdit` every time; sets/returns `true` only if it returned true), `void DisableTableEdit()`.
- Produces: `public sealed record ComposerTestResult(bool Ok, string? Error, string? Report);`

- [ ] **Step 1: Write the failing tests**

```csharp
public class ComposerCoreTests
{
    [Fact] public void AddThenRemove_LeavesExpectedOps()
    {
        var c = new ComposerCore();
        c.AddOperation(new OperationSpec("apcb_check"));
        c.AddOperation(new OperationSpec("add_ec_init"));
        c.RemoveOperation(0);
        Assert.Equal(new[] { "add_ec_init" }, c.Draft.Operations.Select(o => o.Op));
    }

    [Fact] public void EnableTableEdit_WarnsEveryTime_AndRespectsDecline()
    {
        int warnings = 0;
        var c = new ComposerCore { ConfirmTableEdit = () => { warnings++; return false; } };
        Assert.False(c.EnableTableEdit());
        Assert.False(c.EnableTableEdit());
        Assert.Equal(2, warnings);              // warned on every enable
        Assert.False(c.TableEditEnabled);       // declined → stays off
        c.ConfirmTableEdit = () => { warnings++; return true; };
        Assert.True(c.EnableTableEdit());
        Assert.True(c.TableEditEnabled);
        Assert.Equal(3, warnings);
    }

    [Fact] public void Import_Garbage_LeavesDraftUnchanged()
    {
        var c = new ComposerCore();
        c.AddOperation(new OperationSpec("apcb_check"));
        Assert.Throws<Am4ever.Core.Rom.RomFormatException>(() => c.Import(System.Text.Encoding.UTF8.GetBytes("{ not valid")));
        Assert.Equal(new[] { "apcb_check" }, c.Draft.Operations.Select(o => o.Op));
    }

    [RomFact] public void TestBuild_OfImportedHpProfile_Succeeds()
    {
        var c = new ComposerCore();
        c.SetOemDump(File.ReadAllBytes(TestRoms.HpF65!));
        c.SetAdapterId("asrock-ab350m-pro4-p10.43");
        c.SetDonor(File.ReadAllBytes(TestRoms.AsrockP1043!));
        c.Import(ProfileWriter.Write(BundledData.LoadProfile("hp-285-g3-8434")));
        var r = c.TestBuild();
        Assert.True(r.Ok, r.Error);
    }
}
```

- [ ] **Step 2: Run them to verify they fail** — Expected: FAIL (`ComposerCore` missing).
- [ ] **Step 3: Implement** `ComposerCore` + `ComposerTestResult`. `TestBuild` catches `RomFormatException`/`RomEditException`/`IOException` → `Ok=false`; `Import` parses first, swaps the draft only if parsing succeeded. `IdentitySupported` checks the known extractor set.
- [ ] **Step 4: Run tests to verify they pass** — `dotnet test --filter FullyQualifiedName~ComposerCoreTests` · Expected: PASS (ROM test skips without ROMs).
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/ComposerCore.cs tests/Am4ever.Tests/Lab/ComposerCoreTests.cs
git commit -m "feat(app): ComposerCore — author, test-build, import/export a profile"
```

---

### Task 5: App — inspection cores (hex, disassembly, table rows)

**Files:**
- Create: `src/Am4ever.App/Lab/HexDump.cs`, `src/Am4ever.App/Lab/Disassembler.cs`, `src/Am4ever.App/Lab/TableRows.cs`
- Modify: `src/Am4ever.App/Am4ever.App.csproj` (add the `Iced` package)
- Test: `tests/Am4ever.Tests/Lab/InspectionTests.cs`

**Interfaces:**
- Produces: `HexDump.Format(byte[] data, int baseOffset = 0) -> IReadOnlyList<string>` (16 bytes/line, `OFFSET  hex…  ascii`).
- Produces: `Disassembler.Decode(byte[] code, ulong rip = 0) -> IReadOnlyList<(ulong Address, string Text)>` using Iced 32-bit mode; never throws on arbitrary bytes.
- Produces: `TableRows.For(byte[] moduleImage, string tag, uint platformMask) -> IReadOnlyList<string>` reusing the Core CPM/GPIO/DXIO parsers.

- [ ] **Step 1: Write the failing tests**

```csharp
public class InspectionTests
{
    [Fact] public void Hex_FormatsOffsetBytesAscii()
    {
        var line = HexDump.Format(new byte[] { 0x41, 0x42, 0x43 })[0];
        Assert.StartsWith("00000000", line);
        Assert.Contains("41 42 43", line);
        Assert.Contains("ABC", line);
    }

    [Fact] public void Disasm_DecodesTheNeutralisedStub()
    {
        var text = string.Join(" ; ", Disassembler.Decode(new byte[] { 0x31, 0xC0, 0xC3 }).Select(i => i.Text));
        Assert.Contains("xor", text);
        Assert.Contains("ret", text);
    }

    [Fact] public void Disasm_DoesNotThrowOnGarbage()
    {
        var ex = Record.Exception(() => Disassembler.Decode(new byte[] { 0xFF, 0xFF, 0x00, 0x12, 0x9A }));
        Assert.Null(ex);
    }
}
```

- [ ] **Step 2: Run them to verify they fail** — Expected: FAIL (types missing / Iced not referenced).
- [ ] **Step 3: Implement** — add `Iced` (`<PackageReference Include="Iced" Version="1.21.0" />`; adjust to the restorable stable if needed); implement the three helpers. `Disassembler` uses `Iced.Intel.Decoder` in 32-bit mode with a `NasmFormatter`, looping to the end, emitting `(bad)` for undecodable bytes without throwing.
- [ ] **Step 4: Run tests to verify they pass** — `dotnet test --filter FullyQualifiedName~InspectionTests` · Expected: PASS.
- [ ] **Step 5: Commit**
```bash
git add src/Am4ever.App/Lab/HexDump.cs src/Am4ever.App/Lab/Disassembler.cs src/Am4ever.App/Lab/TableRows.cs src/Am4ever.App/Am4ever.App.csproj tests/Am4ever.Tests/Lab/InspectionTests.cs
git commit -m "feat(app): read-only hex, Iced disassembly and parsed-table inspection"
```

---

### Task 6: App — Board Lab composer UI

**Files:**
- Modify: `src/Am4ever.App/Lab/BoardLabView.axaml.cs`
- Test: `tests/Am4ever.Tests/App/BoardLabViewTests.cs` (add tests)

**Interfaces:**
- Consumes: `ComposerCore`, `ProfileDiscovery`, `HexDump`/`Disassembler`/`TableRows`, `SegmentedProgressBar`, `IToastService`/`ToastService`, the storage-provider file pickers.
- Produces: `BoardLabView` gains a Profile Composer section with public controls for tests — e.g. `public Button TestButton { get; }`, `public Button ExportButton { get; }`, `public ToggleButton TableEditToggle { get; }`, `public SegmentedProgressBar ComposerProgressBar { get; }` — and a constructor `BoardLabView(IToastService toasts)` plus a parameterless convenience overload (existing tests call `new BoardLabView()`).

- [ ] **Step 1: Write the failing tests**

```csharp
[AvaloniaFact] public void Composer_TestDisabledUntilDumpAndAdapterSet()
{
    var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
    Assert.NotNull(view.TestButton);
    Assert.False(view.TestButton.IsEnabled);        // no OEM dump / adapter yet
}

[AvaloniaFact] public void TableEditToggle_On_RaisesWarning()
{
    var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
    view.TableEditToggle.IsChecked = true;          // user flips it on
    Assert.True(view.LastTableEditWarningShown);    // test-visible flag the view sets when it warns
}
```

- [ ] **Step 2: Run them to verify they fail** — Expected: FAIL (new members/ctor missing).
- [ ] **Step 3: Implement** the composer section: load OEM dump + choose bundled adapter + load donor; a discovery list with **Add** buttons; the operations list with remove/reorder; a JSON preview (`ComposerCore.ToJson`); **Test** (runs `TestBuild` off-thread with `ComposerProgressBar` + a toast on done, like the wizard); **Export/Import** via file pickers; the hex/disasm/table views; the `TableEditToggle` whose checked-handler shows an **inline, non-modal warning banner every time** it goes on (no modal dialog — that keeps the `[AvaloniaFact]` driveable), sets `public bool LastTableEditWarningShown`, and calls `ComposerCore.EnableTableEdit` (the view's `ConfirmTableEdit` returns true after showing the banner; the core's decline path is exercised by Task 4, not the shipped view). Keep the existing analyse/compare/emulate UI.
- [ ] **Step 4: Run tests to verify they pass** — `dotnet test --filter FullyQualifiedName~BoardLabViewTests` · Expected: PASS.
- [ ] **Step 5: Wire the view into MainWindow** — pass the shared `ToastService` into `new Lab.BoardLabView(toasts)`.
- [ ] **Step 6: Run the App suite** — `dotnet test --filter "FullyQualifiedName~App|FullyQualifiedName~Lab"` · Expected: PASS.
- [ ] **Step 7: Commit**
```bash
git add src/Am4ever.App/Lab/BoardLabView.axaml.cs src/Am4ever.App/MainWindow.axaml.cs tests/Am4ever.Tests/App/BoardLabViewTests.cs
git commit -m "feat(app): profile composer UI in Board Lab"
```

---

### Task 7: Docs + full suite

**Files:**
- Modify: `docs/manual/board-lab.md`
- Test: whole solution.

- [ ] **Step 1: Update the manual** — rewrite board-lab.md's composer section: author/discover/add/test/export-import, the read-only hex/disasm/table views, and the warning-gated table edit; drop the old hex/disasm/editor "future work" wording.
- [ ] **Step 2: Release build** — `dotnet build -c Release` · Expected: succeeds for win-x64 + linux-x64 (Iced is managed).
- [ ] **Step 3: Full suite** — `dotnet test` · Expected: all pass; ROM/T5/emulator tests skip without fixtures. Record pass/skip counts in the ledger.
- [ ] **Step 4: Commit**
```bash
git add docs/manual/board-lab.md
git commit -m "docs: document the profile composer in the Board Lab manual"
```
