# G-003 M3 — the Startup tab on Linux (2026-10-12)

What was run to show the Startup tab reads and switches XDG autostart entries on Linux, lists systemd
user services read-only, and leaves Windows unchanged. On WSL Ubuntu 24.04 (GNOME session files,
systemd user manager running).

## Gates
Final run, after the /code-review fixes:
- Windows `scripts/check.ps1` on `MemoryMap.slnx`: format clean, build 0 warnings, 332 tests, 306 passed,
  26 skipped (the Linux-only tests).
- WSL, the same steps on `MemoryMap.slnx`: format clean, build clean, 249/249;
  `scripts/crosscheck-linux.py` PASS, worst difference 0.17% of RAM.

## The round trip against the session's own reader
`scripts/startup-roundtrip-linux.py` builds the CLI and works in throwaway XDG folders (`XDG_CONFIG_HOME`
and `XDG_CONFIG_DIRS` pointed at a temp dir; the real folders are only read). For three entries — one
for every user, one the user's own, one a user copy that is off — it switches with
`memmap-dump --startup-set` and after each step asks both Memory Map and `systemd-xdg-autostart-generator`
(what GNOME on systemd uses to start autostart entries) whether the entry starts. Every check agreed:
the system file is byte-identical afterwards, the app's own copy is removed once back on, the user's own
entry and copy are kept, no temporary file is left. Exit code 0.
A read-only listing of the real folders then gave 4 entries, none unreadable: AT-SPI D-Bus Bus, Snap
user application autostart helper and User folders update (from /etc/xdg/autostart), and Session
Migration (a systemd user service, read-only).

## The window under WSLg
The Startup tab listed the same 4 rows; the systemd row's switch is shown greyed with the hint; AT-SPI
showed as running at about 1 MB. Taken before the review fixes, which changed no wording on that tab
apart from the "for every user" hint ("your autostart folder" in place of a fixed path).

## After /simplify
- Fixed: the two OS path caches became one `ProcessPathCache`; `DesktopEntry.StartsAtLogin` replaces
  three copies of the Hidden/GNOME check; the reader resolves the shared program folders once and the
  actions reuse its `Autostart(files)`; `ProgramFile.Counts` is one expression; `StartupEntry.CanSwitch`
  is used by the view model.
- Kept, with reason: executability stays a method (an attribute would reach every caller); the CLI and
  the app keep their own three-line service choice (a shared project for three lines); systemctl runs
  after the file reads (the whole read is already off the UI thread); the Windows ReadOnly arm stays
  (defensive, free).

## After /code-review
- Fixed:
  - The override mark is a checksum of the copy as written, so a copy the user edited since is theirs
    and never removed (D032). The app's own copy switched off again keeps a valid mark.
  - A bare override (`Hidden=true` alone) switched on is completed with the system entry's missing keys
    and keeps its own lines and comments, instead of being replaced.
  - Writes go through a symlinked entry (a dotfiles repo) and keep the file's mode.
  - The path cache keys on the process name too (exec() keeps the PID), and an unreadable link is asked
    again rather than remembered as null.
  - A systemctl that times out is reported as such rather than as "not installed".
  - A service argument holding " ; " is kept whole.
  - `XDG_CONFIG_HOME` is one path, not a list; more shared program folders and hosts (launchers,
    runtimes, terminals, versioned names such as `lua5.4`) are known.
- Each fix has a test in `tests/MemoryMap.Linux.Tests/`.

## Not checked
- KDE and other desktops' own autostart settings pages: WSL has no desktop session. The Owner's Linux
  checklist (M4) covers switching in the app and seeing it in the desktop's settings.
- The round trip against GNOME's session itself (as opposed to its systemd generator): no GNOME session
  on WSL.
