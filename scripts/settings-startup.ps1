<#
.SYNOPSIS
    Shared by the startup check scripts (dot-source it): builds memmap-dump, and opens
    Windows Settings > Apps > Startup and reads its switches through UI Automation.
#>
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# Builds the CLI in Release and returns the path of memmap-dump.exe.
function Build-MemmapDump {
    $root = Split-Path $PSScriptRoot -Parent
    dotnet build (Join-Path $root 'src/MemoryMap.Cli') -c Release --nologo -v quiet | Out-Null
    if ($LASTEXITCODE) { throw 'Build failed.' }
    Join-Path $root 'src/MemoryMap.Cli/bin/Release/net10.0/memmap-dump.exe'
}

# Opens Settings on its Startup page and returns the window. -Restart closes Settings first,
# because the page does not re-read the registry while it is open (it closes any other page too).
function Open-StartupSettings([switch]$Restart) {
    $A = [System.Windows.Automation.AutomationElement]
    $any = [System.Windows.Automation.Condition]::TrueCondition
    $children = [System.Windows.Automation.TreeScope]::Children
    if ($Restart) {
        Get-Process SystemSettings -ErrorAction SilentlyContinue | Stop-Process
        Start-Sleep -Milliseconds 500
    }
    Start-Process 'ms-settings:startupapps'
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 700
        # Found by process, not by title: the title is in the display language.
        $pids = @(Get-Process SystemSettings -ErrorAction SilentlyContinue | ForEach-Object Id)
        $window = $A::RootElement.FindAll($children, $any) |
            Where-Object { $_.Current.ClassName -eq 'ApplicationFrameWindow' } |
            Where-Object { $_.FindAll($children, $any) | Where-Object { $_.Current.ProcessId -in $pids } } |
            Select-Object -First 1
        if ($window) {
            Start-Sleep -Seconds 2   # the list fills in after the window opens
            return $window
        }
    }
    throw 'The Settings window did not appear.'
}

# Every switch on the page, as { Name, Toggle } (Toggle is the UIA TogglePattern).
function Get-SettingsSwitches($window) {
    $any = [System.Windows.Automation.Condition]::TrueCondition
    foreach ($e in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $any)) {
        $toggle = $null
        if ($e.Current.Name -and $e.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$toggle)) {
            [pscustomobject]@{ Name = $e.Current.Name; Toggle = $toggle }
        }
    }
}
