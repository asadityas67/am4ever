# The beginner wizard

The wizard is the default view when am4ever starts (after you accept the risk warning). It is six
steps with **Back**/**Next**; **Next** stays disabled, with a one-line reason, until the current
step is valid. If any check fails there is **no "build anyway"** — fix the input or stop.

You need: your board's own BIOS dump (read with your programmer), the matching **board profile**
(bundled with the app, or a file from someone who profiled your board), and the **donor** BIOS the
profile names.

## 1. Your BIOS
Pick your dump. A second read of the same chip is recommended — if you give two, they must match
byte for byte (a mismatch means a bad read). The wizard checks the file is the right size
(8/16/32 MiB), is an AMD AM4 image (has PSP/BIOS directories), and isn't blank/erased.

## 2. Board profile
The wizard lists the bundled profiles whose ROM size matches your dump. Each row shows the board,
the author, the CPUs it was **tested** on, and any **⚠ untested / inferred** items in plain words —
read these: an item marked inferred is a best guess, not a verified fact. Select your profile.

## 3. Donor BIOS
Download the exact donor BIOS the profile names (e.g. ASRock AB350M Pro4 P10.43) from the vendor's
site, and pick it here. The wizard verifies its **SHA-256** against the profile — a mismatch means
you have the wrong file or version.

## 4. Identity
By default the image gets a **generic** board identity (e.g. "HP"). You can instead load a
machine-data file (the `hp_dmi.json`-style output of a helper script) to carry your real serial,
UUID and SKU. Keys are never shown in full.

## 5. Build & checks
The wizard builds the modded image into a file you name and runs its checks (structure, directory
and FFS checksums, and that every recompressed volume decompresses back to exactly the patched
contents). On success it also writes a `<name>.report.md` listing everything changed, inferred and
untested. **If a check fails, no image is written** — the reason is shown and you cannot continue.

## 6. Flash checklist
Before you write the chip:
1. **3.3 V** — a CH341A must power the chip at 3.3 V (many boards default to 5 V and can damage it).
2. **Back up** — read the chip and save the original dump; it's your recovery.
3. **Erase → write → verify** with your programmer software; don't skip verify.
4. **Clear CMOS** after flashing.
5. First boot can take **1–2 minutes** with no display while memory trains — be patient.
6. If it won't boot, re-flash your backup and report back with the build report.
