# D033 · systemd user services are listed read-only
Date: 2026-10-12 · Goal: G-003 M3 · Status: active (superseded by: —)
Context: A systemd session also starts the user's enabled systemd user services at sign-in; they belong in the list, but switching them is not plain file editing.
Decision: Enabled user services (`systemctl --user list-unit-files --state=enabled`, then one `show` for all) are listed with a disabled switch whose hint names `systemctl --user`; template units are left out.
Force: requirement — G-003 criterion 4.
Rejected: switching them with `systemctl --user disable` (criterion 4 makes them read-only, and presets can bring a vendor unit back); listing templates (they run only as instances).
Consequence: Without systemctl there are no services and no error; a systemctl that fails is reported as an unreadable source.
Evidence: src/MemoryMap.Linux/Startup/SystemdUserServices.cs; tests/MemoryMap.Linux.Tests/SystemdUserServicesTests.cs
