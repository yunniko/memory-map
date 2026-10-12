# D002 · Native APIs, not WMI, for the 1 s sampler
Date: 2026-10-12 · Goal: G-001 M1 · Status: active (superseded by: —)
Context: The app re-samples every process and the kernel figures every second; the PowerShell predecessors used WMI, which takes seconds per query.
Decision: Per-process figures come from one `NtQuerySystemInformation(SystemProcessInformation)` call, totals from `GetPerformanceInfo`, kernel and standby figures from one PDH query on English `\Memory` counter names.
Force: judgment — WMI is too slow for a 1 s refresh within the 2% CPU budget (criterion 9); among the fast options, this one needs no per-process handles, so protected processes are included without elevation.
Rejected: WMI (seconds per query); `System.Diagnostics.Process` per process (handle per process, misses protected ones, no private working set); `PerformanceCounter` (one registry read per counter).
Consequence: The process struct is read by fixed 64-bit offsets (`Native.cs`); the sampler refuses 32-bit processes. If a Windows release moves a field, the sampler integration tests fail.
Evidence: docs/reviews/2026-10-12-m1-sampler-crosscheck.md; tests/MemoryMap.Windows.Tests/WindowsMemorySamplerTests.cs
