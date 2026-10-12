# M1 sampler cross-check — 2026-10-12

Machine: the Owner's laptop (15.4 GB RAM, Windows 11 Home 10.0.26200), shortly after a reboot.
Method: `scripts/crosscheck.ps1` runs `memmap-dump --json` and then reads the same figures
from WMI (`Win32_OperatingSystem`, `Win32_PerfFormattedData_PerfOS_Memory`,
`Win32_PerfFormattedData_PerfProc_Process`) straight after. The two reads are a few hundred
milliseconds apart, so live memory moves between them.

## Results (three runs)

| Figure | Run 1 diff | Run 2 diff | Run 3 diff |
|---|---|---|---|
| Total | 0.00% | 0.00% | — |
| Available | −0.07% | −0.13% | — |
| Standby | +0.23% | −0.07% | — |
| Nonpaged pool | 0.00% | +0.01% | — |
| Paged pool resident | 0.00% | −0.01% | — |
| Commit | +0.16% | +0.26% | — |
| Sum of private working sets | −0.11% | −0.03% | −0.08% |

Differences are in % of total RAM; limit (criterion 3) is 2%. Worst: 0.26%. Run 3 was after
adding the modified-page-list counter and printed only the summary line and last row.
Top apps in run 1 (chrome, claude, firefox, jetbrains-toolbox, dotnet) matched WMI's
per-process private working set within 0.07% of RAM.

## Refresh cost

`memmap-dump --bench` (50 refreshes of sample + breakdown, ~390 processes): 6.5 ms wall /
7.5 ms CPU and 7.9 ms wall / 8.1 ms CPU per refresh in two runs — under 1% of one core at a
1 s refresh.

## Observation

"Shared & other" measured about 4.0 GB (25% of RAM) with Chrome, Firefox and Claude open; the
modified page list was only 45 MB of it. The rest is memory shared between processes, which
these counters cannot split per process (D003).

## Re-run after the M1 restructure (D005)

The first run, straight after a build and test run, failed: sum of private working sets +2.47%
of RAM. Compiler and test-host processes were exiting between the sampler's read and WMI's.
Three runs, 5 s apart once the machine was quiet: −0.86%, −0.02%, 0.00%; worst figure overall
0.86% (PASS). Refresh cost after the restructure: 7.86 ms wall / 7.81 ms CPU per refresh,
428 processes. The cross-check needs a quiet machine; a failure right after heavy process
churn should be re-run before it is believed.
