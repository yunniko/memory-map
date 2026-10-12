# D019 · Store apps' startup tasks are read from the AppModel registry and package manifests
Date: 2026-10-12 · Goal: G-002 M1 · Status: active (superseded by: —)
Context: Store apps (Copilot, Xbox, Teams) register startup through a manifest extension, not a Run value, and keep the user's state per task.
Decision: Take each family's newest package from the user's AppModel Repository\Packages key, parse its manifest for windows.startupTask, and read the task's State (2 or 4 on) from SystemAppData.
Force: judgment — it keeps the reader one plain registry-and-file pass with no WinRT projection, and the same fixture tests cover it; the keys are undocumented but stable since Windows 10.
Rejected: WinRT StartupTask/PackageManager (adds the Windows SDK projection, and StartupTask only answers for the calling package); the StateRepository database (locked, undocumented schema).
Consequence: Store tasks are switched in Windows Settings, never written by this app. Killer Control Center reads State 2 (on) while Settings shows it Off; the app shows the registry value and the difference stays open.
Evidence: tests/MemoryMap.Windows.Tests/StartupReaderTests.cs; docs/reviews/2026-10-12-g002-m1-startup-crosscheck.md
