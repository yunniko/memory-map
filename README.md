# memory-map

A Windows desktop app that shows the PC's RAM as a live treemap — like SequoiaView does
for disk space — so you can see at a glance what is using memory, including memory no
app owns (kernel pools, shared pages).

Built in C# (.NET 10) with Avalonia. **State:** goal G-001's five milestones are built
and verified; it awaits the Owner's sign-off (see `GOALS.md`).

## What it does

- **Map:** RAM in six groups (Apps, Windows & services, WSL/Docker VMs, Kernel & drivers,
  Shared & other, Available) with each app as a tile sized by its private working set,
  refreshed every second. Hover for details; click to zoom into a group or app; Back, Esc
  or the mouse back button to return.
- **Right-click a tile:** *End process* (always confirmed) and *Open file location*.
- **Table:** the same items as a list, largest first within each group, with process counts and PIDs.
- **History:** the last 5–60 minutes of memory in use, kernel pools and the five biggest apps.
- **Tray:** a gauge of RAM in use; an alert when available memory runs low; closing the
  window hides it to the tray (*Exit* on the tray menu quits).
- **Settings:** refresh interval, alert threshold, close-to-tray; saved in
  `%APPDATA%\MemoryMap\settings.json`.

## Use

- `C:\bin\MemoryMap.exe` — the installed app (one self-contained file; no .NET needed).
- `./scripts/publish.ps1` — rebuild that exe into `publish\` and copy it to `C:\bin`
  (`-NoInstall` to skip the copy; exit the running app first).
- `dotnet run --project src/MemoryMap.App -c Release` — run from source.
- `dotnet run --project src/MemoryMap.Cli -- --json` — one snapshot as text or JSON.
- `./scripts/check.ps1` — format check, warnings-as-errors build, all tests.
  Set `MEMORYMAP_SCREENSHOTS=<folder>` to have the window tests save PNGs there.
- `./scripts/crosscheck.ps1` — compare the sampler's totals with Windows' own (WMI).

Predecessors (one-off snapshots, PowerShell): `C:\bin\memory-report.ps1` (text report)
and `C:\bin\memory-map.ps1` (HTML treemap).

See `HANDOVER.md` for the current state and `docs/decisions/` for why things are the way
they are.
