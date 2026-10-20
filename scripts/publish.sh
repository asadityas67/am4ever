#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-2.0-or-later
# Builds the portable am4ever binaries into publish/am4ever-<rid>/.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
rids=("${@:-linux-x64}")
for rid in "${rids[@]}"; do
  out="$root/publish/am4ever-$rid"
  rm -rf "$out"
  for proj in src/Am4ever.App src/Am4ever.Cli; do
    dotnet publish "$root/$proj" -p:PublishProfile="$rid" -nologo
  done
  ls -l "$out"
done
