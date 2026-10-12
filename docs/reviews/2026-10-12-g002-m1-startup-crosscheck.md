# G-002 M1 — startup list against Windows Settings

Date: 2026-10-12 · PC: Windows 11 Home build 26200, en-GB, standard (unelevated) session.

## Method

`pwsh scripts/startup-crosscheck.ps1` builds `memmap-dump`, lists `--startup --json`, opens
Settings → Apps → Startup (`ms-settings:startupapps`) and reads every switch's name and
state through UI Automation. It clicks nothing. Task Manager's Startup apps page keeps the
same states but runs elevated, so UI Automation from an unelevated script reads nothing in
it (0 items, tried); Settings is the reference.

## Result

| | Memory Map | Settings |
|---|---|---|
| Entries | 41 | 41 |
| Names only on one side | 0 | 0 |
| State differences | 1 | |

The one difference: **Killer Control Center** (Store task, family `RivetNetworks.KillerControlCenter_*`)
has `State = 2` (Enabled) in `SystemAppData` and `ShowNotification = 1`, yet Settings shows it
Off. No documented field explains it; the StateRepository database was not examined
(locked, undocumented). Memory Map shows the registry state. Open question, D019.

## Timing

`memmap-dump --startup`: 41 entries, read 336 ms, matched to processes 26 ms (after the code
review's fixes; the first run, 331 / 43 ms, timed process sampling as part of matching).

## Not checked

- Another PC, Windows 10, another display language, a standard (non-admin) account: planned
  for G-002 M4's second-PC run.
- Whether switching a machine-wide entry in Settings writes a flag under HKCU. No HKCU flag for
  any HKLM Run value or common Startup file exists here, and Settings agrees with the HKLM flags.
- Entries under Policies\Explorer\Run and RunOnce: excluded by D018, Task Manager does not list them.
