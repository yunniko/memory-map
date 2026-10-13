# Builds the Native AOT MemoryMap.exe (D043) into publish\win-x64 and copies it to C:\bin.
# The exe needs its two native libraries beside it, so all three files are copied.
# Usage: pwsh scripts/publish.ps1 [-Install <dir>] [-NoInstall]
# Needs the C++ build tools (Visual Studio's "Desktop development with C++" workload).
param([string]$Install = 'C:\bin', [switch]$NoInstall)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'publish\win-x64'

# The AOT compiler finds the C++ linker through vswhere, which is not on PATH outside a developer prompt.
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $root 'src/MemoryMap.App') -c Release -r win-x64 -p:DebugType=none -nologo -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
# The native libraries' own symbol files come with their packages; they do not ship.
Remove-Item (Join-Path $out '*.pdb')
$files = Get-ChildItem $out -File
$names = ($files.Name | Sort-Object) -join ', '
if ($names -ne 'libHarfBuzzSharp.dll, libSkiaSharp.dll, MemoryMap.exe') { throw "Unexpected files in ${out}: $names" }
"Built $out ($([math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB)) MB): $names"
if (-not $NoInstall) {
    if (Get-Process MemoryMap -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $Install 'MemoryMap.exe')) {
        throw "MemoryMap.exe is running from $Install; exit it (tray menu > Exit) and run this again."
    }
    # Into the folder, never as a file named after it. The exe keeps its 0.4.1 path (D044).
    New-Item -ItemType Directory -Force $Install | Out-Null
    Copy-Item $files.FullName $Install -Force
    "Installed $names in $Install"
}
