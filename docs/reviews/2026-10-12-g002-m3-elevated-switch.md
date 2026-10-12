# G-002 M3 — machine-wide and Store switches (2026-10-12)

What was run to verify M3 (D024), on this PC (Windows 11 build 26200, unelevated session).

## Unit and integration tests (`scripts/check.ps1`)
- 237/237 (was 220 at M2), format clean, Release build with 0 warnings.
- The elevated path runs in-process: a fake launcher calls the helper's own code against the
  `StartupFixture` scratch key. It writes the machine Run, Run32 and common Startup-folder flags under the
  fixture's machine root, both directions, and nothing under the user root.
- A declined prompt leaves no flag and returns "declined, so nothing changed".
- Exit codes 0, 3, 4 and an unknown code (an NTSTATUS crash value) map to Done, AlreadyExited, Failed and Failed.
- The helper refuses (exit 2) and writes nothing for: a user Run id, a user folder id, a folder name
  with `..\`, a bad state word, a Store task id, or a missing state; `StartupIds.Parse` also rejects `.` and `..` as folder names.
- A Store task's switch opens `ms-settings:startupapps` and starts no helper.

## Real exes, helper mode, unelevated (`scripts/startup-helper-check.ps1`)
- 8/8 over `MemoryMap.exe` and `memmap-dump.exe`: four refusals exit 2, and an unregistered machine id
  containing spaces exits 3, so the quoted argument arrived whole. No flag is written in any case.

## Real app (UI Automation smoke run, Release build)
- The Startup tab showed 41 switches, all 41 enabled (12 at M2). Its message read "41 programs start
  when you sign in; 20 are switched on." The app's private memory was 82 MB.
- Calendar (a Store app) was flipped. The notice read "Calendar is a Store app: switch it in the Windows
  Settings page that just opened (Apps > Startup).", the switch stayed Off, and Settings showed its
  Startup apps page (41 switches, Calendar among them).

## Not checked
- The real administrator prompt, approved and declined. It shows on the secure desktop, where
  automation cannot answer it, so it goes to the M4 hand checklist.
- A machine-wide entry actually switched on this PC, which needs that prompt.
