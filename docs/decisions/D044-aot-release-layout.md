# D044 · AOT releases: a flat Windows zip, Linux tarballs with a folder, the published app run in CI
Date: 2026-10-13 · Goal: G-007 M2 · Status: active (superseded by: —)
Context: Native AOT (D043) turns each program into three files, so downloads, the C:\bin install and CI's check of the shipped build change shape.
Decision: Windows ships `MemoryMap-<v>-win-x64.zip` with the three files at its top; Linux ships `MemoryMap-<v>-<rid>.tar.gz` holding one folder; `publish.ps1` copies the three files into `C:\bin`; CI publishes the Windows app and runs `scripts/smoke-published.ps1` against it; every shipped project builds with the AOT analyzers on.
Force: judgment — a flat zip extracts to one folder with Explorer's "Extract all", a tarball with a folder is the Linux convention; the C:\bin exe keeps its path, so shortcuts to it still work. Running the published app in CI is a requirement (STANDARDS.md, "Verified means the path production runs").
Rejected: a folder inside the zip (nested folders after "Extract all"); a `C:\bin\MemoryMap\` subfolder (moves the exe away from where shortcuts point).
Consequence: release assets are named differently from 0.4.1, so the README says so. memmap-dump (not shipped) builds without the AOT analyzers.
Evidence: scripts/release-files.sh; scripts/smoke-published.ps1; .github/workflows/check.yml
