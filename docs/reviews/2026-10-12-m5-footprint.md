# M5 footprint measurements — 2026-10-12

G-001 criterion 9: under 150 MB private working set and under 2% average CPU at a 1 s
refresh, on this PC (12 logical cores, about 440 processes). The scripts are throwaway
PowerShell (UI Automation picks the tab; `Process.TotalProcessorTime` gives CPU).

## Memory growth on the History tab (GPU rendering, before the fix)

Published single-file exe, History tab, sampled every 5 minutes: private memory went
169 → 229 → 241 → 211 → 228 → 229 MB over 30 minutes. Live managed heap after a full GC
was 14.6 MB (dotnet-gcdump) with 42 MB committed, so the rest was native.

## Rendering variants, side by side for 10 minutes (private MB at 1, 5 and 10 min; CPU)

| Variant | 1 min | 5 min | 10 min | CPU, one core |
|---|---|---|---|---|
| GPU (Avalonia default), History tab | 135 | 169 | 231 | 2.01% |
| Software rendering, History tab | 48 | 52 | 55 | 1.92% |
| GPU, Skia cache cut to 8 MB, History tab | 140 | 136 | 138 | 2.21% |
| GPU, Map tab | 137 | 128 | 130 | 2.44% |

Conclusion: the growth comes from GPU rendering of the chart's changing line paths. The GPU
backend also costs about 80 MB on its own. Software rendering removes both (D015).

## Pointer sweeping the plot (~33 moves a second, 30 s)

| Build | History tab | Map tab |
|---|---|---|
| Software rendering | 15.3% of one core, 51 MB | 14.2%, 48 MB |
| GPU | 15.2%, 138 MB | 13.7%, 131 MB |

## Where idle CPU goes

`memmap-dump --bench` costs 7.2 ms of CPU per refresh (0.7% of one core). The window
minimised costs 2.79% of one core, so drawing is not the main cost. Idle readings:
Settings tab 2.34%, Map tab 3.57%, History tab 2.71% (GPU build, 60 s each after 20 s
warm-up) — 0.2–0.3% of the machine's 12 cores.
