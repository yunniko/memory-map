# G-003 M1 — Linux sampler cross-check (2026-10-12)

What was run for the /proc sampler, on WSL Ubuntu 24.04 (4.8 GB given to WSL) and on this PC.

## Linux (WSL)
- Gate commands against `MemoryMap.Linux.slnf` (WSL has no pwsh, so they were run by hand): format clean,
  Release build with 0 warnings, 133/133 (Core 106, Linux 27, including the live /proc test).
- `scripts/crosscheck-linux.py`, three runs back to back: PASS each time, worst difference 0.23%, 0.06%
  and 0.06% of RAM. Total matches `free` exactly. Available is within 11 MB, shared within 2 MB, and slab
  and page tables within 1 MB.
- Process count: sampler 43, `ps` 43 before and 43 after, with kernel threads excluded. On WSL, pid 2 is
  WSL's own init, and no kernel threads are visible.
- Committed differs by 60–310 MB. That is the dump's own .NET runtime, which has exited by the time
  meminfo is read, so the script shows the row but doesn't check it.
- Private memory of listed processes (RssAnon) 127 MB, against 363 MB `ps` resident: below it, as expected
  (D026).
- "Programs not visible here" held about 1.1 GB while Docker Desktop's WSL distribution was running: its
  processes live in another PID namespace (D026).
- Refresh cost: 3.99 ms wall time and 4.42 ms CPU per sample with about 45 processes.

## Windows (this PC)
- `scripts/check.ps1`: 266 tests, 265 passed, 1 skipped (the Linux-only live test).
- `scripts/crosscheck.ps1`, now reading figures by id (D025): PASS, worst 0.53% of RAM (sum of private
  working sets).

## After /simplify and /code-review
- Fixed: a process is re-identified when it execs or changes account (cache key adds comm and uid);
  a meminfo without MemTotal or MemFree is an error, not an empty map; container marks no longer
  match the runtimes' own daemons (docker.service, containerd.service, lxcfs.service stay services);
  an argv0 path with spaces keeps its file name; the CLI refuses `--startup` on Linux before sampling;
  the Windows startup tests' fixture key is unique per fixture (a parent-key delete raced between
  parallel classes and failed one run in three).
- Kept, with reason: boot time read once per sampler (it is part of the identity key, so it must not
  drift); the group tooltip's repeated label line removed on purpose; `MemoryMap.Linux.slnf` lists
  projects the slnx also lists (until M2 removes it); the cross-check stays in Python (WSL has no pwsh);
  test fixtures are per test project (no shared test library yet).
- Final runs: Windows `check.ps1` 275 tests, 274 passed, 1 skipped (the Linux-only live test); WSL gate
  on the slnf 142/142 with format and build clean; `crosscheck-linux.py` PASS, worst 0.06% of RAM.

## Not checked
- The Ubuntu CI runner's first run. It is confirmed after the push and noted in the progress log.
- A real Linux PC with a desktop: G-003 M4 checklist.
