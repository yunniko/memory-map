# Checks the elevated startup helper's plumbing without elevating: runs the built MemoryMap.exe
# and memmap-dump.exe in helper mode, unelevated, with ids it must refuse and one machine id that
# is not registered, and checks each exit code (2 refused, 3 gone; see FlagWrite). Nothing is
# written: every case stops before the flag write. Refuses to run elevated, where a mistake could
# write a real machine flag. The approve/decline prompt itself is checked by hand (D024).
# Run: pwsh scripts/startup-helper-check.ps1 [-Exe <path>,...]   (default: both Release builds,
# after scripts/check.ps1; pass C:\bin\MemoryMap.exe to check the installed copy)
param([string[]]$Exe)
$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this unelevated: elevated, the helper could write real machine-wide flags.'
}
$root = Split-Path $PSScriptRoot
$exes = if ($Exe) { $Exe } else { @(
        "$root\src\MemoryMap.App\bin\Release\net10.0-windows\MemoryMap.exe",
        "$root\src\MemoryMap.Cli\bin\Release\net10.0\memmap-dump.exe"
    ) }
$cases = @(
    @{ Name = 'user id refused'; Args = @('--startup-helper', 'run/user/Anything', 'off'); Want = 2 },
    @{ Name = 'bad state refused'; Args = @('--startup-helper', 'run/machine/X', 'maybe'); Want = 2 },
    @{ Name = 'path out of the folder refused'; Args = @('--startup-helper', 'folder/machine/..\..\x.lnk', 'off'); Want = 2 },
    @{ Name = 'unregistered id with spaces is gone'; Args = @('--startup-helper', 'run/machine/No Such Entry MemoryMap', 'off'); Want = 3 }
)
$failed = 0
foreach ($path in $exes) {
    if (-not (Test-Path $path)) { throw "Not built: $path" }
    foreach ($case in $cases) {
        $quoted = $case.Args | ForEach-Object { '"' + $_ + '"' }
        $p = Start-Process -FilePath $path -ArgumentList $quoted -Wait -PassThru -WindowStyle Hidden
        $ok = $p.ExitCode -eq $case.Want
        if (-not $ok) { $failed++ }
        '{0} {1,-16} {2,-36} exit {3} (want {4})' -f ($(if ($ok) { 'ok  ' } else { 'FAIL' })), (Split-Path $path -Leaf), $case.Name, $p.ExitCode, $case.Want
    }
}
$total = $exes.Count * $cases.Count
"startup-helper-check: $($total - $failed)/$total"
if ($failed) { exit 1 }
