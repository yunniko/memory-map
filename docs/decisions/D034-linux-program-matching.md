# D034 · A Linux startup program is matched by its resolved file; hosts and bare scripts are not counted
Date: 2026-10-12 · Goal: G-003 M3 · Status: active (superseded by: —)
Context: Startup memory (as on Windows, D020) needs the program's processes; on Linux they report their resolved file in /proc/<pid>/exe.
Decision: Exec's first word is looked up on PATH and resolved with realpath; a dedicated folder also counts, shared ones (/usr/bin, PATH folders, home) do not; shells, interpreters and launchers are not counted, nor is a script outside a folder of its own.
Force: judgment — a script runs as its interpreter, so its own file never appears as a process's exe.
Rejected: matching on the process name (15 characters, and renameable); counting a script's interpreter (would count every shell).
Consequence: A program not on PATH, missing, or with a missing TryExec shows why it will not start.
Evidence: src/MemoryMap.Linux/Startup/ProgramFile.cs; tests/MemoryMap.Linux.Tests/LinuxStartupTests.cs
