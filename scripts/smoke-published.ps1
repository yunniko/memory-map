# Runs a published MemoryMap.exe the way a user would, through UI Automation (G-007 criterion 2):
# the window opens, every tab shows its content, a setting changed in the window is saved to the
# settings file and back, and a value written to the file is shown at the next start. The real
# settings file is backed up first and put back after (or removed, if there was none).
# Run in Windows PowerShell 5.1, which has UI Automation:
#   powershell -File scripts/smoke-published.ps1 [-Exe publish\win-x64\MemoryMap.exe]
param([string]$Exe = (Join-Path (Split-Path $PSScriptRoot) 'publish\win-x64\MemoryMap.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "Not built: $Exe (run scripts/publish.ps1)" }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace SmokeTest -Name User32 -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
'@
$A = [Windows.Automation.AutomationElement]
$settings = Join-Path $env:APPDATA 'MemoryMap\settings.json'
$backup = if (Test-Path $settings) { Get-Content $settings -Raw -Encoding UTF8 }
$failed = 0

function Check($ok, $what) { if ($ok) { "  ok    $what" } else { "  FAIL  $what"; $script:failed++ } }
function ById($root, $id) {
    $root.FindFirst('Descendants', (New-Object Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)))
}
function ChildCount($el) { if ($el) { $el.FindAll('Children', [Windows.Automation.Condition]::TrueCondition).Count } else { -1 } }
function SelectTab($win, $name) {
    $item = $win.FindFirst('Descendants', (New-Object Windows.Automation.AndCondition(
        (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)),
        (New-Object Windows.Automation.PropertyCondition($A::NameProperty, $name)))))
    $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Titles($win) { (ById $win 'TitlesBox').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern) }
function TitlesSaved { if (Test-Path $settings) { (Get-Content $settings -Raw | ConvertFrom-Json).ShowGroupTitles } }
# Polls $condition every 100 ms until it returns something true (returned) or $seconds pass ($null).
# UI Automation throws while the window is still building its tree; that counts as not yet.
function WaitFor([scriptblock]$condition, [int]$seconds = 15) {
    for ($i = 0; $i -lt $seconds * 10; $i++) {
        try { $result = & $condition; if ($result) { return $result } } catch { }
        Start-Sleep -Milliseconds 100
    }
    $null
}
function Start-App {
    $p = Start-Process $Exe -PassThru
    $win = WaitFor -seconds 30 {
        if (-not $p.HasExited) {
            $A::RootElement.FindFirst('Children', (New-Object Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)))
        }
    }
    $p, $win
}
function Restore-Settings {
    if ($null -ne $backup) { [IO.File]::WriteAllText($settings, $backup) } else { Remove-Item $settings -ErrorAction SilentlyContinue }
}

"== $Exe"
try {
    $p, $win = Start-App
    try {
        Check $win 'window opened within 30 s'
        if (-not $win) { throw 'no window' }
        [void][SmokeTest.User32]::SetForegroundWindow([IntPtr]$win.Current.NativeWindowHandle)
        Check (WaitFor { (ById $win 'SummaryText').Current.Name -like 'In use*' }) 'summary shows memory in use'
        Check (WaitFor { (ChildCount (ById $win 'LegendList')) -ge 5 }) 'legend lists the groups'
        # Each tab and an element only its content has (the treemap itself has no automation peer).
        $tabs = [ordered]@{ Map = 'BackButton'; Table = 'TableList'; History = 'SeriesLegend'; Startup = 'StartupList'; Settings = 'RefreshPicker' }
        foreach ($tab in $tabs.Keys) {
            SelectTab $win $tab
            $list = $tab -in 'Table', 'History', 'Startup'
            $shown = WaitFor { $el = ById $win $tabs[$tab]; if ($list) { (ChildCount $el) -gt 0 } else { [bool]$el } }
            Check ($shown -and -not $p.HasExited) "$tab tab shows its content"
        }
        # "Titles above the map's groups", switched in the window: the file follows, and back.
        $toggle = Titles $win
        $before = TitlesSaved
        $toggle.Toggle()
        $first = WaitFor { $v = TitlesSaved; if ($null -ne $v -and $v -ne $before) { "$v" } }
        $toggle.Toggle()
        Check ($first -and (WaitFor { "$(TitlesSaved)" -ne $first })) 'settings file saved a change and the change back'
        Check (-not $p.HasExited) 'still running after every tab'
    }
    finally {
        if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; $p.WaitForExit() }
    }

    # The file read at start: write the opposite of the saved value, start again, and look at the switch.
    try {
        $json = Get-Content $settings -Raw | ConvertFrom-Json
        $json.ShowGroupTitles = -not $json.ShowGroupTitles
        $json | ConvertTo-Json | Set-Content $settings -Encoding UTF8
        $p, $win = Start-App
        Check $win 'window opened again'
        if ($win) {
            WaitFor { SelectTab $win 'Settings'; $true } | Out-Null
            $state = WaitFor { "$((Titles $win).Current.ToggleState)" }
            Check (($state -eq 'On') -eq $json.ShowGroupTitles) 'setting read from the file at start'
        }
    }
    finally {
        if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force; $p.WaitForExit() }
    }
}
finally { Restore-Settings }
"smoke-published: $failed failure(s)"
exit [int]($failed -gt 0)
