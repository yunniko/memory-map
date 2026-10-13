# memory-map

A desktop app for Windows and Linux that shows the PC's RAM as a live treemap — like SequoiaView does
for disk space — so you can see at a glance what is using memory, including memory no
app owns (kernel pools, shared pages).

Built in C# (.NET 10) with Avalonia. **State:** G-001 (the map) is done; G-002 (managing other
programs' autostart) is built, works on a second PC (Windows 10) and awaits the Owner's check of the
admin prompt (`docs/second-pc-checklist.md`, `GOALS.md`); G-003 (the Linux version, with a `.deb`) is built and
awaits the Owner's checks on a Linux PC (`docs/linux-checklist.md`). Source: `github.com/yunniko/memory-map` (private).

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
- **Startup:** every program that starts at sign-in, whether it is running now and its memory,
  *Show on map* and *Open file location*, and a switch for each. Switches use the same flag
  Windows Settings uses; nothing is deleted, so every switch can be undone. Entries for every user
  of the PC ask for administrator rights for that one change; Store apps' switch opens Settings.
- **Settings:** refresh interval, a slower refresh while hidden (off by default), alert
  threshold, close-to-tray; saved in `%APPDATA%\MemoryMap\settings.json`.

On Linux the same tabs work without root: apps are sized by RssAnon, *End process* and *Open file
location* act on your own processes only, the tray is used where the desktop shows one (else closing
quits), and the Startup tab lists XDG autostart entries (switchable) and systemd user services
(listed only). Settings are in `~/.config/MemoryMap/settings.json`.

## Use

- `C:\bin\MemoryMap.exe` — the installed app (one self-contained file; no .NET needed).
- `./scripts/publish.ps1` — rebuild that exe into `publish\` and copy it to `C:\bin`
  (`-NoInstall` to skip the copy; exit the running app first).
- `dotnet run --project src/MemoryMap.App -c Release` — run from source.
- `dotnet run --project src/MemoryMap.Cli -- --json` — one snapshot as text or JSON;
  `-- --startup` lists the programs Windows starts at sign-in, with their state and memory;
  `-- --startup-set <id> on|off` switches one entry (ids are in `--startup --json`; a machine-wide
  one asks for administrator rights).
- `./scripts/check.ps1` — format check, warnings-as-errors build, all tests.
  Set `MEMORYMAP_SCREENSHOTS=<folder>` to have the window tests save PNGs there.
- `./scripts/crosscheck.ps1` — compare the sampler's totals with Windows' own (WMI).
- `./scripts/startup-crosscheck.ps1` — compare the startup list with Settings → Apps → Startup (read-only).
- `./scripts/startup-roundtrip.ps1` — switch two throwaway entries it creates (and removes) through
  the app's code and through Settings, and check that each side sees the other's change.
  It closes and reopens the Settings app for each check.
- `./scripts/startup-helper-check.ps1 [-Exe <path>]` — run the exes' administrator helper mode
  unelevated with ids it must refuse, and check the exit codes (writes nothing).

## Use on Linux

- `sudo apt install ./memory-map_*_amd64.deb` — installs the app (menu entry "Memory Map" and the
  `memory-map` command); `sudo apt remove memory-map` removes it.
- `publish/linux-x64/MemoryMap` — the same app as one file to copy anywhere and run.
- `bash scripts/publish-linux.sh` — build both into `publish/` (on Linux, with the .NET SDK and dpkg-dev).
- `bash scripts/deb-check.sh publish/memory-map_*_amd64.deb` — lintian, then install, run and remove the
  package in throwaway Ubuntu containers (needs docker).
- `python3 scripts/crosscheck-linux.py` — compare the sampler's totals with `free` and `ps`.
- `python3 scripts/startup-roundtrip-linux.py` — switch throwaway autostart entries in temp folders and
  check that systemd's own autostart generator agrees.
- The gate: `pwsh scripts/check.ps1` where PowerShell is installed; CI runs it on Ubuntu too.

Predecessors (one-off snapshots, PowerShell): `C:\bin\memory-report.ps1` (text report)
and `C:\bin\memory-map.ps1` (HTML treemap).

See `HANDOVER.md` for the current state and `docs/decisions/` for why things are the way
they are.
