# memory-map — Handover
Last verified: 2026-10-12 at af0ae53

A Windows desktop app (C#, Avalonia) showing RAM as a live treemap. Goal: `GOALS.md` G-001
(ACTIVE; M1 and M2 done, M3 next). Charter: `E:\CLAUDE\COMPANY\`.

## Current state

- **The live map window works.** `dotnet run --project src/MemoryMap.App -c Release` opens
  it: the six groups as framed areas with their apps inside, refreshed every second. It
  shows a summary line, a legend, a hover tooltip (size, % of RAM, process count, PIDs),
  a Table tab with the same items, and light and dark themes that follow Windows.
- `scripts/check.ps1` passes: format clean, Release build with 0 warnings, 73/73 tests
  (Core unit, Windows integration, App headless window tests).
- Measured live on this PC, 441 processes: 0.45% CPU over 20 s, 134 MB private
  (criterion 9's limits: 2% and 150 MB). The memory figure is close to the limit; M5
  measures it properly.
- A failed sample keeps the last map and says so in the status line. A sampler that cannot
  start shows its error in the window.
- `memmap-dump` (`src/MemoryMap.Cli`) prints one breakdown as text or `--json`. A refresh
  costs about 7.9 ms with 428 processes (`--bench`).
- `scripts/crosscheck.ps1` compares the dump with WMI. Latest run: worst difference 0.08%
  of RAM (limit 2%). It needs a quiet machine: right after a build it failed once at 2.47%
  from process churn. Detail: `docs/reviews/2026-10-12-m1-sampler-crosscheck.md`.
- "Shared & other" is about 3.4–4 GB (22–25% of RAM) on this PC. It is real shared memory,
  not a bug (D003).
- Not built yet: drill-down and process actions (M3), history and tray (M4), single-file
  build (M5).

## How things fit together

- `MemoryMap.Core` is pure and OS-neutral:
  - `Breakdown.cs` turns a snapshot into one `MapNode` tree, keyed by stable IDs (D005).
  - `Treemap.cs` holds the squarified layout and `StableOrder`.
  - `Scene.cs` (`SceneArranger`) arranges any node as frames plus tiles and hit-tests them.
  - `Describe.cs` holds the words the tooltip and table share.
  - `LiveMemory.cs` is the sampling loop.
  - Views go through these, never their own layout or wording (D007).
- `MemoryMap.Windows` holds `WindowsMemorySampler` (native APIs; D002). The struct offsets
  in `Native.cs` are 64-bit only.
- `MemoryMap.App`:
  - `TreemapControl.cs` draws a scene.
  - `MainViewModel.cs` takes `LiveMemory` events on a pool thread and applies the latest on
    the UI thread.
  - Colours come from the `Map.*` theme resources in `App.axaml` through `MapPalette.cs`,
    cached per theme.
- The groups sum to total RAM plus `MemoryBreakdown.Skew`: processes and kernel are read a
  moment apart. It is normally 0.

## Rules in force

- Nothing leaves this PC without Owner approval: no GitHub repo, upload or store listing
  (G-001 constraint, VALUES.md → Restraint).
- v1 reads memory on Windows only. The sampler stays behind `IMemorySampler`, and Core
  holds no Windows rules (G-001 constraint).
- Process tiles are sized by private working set (G-001 criterion 1; D003).
- xunit.v3 stays at the version `Avalonia.Headless.XUnit` is built against, 3.2.2 today
  (D006).
- `scripts/check.ps1` is the merge gate until there is CI: no remote exists (STANDARDS.md
  → formatter in CI; blocked on the remote question below).

## Next steps and open questions

- M3: drill-down and process actions.
  - Click an app to zoom to its processes; a breadcrumb or Back returns. `SceneArranger`
    already arranges any node.
  - A context menu offers *End process*, always confirmed. It is guarded against PID reuse
    by the process key's start time, and reports access-denied honestly.
  - The same menu offers *Open file location*.
- Deferred from the M2 review: labels are re-created on every hover repaint. This is
  revisited in M5 if the footprint measurement calls for it.
- Open: a GitHub remote, needed for CI, has not been requested. Ask the Owner before
  creating one.

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|

## Decisions

See `docs/decisions/README.md`.
