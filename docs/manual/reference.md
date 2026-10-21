# Reference & cheat sheet

Concise reference for the AM4/AMI structures am4ever edits, the profile operations, the donor-adapter
format, and the emulator host. The authoritative behaviour lives in `src/Am4ever.Core` and the
Python reference in `G:\HPBIOS\tools`.

## ROM layout (AMI Aptio V, AM4)
- Sizes: 8 / 16 / 32 MiB SPI flash. AMD **PSP/BIOS directories** (`$PSP $PL2 $BHD $BL2`) carry a
  Fletcher-32 checksum over everything after the checksum field; found on 0x100-byte boundaries.
- **Boot (PEI) volume**: on the ASRock donor it is an AMD-zlib blob (0x100-byte header, compressed
  length at +0x14) at `0xE02000`, wrapping a UEFI FV `0x20` bytes in. On HP it is an uncompressed FV
  at `0xC00000`.
- **Main DXE volume**: FV at `0x94F000`; the container file (GUID `9e21fd93-…`) holds an EDK
  "LZMA alone" GUID-defined section (header: 5 property bytes + UInt64 size + stream) wrapping the
  inner DXE FV.

## CPM tables
A CPM table is a 4-byte tag beginning `$A` (plus `$CPM`), a 16-byte header, then a body:
`UInt16 size, byte featureRev, byte contentRev, UInt32 platformMask, UInt32 attributes` at tag+4.
Key tables: `$A10` GPIO, `$A11` GEVENT/SCI, `$A12–$A15`/`$A17`/`$A19` device/clock/secondary,
`$A26` DXIO topology (+ DDI display block at the tail).
- **GPIO** (`$A10`): `{UInt16 pin, UInt16 setting}` entries, terminated by pin `0xFF`.
- **DXIO** (`$A26`): descriptors after a 0x14 header (0x10 CPM header + 4-byte SocketId);
  **ComboAM4v2 = 0x1A4 bytes**, PinnaclePI = 0x94. Per descriptor: `engine@+4`, `laneStart@+6`,
  `laneEnd@+7`, `port@+0xC` (present bit0, device bits3-7, function bits8-10, clkReq bits24-27),
  `type@+0x14`. The **DDI** display block follows the 16 port descriptors: connectors 12 bytes each
  at +0x0C (flags UInt32 w/ last-entry bit 0x80000000, then connector/aux/hpd bytes).
- **HD-Audio verbs**: a codec list `{UInt32 codecId, verbs…, 0xFFFFFFFF}` per codec, ended by a
  codecId of `0xFFFFFFFF`. HP ALC671 = `0x10EC0671`; ASRock = `0x10EC0899`/`0x10EC0898`.

## SMBIOS & APCB
- **SMBIOS static data**: AMI file GUID `daf4bf89-…`, a freeform-subtype-GUID section (type 0x18,
  data GUID `ab56dc60-…`), body at +0x14. Identity strings live in types 1/2/3; type-1 UUID at
  formatted-area offset 8 in SMBIOS 2.6+ byte order; OEM strings in type 11.
- **APCB** (v2/v3): `APCB` header then groups (MEMG, CCXG, …) of typed entries. The DRAM SPD/I2C
  address map is type `0x31` in the memory group; am4ever only *checks* it matches (never rewrites it).

## Profile (`am4ever.profile/1`)
JSON describing the board, its donor, authors, status (tested/untested), and a list of typed
**operations** applied in order. No firmware bytes; the only byte strings allowed are short
`expected`/`new` patches (≤64 bytes). OEM tables are read from the user's dump at build time.

Operations: `neutralise_override` (make a donor routine `xor eax,eax; ret`), `port_gpio` (copy OEM
GPIO, optional `family_delta` merge), `port_gevent`, `empty_tables`, `dxio_layout` (per-descriptor
edits; inferred maps carry `confidence`/`evidence`), `ddi_layout` (copy OEM display block or keep),
`verb_table`, `smbios_identity`, `add_ec_init` (insert the EC fan driver), `apcb_check`, `byte_patch`.

## Donor adapter
Ships with the app; names the donor (vendor/board/version + the official ROM's SHA-256), the
module→CPU-family map, the override-routine offsets + expected bytes, the volume offsets, and known
hazards. First donor: `asrock-ab350m-pro4-p10.43`.

## Emulator host
Unicorn cannot run in a .NET process on CET-enabled Windows (its `longjmp`-based JIT trips the shadow
stack → `0xC0000409`). So a small **native host** `uc_host` (a plain non-CET exe) runs the emulation
and the .NET side drives it as a subprocess (`Am4ever.Emu`). It is **not committed** (binaries exceed
the repo size limit) — build it with `native/build.cmd` / `native/build.sh` (needs a Unicorn build;
see `native/README.md`); emulator tests skip when it is absent. Commands: `uc_host selftest`,
`uc_host ecfan …` (DXE EC sim), `uc_host pei …` (run a CPM OEM PEIM under a CPU family).

## Cheat sheet
```
boot blob (ASRock)   0xE02000  (len at +0x14, FV at +0x20)   HP PEI FV   0xC00000
DXE FV               0x94F000  file 9e21fd93-…  LZMA section
override sites (image-section offset → expected): ZP 0x2D6 81ecc4000000 | MTS 0x344 8b018d94244cffffff
                                                   RV 0x394 81ecc0000000 | RN 0x36C 8b018d942460ffffff
neutralised to       31 C0 C3  (xor eax,eax; ret)
CPU families (CPUID eax): Summit 00800F11 Pinnacle 00800F82 Raven 00810F10 Picasso 00810F81
                          Matisse 00870F10 Renoir 00860F01 Vermeer 00A20F10 Cezanne 00A50F00
EC fan driver        GUID 6f9a1c2e-5b7d-4e41-9c3a-8534a0e7f285  "HpEcFanInit"  (EC[0x7E]=0xE7, EC[0x80]&=~2, LPC D14F3x44 bit30)
```
