# Builds the self-contained single-file MemoryMap.exe into publish\ and copies it to C:\bin (D014).
# Usage: pwsh scripts/publish.ps1 [-Install <dir>] [-NoInstall]
param([string]$Install = 'C:\bin', [switch]$NoInstall)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'publish'

# Native Skia/HarfBuzz libraries go inside the exe too, so it is one file to copy.
# Not compressed and not trimmed: see D014.
dotnet publish (Join-Path $root 'src/MemoryMap.App') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $out 'MemoryMap.exe'
"Built $exe ($([math]::Round((Get-Item $exe).Length / 1MB)) MB)"
if (-not $NoInstall) {
    if (Get-Process MemoryMap -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $Install 'MemoryMap.exe')) {
        throw "MemoryMap.exe is running from $Install; exit it (tray menu > Exit) and run this again."
    }
    Copy-Item $exe $Install -Force
    "Installed $(Join-Path $Install 'MemoryMap.exe')"
}
