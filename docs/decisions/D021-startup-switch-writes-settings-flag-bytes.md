# D021 · A startup switch writes the same StartupApproved bytes Windows Settings writes
Date: 2026-10-12 · Goal: G-002 M2 · Status: active (superseded by: —)
Context: Switching an entry means writing its 12-byte flag under `StartupApproved`. Windows reads an odd first byte as off, and existing flags on this PC hold 02/03 as well as 00/01.
Decision: On writes twelve zero bytes. Off writes `01 00 00 00` followed by the current FILETIME. These are the values Settings wrote on build 26200 for both Run values and Startup-folder files. The registration itself is never touched.
Force: requirement — Windows itself must read the flag back the same way; the round-trip check proves this against Settings in both directions.
Rejected: 02/03 values (older Task Manager values; Task Manager's current bytes could not be observed because it runs elevated and UI Automation cannot reach it from an unelevated script); deleting the Run value (not reversible, against criterion 3).
Consequence: The M4 second-PC checklist confirms Task Manager shows the same state. A missing-program entry that Settings hides still gets a flag.
Evidence: src/MemoryMap.Windows/Startup/StartupApproval.cs; scripts/startup-roundtrip.ps1; docs/reviews/2026-10-12-g002-m2-startup-switch.md
