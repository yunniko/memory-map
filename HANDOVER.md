# memory-map — Handover
Last verified: 2026-10-12 at a89c100

A Windows desktop app (C#, Avalonia) showing RAM as a live treemap. Goal: `GOALS.md` G-001
(ACTIVE; M1–M4 done, M5 next). Charter: `E:\CLAUDE\COMPANY\`.

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
- **History works.** The History tab charts In use, Kernel pools and the window's five
  biggest apps on one byte axis. Its window is 5, 15, 30 or 60 minutes. Lines break where
  sampling paused, and hovering shows every line's value at that second. One point is kept
  per second for 60 minutes (D010). A line keeps its colour while its app stays charted
  (D012).
- **Tray works.** The icon is a gauge of RAM in use, and its tooltip gives the figures.
  Clicking it or *Show Memory Map* brings the window back; *Exit* quits. Closing the window
  hides it to the tray when that setting is on (the default).
- **The low-memory alert works.** It fires when available RAM falls below a threshold
  (default 10%). It is a corner window that takes no focus, names the biggest app, and
  closes after 20 s or when memory recovers by 2 points. One alert is shown at a time (D011).
- **Settings work.** The Settings tab covers refresh (0.5–5 s), alerts on/off, the
  threshold and close-to-tray. Each change applies at once and is saved to
  %APPDATA%\MemoryMap\settings.json. A file that cannot be read or saved is reported in
  the tab, and the defaults are used (D013).
- `scripts/check.ps1` passes: format clean, Release build with 0 warnings, 137/137 tests
  (Core 79, Windows integration 20, App headless 38).
- Not run: the critical-process refusal, because it needs an elevated session. The
  System-process test checks the unelevated path (access denied).
- Live on this PC, about 440 processes, refresh 1 s, after a 20 s warm-up and measured
  over 60 s on the History tab: 2.71% of one core (0.23% of all 12) and 129 MB private.
  Criterion 9's limits are 150 MB and 2% CPU. M5 measures with a full hour of history.
- A failed sample keeps the last map and says so in the status line. A sampler that cannot
  start shows its error in the window.
- `memmap-dump` (`src/MemoryMap.Cli`) prints one breakdown as text or `--json`. A refresh
  costs about 7.9 ms with 428 processes (`--bench`).
- `scripts/crosscheck.ps1` compares the dump with WMI. Latest run: worst difference 0.08%
  of RAM (limit 2%). It needs a quiet machine. Detail:
  `docs/reviews/2026-10-12-m1-sampler-crosscheck.md`.
- "Shared & other" is about 3.4–4 GB (22–25% of RAM) on this PC. It is real shared memory,
  not a bug (D003).
- Not built yet: the single-file build (M5).

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
  - `History.cs` holds the per-second ring. `HistoryChart.cs` builds the chart's series,
    ticks and tooltip; `SeriesSlots` keeps line colours sticky.
  - `LowMemoryAlert.cs` decides when the alert is raised and cleared, and words it.
  - `AppSettings.cs` holds the choices, their labels, and the JSON store.
  - Views go through these, never their own layout or wording (D007).
- `MemoryMap.Windows` holds `WindowsMemorySampler` (native APIs; D002) and
  `WindowsProcessActions` (D008). The struct offsets in `Native.cs` are 64-bit only.
- `MemoryMap.App`:
  - `TreemapControl.cs` draws a scene and raises `NodeClicked`.
  - `MainViewModel.cs` takes `LiveMemory` events on a pool thread and applies the latest on
    the UI thread. Menu commands run through `RunAsync`, which never throws. Every sample
    feeds the history and the alert. The chart is built only while the History tab is on
    screen (`IsOnScreen`, set by the window).
  - `HistoryViewModel.cs` and `SettingsViewModel.cs` hold those tabs. All view models share
    `ObservableObject.cs`.
  - `TrayController.cs` owns the tray icon and the one open `AlertWindow`.
    `App.axaml.cs` wires them up with `ShutdownMode.OnMainWindowClose`.
  - Colours come from the `Map.*` and `Series.*` theme resources in `App.axaml` through
    `MapPalette.cs`, cached per theme. The tray icon uses the dark set.
- The groups sum to total RAM plus `MemoryBreakdown.Skew`: processes and kernel are read a
  moment apart. It is normally 0.

## Rules in force

- Nothing leaves this PC without Owner approval: no GitHub repo, upload or store listing
  (G-001 constraint, VALUES.md → Restraint).
- v1 reads memory on Windows only. The sampler stays behind `IMemorySampler`, and Core
  holds no Windows rules (G-001 constraint).
- Process tiles are sized by private working set (G-001 criterion 1; D003).
- The chart's series colours are the validated dark and light sets in `App.axaml`. Change
  them only after re-running the dataviz palette validator (D012).
- Ending a process is always confirmed and acts on one process only, on a handle whose
  start time matches the sample (G-001 criterion 6; D008).
- xunit.v3 stays at the version `Avalonia.Headless.XUnit` is built against, 3.2.2 today
  (D006).
- `scripts/check.ps1` is the merge gate until there is CI: no remote exists (STANDARDS.md
  → formatter in CI; blocked on the remote question below).

## Next steps and open questions

- M5: finish and hand over.
  - Measure the footprint against criterion 9, including the History tab with a full
    hour of data.
  - Revisit the M2 review deferral: labels are re-created on every hover repaint, and the
    chart's tick labels on every frame. Act on it only if the measurement calls for it.
  - Build a self-contained single file to C:\bin\MemoryMap.exe (local only).
  - Final README and handover, and a full end-to-end pass.
- Open: a GitHub remote, needed for CI, has not been requested. Ask the Owner before
  creating one.

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|

## Decisions

See `docs/decisions/README.md`.
