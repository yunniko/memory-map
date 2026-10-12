# D008 · Process actions re-check the start time and never end several processes at once
Date: 2026-10-12 · Goal: G-001 M3 · Status: active (superseded by: —)
Context: *End process* acts on a sample that can be up to a second old; by then the PID may have been freed and reused by another process.
Decision: `WindowsProcessActions` opens the PID, compares its creation time with the sample's `StartTime`, and refuses critical processes and itself. Ending targets exactly one process, and only after the user confirms (`ProcessTarget.ToEnd`).
Force: requirement — G-001 criterion 6 (always confirm, report access-denied honestly); ending the wrong process loses the user's work.
Rejected: `Process.GetProcessById(pid).Kill()` (no reuse check, opaque errors); ending every process of an app in one click (a browser's processes are one app, but a mis-click would close it whole).
Consequence: Every new process action goes through `Open` in `WindowsProcessActions.cs` and returns an `ActionResult` sentence; access-denied is reported, never retried elevated.
Evidence: src/MemoryMap.Windows/WindowsProcessActions.cs; tests/MemoryMap.Windows.Tests/WindowsProcessActionsTests.cs; tests/MemoryMap.App.Tests/DrillDownAndActionsTests.cs
