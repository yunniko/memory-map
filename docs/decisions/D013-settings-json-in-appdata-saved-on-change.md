# D013 · Settings live in %APPDATA%\MemoryMap\settings.json and save on every change
Date: 2026-10-12 · Goal: G-001 M4 · Status: active (superseded by: —)
Context: Refresh rate, alert threshold, close-to-tray and the history window must survive a restart.
Decision: `SettingsStore` reads and writes one JSON file in the roaming app-data folder; the Settings tab applies and saves each change at once, writing a temp file then swapping it in; a missing or broken file means defaults plus a visible notice.
Force: judgment — per-user, no registry, readable by hand, nothing to install.
Rejected: the registry (opaque, harder to reset); an OK/Cancel dialog (the effect of a change is visible live anyway).
Consequence: Values read from the file are normalised into range; a new setting needs a default that suits a file without it.
Evidence: src/MemoryMap.Core/AppSettings.cs; src/MemoryMap.App/SettingsViewModel.cs; tests/MemoryMap.App.Tests/HistoryAndTrayTests.cs
