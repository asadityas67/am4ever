# am4ever manual

am4ever ports a retail AMD **AM4** motherboard BIOS onto an OEM board (Lenovo/Dell/HP and
similar) so the OEM board boots newer Ryzen CPUs its locked firmware never supported — the
socket already takes them; only the firmware is missing. It takes the OEM board's own BIOS
dump plus a **donor** retail BIOS and produces a flashable image carrying the donor's newer
AGESA with the OEM board's own configuration (GPIO, lanes, display, audio, identity, fan).

It is the general form of the HP 285 G3 → ASRock AB350M Pro4 port; that board is the first
supported profile and the project's reference test.

## Read first — the risk

**Flashing a modified BIOS can permanently kill your PC.** You are responsible for what you do.
am4ever comes with no warranty (GPL-2.0-or-later). Always keep a backup of your original BIOS,
flash only with a programmer you know how to use (a CH341A must run at **3.3 V**), and verify
after writing. am4ever is not affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.

The app shows this warning on every launch and will not build for a beginner if a check fails.

## What's here

| Doc | For |
|---|---|
| [wizard.md](wizard.md) | **Beginners** — the six-step wizard: dump → profile → donor → identity → build → flash. |
| [board-lab.md](board-lab.md) | **Experts** — analysing a ROM, comparing modules, running them in the emulator. |
| [cli.md](cli.md) | The `am4ever` command line (`inspect`, `build`) and every build operation a profile can name. |
| [example-profile.md](example-profile.md) | The complete HP 285 G3 profile, annotated — the project's reference port. |
| [reference.md](reference.md) | The AM4/AMI structures, the profile operations, the donor-adapter format, the emulator host, and a one-page cheat sheet. |

## How it works, in one paragraph

An AM4 BIOS is an AMI Aptio V image with AMD PSP/BIOS directories. The CPU-support logic lives
in the donor's **CPM OEM PEIMs** (per-family modules that publish GPIO/lane/display tables) and
its AGESA. am4ever decompresses the donor's boot (PEI) volume, neutralises the donor's own
board-specific override routines, and writes the OEM board's GPIO/GEVENT/DXIO/DDI/audio tables
(read from the user's own dump) into each family's module; then it applies the OEM SMBIOS
identity and adds a small EC fan hand-off driver in the DXE volume. Everything is read from the
two ROMs the user supplies — **no firmware ships with am4ever**, and nothing leaves the machine.

## Everything is local

am4ever makes no network calls with your data. No dump, serial, UUID or key is ever sent
anywhere. Board profiles are small JSON files that describe *operations*, never firmware bytes;
the OEM tables are read from your own dump at build time.
