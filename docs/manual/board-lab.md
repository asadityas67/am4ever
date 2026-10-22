# Board Lab (expert mode)

Switch to Board Lab from **View ▸ Board Lab (expert)**. It is a read-only analysis workbench for
understanding a ROM and checking a port — not a beginner path.

## What it does
- **Open OEM ROM** / **Open donor ROM** — load either AM4 image. Board Lab reads the PEI volume
  (uncompressed, as HP ships it, or decompressed from the donor's AMD-zlib boot blob automatically)
  and shows a summary: size, number of AMD directories, and the AGESA string.
- **Module list** — the FFS modules in the PEI volume, by UI name. The CPM OEM PEIMs
  (`AmdCpmOemInitPeim`, `AmdCpmOemRvInitPeim`, `AmdCpmOemRnInitPeim`) are the ones that carry the
  board configuration.
- **CPM tables** — selecting a module shows the CPM tables it publishes (`$CPM`, `$A10` GPIO,
  `$A11` GEVENT, `$A26` DXIO, …) with their platform masks — the per-family configuration.
- **Byte-compare vs donor** — when a donor is loaded, the selected module is compared to the donor's
  same-named module: same/different size, the first differing offset, and how many bytes differ.
- **Run in emulator** — runs the selected module in the out-of-process Unicorn host under the
  Pinnacle family and reports the entry result, how many CPM tables it published, and whether it
  wrote outside its image/stack/heap. Disabled if the native emulator host isn't built
  (see [reference.md](reference.md#emulator-host)).

## Using it to check a port
Load your built image as the OEM ROM and the donor as the donor. For each CPM OEM PEIM: confirm the
CPM table list looks right, the byte-compare against the donor shows the expected regions changed,
and the emulator run shows `writes_outside=0`. This mirrors what the project's PEI verification test
does automatically.

## Profile Composer
The composer builds a shareable **board profile** (like `profiles/hp-285-g3-8434.json`) for a new OEM
board against a donor that already has a bundled adapter — the same path the HP 285 G3 profile was
authored by hand. It does not author a brand-new donor adapter (flash offsets, boot-blob, DXE GUID for
an unseen donor); that is left to experts to add in the core.

1. **Load your OEM dump** and the **donor**, and **choose the donor adapter**.
2. **Discover operations** — the composer reads the structures (modules per family, the GPIO platform
   masks, the override sites from the adapter, the audio codec) and suggests the matching operations
   with real values filled in. `dxio_layout`/`ddi_layout` come as **inferred templates** to refine.
3. **Add** the operations you want; the ordered list and a **live JSON preview** update as you go.
4. **Test build** runs the real build + checks against your ROMs and reports success or the exact
   failure (no file is written). **Export** the `.json` to share; **Import** someone's draft to tweak.
5. **Identity:** `smbios_identity` needs a board-specific identity extractor in the core (today only
   `hp-8434`). For a brand-new board it is omitted with a note — everything else is board-agnostic and
   the profile still builds, carrying the donor's identity until an extractor is added.

## Inspection views (read-only)
Three basic viewers help you see what you are about to add: a **hex view**, an x86 **disassembly**
(managed Iced decoder — handy for eyeballing an override routine), and a **parsed-table** list of a
module's CPM tables. The table view has an **Edit toggle**: turning it on shows a warning **every
time**, and edits are captured as profile **operations** — nothing ever writes raw ROM bytes. Deeper
tools (a full hex editor, per-cell grid editors) are intentionally out of scope; the project is open
source and an expert can bolt their own onto the core.
