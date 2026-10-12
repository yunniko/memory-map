<#
.SYNOPSIS
    Compares Memory Map's startup list with Windows Settings > Apps > Startup on this PC: the same
    entries, by name, and the same On/Off state. Read-only on both sides: it opens the Settings
    page and reads its switches through UI Automation, and never clicks one.
    Task Manager keeps the same states but runs elevated, so an unelevated script cannot read it;
    Settings is the reference (D018).
.NOTES
    Usage: pwsh scripts/startup-crosscheck.ps1   (builds memmap-dump first)
    Exit code 0 when everything agrees, 1 when anything differs (each difference is printed).
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'settings-startup.ps1')
$dump = Build-MemmapDump
$ours = (& $dump --startup --json | ConvertFrom-Json).entries

$theirs = @{}
foreach ($switch in Get-SettingsSwitches (Open-StartupSettings)) {
    if (-not $theirs.ContainsKey($switch.Name)) { $theirs[$switch.Name] = [System.Collections.Generic.List[bool]]::new() }
    $theirs[$switch.Name].Add($switch.Toggle.Current.ToggleState -eq 'On')
}
if ($theirs.Count -eq 0) { throw 'Read no switches from Settings; is the Startup page open?' }

$differences = 0
$ourNames = $ours | Group-Object Name
foreach ($group in $ourNames) {
    $states = @($group.Group | ForEach-Object { [bool]$_.enabled })
    if (-not $theirs.ContainsKey($group.Name)) {
        "Only in Memory Map: $($group.Name) ($($group.Group[0].location))"; $differences++; continue
    }
    $their = @($theirs[$group.Name])
    if ($their.Count -ne $states.Count) {
        "Count differs for $($group.Name): Memory Map $($states.Count), Settings $($their.Count)"; $differences++; continue
    }
    $a = ($states | Sort-Object) -join ','; $b = ($their | Sort-Object) -join ','
    if ($a -ne $b) { "State differs for $($group.Name): Memory Map $a, Settings $b"; $differences++ }
}
foreach ($name in $theirs.Keys | Where-Object { $_ -notin $ourNames.Name }) { "Only in Settings: $name"; $differences++ }

"Memory Map $($ours.Count) entries, Settings $(($theirs.Values | ForEach-Object Count | Measure-Object -Sum).Sum); $differences difference(s)"
exit [int]($differences -gt 0)
