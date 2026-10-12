# D029 · Linux process actions act on the user's own processes, through a pidfd
Date: 2026-10-12 · Goal: G-003 M2 · Status: active (superseded by: —)
Context: End process and Open file location must work on Linux without root, and must never act on a different process that reused a sampled PID (D008).
Decision: Each action pins the PID with `pidfd_open`, checks the start time against the sample and the owner's uid against `getuid()`, then acts through the pidfd; End sends SIGKILL and refuses init (PID 1) as critical.
Force: requirement — G-003 criteria 1–2 (no root; other users' processes refused clearly) and D008's PID-reuse rule.
Rejected: SIGTERM first (a second outcome the confirmation does not describe; Windows ends at once too); pkexec for other users' processes (a password prompt, against the goal's "no root" constraint).
Consequence: Without a pidfd (kernels before 5.3, or a sandbox returning EPERM) actions fall back to `kill`, leaving a short PID-reuse window; the code says so where it falls back. An unreadable uid counts as another user's.
Evidence: src/MemoryMap.Linux/LinuxProcessActions.cs; tests/MemoryMap.Linux.Tests/LinuxProcessActionsTests.cs
