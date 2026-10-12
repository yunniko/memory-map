#!/usr/bin/env python3
"""Checks Memory Map's autostart switch against systemd's reading of the same folders.

G-003 acceptance criterion 4. Throwaway autostart folders stand in for ~/.config/autostart and
/etc/xdg/autostart (through XDG_CONFIG_HOME and XDG_CONFIG_DIRS), so no real entry is touched.
Each entry is switched through memmap-dump (the app's own code); systemd-xdg-autostart-generator,
which turns autostart entries into the services a systemd session (GNOME, KDE) starts, is then run
on the same folders, and the entry must be among its services exactly when Memory Map says it is on.
It also checks which files the switch wrote and removed (D032), and finally lists the real folders,
read-only. Exit code 0 when every check agrees, 1 otherwise.

Build first: dotnet build src/MemoryMap.Cli -c Release (on Linux).
Usage: scripts/startup-roundtrip-linux.py [path to memmap-dump]
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
exe = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "src/MemoryMap.Cli/bin/Release/net10.0/memmap-dump")
generator = "/usr/lib/systemd/user-generators/systemd-xdg-autostart-generator"
for path in (exe, generator):
    if not os.path.exists(path):
        sys.exit(f"Not found: {path} (build memmap-dump first; the generator comes with systemd)")

work = tempfile.mkdtemp(prefix="memmap-roundtrip-")
user = os.path.join(work, "config", "autostart")
system = os.path.join(work, "xdg", "autostart")
env = dict(os.environ, XDG_CONFIG_HOME=os.path.join(work, "config"), XDG_CONFIG_DIRS=os.path.join(work, "xdg"))
env.pop("XDG_CURRENT_DESKTOP", None)
failures = 0


def write(folder, name, text):
    os.makedirs(folder, exist_ok=True)
    with open(os.path.join(folder, name), "w", encoding="utf-8") as f:
        f.write(text)


def entry(name):
    return f"[Desktop Entry]\nType=Application\nName={name}\nExec=/usr/bin/true --{name}\n"


def memmap_enabled(name):
    out = subprocess.run([exe, "--startup", "--json"], env=env, capture_output=True, text=True, check=True).stdout
    found = [e for e in json.loads(out)["entries"] if e["Id"] == f"xdg/{name}.desktop"]
    if not found:
        raise SystemExit(f"memmap-dump does not list {name}")
    return found[0]["Enabled"]


def systemd_starts(name):
    # Unit names escape '-', so the test entries' names have none.
    out = tempfile.mkdtemp(dir=work)
    subprocess.run([generator, out, out, out], env=env, check=True, capture_output=True)
    return any(name in unit for unit in os.listdir(out))


def check(what, expected, actual):
    global failures
    ok = expected == actual
    failures += not ok
    print(f"{'ok' if ok else 'FAIL':<4} {what}: expected {expected}, got {actual}")


def switch(name, state):
    run = subprocess.run([exe, "--startup-set", f"xdg/{name}.desktop", state], env=env, capture_output=True, text=True)
    check(f"{name} switched {state} (exit code)", 0, run.returncode)


try:
    write(system, "memmaprtsystem.desktop", entry("memmaprtsystem"))
    write(user, "memmaprtuser.desktop", entry("memmaprtuser"))
    write(system, "memmaprtcopy.desktop", entry("memmaprtcopy"))
    write(user, "memmaprtcopy.desktop", entry("memmaprtcopy") + "Hidden=true\n")  # the user's own copy
    system_bytes = open(os.path.join(system, "memmaprtsystem.desktop"), "rb").read()

    for name in ("memmaprtsystem", "memmaprtuser"):
        check(f"{name} at the start, Memory Map", True, memmap_enabled(name))
        check(f"{name} at the start, systemd", True, systemd_starts(name))
        for state in ("off", "on"):
            switch(name, state)
            check(f"{name} {state}, Memory Map", state == "on", memmap_enabled(name))
            check(f"{name} {state}, systemd", state == "on", systemd_starts(name))

    copy = os.path.join(user, "memmaprtsystem.desktop")
    check("the system file is unchanged", True, system_bytes == open(os.path.join(system, "memmaprtsystem.desktop"), "rb").read())
    check("the app's own copy is removed once it is back on", False, os.path.exists(copy))
    check("the user's own entry is kept", True, os.path.exists(os.path.join(user, "memmaprtuser.desktop")))

    check("memmaprtcopy at the start, systemd", False, systemd_starts("memmaprtcopy"))
    switch("memmaprtcopy", "on")
    check("memmaprtcopy on, systemd", True, systemd_starts("memmaprtcopy"))
    check("the user's own copy is kept", True, os.path.exists(os.path.join(user, "memmaprtcopy.desktop")))
    check("no temporary file is left", [], [f for f in os.listdir(user) if f.endswith(".memory-map-tmp")])
finally:
    shutil.rmtree(work)

# The real folders, read only: the list must load without an unreadable source.
real = json.loads(subprocess.run([exe, "--startup", "--json"], capture_output=True, text=True, check=True).stdout)
print(f"Real session: {len(real['entries'])} entries; unreadable: {real['Unreadable'] or 'none'}")
for e in real["entries"]:
    print(f"  {'on ' if e['Enabled'] else 'off'} {e['Name']:<40} {e['switch']:<9} {e['Location']}")
check("real folders read without errors", [], real["Unreadable"])
sys.exit(1 if failures else 0)
