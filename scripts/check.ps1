<#
.SYNOPSIS
    The project's merge gate, run locally and in CI (.github/workflows/check.yml) on Windows and Linux:
    formatting, a warnings-as-errors build, and every test that runs on this OS (on Linux the
    Windows tests are built but not run, D030).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$solution = 'MemoryMap.slnx'
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
