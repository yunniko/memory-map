# Linux — checks for the Owner (G-003 M4)

What automation could not check: a real desktop with a tray, a file manager and its own
autostart settings. Each step says what to expect; note anything different. Any switch flipped
here can be flipped back from the app or the desktop's settings.

You need: a Debian-family system (Ubuntu, Debian, Mint, Pop!_OS …) with a desktop, on Intel/AMD
(amd64) or 64-bit ARM (arm64; `dpkg --print-architecture` says which), and that architecture's two
files from a release: `memory-map_<version>_<arch>.deb` and `MemoryMap-<version>-linux-<x64|arm64>`.

## A. Install the package

1. In a terminal in the folder holding the file: `sudo apt install ./memory-map_*.deb`
   (the `./` matters). Expect: apt lists Memory Map and any libraries it needs, and installs them.
2. Open the applications menu and search "Memory Map". Expect: an entry with a small treemap icon,
   under System or Utilities. Start it from there.
3. Expect: the window opens, the dock or taskbar shows the same icon, and the map fills within
   a few seconds.

## B. The map and tabs

1. In a terminal run `free -m`. Expect: the app's header "In use" and "Available" agree with
   `free`'s used and available to within a few percent (they are a second apart).
2. Hover a tile, click a group to zoom in, press Esc to come back. Open Table and History.
3. Right-click one of your own apps' tiles (e.g. a text editor you opened for this) > *Open file location*.
   Expect: the file manager opens on the folder holding its program.
4. Right-click the same tile > *End …* > confirm. Expect: that app closes.
5. Right-click a system process (in "System & services", e.g. a daemon) > *End …*. Expect: a notice
   that it runs as another user and Memory Map acts only on your own processes; nothing is ended.
6. Settings: change the refresh interval. Expect: the footer names `~/.config/MemoryMap/settings.json`,
   and the setting is still there after a restart.

## C. Tray

On KDE, or GNOME with the AppIndicator extension (Ubuntu ships it on):
1. Expect: a gauge icon in the tray. Close the window: it goes to the tray. Click the icon to bring
   it back; the tray menu's *Exit* quits.

On a desktop without a tray (plain GNOME on Debian/Fedora): close the window. Expect: the app quits,
and Settings says "This desktop shows no tray icons, so closing the window quits Memory Map".

## D. Startup tab

1. Open Startup. Expect: the programs your desktop starts at login (compare with the desktop's
   "Startup Applications" / "Autostart" settings), plus enabled systemd user services, whose switch
   only says to change them with `systemctl --user`.
2. Switch off an entry you do not need. Open the desktop's own autostart settings. Expect: it shows
   that entry off too (on GNOME, *Startup Applications* may hide disabled system entries).
3. Switch it back on in Memory Map. Expect: the desktop's settings show it on again. If it was a
   system entry (from `/etc/xdg/autostart`), `ls ~/.config/autostart` shows no copy of it left,
   unless you edited that copy in between (then it is yours and is kept).

## E. Remove, and the single file

1. Quit the app, then `sudo apt remove memory-map`. Expect: the menu entry and the `memory-map`
   command are gone. Your settings in `~/.config/MemoryMap/` stay (apt never touches your home folder).
2. Copy the single file (`MemoryMap-<version>-linux-…`) anywhere, `chmod +x MemoryMap`, run `./MemoryMap`. Expect: the same app.
   It needs the system libraries the package listed; if it does not start, `sudo apt install libice6 libsm6`
   (the two a desktop most often lacks) and try again.

Report: the distribution and version, the desktop (GNOME, KDE, …), and for each step "as expected" or
what happened instead.
