# D023 · The code lives in a private GitHub repo; CI runs the local gate on a Windows runner
Date: 2026-10-12 · Goal: G-002 M2 · Status: active (superseded by: —)
Context: The Owner asked for a git repo for the app; until then, `scripts/check.ps1` run by hand was the only gate.
Decision: The repo is `yunniko/memory-map` (private). GitHub Actions runs `scripts/check.ps1` on `windows-2025` for every push to main and every pull request.
Force: requirement — the Owner asked for the repo (2026-10-12), and STANDARDS.md requires the formatter check in CI. Windows is needed because the sampler and startup tests call Windows APIs.
Rejected: a Linux runner (the Windows integration tests cannot run on it; revisit when the Linux port adds its own tests); a separate CI script (it would drift from the local gate).
Consequence: CI and the local gate are the same script, so a change to the gate changes both. Nothing is published from CI.
Evidence: .github/workflows/check.yml; commit b02dd08
