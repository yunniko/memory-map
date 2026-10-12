# G-002 M2 — the startup switch against Windows (2026-10-12)

Measured on the development PC: Windows 11 Home, build 26200, unelevated account.

## What Windows Settings writes (probe)
- These are the bytes Settings > Apps > Startup wrote when its switch was flipped, read back from `HKCU\…\Explorer\StartupApproved\Run` and `\StartupFolder`:
  - **on**: twelve zero bytes;
  - **off**: `01 00 00 00` followed by the FILETIME of the change.
  - Run values and Startup-folder files get the same bytes.
- Some older flags on this PC hold `02`/`03`, which are older Task Manager values. The reader treats every odd first byte as off (D018).
- Task Manager's current bytes were **not observed**: it runs elevated, so UI Automation from an unelevated script cannot drive it.
- Settings does not list a Run entry whose program file is missing, but it still wrote a `01`+time flag for such an entry.
- D021 follows from this probe.

## Round trip (`scripts/startup-roundtrip.ps1`)
- The script creates two throwaway entries: a Run value pointing at a copy of whoami.exe, and a `.cmd` in the Startup folder.
- For each entry it checks two directions:
  - switch off and on through `memmap-dump --startup-set`, then read Settings;
  - flip Settings, then read `memmap-dump`.
- Result: **8/8 ok**, and the throwaway entries were removed and the removal verified. It was run again after the /simplify refactor, with the same result.

## Crosscheck (`scripts/startup-crosscheck.ps1`)
- 41 entries on both sides.
- 1 difference: Killer Control Center (Memory Map on, Settings off). This is known; see the handover's open questions.

## Real app (UI Automation smoke run, Release build)
- The Startup tab showed 41 switches: 20 on, 12 switchable.
- Its message read "41 programs start when you sign in; 20 are switched on."
- The app's private memory was 81 MB.
- From the CLI: the list read took 322 ms and matching took 27 ms (D022).

## Not checked
- Task Manager's own state for an entry switched here (M4 second-PC checklist).
- Any PC other than this one, any other Windows build or display language (M4).
