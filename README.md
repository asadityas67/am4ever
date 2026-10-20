# am4ever

Keep old AMD AM4 office PCs out of e‑waste.

AMD AM4 office PCs from Lenovo, Dell and HP (HP 285 G3, HP ProDesk 405, Dell
OptiPlex 5055, Lenovo M715q/M75q and similar) shipped with OEM BIOSes that stop
at Ryzen 1000/2000 or 3000. The socket would take a Ryzen 5000. The only thing
missing is firmware, and OEMs will never ship it.

am4ever lets you fix that yourself. It takes **your own** OEM BIOS dump and a
retail AM4 board BIOS (the *donor*). It builds a flashable image: the donor's
newer AMD firmware, carrying your board's own configuration (lanes, GPIO, fans,
audio, display outputs, identity). Everything happens on your computer; nothing
is uploaded. You flash the result with an external SPI programmer.

## Status

**Milestone 1: shell only.** The app starts with its mandatory risk warning and
an empty main window (classic WinForms look, light and dark). It cannot build a
BIOS yet. See [`docs/design.md`](docs/design.md) for the full design and
[`docs/superpowers/plans/`](docs/superpowers/plans/) for the build plans.

## No warranty: you can kill your PC

Flashing a modified BIOS can permanently kill your PC if you are not careful.
You are responsible for what you do. am4ever comes with **no warranty**
(GPL‑2.0, sections 11 and 12). Always keep your original BIOS backup and know how
to write it back with your programmer before you flash anything.

am4ever is not affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.

## Building

Requires the .NET 8 SDK.

```bash
dotnet test
dotnet run --project src/Am4ever.App
```

Portable single‑file builds for Windows x64 and Linux x64 are produced by
`scripts/publish.ps1` / `scripts/publish.sh` and by CI.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Never post BIOS/ROM images.

## Licence

GPL‑2.0‑or‑later. See [LICENSE](LICENSE).
