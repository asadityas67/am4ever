# Expert mode: the profile composer

## Intent & why
Board Lab's "editors" are not really about editing bytes — the thing an expert actually needs is to
**author and test a board profile JSON** (like `profiles/hp-285-g3-8434.json`) so that someone with a
*different* OEM board can make one and share it. Today a profile is written by hand, the way the HP
285 G3 one was. Expert mode should walk a user down that same path inside the app: load their OEM
dump and the donor, **discover** the structures in them, **inspect** them, **add** the ones they want
as profile operations, **test-build** the result, and **export** the JSON to share (or **import**
someone's draft to tweak). The composer is the one feature that makes profiles shareable; alongside it
are **lightweight, read-only inspection views** (hex, disassembly, parsed tables) so an expert can see
what they are about to add. Those views stay deliberately basic — full editors are out of scope and an
expert can bolt their own onto the open-source core.

## Scope
- Author **new profiles against a donor we already support** — i.e. one that has a bundled donor
  adapter (today: `asrock-ab350m-pro4-p10.43`). The user's own OEM board + the known donor.
- **Not** in scope: authoring a brand-new donor *adapter* (flash offsets, boot-blob location, DXE
  GUID for an unknown donor). That is the genuinely hard part and is left to experts to add in the
  core; the composer picks from the bundled adapters only.

## Goals
- From a loaded OEM dump + a chosen bundled donor, **discover** and present the structures a profile
  operates on: the CPM OEM PEIM modules per family, each CPM table with its platform mask, the
  HD-audio codec id, the override sites (from the adapter), and whether SMBIOS/APCB are present.
- Let the user **build an ordered operations list** by adding operations from those discoveries —
  with the real values filled in (platform masks, codec ids, module/offset) where they are
  auto-derivable — plus the standard parameterless ops (`smbios_identity`, `add_ec_init`,
  `apcb_check`, `empty_tables`, `port_gevent`). Remove and reorder operations.
- For operations that need expert judgment (`dxio_layout`, `ddi_layout`), add a **template seeded
  from the donor's current values**, marked `confidence: inferred`, that the user refines.
- Show a **live JSON preview** that is exactly what Export writes, and that round-trips: Export →
  Import yields the same draft.
- **Test** the draft: run the real `BuildPipeline` with the chosen bundled adapter against the loaded
  OEM + donor, show the build report, and surface any error (bad op, failed check) without writing a
  file unless the user asks to save the image.
- **Export** the profile `.json`; **Import** a profile `.json` (the app's own or someone else's) into
  the composer to edit. Import reuses the existing `ProfileLoader`.
- Provide **read-only inspection views** for the selected module/region, kept basic:
  - a **hex view** (offset + bytes + ASCII, go-to-offset) of a module's image section;
  - a **disassembly view** of that section (x86, via the managed Iced disassembler — no native
    dependency, keeps the portable win-x64/linux-x64 builds), to eyeball an override routine;
  - **parsed-table views** that render a chosen CPM table's fields using the existing Core parsers
    (GPIO entries, DXIO descriptors, the DDI block, verb codecs) so the expert can read real values
    before adding the matching operation. The table view has an **Edit toggle** (read-only by
    default): switching it **on** raises a warning that must be acknowledged **every time** (no
    "don't show again"), and cancelling leaves the toggle off. While on, the expert can change field
    values, and committing an edit **adds or updates the matching profile operation**
    (`dxio_layout`/`ddi_layout`/`port_gpio`/`verb_table`) in the draft — it never writes raw ROM bytes.
  The hex and disassembly views are read-only; the table view is the one that can author edits, behind
  the warned toggle.

## Non-goals / anti-goals
- The hex and disassembly views are **read-only**. The table view can edit, but only through the
  warned toggle, and its edits produce/update profile **operations** — nothing ever mutates raw ROM
  bytes in any view.
- The table-edit warning is shown on **every** toggle-on and cannot be permanently dismissed; a build
  that silently let an expert edit tables without that warning is a failure.
- No new-donor adapter authoring (see Scope).
- The composer must never emit a profile that `ProfileLoader` then rejects — the live JSON is always
  loadable, or the preview shows the parse/validation error instead of a file that looks fine.
- It must never claim a draft is tested when the build failed — Test shows the failure; Export of an
  untested or failing draft is allowed but the draft's `status` stays untested and the UI says so.
- No firmware bytes in the output — same as every profile: operations only (the `expected`/`new`
  byte caps from `ProfileLoader` still apply).

## How it's built (decisions made)
1. **ProfileDraft (mutable authoring model).** A small mutable model holding the board info
   (vendor/model/board id/match), the chosen donor adapter id, the identity extractor, and an ordered
   `List<OperationSpec>`. UI-free, in `Am4ever.App` (or Core if cleaner). The composer edits this.
2. **ProfileWriter (draft → JSON).** Serialises a `ProfileDraft` to the `am4ever.profile/1` JSON that
   `ProfileLoader` reads (snake_case keys, the op fields). **Round-trip is the contract:** writing the
   bundled HP profile's draft and loading it back yields an equal `BoardProfile`. This is the one new
   Core-ish serializer; `ProfileLoader` already covers the read direction.
3. **ProfileDiscovery (suggestions).** Given the loaded OEM `BoardAnalysis`, the donor `BoardAnalysis`,
   and the chosen `DonorAdapter`, produce a list of **suggested `OperationSpec`s** with real values:
   `neutralise_override` per family from the adapter's override sites; `port_gpio`/`port_gevent` per
   family from the OEM CPM tables; `empty_tables` with the standard secondary-table set; `verb_table`
   from the OEM codec scan; `dxio_layout`/`ddi_layout` templates seeded from the donor marked
   inferred; and the trailing `smbios_identity`/`add_ec_init`/`apcb_check`. Reuses `BoardAnalysis`,
   extended only where discovery needs a value it doesn't yet expose (e.g. the codec id).
4. **Composer core (UI-free orchestration).** Load OEM dump (validate AM4), pick a bundled adapter,
   run discovery, add/remove/reorder operations on the draft, render JSON, and **TestBuild** →
   `BuildPipeline.Build(donor, oem, draftProfile, adapter, dmi)` returning the report or the error.
   This is the unit-tested heart; the view is a thin shell over it, like `WizardFlow` is for the wizard.
5. **Board Lab view.** A "Profile Composer" area in expert mode: an OEM/donor load row, a discovery
   list with **Add** buttons, the operations list (remove/reorder), the JSON preview, and
   **Test / Export / Import** buttons. Reuses the Win98 theme, the toast service, and the segmented
   progress bar for Test. Built in code like the other views, headless-testable.
6. **Docs.** Update `docs/manual/board-lab.md` to describe the composer and drop the old
   hex/disasm/editor "future work" wording.

## Calls settled during review
- **Board identity metadata is user-entered.** `vendor`, `model`, `board_id`, `match.ssid` and
  `match.bios_versions` come from form fields the user fills (they know their board); `match.rom_size`
  is auto-filled from the dump length. These are not reliably discoverable from the dump.
- **`smbios_identity` needs a registered identity extractor.** The SMBIOS rewrite is board-specific
  (today only `hp-8434` exists in Core). For a brand-new board with no extractor, the composer offers
  `smbios_identity` as unavailable and omits it, with a one-line note that an expert must add an
  extractor in Core to get a verified identity rewrite — everything else (GPIO/GEVENT/DXIO/DDI/verb/
  override/EC/APCB) is board-agnostic and fully supported. The resulting profile is valid and builds;
  it just carries the donor's identity until an extractor exists.
- **Round-trip is checked by canonical JSON, not record equality.** `BoardProfile` is a record whose
  nested lists compare by reference, so the round-trip test compares *normalised JSON* (or the
  operation `Op` sequence plus each op's fields), never `Assert.Equal(profileA, profileB)`.

## What's left to the builder
- The exact composer layout and how `dxio_layout`/`ddi_layout` templates are refined (a small field
  form vs. editing the JSON preview text and re-parsing) — pick the simplest that lets an expert set
  descriptor values; editing the JSON preview and re-parsing is an acceptable minimum.
- Whether `ProfileDraft`/`ProfileWriter` live in `Am4ever.Core` (next to `ProfileLoader`) or in the
  App — either is fine; the round-trip test pins correctness.
- Which bundled adapters appear in the donor picker (today there is one).
- Toast/label wording.

## Testing approach (TDD)
- **ProfileWriter round-trip:** import the bundled HP JSON → draft → `ProfileWriter.Write` → load
  again; assert the two load the same (normalised-JSON equal, or equal `Op` sequence + per-op fields —
  not `BoardProfile` record equality, whose lists compare by reference). Plain xUnit, no ROMs.
- **ProfileDiscovery:** with the real OEM + donor + ASRock adapter (`[RomFact]`), discovery suggests a
  `port_gpio` for family ZP with the OEM's platform mask and a `verb_table` with the OEM codec id;
  shape/values asserted. Skips without ROMs.
- **Composer core:** add two suggestions then remove one → the draft's operation list matches; render
  → `ProfileLoader` accepts it. `TestBuild` with the real ROMs (`[RomFact]`) returns a report and,
  for the bundled HP draft, an image equal to the reference (reusing the existing reference check).
- **View:** an `[AvaloniaFact]` builds the composer view, loads nothing, and asserts the Add/Test/
  Export buttons exist and Test is disabled until an OEM dump and adapter are set.
- **Inspection views:** pure-logic tests only (kept light) — the hex formatter renders a known buffer
  to the expected offset/bytes/ASCII lines; the disassembler returns a non-empty instruction list for a
  known code slice (e.g. the `31 C0 C3` neutralised stub → `xor eax, eax` / `ret`); a parsed-table view
  reflects the Core parser's entries. No pixel/rendering assertions.
- **Table edit toggle:** enabling the toggle invokes the warning each time (a test double counts the
  warning calls across two enables → 2, and a cancelled warning leaves the toggle off); committing an
  edit adds/updates the matching operation in the draft. UI-free where possible (the toggle/edit logic
  sits in the composer core, the view binds to it).
