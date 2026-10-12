# memory-map — Handover
Last verified: 2026-10-12 at d066421

A Windows desktop app (C#, Avalonia) showing RAM as a live treemap. G-001 is done
(`docs/goals-archive.md`); G-002, a Startup view, is active in `GOALS.md` (M1 and M2 done).
Source: github.com/yunniko/memory-map (private, D023). Charter: `E:\CLAUDE\COMPANY\`.

## Current state

- **Installed and running as one file.** `scripts/publish.ps1` builds a self-contained
  97 MB exe into `publish\` and copies it to C:\bin\MemoryMap.exe (D014). The installed
  copy is built from 27ddf24, and its Map and Settings tabs were checked by screenshot.
- **The live map works.** It shows the six groups as framed areas with their apps inside,
  refreshed every second, with a summary line, a legend, a hover tooltip and a Table tab.
  Light and dark themes follow Windows.
- **Drill-down works.** Click a group, an app or the "Other" fold to zoom into it. The
  breadcrumb, Back, Esc, Backspace, Alt+Left and the mouse back button return. The zoom
  follows its node across refreshes and falls back to the nearest ancestor (D009).
- **Process actions work.** Right-click a single process for *End …*: always confirmed,
  guarded by start time (D008), critical processes and the app itself refused. *Open file
  location* opens Explorer. Failures show in a dismissable notice bar.
- **History works.** It charts In use, Kernel pools and the window's five biggest apps
  over 5–60 minutes, keeping at most one point a second for an hour (D010). Lines break
  where sampling paused. Line colours stay with their app (D012).
- **Tray and alert work.** The tray icon is a gauge of RAM in use. Closing the window hides
  it to the tray by default. The low-memory alert is a corner window that takes no focus
  (D011).
- **Settings work.** Refresh (0.5–5 s), a slower refresh while hidden or minimised (same as
  shown, 5, 10 or 30 s; off by default, D016), alerts, threshold and close-to-tray. Each
  applies at once and is saved to %APPDATA%\MemoryMap\settings.json (D013).
- **Rendering is software, not GPU (D015).** GPU rendering grew 135 → 231 MB in 10 minutes
  on the History tab. Software rendering holds 48–55 MB at equal CPU.
- **Footprint (criterion 9).** Measurements are in `docs/reviews/2026-10-12-m5-footprint.md`.
  - An hour shown on the History tab at 1 s (build 7720d4e): 1.42% of one core (0.12% of
    all 12), private memory 64–67 MB from minute 20 on. Criterion 9 (under 2% CPU and
    150 MB) is met even on a one-core reading.
  - Minimised for 300 s: 0.98% of one core with the hidden refresh off, 0.37% at 5 s and
    0.24% at 30 s.
- **Startup tab (G-002 M2).** It lists every program that starts at sign-in (D018, D019),
  whether it runs now and its private working set (host programs not counted, D020), with
  *Show on map* and *Open file location*. This user's Run values and Startup-folder files
  switch off and on by their StartupApproved flag, written with the bytes Settings writes (D021).
  Machine-wide entries and Store tasks show a disabled switch saying why (M3 adds them).
  The list is read when the tab opens; samples are matched off the UI thread while it is
  shown (D022). `memmap-dump --startup` and `--startup-set <id> on|off` do the same from the CLI.
  - On this PC: 41 entries, 20 on, 12 switchable; read 316–375 ms, first match 26–33 ms,
    later samples 7–9 ms. Real app via UI Automation: 41 switches, 81 MB private.
  - `scripts/startup-roundtrip.ps1`: 8/8 (app and Settings, both directions, two throwaway
    entries, removed afterwards). `scripts/startup-crosscheck.ps1`: 41/41 names, one state
    differs (Killer Control Center: registry on, Settings Off; unexplained, D019).
    Records: `docs/reviews/2026-10-12-g002-m2-startup-switch.md`,
    `docs/reviews/2026-10-12-g002-m1-startup-crosscheck.md`.
  - The installed C:\bin copy has no Startup tab yet; M4 reinstalls it.
- `scripts/check.ps1` passes: format clean, Release build with 0 warnings, 220/220 tests
  (Core 104, Windows integration 66, App headless 50). GitHub Actions runs the same script
  on every push to main (D023); the run for b02dd08 passed.
- `scripts/crosscheck.ps1` against WMI: worst difference 0.15% of RAM (limit 2%).
- Not run: the critical-process refusal, because it needs an elevated session.
- "Shared & other" is about 3.5 GB (23% of RAM) on this PC. It is real shared memory (D003).

## How things fit together

- `MemoryMap.Core` is pure and OS-neutral; views take layout and wording from it (D007):
  - `Breakdown.cs` builds one `MapNode` tree with stable keys (D005).
  - `Treemap.cs` holds the squarified layout. `Scene.cs` arranges frames and tiles and
    hit-tests them.
  - `Describe.cs` holds the tooltip and table wording. `Navigation.cs` holds the zoom chain.
  - `ProcessActions.cs` holds `IProcessActions`, the outcomes, and `NodeCommands`.
  - `LiveMemory.cs` is the sampling loop. Shortening its `Interval` samples at once.
  - `History.cs` is the ring; each point records the refresh interval it was taken at.
    `HistoryChart.cs` builds series, ticks, the tooltip and the line-break rule.
  - `LowMemoryAlert.cs` holds the alert rule; `AppSettings.cs` holds the choices and JSON store.
- Startup: `src/MemoryMap.Core/Startup.cs` holds the entry model, `IStartupReader`, `IProcessPaths` (which
  also says how the OS compares paths) and `StartupMatching` (a process counts for the
  entry with its exact file, else the deepest program folder). `src/MemoryMap.Core/StartupActions.cs`
  holds `IStartupActions` and the wording shared by the app and the CLI. `src/MemoryMap.Windows/Startup/`
  reads the sources and switches them; `StartupIds.cs` is the one place ids are built and parsed;
  `StartupLocations` lets tests point both at a scratch key and temp folders.
- The Startup tab is `src/MemoryMap.App/StartupViewModel.cs`: rows are kept by id so a reload
  updates them in place, and a row's switch shows what Windows reads back after the change.
  *Show on map* uses `Navigation.Reveal` (the deepest node holding all of its processes).
- `MemoryMap.Windows` holds `WindowsMemorySampler` (native APIs, D002) and
  `WindowsProcessActions` (D008). The `Native.cs` offsets are 64-bit only. Of a sample's
  7.2 ms, the kernel's process list is 4.8 ms.
- `MemoryMap.App`:
  - `Program.cs` selects software rendering.
  - `MainViewModel.cs` applies the latest breakdown on the UI thread. It feeds the history
    and the alert on every sample. Its `IsOnScreen` (set by the window) picks the sampling
    interval and whether the chart is built.
  - `TreemapControl.cs` redraws the whole map each refresh (D017).
  - `TrayController.cs` owns the tray icon and the `AlertWindow`.
  - Colours come from `App.axaml` resources through `MapPalette.cs`.
- The groups sum to total RAM plus `MemoryBreakdown.Skew`, normally 0.

## Rules in force

- Nothing is published, released or signed without Owner approval; the private repo is the
  only remote (Owner, 2026-10-12; VALUES.md → Restraint).
- v1 reads memory on Windows only. The sampler stays behind `IMemorySampler`, and Core holds
  no Windows rules (G-001 constraint).
- Process tiles are sized by private working set (G-001 criterion 1; D003).
- Keep software rendering unless a GPU build is re-measured for an hour on the History tab
  under 150 MB (criterion 9; D015).
- The slower hidden refresh stays off by default (Owner, 2026-10-12; D016).
- Change the chart's series colours only after re-running the dataviz palette validator
  (D012).
- Ending a process is always confirmed and acts on one process only, on a handle whose start
  time matches the sample (G-001 criterion 6; D008).
- xunit.v3 stays at the version `Avalonia.Headless.XUnit` is built against, 3.2.2 today (D006).
- Tests never touch real autostart entries: they read and write only the `StartupFixture`
  scratch key and temp folders (G-002 constraint). Checks against Windows use throwaway
  entries the script creates and removes (`scripts/startup-roundtrip.ps1`).
- A switch only writes the StartupApproved flag; nothing is deleted (G-002 criterion 3).
- The app stays unelevated; machine-wide startup changes will go through a per-change admin
  prompt (G-002 constraint).
- `scripts/check.ps1` is the merge gate, locally and in CI (STANDARDS.md → formatter in CI; D023).

## Next steps and open questions

- G-002 M3: machine-wide entries through a one-shot elevated `MemoryMap.exe` helper per change
  (a declined prompt changes nothing), and Store tasks' switch opening Settings. Then M4.
  G-002 must work on other PCs (Owner, 2026-10-12); the second-PC run is in M4.
- For the M4 checklist: Task Manager shows the same state for an entry switched here (its
  bytes could not be observed, D021); Settings hides a Run entry whose program is missing.
- Open: why Settings shows Killer Control Center Off while its task state reads on (D019).
- For the M4 second-PC run: whether switching a machine-wide entry off in Settings writes a
  user-key flag (none exists here; the reader reads machine flags only, D018).
- Proposed later goal (Owner's call): Linux. It needs a `/proc` sampler behind `IMemorySampler`,
  Linux groups, a Linux rendering choice, and a check of tray support on GNOME.
- Not measured: software rendering on a 4K or maximised window. Re-measure before relying
  on criterion 9 there (D015).

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|
| 2026-10-12 | 27ddf24 | Installed C:\bin\MemoryMap.exe (local only, D014) | Map and Settings tabs captured; close-to-tray checked |

## Decisions

See `docs/decisions/README.md`.
