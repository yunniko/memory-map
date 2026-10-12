# memory-map — Handover
Last verified: 2026-10-12 at 6a53bec

A Windows desktop app (C#, Avalonia) showing RAM as a live treemap. Goal: `GOALS.md` G-001
(ACTIVE; M1–M3 done, M4 next). Charter: `E:\CLAUDE\COMPANY\`.

## Current state

- **The live map window works.** `dotnet run --project src/MemoryMap.App -c Release` opens
  it: the six groups as framed areas with their apps inside, refreshed every second. It
  shows a summary line, a legend, a hover tooltip, a Table tab with the same items, and
  light and dark themes that follow Windows.
- **Drill-down works.** Clicking a group, an app or the "Other" fold zooms into it. A
  breadcrumb, the Back button, Esc, Backspace, Alt+Left and the mouse back button return
  (the keys and mouse button act only on the Map tab). The zoomed view follows its node
  across refreshes and falls back to the nearest ancestor when the node goes (D009).
- **Process actions work.** Right-click opens a menu for the tile under the pointer:
  - *End …* for a single process (or an app with exactly one process). It is always
    confirmed, defaults to Cancel, and is guarded by the start time against PID reuse
    (D008). Critical processes and the app itself are refused.
  - An app with several processes offers no End; the menu says to zoom in and pick one.
  - *Open file location* opens Explorer at the executable, trying the app's next process
    if one has exited.
  - Access denied, exited and failed outcomes show in a dismissable notice bar. A
    command that throws becomes a notice, not a crash.
- `scripts/check.ps1` passes: format clean, Release build with 0 warnings, 105/105 tests
  (Core 62, Windows integration 20, App headless 23).
- Not run: the critical-process refusal, because it needs an elevated session. The
  System-process test checks the unelevated path (access denied).
- Live on this PC, 440 processes: 122 MB private after 6 s (criterion 9's limit is
  150 MB). CPU was last measured in M2 (0.45% over 20 s); M5 measures both properly.
- A failed sample keeps the last map and says so in the status line. A sampler that cannot
  start shows its error in the window.
- `memmap-dump` (`src/MemoryMap.Cli`) prints one breakdown as text or `--json`. A refresh
  costs about 7.9 ms with 428 processes (`--bench`).
- `scripts/crosscheck.ps1` compares the dump with WMI. Latest run: worst difference 0.08%
  of RAM (limit 2%). It needs a quiet machine. Detail:
  `docs/reviews/2026-10-12-m1-sampler-crosscheck.md`.
- "Shared & other" is about 3.4–4 GB (22–25% of RAM) on this PC. It is real shared memory,
  not a bug (D003).
- Not built yet: history and tray (M4), single-file build (M5).

## How things fit together

- `MemoryMap.Core` is pure and OS-neutral:
  - `Breakdown.cs` turns a snapshot into one `MapNode` tree, keyed by stable IDs (D005).
  - `Treemap.cs` holds the squarified layout and `StableOrder`.
  - `Scene.cs` (`SceneArranger`) arranges any node as frames plus tiles and hit-tests them.
  - `Describe.cs` holds the words the tooltip and table share.
  - `Navigation.cs` keeps the zoom as a chain of keys (D009).
  - `ProcessActions.cs` holds `IProcessActions`, the outcome wording (`ActionResult`), the
    confirm question, and `NodeCommands`, the one rule set behind both the context menu
    and the hover hint.
  - `LiveMemory.cs` is the sampling loop.
  - Views go through these, never their own layout or wording (D007).
- `MemoryMap.Windows` holds `WindowsMemorySampler` (native APIs; D002) and
  `WindowsProcessActions` (D008). The struct offsets in `Native.cs` are 64-bit only.
- `MemoryMap.App`:
  - `TreemapControl.cs` draws a scene and raises `NodeClicked`.
  - `MainViewModel.cs` takes `LiveMemory` events on a pool thread and applies the latest on
    the UI thread. Menu commands run through `RunAsync`, which never throws.
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
- Ending a process is always confirmed and acts on one process only, on a handle whose
  start time matches the sample (G-001 criterion 6; D008).
- xunit.v3 stays at the version `Avalonia.Headless.XUnit` is built against, 3.2.2 today
  (D006).
- `scripts/check.ps1` is the merge gate until there is CI: no remote exists (STANDARDS.md
  → formatter in CI; blocked on the remote question below).

## Next steps and open questions

- M4: history graph and tray.
  - An in-memory ring buffer of samples covering at least 30 minutes.
  - A timeline view of used memory, kernel pools and the top apps.
  - A tray icon with a live % tooltip, a low-memory alert and close-to-tray.
  - Settings for the refresh interval and the alert threshold.
- Deferred from the M2 review: labels are re-created on every hover repaint. This is
  revisited in M5 if the footprint measurement calls for it.
- Open: a GitHub remote, needed for CI, has not been requested. Ask the Owner before
  creating one.

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|

## Decisions

See `docs/decisions/README.md`.
