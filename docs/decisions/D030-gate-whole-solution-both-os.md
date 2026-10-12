# D030 · The gate builds the whole solution on both OSes; Windows tests run only on Windows
Date: 2026-10-12 · Goal: G-003 M2 · Status: active (superseded by: —)
Context: D028 limited Linux CI to a solution filter until the app built on Linux; since M2 the app targets plain `net10.0` and runs there.
Decision: `scripts/check.ps1` formats, builds and tests `MemoryMap.slnx` on both OSes; off Windows, the Windows test project is not a test project.
Force: requirement — STANDARDS.md (CI exercises the path production runs) and G-003 criterion 7.
Rejected: keeping the filter (Linux CI would never build the app it ships); per-test skips in the Windows tests (dozens of guards, and one forgotten crashes on kernel32).
Consequence: The Windows test csproj sets both `IsTestProject` and `IsTestingPlatformApplication` to false off Windows; either alone fails. Supersedes D028's filter; its two-runner part stands.
Evidence: scripts/check.ps1; tests/MemoryMap.Windows.Tests/MemoryMap.Windows.Tests.csproj; .github/workflows/check.yml
