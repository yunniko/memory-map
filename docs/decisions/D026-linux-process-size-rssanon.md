# D026 · A Linux process is sized by RssAnon; memory of processes not visible is its own figure
Date: 2026-10-12 · Goal: G-003 M1 · Status: active (superseded by: —)
Context: G-003 asks for one stated per-process measure that needs no root; PSS and USS need smaps_rollup, which only the process owner (or root) may read.
Decision: Size = `RssAnon` from /proc/pid/status (world-readable); working set = `VmRSS`; commit = `RssAnon + VmSwap`. `AnonPages` minus the sum of `RssAnon` is named "Programs not visible here".
Force: requirement — G-003 criterion 1 (no root) and the kernel's permissions on smaps_rollup.
Rejected: PSS and USS (other users' processes unreadable); VmRSS (counts shared libraries in every process, overflowing the total).
Consequence: Copy-on-write pages shared after fork count in each process, so the sum can exceed AnonPages; the unseen figure is then 0. On WSL that figure holds other distributions' memory.
Evidence: src/MemoryMap.Linux/LinuxMemorySampler.cs; tests/MemoryMap.Linux.Tests/LinuxMemorySamplerTests.cs; scripts/crosscheck-linux.py; docs/reviews/2026-10-12-g003-m1-linux-sampler.md
