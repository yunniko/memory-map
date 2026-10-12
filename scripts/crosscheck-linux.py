#!/usr/bin/env python3
"""Compares memmap-dump's figures on Linux with free(1), ps(1) and /proc/meminfo, sampled back to back.

G-003 acceptance criterion 1: totals agree with /proc/meminfo and free. Read-only.
Build first: dotnet build src/MemoryMap.Cli -c Release (on Linux).
Usage: scripts/crosscheck-linux.py [path to memmap-dump]
"""
import json
import os
import subprocess
import sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
exe = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "src/MemoryMap.Cli/bin/Release/net10.0/memmap-dump")
if not os.path.exists(exe):
    sys.exit(f"Build first: dotnet build src/MemoryMap.Cli -c Release ({exe} not found)")


def user_processes():
    """(pid, rss kB) of every process ps lists, kernel threads left out: kthreadd and its children.
    Found by name, not as pid 2: in WSL pid 2 is WSL's own init and no kernel threads are visible."""
    out = subprocess.run(["ps", "-e", "--no-headers", "-o", "pid=,ppid=,rss=,comm="], check=True, capture_output=True, text=True).stdout
    rows = [line.split(None, 3) for line in out.splitlines()]
    kthreadd = {int(r[0]) for r in rows if r[3] == "kthreadd"}
    return [(int(pid), int(rss)) for pid, ppid, rss, _ in rows if int(pid) not in kthreadd and int(ppid) not in kthreadd]


# Short-lived processes (udev workers, shells) come and go between reads, so ps runs either side of the dump.
before = user_processes()
dump = json.loads(subprocess.run([exe, "--json"], check=True, capture_output=True, text=True).stdout)
after = user_processes()
free = subprocess.run(["free", "-b"], check=True, capture_output=True, text=True).stdout.splitlines()
mem = free[1].split()  # Mem: total used free shared buff/cache available
meminfo = {}
with open("/proc/meminfo") as f:
    for line in f:
        key, value = line.split(":", 1)
        meminfo[key] = int(value.split()[0]) * 1024

system = dump["system"]
total = int(mem[1])


def figure(group, fid):
    return next(f["Bytes"] for f in system[group] if f["Id"] == fid)


checked = [
    ("Total (free)", system["TotalPhysical"], total),
    ("Available (free)", system["Available"], int(mem[6])),
    ("Shared memory (free 'shared')", figure("InUseNamed", "shmem"), int(mem[4])),
    ("Kernel slab unreclaimable (meminfo)", figure("Kernel", "slab"), meminfo["SUnreclaim"]),
    ("Page tables (meminfo)", figure("Kernel", "page-tables"), meminfo["PageTables"] + meminfo.get("SecPageTables", 0)),
]
# The dump's own .NET runtime commits a few hundred MB that is released before meminfo is read.
shown = [("Committed (meminfo), not checked", system["CommitTotal"], meminfo["Committed_AS"])]


def diff(ours, theirs):
    return 100 * (ours - theirs) / total


print(f"{'Figure':38} {'Sampler MB':>11} {'Linux MB':>11} {'Diff % RAM':>11}")
for name, ours, theirs in checked + shown:
    print(f"{name:38} {ours // 2**20:>11} {theirs // 2**20:>11} {diff(ours, theirs):>11.2f}")
worst = max(abs(diff(ours, theirs)) for _, ours, theirs in checked)

low, high = sorted((len(before), len(after)))
print(f"\nProcesses: sampler {dump['processCount']}, ps {len(before)} before and {len(after)} after (kernel threads left out)")
seen = sum(g["Bytes"] for g in dump["groups"] if g["group"] in ("Apps", "System", "VirtualMachines"))
ps_rss = max(sum(rss for _, rss in before), sum(rss for _, rss in after)) * 1024
print(f"Process memory: sampler private {seen // 2**20} MB <= ps resident {ps_rss // 2**20} MB: {seen <= ps_rss}")

failures = []
if worst > 2:
    failures.append(f"worst difference {worst:.2f}% of RAM exceeds 2%")
# ps sees itself but not the dump, and the dump the reverse: allow 2 either side of what ps saw.
if not low - 2 <= dump["processCount"] <= high + 2:
    failures.append("the sampler's process count is outside what ps saw before and after it")
if seen > ps_rss:
    failures.append("private process memory exceeds what ps reports resident")
if failures:
    sys.exit("FAIL: " + "; ".join(failures))
print(f"PASS: worst difference {worst:.2f}% of RAM (limit 2%)")
