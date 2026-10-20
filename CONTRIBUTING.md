# Contributing to am4ever

Thanks for helping keep AM4 machines out of e‑waste.

## The one hard rule

Never attach or commit BIOS/ROM images, dumps or finished modded images, in issues, pull requests or anywhere else.

The firmware belongs to AMD, AMI and the board vendors. am4ever stays legal to
host and use because it never distributes it: every user supplies their own
dump and downloads the donor BIOS from the vendor. CI rejects pull requests that
add large binaries or files that look like firmware.

## What to contribute

- **Board profiles** (`profiles/`) and **donor adapters** (`donors/`) arrive as
  pull requests. The maintainer reviews every one before it ships. A profile
  holds recipes and offsets only, never firmware.
- **Bug reports** go in issues, with the am4ever **build report** (the
  Markdown file saved next to your built image), not ROMs.
- **Code and docs:** keep the `// SPDX-License-Identifier: GPL-2.0-or-later`
  header on every C# file, add tests for every change, and run `dotnet test`
  before opening a pull request.

## Licence

By contributing you agree your contribution is licensed GPL‑2.0‑or‑later.
