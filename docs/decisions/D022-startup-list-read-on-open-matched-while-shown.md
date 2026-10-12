# D022 · The startup list is read when the tab opens; samples are matched only while it is shown
Date: 2026-10-12 · Goal: G-002 M2 · Status: active (superseded by: —)
Context: Reading every startup source takes about 320 ms on this PC. Matching takes about 27 ms the first time and 7–9 ms on later samples (new processes' paths are queried), and has to follow the 1 s sample.
Decision: The list is read on a pool thread when the tab opens, on Refresh, and after a switch. Each sample is matched on a pool thread too, and only while the tab is on screen. A sample that arrives while a match is still running is skipped.
Force: judgment — the list changes rarely; the memory column matters only while someone looks at it.
Rejected: matching on the UI thread (7–9 ms of stalls a second); reading the list every sample (320 ms of I/O a second); watching the registry and folders (much code; Refresh is one click).
Consequence: An entry another program adds while the tab is open appears only on Refresh or when the tab is reopened.
Evidence: tests/MemoryMap.App.Tests/StartupTabTests.cs; docs/reviews/2026-10-12-g002-m2-startup-switch.md
