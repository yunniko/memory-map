<#
.SYNOPSIS
    The project's merge gate, run locally (no CI until the project has a remote):
    formatting, a warnings-as-errors build, and every test.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet format MemoryMap.slnx --verify-no-changes
    if ($LASTEXITCODE) { throw 'Formatting check failed: run `dotnet format MemoryMap.slnx`.' }
    dotnet build MemoryMap.slnx -c Release
    if ($LASTEXITCODE) { throw 'Build failed.' }
    dotnet test --solution MemoryMap.slnx -c Release --no-build
    if ($LASTEXITCODE) { throw 'Tests failed.' }
    'check: all passed'
}
finally {
    Pop-Location
}
