# D024 · Machine-wide startup changes run this exe elevated in a one-shot helper mode
Date: 2026-10-12 · Goal: G-002 M3 · Status: active (superseded by: —)
Context: Machine-wide flags live under HKLM and need administrator rights, while the app must stay unelevated and ask per change.
Decision: The switch runs the same exe through the shell's "runas" verb as `--startup-helper <id> on|off`. That one write reports through the exit code, and a declined prompt (error 1223) leaves everything unchanged and says so. A Store app's switch opens `ms-settings:startupapps`.
Force: requirement — G-002 constraint (unelevated app, admin per change), and Store startup tasks have no documented writable flag.
Rejected: an elevated scheduled task or service (persistent privilege, needs installation); a second helper exe (two files to ship against D014's single file); writing Store task state directly (undocumented).
Consequence: The helper refuses user-scope ids, because elevated it would see the administrator's HKCU, and it refuses folder names with path separators. Tests run the helper code through a fake launcher. The real approve and decline prompts are checked by hand in M4.
Evidence: src/MemoryMap.Windows/Startup/StartupHelper.cs; tests/MemoryMap.Windows.Tests/StartupActionsTests.cs; scripts/startup-helper-check.ps1
