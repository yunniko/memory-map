<#
.SYNOPSIS
    The project's merge gate, run locally and in CI (.github/workflows/check.yml) on Windows and Linux:
    formatting, a warnings-as-errors build, and every test that runs on this OS.
.NOTES
    On Linux the gate covers MemoryMap.Linux.slnf: the app and the Windows tests need Windows until
    G-003 M2 makes the app cross-platform.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# $IsWindows is unset in Windows PowerShell 5.1 ('Desktop'), which runs on Windows only.
$onWindows = $IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop'
$solution = if ($onWindows) { 'MemoryMap.slnx' } else { 'MemoryMap.Linux.slnf' }
Push-Location $root
try {
    dotnet format $solution --verify-no-changes
    if ($LASTEXITCODE) { throw "Formatting check failed: run ``dotnet format $solution``." }
    dotnet build $solution -c Release
    if ($LASTEXITCODE) { throw 'Build failed.' }
    dotnet test --solution $solution -c Release --no-build
    if ($LASTEXITCODE) { throw 'Tests failed.' }
    "check: all passed ($solution)"
}
finally {
    Pop-Location
}
