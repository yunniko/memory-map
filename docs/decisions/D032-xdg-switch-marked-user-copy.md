# D032 · An XDG autostart entry is switched through a marked per-user copy
Date: 2026-10-12 · Goal: G-003 M3 · Status: active (superseded by: —)
Context: Entries in /etc/xdg/autostart apply to every user; the app must switch them for this user only, with no root.
Decision: The user's own entry gets Hidden=false or true in place; a system entry gets a full copy in the user's autostart folder carrying `X-MemoryMap-Override=<checksum of the copy as written>`, and switching it back on removes that copy only while the checksum still matches and the system entry starts by itself.
Force: requirement — G-003 criterion 4; the Owner's instruction of 2026-10-12, "files created by app should be allowed to be deleted by the app".
Rejected: a bare `Hidden=true` file (cannot be switched on again alone); never deleting (a stale copy hides later system changes); a plain `=true` mark (an edited copy would be deleted).
Consequence: A copy the user wrote or edited is switched in place and never removed; a bare override is completed with the system entry's missing keys, keeping its own lines. Writes are atomic, go through a symlink and keep the file's mode.
Evidence: tests/MemoryMap.Linux.Tests/LinuxStartupTests.cs; scripts/startup-roundtrip-linux.py; docs/reviews/2026-10-12-g003-m3-startup-on-linux.md
