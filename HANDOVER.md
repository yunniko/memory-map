# memory-map — Handover
Last verified: 2026-10-13 at 3e700a5

A desktop app (C#, Avalonia) showing RAM as a live treemap, on Windows and Linux.
G-001 to G-004 are done (`docs/goals-archive.md`): the map, the Startup tab, the Linux version
with a `.deb`, optional group titles. G-005, signed Windows releases through SignPath Foundation,
is active (`GOALS.md`).
Source: github.com/yunniko/memory-map (public, D039; MIT, D038). Charter: `E:\CLAUDE\COMPANY\`.

## Current state

- **Installed and running as one file.** `scripts/publish.ps1` builds a self-contained
  97 MB exe into `publish\` and copies it to C:\bin\MemoryMap.exe (D014). The installed
  copy is built from 44933d1 (same hash as the publish output); its Startup tab, helper mode
  and Store switch were checked (`docs/reviews/2026-10-12-g002-m4-end-to-end.md`).
- **The live map works.** It shows the six groups as framed areas with their apps inside,
  refreshed every second, with a summary line, a legend, a hover tooltip and a Table tab.
  Light and dark themes follow Windows.
- **Drill-down works.** Click a group, an app or the "Other" fold to zoom into it. The
  breadcrumb, Back, Esc, Backspace, Alt+Left and the mouse back button return. The zoom
  follows its node across refreshes and falls back to the nearest ancestor (D009).
- **Process actions work.** Right-click a single process for *End …*: always confirmed,
  guarded by start time (D008), critical processes and the app itself refused. *Open file
  location* opens Explorer, or the file manager on Linux. Failures show in a dismissable notice
  bar. On Linux both act on the user's own processes only, through a pidfd (D029).
- **History works.** It charts In use, Kernel allocations and the window's five biggest apps
  over 5–60 minutes, keeping at most one point a second for an hour (D010). Lines break
  where sampling paused. Line colours stay with their app (D012).
- **Tray and alert work.** The tray icon is a gauge of RAM in use. Closing the window hides
  it to the tray by default, only while the desktop shows a tray icon; otherwise closing quits
  and Settings says why (D031). The low-memory alert is a corner window that takes no focus
  (D011).
- **Settings work.** Refresh (0.5–5 s), a slower refresh while hidden or minimised (same as
  shown, 5, 10 or 30 s; off by default, D016), alerts, threshold, close-to-tray and group
  titles on the map (off: each group is a thin outline, D037). Each
  applies at once and is saved to %APPDATA%\MemoryMap\settings.json, or
  `~/.config/MemoryMap/settings.json` on Linux; the footer names the file (D013).
- **Rendering is software, not GPU (D015).** GPU rendering grew 135 → 231 MB in 10 minutes
  on the History tab. Software rendering holds 48–55 MB at equal CPU.
- **Footprint (criterion 9).** Measurements are in `docs/reviews/2026-10-12-m5-footprint.md`.
  - An hour shown on the History tab at 1 s (build 7720d4e): 1.42% of one core (0.12% of
    all 12), private memory 64–67 MB from minute 20 on. Criterion 9 (under 2% CPU and
    150 MB) is met even on a one-core reading.
  - Minimised for 300 s: 0.98% of one core with the hidden refresh off, 0.37% at 5 s and
    0.24% at 30 s.
- **Startup tab (G-002 M2–M3).** It lists every program that starts at sign-in (D018, D019),
  whether it runs now and its private working set (host programs not counted, D020), with
  *Show on map* and *Open file location*. Every entry has a working switch:
  - This user's Run values and Startup-folder files: their StartupApproved flag is written
    in-process, with the bytes Settings writes (D021).
  - Machine-wide entries: the same exe runs elevated as `--startup-helper <id> on|off` for that
    one change (D024). A declined prompt changes nothing and the notice says so.
  - Store apps: the switch opens Settings > Apps > Startup; the row springs back and says so.
  The list is read when the tab opens; samples are matched off the UI thread while it is
  shown (D022). `memmap-dump --startup` and `--startup-set <id> on|off` do the same from the CLI.
  - On this PC: 41 entries, 20 on; 12 direct, 11 machine-wide, 18 Store. Read 316–416 ms, first
    match 26–41 ms, later samples 7–9 ms. Real app via UI Automation: 41 switches, all enabled,
    82 MB private; Calendar's switch opened Settings' Startup page and stayed Off.
  - `scripts/startup-helper-check.ps1`: 8/8 (both exes in helper mode, unelevated, refusals
    and an unregistered id; nothing written). Record: `docs/reviews/2026-10-12-g002-m3-elevated-switch.md`.
  - A second PC (Windows 10): works, per the Owner (2026-10-13). Not run: the real administrator
    prompt (secure desktop), part A of `docs/second-pc-checklist.md`.
  - `scripts/startup-roundtrip.ps1`: 0 failures (app and Settings, both directions, two throwaway
    entries, removed afterwards). `scripts/startup-crosscheck.ps1`: 41/41 names, one state
    differs (Killer Control Center: registry on, Settings Off; unexplained, D019).
    Records: `docs/reviews/2026-10-12-g002-m2-startup-switch.md`,
    `docs/reviews/2026-10-12-g002-m1-startup-crosscheck.md`.
- **Linux data engine (G-003 M1).** `src/MemoryMap.Linux/` samples /proc without root: sizes by
  RssAnon (D026), kinds from name, cgroup and account (D027), memory in other PID namespaces
  shown as "Programs not visible here". `memmap-dump` runs on Linux (table and `--json`).
  Record: `docs/reviews/2026-10-12-g003-m1-linux-sampler.md`.
  - On WSL Ubuntu 24.04: `scripts/crosscheck-linux.py` against `free`, meminfo and `ps`: PASS,
    worst 0.17% of RAM; process count 43/43. A sample takes about 4 ms.
- **The app runs on Linux (G-003 M2).** Under WSLg the Map, Table, History and Settings tabs
  work, a setting saves to `~/.config`, and with no tray host closing quits. The app's private
  memory there is 43 MB.
  Record: `docs/reviews/2026-10-12-g003-m2-app-on-linux.md`.
- **Startup tab on Linux (G-003 M3).** It lists XDG autostart entries the session would start
  (user folder and system folders, OnlyShowIn/NotShowIn honoured) and enabled systemd user
  services, read-only (D033), with running state and memory (matching, D034). The switch writes
  `Hidden=` in the user's own entry, or a checksum-marked copy of a system entry, removed again
  only while unedited (D032). `memmap-dump --startup`/`--startup-set` work on Linux too.
  - `scripts/startup-roundtrip-linux.py` on WSL: every check agrees with
    `systemd-xdg-autostart-generator`; system file unchanged; exit 0. Real session: 4 entries.
    Record: `docs/reviews/2026-10-12-g003-m3-startup-on-linux.md`.
- **Linux package (G-003 M4).** `scripts/publish-linux.sh` builds publish/linux-x64/MemoryMap
  (97 MB single file) and publish/memory-map_0.4.0_amd64.deb (30 MB): the program in
  /usr/lib/memory-map with its two libraries, the `memory-map` command, menu entry, icon and man
  page (D035). Depends is dpkg-shlibdeps' output plus the libraries opened at run time (D036).
  Record: `docs/reviews/2026-10-12-g003-m4-deb.md`.
  - `scripts/deb-check.sh` in clean ubuntu:24.04 containers (also in CI): lintian no error or
    warning, desktop-file-validate ok, window of class memory-map shown, nothing unpacked into the
    home folder, all 9 libraries opened at run time in Depends, `apt-get remove` leaves no file.
  - On this PC's WSL Ubuntu the package installs (apt adds libice6 and libsm6), `memory-map` shows
    the map as the user (46 MB RssAnon) with a WSLg Start menu shortcut, and `apt-get remove`
    leaves no file or shortcut. It is not installed there now.
  - Not run: a real desktop's tray, file manager and autostart settings (`docs/linux-checklist.md`).
- `scripts/check.ps1` passes on Windows: format clean, Release build with 0 warnings, 332 tests
  (306 passed, 26 skipped: the Linux-only tests). On WSL Ubuntu, which has no pwsh, the same steps
  pass over the whole solution: 249/249 (D030). GitHub Actions runs the gate on windows-2025 and
  ubuntu-24.04 on every push to main (D028), and a third job builds the .deb and runs `scripts/deb-check.sh`.
- `scripts/crosscheck.ps1` against WMI: PASS, worst difference 1.4% of RAM (limit 2%).
- Not run: the Windows critical-process refusal, because it needs an elevated session (on
  Linux, init's refusal is tested).
- "Shared & other" is about 3.5 GB (23% of RAM) on this PC. It is real shared memory (D003).

## How things fit together

- `MemoryMap.Core` is pure and OS-neutral; views take layout and wording from it (D007):
  - `Breakdown.cs` builds one `MapNode` tree with stable keys (D005). Group labels, the
    remainder's name and every named figure come from the sampler's `SystemMemory` (D025).
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
- Switching: `WindowsStartupActions` routes by `StartupEntry.Switch`. `StartupFlags.cs` writes
  and reads back a flag, both in-process and in `StartupHelper.cs` (the elevated mode, checked
  first in both `Program.cs` files). `StartupLauncher.cs` is the only code that elevates or opens
  Settings; tests swap it for `FakeLauncher`, which runs the helper code against the fixture.
- The Startup tab is `src/MemoryMap.App/StartupViewModel.cs`: rows are kept by id so a reload
  updates them in place, and a row's switch shows what Windows reads back after the change.
  *Show on map* uses `Navigation.Reveal` (the deepest node holding all of its processes).
- `MemoryMap.Windows` holds `WindowsMemorySampler` (native APIs, D002) and
  `WindowsProcessActions` (D008). The `Native.cs` offsets are 64-bit only. Of a sample's
  7.2 ms, the kernel's process list is 4.8 ms.
- `MemoryMap.Linux` holds `LinuxMemorySampler`, reading through `IProcFs` so tests feed it text
  (`tests/MemoryMap.Linux.Tests/FakeProcFs.cs`); `ProcText.cs` parses stat, status and meminfo.
  A process's name and kind are cached per (pid, start, comm, uid).
- `MemoryMap.Linux` also holds `LinuxProcessActions` and `Desktop.cs` (the session bus through
  `dbus-send`: tray host, file manager). `ProcClock.cs` gives sampler and actions the same start time.
- Linux startup: `src/MemoryMap.Linux/Startup/` — `LinuxStartupLocations` (XDG folders, PATH; tests
  point it at temp folders), `DesktopEntry` (parse and one-key edits), `ProgramFile` (Exec to the
  file its processes report), `LinuxStartupReader` and `LinuxStartupActions`, `SystemdUserServices`
  (through `Command.cs`). `src/MemoryMap.Core/ProcessPathCache.cs` serves both OSes' `IProcessPaths`.
- `MemoryMap.Cli` picks the sampler by OS; the app picks all its OS services once, in
  `src/MemoryMap.App/PlatformServices.cs`. The app targets plain `net10.0` for both OSes.
- Linux packaging: `assets/linux/` holds the menu entry, icon, man page, Debian changelog (its head
  version must equal `Version` in `Directory.Build.props`; the script refuses otherwise), copyright
  and lintian overrides; `scripts/publish-linux.sh` stages them. `src/MemoryMap.App/Program.cs` sets
  the X11 window class `memory-map` to match the entry's StartupWMClass; `scripts/deb-check.sh`
  checks the window by that class.
- `MemoryMap.App`:
  - `Program.cs` selects software rendering.
  - `MainViewModel.cs` applies the latest breakdown on the UI thread. It feeds the history
    and the alert on every sample. Its `IsOnScreen` (set by the window) picks the sampling
    interval and whether the chart is built.
  - `TreemapControl.cs` redraws the whole map each refresh (D017).
  - `TrayController.cs` owns the tray icon and the `AlertWindow`; `TrayHost.cs` says whether a
    tray icon is shown, and `MainWindow.OnClosing` asks it before hiding (D031).
  - Colours come from `App.axaml` resources through `MapPalette.cs`.
- The groups sum to total RAM plus `MemoryBreakdown.Skew`, normally 0.

## Rules in force

- Nothing is published, released or signed without Owner approval; the private repo is the
  only remote (Owner, 2026-10-12; VALUES.md → Restraint).
- Core holds no OS rules: samplers stay behind `IMemorySampler` and supply names and figures
  (G-001 constraint, G-003 plan; D025).
- Process tiles are sized by private working set on Windows (G-001 criterion 1; D003) and by
  RssAnon on Linux, which needs no root (G-003 criterion 1; D026).
- Keep software rendering unless a GPU build is re-measured for an hour on the History tab
  under 150 MB (criterion 9; D015).
- The slower hidden refresh stays off by default (Owner, 2026-10-12; D016).
- Change the chart's series colours only after re-running the dataviz palette validator
  (D012).
- Ending a process is always confirmed and acts on one process only, on a handle whose start
  time matches the sample (G-001 criterion 6; D008). On Linux, only the user's own processes,
  never with root (G-003 criteria 1–2; D029).
- Closing hides the window only while a tray icon is shown, else it quits (G-003 criterion 3;
  D031).
- xunit.v3 stays at the version `Avalonia.Headless.XUnit` is built against, 3.2.2 today (D006).
- Tests never touch real autostart entries: they read and write only the `StartupFixture`
  scratch key and temp folders (G-002 and G-003 constraints). Checks against the OS use
  throwaway entries (`scripts/startup-roundtrip.ps1`) or throwaway XDG folders
  (`scripts/startup-roundtrip-linux.py`).
- On Windows a switch only writes the StartupApproved flag; nothing is deleted (G-002 criterion 3).
- On Linux system autostart files are never written, and the only file removed is the app's own
  marked copy, unedited since (G-003 criterion 4, Owner 2026-10-12; D032).
- The app stays unelevated; a machine-wide change goes through the one-shot helper, one admin
  prompt per change (G-002 constraint; D024). The helper refuses user-scope ids: elevated, it
  would see the administrator's HKCU (D024).
- `scripts/check.ps1` is the merge gate on both OSes over the whole solution, locally and in CI
  (STANDARDS.md → formatter in CI; G-003 criteria 5 and 7; D028, D030).

## Next steps and open questions

- G-005: release workflow (M1), repo ready for SignPath's application (M2), then a signed release
  once the Owner's application is accepted (M3).
- Not run, signed off without: `docs/linux-checklist.md` on a real Linux PC (G-003) and part A of
  `docs/second-pc-checklist.md`, the real administrator prompt (G-002).
- Known: Depends accepts ICU 76, 74, 72 or 70; a distribution with only a newer ICU needs its
  package name added in `scripts/publish-linux.sh` (only Ubuntu 24.04 is checked, D036).
- Known: Settings hides a Run entry whose program is missing; the app lists it with a problem note.
- Open: why Settings shows Killer Control Center Off while its task state reads on (D019).
- Open, step B.5 of the checklist: whether switching a machine-wide entry off in Settings
  writes a user-key flag (none exists here; the reader reads machine flags only, D018).
- Not measured: software rendering on a 4K or maximised window. Re-measure before relying
  on criterion 9 there (D015).

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|
| 2026-10-12 | 27ddf24 | Installed C:\bin\MemoryMap.exe (local only, D014) | Map and Settings tabs captured; close-to-tray checked |
| 2026-10-12 | 44933d1 | Reinstalled C:\bin\MemoryMap.exe with the Startup tab (local only) | Hash matches publish; helper check 4/4; UI smoke 41/41 switches |
| 2026-10-13 | 2255e21 | Reinstalled C:\bin\MemoryMap.exe with optional group titles (local only) | Started from C:\bin; window "Memory Map" responding, 118 MB |
| 2026-10-13 | 3e700a5 | GitHub release v0.4.0 (repo public since D039): Windows exe, .deb, Linux single file, notices zip; C:\bin reinstalled | CI green; deb-check passed; asset SHA-256 match the local builds |

## Decisions

See `docs/decisions/README.md`.
