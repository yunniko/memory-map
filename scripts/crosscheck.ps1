<#
.SYNOPSIS
    Compares memmap-dump's figures with Windows' own WMI counters, sampled back to back.
    G-001 acceptance criterion 3: totals agree within 2% of total RAM.
    Build first: dotnet build -c Release
#>
$root = Split-Path $PSScriptRoot -Parent
$dll = Join-Path $root 'src\MemoryMap.Cli\bin\Release\net10.0\memmap-dump.dll'
if (-not (Test-Path $dll)) { throw "Build first: dotnet build -c Release ($dll not found)" }

$dump = & dotnet $dll --json | ConvertFrom-Json
$os = Get-CimInstance Win32_OperatingSystem
$pm = Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory
$wmiProcs = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process | Where-Object { $_.Name -notin '_Total', 'Idle' }
$total = $os.TotalVisibleMemorySize * 1KB

function Row($name, $sampler, $wmi) {
    [pscustomobject]@{
        Figure          = $name
        'Sampler MB'    = [int]($sampler / 1MB)
        'WMI MB'        = [int]($wmi / 1MB)
        'Diff % of RAM' = [math]::Round(100 * ($sampler - $wmi) / $total, 2)
    }
}

$processGroups = $dump.groups | Where-Object { $_.group -in 'Apps', 'Windows', 'VirtualMachines' }
$rows = @(
    Row 'Total' $dump.system.TotalPhysical $total
    Row 'Available' $dump.system.Available ($os.FreePhysicalMemory * 1KB)
    Row 'Standby' $dump.system.Standby ($pm.StandbyCacheCoreBytes + $pm.StandbyCacheNormalPriorityBytes + $pm.StandbyCacheReserveBytes)
    Row 'Nonpaged pool' $dump.system.NonpagedPool $pm.PoolNonpagedBytes
    Row 'Paged pool resident' $dump.system.PagedPoolResident $pm.PoolPagedResidentBytes
    Row 'Modified pages' $dump.system.ModifiedPages $pm.ModifiedPageListBytes
    Row 'Commit' $dump.system.CommitTotal (($os.TotalVirtualMemorySize - $os.FreeVirtualMemory) * 1KB)
    Row 'Sum of private WS' (($processGroups.Bytes | Measure-Object -Sum).Sum) (($wmiProcs | Measure-Object WorkingSetPrivate -Sum).Sum)
)
$rows | Format-Table -AutoSize
$worst = ($rows | ForEach-Object { [math]::Abs($_.'Diff % of RAM') } | Measure-Object -Maximum).Maximum
if ($worst -gt 2) { Write-Error "Worst difference $worst% of RAM exceeds 2%"; exit 1 }
"PASS: worst difference $worst% of RAM (limit 2%)"
