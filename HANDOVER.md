# memory-map — Handover
Last verified: 2026-10-12 at dbbeb13

A Windows desktop app (C#, Avalonia) showing RAM as a live treemap. Goal: `GOALS.md` G-001
(ACTIVE; M1 done, M2 next). Charter: `E:\CLAUDE\COMPANY\`.

## Current state

- **M1 data engine works; there is no window yet.** `scripts/check.ps1` passes: format
  clean, Release build with 0 warnings, 48/48 tests (Core unit and Windows integration).
- `memmap-dump` (`src/MemoryMap.Cli`) prints one breakdown as text or `--json`; `--bench`
  measured 7.9 ms wall and 7.8 ms CPU per refresh with 428 processes.
- `scripts/crosscheck.ps1` compares the dump with WMI. Latest run: worst difference 0.08% of RAM (limit 2%).
  It needs a quiet machine: right after a build it failed once at 2.47% from process churn.
  Detail: `docs/reviews/2026-10-12-m1-sampler-crosscheck.md`.
- "Shared & other" is about 3.4–4 GB (22–25% of RAM) on this PC. It is real shared memory,
  not a bug (D003).
- Not built yet: the Avalonia app (M2), drill-down and process actions (M3), history and
  tray (M4), single-file build (M5).
- The PowerShell predecessors `C:\bin\memory-report.ps1` and `C:\bin\memory-map.ps1` sit
  outside this repo; the six groups and the sizing rule come from them.

## How things fit together

- `MemoryMap.Core` is pure and OS-neutral. `Model.cs` defines the snapshot types and
  `IMemorySampler`. `Breakdown.cs` turns a snapshot into one `MapNode` tree
  (root → six groups → apps, folded "Other" nodes and figures → processes). `Treemap.cs`
  holds the squarified layout and `StableOrder`, which uses hysteresis so tiles don't reshuffle.
- `MemoryMap.Windows` holds `WindowsMemorySampler` (NtQuerySystemInformation +
  GetPerformanceInfo + one PDH query; D002) and sets each process's `ProcessKind`. The
  struct offsets in `Native.cs` are 64-bit only.
- Node keys are stable across refreshes and never derived from display names. Layout
  order and colours key on them (D005).
- The groups sum to total RAM plus `MemoryBreakdown.Skew`. Skew is the amount by which
  processes and kernel overshoot used memory because they are read a moment apart.
  It is normally 0.

## Rules in force

- Nothing leaves this PC without Owner approval: no GitHub repo, upload or store listing
  (G-001 constraint, VALUES.md → Restraint).
- v1 reads memory on Windows only. The sampler stays behind `IMemorySampler`, and Core
  holds no Windows rules (G-001 constraint).
- Process tiles are sized by private working set (G-001 criterion 1; D003).
- `scripts/check.ps1` is the merge gate until there is CI: no remote exists (STANDARDS.md
  → formatter in CI; blocked on the remote question below).

## Next steps and open questions

- M2: the Avalonia window. A custom-drawn treemap over `Treemap.Layout` with `StableOrder`,
  plus a hover tooltip, legend, table view, light/dark themes and headless UI tests.
  `Sample()` throws if any counter read fails (for example right after resume from sleep). The
  refresh loop must catch that, keep the last frame and retry; it must not crash the map.
- Open: a GitHub remote, needed for CI, has not been requested. Ask the Owner before
  creating one.

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|

## Decisions

See `docs/decisions/README.md`.
