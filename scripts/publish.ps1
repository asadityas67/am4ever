# SPDX-License-Identifier: GPL-2.0-or-later
# Builds the portable am4ever binaries into publish/am4ever-<rid>/.
param([string[]]$Rid = @('win-x64'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
foreach ($r in $Rid) {
    $out = Join-Path $root "publish/am4ever-$r"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    foreach ($proj in 'src/Am4ever.App', 'src/Am4ever.Cli') {
        dotnet publish (Join-Path $root $proj) -p:PublishProfile=$r -nologo
        if ($LASTEXITCODE -ne 0) { throw "publish failed: $proj ($r)" }
    }
    Get-ChildItem $out | Select-Object Name, Length | Format-Table | Out-String | Write-Host
}
