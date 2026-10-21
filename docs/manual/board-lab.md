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

## Not in this build
The hex editor, Capstone disassembly, in-place table editors, the EC-init sequence composer, and
profile export/import are not implemented yet — Board Lab currently covers analyse, compare and the
emulator console. The underlying operations all exist in the core and CLI; the expert editing UI is
future work.
