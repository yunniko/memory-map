# D016 · A Settings option slows sampling while the window is hidden; off by default
Date: 2026-10-12 · Goal: G-001 M5 · Status: active (superseded by: —)
Context: Hidden or minimised, nothing is drawn, yet sampling at the shown rate still costs about 1% of one core.
Decision: `AppSettings.HiddenRefreshSeconds` (same as shown, 5, 10 or 30 s) sets the hidden rate; the default is "same as shown".
Force: requirement — Owner instruction 2026-10-12: offer it as a settings option, turned off by default.
Rejected: always slowing when hidden (Owner wanted a choice; it delays the low-memory alert and coarsens history while hidden).
Consequence: Hiding never samples faster than showing; a shorter interval samples at once (`LiveMemory.Interval`). Each history point records its interval, and a line breaks only past three times the slower neighbour's.
Evidence: tests/MemoryMap.Core.Tests/AlertAndSettingsTests.cs; tests/MemoryMap.App.Tests/MainWindowTests.cs; docs/reviews/2026-10-12-m5-footprint.md
