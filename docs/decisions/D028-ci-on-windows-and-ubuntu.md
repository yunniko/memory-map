# D028 · CI runs the gate on Windows and Ubuntu; Linux covers a solution filter until M2
Date: 2026-10-12 · Goal: G-003 M1 · Status: active (superseded by: D030, filter part only)
Context: D023 kept CI on Windows only; G-003 criterion 7 asks for CI on an Ubuntu runner, and the Linux sampler now has its own tests.
Decision: The workflow runs `scripts/check.ps1` on windows-2025 and ubuntu-24.04. On Linux the gate covers `MemoryMap.Linux.slnf`, then runs `scripts/crosscheck-linux.py`.
Force: requirement — G-003 criterion 7, and STANDARDS.md (CI exercises the path production runs).
Rejected: a separate Linux script (it would drift from the local gate); the whole solution on Linux (the app and Windows tests need Windows until M2).
Consequence: M2 removes the filter once the app builds on both OSes. Supersedes D023's runner choice; its private-repo part stands.
Evidence: .github/workflows/check.yml; scripts/check.ps1; MemoryMap.Linux.slnf
