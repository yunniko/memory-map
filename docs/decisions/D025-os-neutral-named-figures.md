# D025 · System memory is OS-neutral: totals, named figures and names, all supplied by the sampler
Date: 2026-10-12 · Goal: G-003 M1 · Status: active (superseded by: —)
Context: `SystemMemory` held Windows-only fields (standby, pools, modified pages), so a Linux sampler had nothing to fill them with.
Decision: `SystemMemory` keeps the shared totals, three lists of `MemoryFigure` (kernel, named in-use, named available) and `PlatformNames` (the system and VM group names, the in-use remainder). `Breakdown` carves the lists in order. The history's kernel line sums the figures flagged `IsKernelAllocation`.
Force: judgment — the Linux figures (slab, shmem, reclaimable) have no Windows counterpart, and per-OS fields or a platform switch in Core would grow with every OS.
Rejected: one record per OS (treemap, history and tooltips would branch on type); a platform enum that Core switches on (every new OS would edit Core); mapping Linux figures onto Windows names (cache is not standby).
Consequence: A new OS supplies figures and names; Core does not change. Tools reading the CLI JSON look figures up by `Id`. The history series "Kernel pools" became "Kernel".
Evidence: src/MemoryMap.Core/Model.cs; src/MemoryMap.Core/Breakdown.cs; tests/MemoryMap.Core.Tests/BreakdownTests.cs; scripts/crosscheck.ps1
