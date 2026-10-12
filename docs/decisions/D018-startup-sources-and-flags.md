# D018 · Startup entries come from the Run keys, Startup folders and Store tasks, with Explorer's StartupApproved flags
Date: 2026-10-12 · Goal: G-002 M1 · Status: active (superseded by: —)
Context: G-002 must list what Task Manager's Startup apps lists, with the same on/off state, on any Windows 10/11 PC.
Decision: Read HKCU Run, HKLM Run, HKLM WOW6432Node Run, both Startup folders and Store startup tasks; an entry is off when its StartupApproved flag's first byte is odd, on when it has no flag; name it by the Shell's file description, else its file name.
Force: requirement — the acceptance criterion "same entries and state as Task Manager", checked against Settings → Startup apps, which Task Manager mirrors and which an unelevated script can read.
Rejected: Policies\Explorer\Run, RunOnce, services and scheduled tasks (Task Manager does not list them); .NET FileVersionInfo for names (misses CCXProcess's description); a user flag for machine entries (Windows keeps those under HKLM).
Consequence: StartupApproved is undocumented; `scripts/startup-crosscheck.ps1` is the check to re-run on a new PC or Windows build.
Evidence: tests/MemoryMap.Windows.Tests/StartupReaderTests.cs; docs/reviews/2026-10-12-g002-m1-startup-crosscheck.md
