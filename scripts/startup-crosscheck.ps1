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
$root = Split-Path $PSScriptRoot -Parent

dotnet build (Join-Path $root 'src/MemoryMap.Cli') -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE) { throw 'Build failed.' }
$dump = Join-Path $root 'src/MemoryMap.Cli/bin/Release/net10.0/memmap-dump.exe'
$ours = (& $dump --startup --json | ConvertFrom-Json).entries

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$any = [System.Windows.Automation.Condition]::TrueCondition
$children = [System.Windows.Automation.TreeScope]::Children
Start-Process 'ms-settings:startupapps'
$settings = $null
for ($i = 0; $i -lt 20 -and -not $settings; $i++) {
    Start-Sleep -Milliseconds 500
    # Found by process, not by title: the title is in the display language.
    $pids = @(Get-Process SystemSettings -ErrorAction SilentlyContinue | ForEach-Object Id)
    $settings = $A::RootElement.FindAll($children, $any) |
        Where-Object { $_.Current.ClassName -eq 'ApplicationFrameWindow' } |
        Where-Object { $_.FindAll($children, $any) |
                Where-Object { $_.Current.ProcessId -in $pids } } |
        Select-Object -First 1
}
if (-not $settings) { throw 'The Settings window did not appear.' }
Start-Sleep -Seconds 2   # the list fills in after the window opens

$theirs = @{}
foreach ($e in $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants, $any)) {
    $toggle = $null
    if ($e.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$toggle) -and $e.Current.Name) {
        if (-not $theirs.ContainsKey($e.Current.Name)) { $theirs[$e.Current.Name] = [System.Collections.Generic.List[bool]]::new() }
        $theirs[$e.Current.Name].Add($toggle.Current.ToggleState -eq 'On')
    }
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
