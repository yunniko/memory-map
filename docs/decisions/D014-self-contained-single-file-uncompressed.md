# D014 · The installed app is one self-contained, uncompressed, untrimmed exe
Date: 2026-10-12 · Goal: G-001 M5 · Status: superseded (superseded by: D043)
Context: M5 installs the app to C:\bin as a single file that runs without a separate .NET install.
Decision: `scripts/publish.ps1` publishes win-x64 self-contained with PublishSingleFile and the native Skia/HarfBuzz libraries inside, with no compression and no trimming (97 MB).
Force: judgment — one file to copy that runs on a machine without .NET 10; size matters less than footprint on a local tool.
Rejected: framework-dependent (needs the .NET 10 runtime installed); single-file compression (assemblies would be unpacked into memory, against criterion 9); trimming (Avalonia's reflection paths need verifying per release, for disk space only); Native AOT (same verification cost).
Consequence: The exe is about 100 MB; revisit trimming only if size starts to matter.
Evidence: scripts/publish.ps1; smoke run of publish/MemoryMap.exe (History tab, close-to-tray) in the G-001 progress log
