# am4ever: design

*Status: draft for review, 2026-10-20.*

## 1. Intent

AMD AM4 office PCs from Lenovo, Dell and HP (HP 285 G3, HP ProDesk 405, Dell
OptiPlex 5055, Lenovo M715q/M75q and similar) shipped with OEM BIOSes that stop
at Ryzen 1000/2000 or 3000. The socket would take a Ryzen 5000. The only thing
missing is firmware, and OEMs will never ship it, so working machines end up as
e‑waste.

am4ever lets people fix that themselves. It takes the user's own OEM BIOS dump
and a retail AM4 board BIOS (the **donor**, e.g. ASRock AB350M Pro4 P10.43).
It produces a flashable image: the donor's ComboAM4v2 AGESA, carrying the OEM
board's own configuration. The user then flashes it with an external SPI
programmer.

It is the general form of the HP 285 G3 port done in `G:\HPBIOS` (builds
T1–T5). That work is the reference implementation and the first supported
board.

**Why now / success:** a beginner with a profiled board and a CH341A goes from
dump to a verified, flashable image in minutes, without understanding firmware.
An intermediate user can profile a new board with the app's guidance, learning
as they go. An expert spends their time only on the parts that truly need
reverse engineering.

## 2. Users and roles

| Role | Does | Needs |
|---|---|---|
| **Beginner** | Owns a profiled board and a programmer. Runs the wizard. | Safety rails, plain language, no firmware knowledge required. |
| **Intermediate** | Wants to profile a new board, learning on the way. | Automatic analysis, guided reverse‑engineering steps, docs that teach. |
| **Expert** | Profiles boards fast, solves the hard bits (EC handshakes, lane maps). | A workbench, raw access, a reference instead of a tutorial, no hand‑holding. |

## 3. Goals, non‑goals, anti‑goals

**Goals**
- Beginner wizard that builds a modded BIOS from: own dump + board profile + donor BIOS.
- Expert mode ("Board Lab") that analyses an unprofiled board against a supported
  donor, ports everything it can, and guides the user through what it can't.
- Shareable **board profiles** (JSON) that capture an expert's work so every later
  owner of that board benefits.
- Built‑in documentation: a learning path for intermediates, a fast reference for experts.
- Portable single‑file binaries for Windows x64 and Linux x64.

**Non‑goals (for now)**
- Intel platforms. Most Intel OEM desktops from Skylake on are Boot Guard fused,
  and Intel sockets already cover their CPU generations.
- Other AMD sockets (FM2+, AM5).
- Flashing from inside the app. Users flash with their own programmer software.
  The app gives a flashing checklist instead.
- Any server, account or upload service.

**Anti‑goals (failure even if it "works")**
- **Data leaves the user's machine.** The app makes no network calls with user data.
  If a profile catalogue is ever fetched it is a plain read‑only download, and no
  dump, serial, UUID or key is ever sent anywhere.
- **Firmware gets redistributed.** Profiles must never contain OEM, AMD, AMI or
  donor‑vendor firmware bytes beyond the small expected/replacement byte strings
  that patch operations need (§7). The user supplies every ROM.
- **A beginner gets an image the app knows is unsafe.** If any check fails, there is
  no "build anyway" for beginners.
- **Defeating a cryptographic protection.** am4ever never bypasses or weakens
  signature checks, AMD Platform Secure Boot (e.g. Lenovo's CPU lock), Intel Boot
  Guard or vendor capsule signatures. AMD‑signed PSP components are used
  unmodified. OEM flash‑time signature checks are avoided only because users
  write the chip with their own programmer, not broken.
- **A silent guess.** Every inferred value (e.g. a lane remap) is marked as inferred,
  with its evidence, in both the profile and the beginner's build report.

## 4. Constraints

- **Language/runtime:** C# on .NET 8 (LTS). *User's decision.*
- **UI:** Avalonia, one UI codebase for Windows and Linux. *My call.*
- **Look:** classic **WinForms** style across the whole app, on both Windows and
  Linux. That means grey `Control` surfaces, Segoe UI/9 pt‑style system font,
  3D‑bordered buttons, group boxes, tab controls, status bar and menu bar.
  There's a **dark mode** with the same shapes in dark colours. It follows the OS
  setting by default and can be switched from the View menu. *User's decision;
  how it's achieved (a custom Avalonia theme vs an existing classic theme
  package) is the builder's call.*
- **Startup warning:** see §6.0. *User's decision.*
- **Distribution:** self‑contained single‑file publish for `win-x64` and
  `linux-x64`. No installer, nothing written outside the user's chosen output
  folder and an app data folder. *User's decision (portable + Linux).*
- **Native helpers**, bundled per platform: Capstone (disassembly), Unicorn
  (emulation), Keystone (assembling EC‑init templates). *My call.* These are
  GPLv2 / BSD licensed, so am4ever's licence must be GPL‑2.0‑compatible. *User's decision:
  GPL‑2.0‑or‑later.*
- **Compression in managed code:** zlib (`System.IO.Compression.ZLibStream`),
  LZMA (port of the public‑domain LZMA SDK, EDK2 "LZMA alone" header with a
  known 64‑bit size), and a C# port of EDK2's EFI/Tiano decompressor. No external
  `.exe` tools at runtime.
- **Everything local.** Inputs are files the user picks. The app never modifies
  input files.
- **Platform:** AMD AM4, AMI Aptio V firmware, 16 MB SPI. Profiled boards may
  declare other sizes; the app refuses mismatches.

## 5. Architecture

```
am4ever/
  src/Am4ever.Core      ROM model + analysis + operations + build (no UI)
  src/Am4ever.Emu       Unicorn/Capstone/Keystone wrappers, PEI/DXE harness, EC sim
  src/Am4ever.Cli       `am4ever` command line (build, analyse, verify, emulate)
  src/Am4ever.App       Avalonia GUI: Wizard + Board Lab + Docs viewer
  donors/               curated donor adapters (JSON + docs), shipped with the app
  profiles/             board profiles (community, JSON)
  docs/                 Markdown docs (also embedded in the app)
  tests/                unit tests + the HP 285 G3 reference test
```

### 5.1 Core ROM model (port of `G:\HPBIOS\tools\amdrom.py` and friends)
- PSP/BIOS directories (`$PSP $PL2 $BHD $BL2`, combo directories), Fletcher‑32 fix‑up,
  address modes, PSP compressed blobs (0x100 header + zlib).
- UEFI firmware volumes, FFS files (incl. large files), sections (raw, PE32, TE,
  UI, depex, GUID‑defined LZMA/AMD‑zlib, freeform‑subtype‑GUID, FV image),
  checksum fix‑ups, in‑place file replacement, appending files to FV free space,
  recompression of nested volumes.
- AGESA identification, microcode inventory, PSP ID → CPU family mapping.
- AMD CPM tables: `$CPM` main table and `$Axx` tables. GPIO (`$A10/$A19`),
  GEVENT (`$A11`), device tables (`$A12–$A15`), clocks (`$A17`), DXIO topology
  (`$A26`) in **both** layouts (PinnaclePI: 0x94‑byte descriptors; ComboAM4v2:
  0x1A4‑byte descriptors) plus the display (DDI) block at the table tail.
- APCB v2/v3 parsing (memory SPD address map, type 0x0031 in v2).
- HD‑Audio verb tables (both the PinnaclePI codec‑list format and the ComboAM4v2
  PCD 0x178 inline format).
- AMI SMBIOS static data (`DAF4BF89…`/`AB56DC60…`) and MSDM/OA3 discovery.
- Per‑machine identity extraction from an OEM dump (serial, UUID, SKU, family,
  board serial, asset tag, OEM strings, MSDM key), with per‑vendor extractors
  added as they're worked out (see §8.4).

### 5.2 Operations
A profile is a list of typed operations, applied by Core in a fixed order.
Each operation:
1. checks its preconditions (expected bytes, table present, family present);
2. applies its change;
3. records a human‑readable line for the build report;
4. registers what to verify afterwards.

Initial operation set (each already exists in the Python reference unless
marked *new*):

| Operation | Purpose |
|---|---|
| `neutralise_override` | Make a donor OEM‑PEIM routine return early. The location comes from the donor adapter (pattern + expected bytes), never a bare offset. |
| `port_gpio` | Copy the OEM GPIO table for a family. Optional family‑delta merge rule as used for Matisse/Vermeer and Renoir/Cezanne (keep OEM value where OEM wires the pin). |
| `port_gevent` | Copy the OEM GEVENT/SCI table. |
| `empty_tables` | Empty device/clock/secondary‑GPIO tables the OEM doesn't use. |
| `dxio_layout` | Rewrite ports per family. Expressed as physical‑port intents ("OEM lane 0 = PCIe x1", "lanes 2‑3 = SATA") plus a per‑family lane map. Each mapping is marked *verified* or *inferred* with evidence. |
| `ddi_layout` | Set the APU display block per family (copy from OEM where it exists, otherwise inferred with evidence). |
| `verb_table` | Install the OEM codec's verb table in place of the donor's. |
| `smbios_identity` | Generic OEM identity plus optional per‑machine identity. |
| `add_ec_init` | Add a generated DXE driver performing an EC/LPC init sequence: LPC decode bits, then EC reads/writes with masks and timeouts. Generalises `HpEcFanInit`. |
| `byte_patch` | Escape hatch: module + offset + **expected** bytes + new bytes. Refused if expected bytes don't match. |
| `apcb_check` *(check only)* | Confirm the OEM SPD address map is compatible with the donor's. Fail with guidance if not. |

New operations are added to Core with docs and tests. Profiles can't run code.

### 5.3 Donor adapters
Donors are few and curated, so they ship with the app as adapter files plus
docs. An adapter states:
- donor identity: vendor, board, version, SHA‑256 of the official ROM file;
- the module list per CPU family (which OEM‑PEIM serves which family; verified
  in the emulator);
- override‑routine signatures (byte patterns + expected bytes);
- known donor‑specific hazards (e.g. ASRock NCT6791D @0x2E and NCT3933 @SMBus
  0x15 checked harmless);
- the boot‑volume and DXE‑volume locations and their compression.

**First donor:** ASRock AB350M Pro4 P10.43 (fully mapped during the HP work).
**Second donor candidate:** Biostar B350GT5 5.23 (partially mapped; its lane
tables are built in code, so it needs more adapter work).

### 5.4 Emulator (port of `emulate_modules.py`)
- **PEI:** run each donor CPM OEM PEIM under every CPU family (CPUID hook), with
  fake PEI services, the IDT‑based PEI services pointer, fake PPIs (PCD PPI,
  SMBus2, generic), and notify dispatch. Walk the `$CPM` list and decode
  GPIO/lanes/displays/verb‑table codec from emulated memory. Flag writes outside
  image/stack/heap.
- **DXE:** run generated EC‑init drivers (and, where available, the OEM's
  original driver) against a simulated ACPI EC and PCI config space. Compare
  results and check the "EC absent" timeout.
- Used in three places: profile authoring (Board Lab), the build checks
  (wizard step 5), and the test suite.

## 6. Beginner wizard

### 6.0 Startup warning (every launch, every mode)
Every time the app starts, before anything else (wizard, Board Lab or docs), it
shows a modal warning. *User's decision.*
- **Red, blinking banner:** "WARNING: You are responsible for what you do. Flashing
  a modified BIOS can permanently kill your PC if you are not careful."
- Below it, plain text covering:
  - am4ever comes with no warranty (GPL‑2.0);
  - keep your original BIOS backup;
  - only flash with a programmer you know how to use;
  - am4ever isn't affiliated with AMD or any PC vendor.
- The user must tick **"I understand and accept the risk"** before **Continue**
  is enabled. **Exit** closes the app. The acknowledgement is never remembered, so
  it's shown on every launch.
- The CLI prints the same warning in red. It needs `--i-understand-the-risk` (or
  an interactive "yes") on every build command.
- *My call:* blink at about 1 Hz, well under the 3 flashes per second limit for
  photosensitive users. Alternate the banner between bright and dark red rather
  than flashing the whole screen, so the text stays readable. If the OS
  "reduce motion" setting is on, keep the banner solid red and add a thick red
  border instead of blinking.

### 6.1 Steps

Six steps, one per screen, with **Next** and **Back**. Plain language
throughout; technical detail is behind "Show details".

1. **Your BIOS.** Pick the dump file and, recommended, a second read of the same chip. If
   two reads are given they must match byte for byte. Checks size, that it's an
   AMI Aptio AM4 image, and that it isn't blank/erased.
2. **Board profile.** The app identifies the board (subsystem/board ID, BIOS
   version, ROM structure) and offers matching profiles bundled with the app.
   The user can also load a profile file. The first release has no online
   catalogue and no network access. *(My call.)* Shows the author, tested CPUs/boards, ported features with
   ✔/⚠ status, and every *inferred* or *untested* item in plain words.
3. **Donor BIOS.** Tells the user exactly which donor file to download and from
   where (the vendor's site URL in the adapter), then verifies its SHA‑256.
4. **Identity.** Lists the serial, UUID, SKU/family, board serial/asset tag and
   Windows key found in the dump. All are ticked by default. The user can untick
   any, choose "generic identity" (resale/privacy), type missing values, or
   import `hp_dmi.json`‑style output from a helper script. Keys are never shown
   in full.
5. **Build & checks.** Builds into a new file and runs every verification:
   structure, checksums, round‑trips, profile post‑checks, emulator. Any failure
   stops here with an explanation. Beginners get no override.
6. **Flash checklist.** Programmer voltage warning (CH341A 3.3 V), back up the
   original file, erase/write/verify, clear CMOS, first boot can take 1–2 minutes,
   how to recover, what to report back. A build report (Markdown) is saved next
   to the image.

## 7. Board profiles

JSON, versioned schema (`am4ever.profile/1`), human‑readable and diff‑friendly.

```jsonc
{
  "schema": "am4ever.profile/1",
  "board": {
    "vendor": "HP", "model": "HP 285 G3 MT", "board_id": "8434",
    "match": { "ssid": "8434", "bios_versions": ["F.65"], "rom_size": 16777216 }
  },
  "donor": { "adapter": "asrock-ab350m-pro4-p10.43",
             "sha256": "1970ffd8…" },
  "authors": ["…"],
  "status": { "tested": [{ "cpu": "Ryzen 5 5600", "result": "boots", "notes": "…" }],
              "untested": ["Renoir/Cezanne display outputs"] },
  "operations": [ { "op": "port_gpio", "family": "MTS_VMR", "from": "oem:pinnacle",
                    "merge": "family_delta", "notes": "…" }, … ],
  "identity": { "extractor": "hp-8434" }
}
```

Rules:
- No firmware payloads. The only bytes allowed are short expected/replacement
  strings for `byte_patch`/`neutralise_override` (proposed limit: 64 bytes per
  patch) and generated code (EC‑init templates assembled at build time).
- OEM tables are always read from the **user's own dump** at build time, never
  stored in the profile.
- Every inferred item carries `"confidence": "inferred"` and an `"evidence"` string.

## 8. Expert mode ("Board Lab")

### 8.1 Analyse
Open an OEM ROM, pick a donor. The app runs the full analysis from the HP work:
- PSP/AGESA layout, families supported by OEM vs donor
- CPM tables per family (both formats)
- GPIO/GEVENT diffs
- lane and display comparison across the families the OEM supports
- APCB SPD map
- audio codec IDs
- SMBIOS
- Super I/O vs EC detection
- an I/O‑port scan for EC/fan code (`0x62/0x66`, `0x2E/0x4E`, CMOS, FCH MMIO)
- the donor's runtime override routines

### 8.2 Report
Three columns:
- **Ported automatically:** with the evidence.
- **Needs a decision:** inferred mappings (e.g. lane numbering for families the
  OEM never supported), each with the evidence and an editor.
- **Needs reverse engineering:** e.g. an EC fan handshake, unknown OEM modules
  touching hardware. Each card says what's suspected, which modules, and the
  relevant disassembly/strings. It suggests byte comparisons and links the
  matching how‑to (§9).

### 8.3 Workbench
- Module browser across both ROMs (names, GUIDs, depex, sizes).
- Table editors (GPIO, lanes, displays, verbs, SMBIOS).
- Hex view and disassembly (Capstone).
- Byte‑compare between OEM and donor modules or tables.
- An emulator console: run a module with a chosen CPU family, see PPIs, PCDs and
  port I/O.
- EC‑init builder: compose a sequence, test it against the EC simulator (and
  against the OEM's own driver where possible).

### 8.4 Export
Save a profile, run the full build + checks on the expert's own dump, and
produce a contribution bundle containing the profile, notes and test log. It must
contain no firmware.

## 9. Documentation

Ships inside the app (offline) and as Markdown in `docs/`:
- **Learning path** for intermediates:
  1. how AM4 boots (PSP → ABL → BIOS) and why OEM boards get stuck;
  2. AGESA, PSP directories and combo directories;
  3. AMD CPM tables;
  4. reading a lane table;
  5. GPIO encoding and IOMUX;
  6. display blocks and verb tables;
  7. SMBIOS and identity;
  8. ECs and fan control;
  9. using the emulator;
  10. your first port: the HP 285 G3 walkthrough, using the real analysis output.
- **How‑tos** linked from every "needs reverse engineering" card: finding an EC
  handshake, mapping lane numbers between CPU generations, locating board tables
  by byte comparison, identifying a donor's override routine, testing a fix in
  the emulator.
- **Reference:** structure layouts (DXIO descriptors in both formats, DDI block,
  GPIO setting bits, APCB SPD map, verb formats, SMBIOS fields), every profile
  operation, the donor adapter format.
- **Expert cheat sheet:** one page of offsets, signatures, PPI/PCD numbers and
  operation syntax.

## 10. Safety and verification

- Two‑dump comparison (recommended, enforced if given). The input is never modified.
- Every operation checks preconditions before applying, so the app refuses rather
  than guesses.
- After build, a full verification:
  - PSP/BIOS directory checksums;
  - every recompressed volume decompresses back to exactly the patched contents;
  - FFS header/data checksums;
  - each operation's own post‑check;
  - emulator run of every touched PEIM under every family, plus EC‑init drivers.
- The build report lists everything changed, inferred and untested, in plain words.
- Beginners can't override failed checks. Experts can, with an explicit
  confirmation that's recorded in the report.

### 10.1 Legal hygiene
- **Licence:** GPL‑2.0‑or‑later, including its no‑warranty clause. The wizard's first
  screen and the README repeat it in plain words.
- **No firmware anywhere.** No firmware in the repo, releases, docs, test fixtures
  or issue attachments. CI rejects pull requests that add binary files over a
  small size limit, or files that look like firmware (contain FV/PSP signatures).
  `CONTRIBUTING.md` forbids posting ROMs or finished modded images, and the issue
  template asks for build reports instead.
- **Trademarks.** Vendor and product names are used only to say what am4ever works
  with. No vendor logos. README and About box state that am4ever isn't
  affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.
- **Reference ROMs** for tests are read from paths the developer supplies
  (environment variable) and never committed (§11).

## 11. Reference test

The HP 285 G3 port is the acceptance test. Given the user's local copies of the
HP F.65 ROM and ASRock P10.43, the CLI applies `profiles/hp-285-g3-8434.json`.
It must reproduce `G:\HPBIOS\build\HP285G3_ComboAM4v2_T5.bin` **semantically**:
- every uncompressed region identical;
- the boot (PEI) and DXE volumes identical after decompression;
- all checksums valid;
- the emulator passes.

Compressed bytes may differ, because .NET's zlib (zlib‑ng) and a managed LZMA
encoder don't emit the same streams as Python's zlib or EDK2's `LzmaCompress`.
*(My call.)* The test reads ROM paths from an
environment variable and skips if they're absent, since ROMs are never committed.

## 12. Scope of the first release

- Core + Emu + CLI + App (Wizard, Board Lab, Docs).
- One donor adapter: ASRock AB350M Pro4 P10.43.
- One profile: HP 285 G3 (8434), status "untested on hardware" until the user's
  tests land.
- Identity extractor: HP 8434 (from `save_hp_dmi.ps1`‑style JSON; dump‑based
  extraction once the HP per‑machine data format is known from a real dump).
- Docs: learning path, how‑tos for every flag type the HP port produced, full
  reference, cheat sheet.

## 13. Decisions

**User's:**
- AMD AM4 only.
- Everything runs locally; nothing is uploaded.
- Beginners use downloaded expert profiles.
- Flagged items are guided, not silently skipped.
- Step‑by‑step wizard for beginners, including a copy‑identity option.
- C#, portable, Windows + Linux binaries.
- Proper documentation for intermediates and experts.
- Name: am4ever.
- Licence: GPL‑2.0‑or‑later.
- Classic WinForms look across the whole app, with a dark mode.
- Red blinking "you are responsible / you can kill your PC" warning on every launch, which must be acknowledged.
- Published on GitHub as a public repository: source, docs, and release binaries
  for win‑x64 and linux‑x64. Profiles and donor adapters come in as pull requests
  that the maintainer reviews. Builds run in GitHub Actions *(my call)*.

**Mine (open to change):**
- Avalonia UI.
- .NET 8.
- Capstone/Unicorn/Keystone.
- Managed compression.
- No in‑app flashing.
- JSON profiles with typed operations and no executable code.
- Curated donor adapters.
- Two‑dump check.
- No "build anyway" for beginners.
- Warning blinks at about 1 Hz and turns solid when "reduce motion" is on. CLI needs `--i-understand-the-risk`.
- First donor ASRock P10.43.
- HP reference test is semantic, not byte‑for‑byte (§11).
- Profiles bundled with the app, no online catalogue in the first release.
- Project at `G:\am4ever`.

**Left to the builder:**
- Internal class design.
- Test framework.
- UI styling and theme.
- How docs are rendered in‑app.
- Exact JSON schema details beyond §7.
- How native libraries are packaged per platform.
- CLI command names.
