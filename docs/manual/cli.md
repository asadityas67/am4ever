# CLI & build operations

The GUI wizard is a front-end over the same core the command line drives. The CLI exposes the two
whole-ROM commands (`inspect`, `build`); the individual **build operations** are not separate
sub-commands — they are the steps a profile lists, and `build` runs them in order. This page
documents both: the commands, and every operation a profile can name.

## The binary
am4ever ships as a portable single file per platform: `am4ever` (linux-x64) / `am4ever.exe`
(win-x64). It needs no install and makes no network calls. Every invocation prints the red risk
warning to stderr first.

```
am4ever <command> [options]

  inspect <rom>                                     read-only report on an AM4 ROM
  build <your-dump> <donor> --profile <id|file> --out <file> [--dmi <file>] --i-understand-the-risk
  --version
  --help
```

Exit codes: `0` ok, `2` risk not accepted, `64` usage error, `1` a build/inspect failure (bad file,
checksum mismatch, malformed profile).

## `inspect <rom>` — read-only
Loads an AM4 image and prints its size, the AMD PSP/BIOS directories it finds (kind, offset, entry
count), and the AGESA version string. It never writes. Use it to confirm a dump is a valid AM4 ROM
and to read a donor's AGESA before trusting a profile.

```
$ am4ever inspect donor.bin
ROM: donor.bin
size: 16 MiB
AMD directories: 4
  PspL1 @ 020000 (16 entries)
  ...
AGESA: AGESA!V9 PinnaclePI-AM4 1.0.0.6
```

## `build` — make a modded image
```
am4ever build <your-dump> <donor> --profile <id|file> --out <out.bin> [--dmi machine.json] --i-understand-the-risk
```
- `<your-dump>` your OEM board's own BIOS dump; `<donor>` the retail donor the profile names
  (donor first in the pipeline, but on the command line it is `<dump> <donor>`).
- `--profile` a **bundled profile id** (e.g. `hp-285-g3-8434`) or a path to a profile JSON.
- `--out` the image to write. On success it also writes `<out>.report.md` listing everything changed,
  inferred and untested.
- `--dmi` optional machine-data JSON (from the save-DMI helper) to carry your real serial/UUID/SKU
  instead of the generic identity.
- `--i-understand-the-risk` accepts the risk non-interactively; without it, an interactive terminal
  prompts for `yes` and a non-terminal (CI) refuses with exit 2.

**If any check fails, no image is written** — the donor SHA, directory/FFS checksums, and the
round-trip recompression are all verified before the file is saved.

## The build operations
A profile's `operations` list is applied in order against the donor's decompressed PEI volume (plus
the DXE volume for `add_ec_init`). OEM table bytes are read from `<your-dump>` at build time; the
only bytes a profile itself may carry are the short `expected`/`new` patches (≤64 bytes each). Most
ops take a `family` (the CPM module family: `ZP` Pinnacle, `RV` Raven, `RN` Renoir, …) and a
`platform_mask` naming which CPM table to touch. An op marked `"confidence": "inferred"` with an
`evidence` note is a best guess, not a verified fact — it surfaces in the build report.

| `op` | Does | Key fields |
|---|---|---|
| `neutralise_override` | Replaces a donor board-specific routine with `xor eax,eax; ret` so the ported tables win | `module`, `offset`, `expected` |
| `byte_patch` | A bounded literal patch, guarded by `expected` | `module`, `offset`, `expected`, `new` |
| `port_gpio` | Copies the OEM GPIO table into the family's `$A10`, optional family-delta merge | `family`, `platform_mask`, `hp_platform_mask`, `merge` |
| `port_gevent` | Copies the OEM GEVENT/SCI table (`$A11`) | `family` |
| `empty_tables` | Empties listed secondary CPM tables to their terminator so donor defaults don't apply | `family`, `tables[]` (`tag`,`term`) |
| `dxio_layout` | Per-descriptor DXIO edits (engine/lanes/device/function/clk_req) | `family`, `platform_mask`, `descriptor_size`, `dxio[]` |
| `ddi_layout` | Copies the OEM display (DDI) block, or keeps the donor's | `family`, `platform_mask`, `ddi_from_platform_mask`, `ddi_keep` |
| `verb_table` | Retargets HD-audio codec verbs to the OEM codec | `family`, `old_codecs[]`, `new_codec` |
| `smbios_identity` | Writes OEM SMBIOS identity (generic, or your `--dmi` data) | — (driven by profile `identity` + `--dmi`) |
| `add_ec_init` | Inserts the EC fan hand-off driver into the DXE volume | — |
| `apcb_check` | Verifies the OEM and donor DRAM SPD/I2C maps match (never rewrites) | — |

### Examples (from the HP 285 G3 profile)
```json
{ "op": "neutralise_override", "module": "AmdCpmOemInitPeim", "offset": 726, "expected": "81ecc4000000" }
{ "op": "port_gpio",     "family": "ZP", "platform_mask": 16776960, "hp_platform_mask": 16776960 }
{ "op": "empty_tables",  "family": "ZP", "tables": [ { "tag": "$A12", "term": "ff00" }, { "tag": "$A19", "term": "ff000000" } ] }
{ "op": "dxio_layout",   "family": "ZP", "platform_mask": 12566272,
  "dxio": [ { "desc": 2, "lanes": [0,0], "clk_req": 1 },
            { "desc": 4, "template": 2, "engine": 1, "device": 1, "function": 2, "lanes": [1,1], "clk_req": 2 } ] }
{ "op": "ddi_layout",    "family": "RV", "platform_mask": 12517376, "ddi_from_platform_mask": 12517376,
  "confidence": "inferred", "evidence": "copied from HP Raven display block" }
{ "op": "verb_table",    "family": "ZP", "old_codecs": [283904153, 283904152], "new_codec": 283903601 }
```

A board port is just this list, repeated per CPU family, ending with `smbios_identity`, `add_ec_init`
and `apcb_check`. The complete, annotated profile is in [example-profile.md](example-profile.md); the
structures each op edits — CPM tables, DXIO descriptors, the DDI block, verb tables, SMBIOS, APCB —
are described in [reference.md](reference.md).

## Programmatic use
The same pipeline is `BuildPipeline.Build(donor, oem, profile, adapter, dmi)` in `Am4ever.Core`; the
CLI is a thin wrapper over it, and the GUI wizard calls the same code. There is no public plug-in API
— a new board is a new **profile** (and, for a new donor, a new adapter), not new code.
