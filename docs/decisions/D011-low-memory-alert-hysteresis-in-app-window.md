# D011 · The low-memory alert warns once, re-arms 2 points higher, and shows its own window
Date: 2026-10-12 · Goal: G-001 M4 · Status: active (superseded by: —)
Context: Available memory hovering at the threshold would raise an alert every second, and a Windows toast needs a registered AppUserModelID and shortcut.
Decision: `LowMemoryAlert` raises once when available memory falls below the threshold and clears only 2 percentage points above it; the warning is a small topmost `AlertWindow` above the tray that takes no focus, closes after 20 s, and closes early when memory recovers.
Force: judgment — the margin stops repeats; the in-app window works unpackaged and in the headless tests.
Rejected: a Windows toast (needs registration, untestable headless); a time-based cooldown (warns again while still low); a modal dialog (steals focus).
Consequence: Only one alert is on screen; a new warning replaces its text.
Evidence: src/MemoryMap.Core/LowMemoryAlert.cs; src/MemoryMap.App/AlertWindow.axaml.cs; tests/MemoryMap.Core.Tests/AlertAndSettingsTests.cs; tests/MemoryMap.App.Tests/HistoryAndTrayTests.cs
