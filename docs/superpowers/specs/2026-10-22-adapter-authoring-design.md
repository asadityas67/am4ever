# Donor-adapter authoring

## Intent & why
Today am4ever ships one donor adapter (ASRock AB350M Pro4 P10.43), and a profile can only target a
donor that has one. Authoring a new adapter is hand work. This adds a tool that **auto-detects
everything it can from a donor ROM and leaves the one hard part — the override sites — to the expert**,
so new donors (e.g. the ASRock X300) become usable without writing the adapter JSON by hand. It is the
donor-side analogue of the profile composer, and the last piece of making the app genuinely generic.

## Scope
- Load a donor ROM → detect the adapter fields that are reliably derivable → produce a **donor-adapter
  draft** the expert finishes and **exports as JSON** (same `DonorAdapter` schema as the bundled one).
- **Not** in scope: auto-finding the override sites (the board-init routines to neutralise). Those are
  left empty with guidance; the expert finds them with the existing hex/disassembly views. Also not in
  scope: validating the adapter by a full build (that needs a profile too).

## Goals
- Auto-fill from the donor ROM: `identity.sha256`; the `volumes` (compression, boot-blob offset/length,
  boot-FV offset, DXE-FV offset, DXE file GUID); and the `modules` list (CPM OEM PEIM names + indices,
  with a **suggested** family tag). Best-effort: fill what can be determined confidently, and clearly
  mark anything it can't.
- Let the expert fill/adjust the rest: the adapter `id`, `identity` vendor/board/version, the module
  family tags, the **`overrides`** (family → offset → expected bytes), hazards, download URL.
- **Export** a valid adapter `.json` that `ProfileLoader.LoadAdapter` accepts; **import** an existing
  adapter to edit.

## Non-goals / anti-goals
- Never emit an adapter that `ProfileLoader.LoadAdapter` rejects (the override `expected` strings stay
  within the 64-byte cap; the JSON is always loadable or the preview shows the error).
- No firmware in the output — an adapter carries only offsets, GUIDs, the SHA, and short override
  `expected` byte strings, exactly as the bundled one does.
- No change to the `DonorAdapter` schema or to how builds consume adapters.
- Detection is best-effort and **honest**: a field it could not determine is shown blank/marked, never
  guessed silently.

## How it's built (decisions made)
1. **AdapterWriter (Core).** `AdapterWriter.Write(DonorAdapter) -> byte[]` — the `ProfileLoader`
   adapter options run in reverse (snake_case, the shared hex converter), round-tripping through
   `ProfileLoader.LoadAdapter`. Mirrors `ProfileWriter`.
2. **AdapterDraft (Core).** A mutable model (id, identity fields, `List<ModuleEntry>`,
   `List<OverrideDraft>`, volume fields, hazards, download url) with `ToAdapter()` / `FromAdapter()`.
   `OverrideDraft` holds family + offset + an `expected` hex string the expert types.
3. **AdapterDetector (App, reusing the analysis code).** `AdapterDetector.Detect(byte[] donorRom) ->
   AdapterDraft` using the existing `AmdDirectory`, `PspZlib`, `FirmwareVolume`, `GuidedSection`,
   `SmbiosTable`:
   - **SHA-256** of the ROM.
   - **Boot/PEI volume:** try an uncompressed FV first (HP-style); else locate the AMD-zlib boot blob
     (scan candidate offsets, accept the one that decompresses to an FV holding a CPM OEM PEIM) → set
     `compression`, `boot_blob_offset`/`length`, `boot_fv_offset`. If none is found confidently, leave
     the volume fields blank and note it.
   - **DXE volume:** scan FVs for the one whose container file has an LZMA GUID-defined section → set
     `dxe_fv_offset` and `dxe_file_guid`.
   - **Modules:** the FV files named `AmdCpm*InitPeim`, with indices; a family **suggestion** from the
     name (`…Rv…`→RV, `…Rn…`→RN, `…Cz…`→CZN, the plain `AmdCpmOemInitPeim` instances → ZP then MTS by
     order) that the expert confirms.
   - **Identity:** vendor/board/version from the donor SMBIOS if readable, else blank for the expert.
4. **Board Lab UI.** An "Adapter Authoring" section: **Detect from donor** (uses the loaded donor ROM),
   a field form for id/identity/volumes, the modules list with editable family tags, an **overrides**
   editor (add family/offset/expected rows), a live JSON preview (`AdapterWriter`), and
   **Export / Import**. Reuses the Win98 theme + toasts; built in code, headless-testable. The
   hex/disassembly views (already there) are how the expert reads the override bytes.

## What's left to the builder
- Exact boot-blob detection heuristic (which candidate offsets to try; how to accept a decompression).
- Where `AdapterDraft`/`AdapterWriter` live (Core, beside `ProfileWriter`); the family-suggestion rules.
- UI layout and how overrides rows are added/edited.

## Testing approach (TDD)
- **AdapterWriter round-trip:** `LoadAdapter(AdapterWriter.Write(bundled))` equals the bundled adapter
  (normalised-JSON / field compare, not record equality). Plain xUnit.
- **AdapterDraft:** `FromAdapter(bundled).ToAdapter()` preserves modules + volumes + overrides; adding
  an override appears in `ToAdapter()`.
- **AdapterDetector:** `[RomFact]` on the real ASRock donor — detects `compression = amd-zlib`, a
  `boot_blob_offset` and `dxe_file_guid` matching the bundled adapter, and lists the CPM OEM PEIMs;
  `overrides` is empty (left to the expert).
- **View:** `[AvaloniaFact]` — the Adapter Authoring section exists, Detect is disabled until a donor is
  loaded, Export produces loadable JSON for a minimal draft.
