<#
.SYNOPSIS
    Checks Memory Map's startup switch against Windows itself, on this PC, using two throwaway
    entries it creates and removes: a Run value (a copy of whoami.exe in %TEMP%) and a .cmd file
    in the user's Startup folder. For each it switches the entry off and on through memmap-dump
    (the app's own code) and reads the state Windows Settings shows, then flips Settings' switch
    and reads the state memmap-dump shows. No real startup entry is touched.
.NOTES
    Usage: pwsh scripts/startup-roundtrip.ps1   (builds memmap-dump first)
    It closes and reopens Windows Settings for each check, because the Startup page does not
    re-read the registry while open. Exit code 0 when every check agrees, 1 otherwise.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'settings-startup.ps1')
$dump = Build-MemmapDump

$name = 'MemoryMapRoundTrip'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$approved = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved'
$exeFolder = Join-Path $env:TEMP $name
$exe = Join-Path $exeFolder "$name.exe"
$cmd = Join-Path ([Environment]::GetFolderPath('Startup')) "$name.cmd"

# The switch Settings shows for an entry, found by its label, read from a freshly opened page.
function SettingsSwitch([string]$label) {
    $window = Open-StartupSettings -Restart
    # The page fills in over a few seconds after the window appears; poll for this entry.
    for ($i = 0; $i -lt 15; $i++) {
        $switch = Get-SettingsSwitches $window | Where-Object Name -eq $label | Select-Object -First 1
        if ($switch) { return $switch.Toggle }
        Start-Sleep -Milliseconds 700
    }
    throw "Settings does not list $label."
}

function Ours([string]$id) {
    $entry = (& $dump --startup --json | ConvertFrom-Json).entries | Where-Object id -eq $id
    if (-not $entry) { throw "memmap-dump does not list $id." }
    [bool]$entry.enabled
}

$failures = 0
function Check([string]$what, $expected, $actual) {
    $ok = $expected -eq $actual
    if (-not $ok) { $script:failures++ }
    '{0,-4} {1}: expected {2}, got {3}' -f ($(if ($ok) { 'ok' } else { 'FAIL' }), $what, $expected, $actual)
}

try {
    New-Item -ItemType Directory $exeFolder -Force | Out-Null
    Copy-Item "$env:SystemRoot\System32\whoami.exe" $exe
    New-ItemProperty $runKey -Name $name -Value "`"$exe`"" -PropertyType String -Force | Out-Null
    Set-Content $cmd 'exit' -Encoding ascii

    $cases = @(
        @{ Id = "run/user/$name"; Label = (Get-Item $exe).VersionInfo.FileDescription },
        @{ Id = "folder/user/$name.cmd"; Label = "$name.cmd" }
    )
    foreach ($case in $cases) {
        foreach ($state in 'off', 'on') {
            $said = & $dump --startup-set $case.Id $state
            if ($LASTEXITCODE) { throw "memmap-dump could not switch $($case.Id) ${state}: $said" }
            Check "$($case.Id): Memory Map switched $state, Settings shows" ($state -eq 'on') `
                ((SettingsSwitch $case.Label).Current.ToggleState -eq 'On')
        }
        foreach ($state in 'off', 'on') {
            (SettingsSwitch $case.Label).Toggle()
            Start-Sleep -Seconds 1
            Check "$($case.Id): Settings switched $state, Memory Map reads" ($state -eq 'on') (Ours $case.Id)
        }
    }
}
finally {
    Get-Process SystemSettings -ErrorAction SilentlyContinue | Stop-Process
    Remove-ItemProperty $runKey -Name $name -ErrorAction SilentlyContinue
    Remove-ItemProperty "$approved\Run" -Name $name -ErrorAction SilentlyContinue
    Remove-ItemProperty "$approved\StartupFolder" -Name "$name.cmd" -ErrorAction SilentlyContinue
    Remove-Item $cmd, $exeFolder -Recurse -ErrorAction SilentlyContinue
    $left = @((Get-Item $runKey).GetValue($name), (Get-Item "$approved\Run").GetValue($name),
        (Get-Item "$approved\StartupFolder").GetValue("$name.cmd")) | Where-Object { $null -ne $_ }
    if ($left -or (Test-Path $cmd) -or (Test-Path $exeFolder)) { 'FAIL cleanup: a throwaway entry was left behind'; $failures++ }
    else { 'Throwaway entries removed.' }
}
"$failures failure(s)"
exit [int]($failures -gt 0)
