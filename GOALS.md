# Goals — memory-map

Completed goals: `docs/goals-archive.md`.

### G-002 · Startup view: see and switch other programs' autostart — ACTIVE
- **What:** A Startup tab listing every program that starts at sign-in, with how much
  memory each is using right now, and switches to turn them off and on again.
- **Why:** The Owner wants to manage autostart from the app that shows what memory those
  programs cost (2026-10-12), and to run it on other PCs too.
- **Acceptance criteria:**
  1. The tab lists the entries Task Manager's Startup apps page lists: the Run keys (user,
     machine, and machine 32-bit), both Startup folders, and Store apps' startup tasks.
     Each shows name, publisher, command, where it is registered, and enabled or disabled.
     The state agrees with Task Manager on this PC.
  2. Each entry shows whether it is running now and its memory (private working set, the
     same measure as the map). One click shows it on the map.
  3. Turning a user entry off or on uses the same switch Task Manager uses (the
     `StartupApproved` flags), so Task Manager and Windows Settings show the same state.
     Nothing is ever deleted, and every change can be undone from here or from Task Manager.
  4. A machine-wide entry is changed through a Windows administrator prompt for that one
     change. If the prompt is declined, nothing changes and the app says so.
  5. Store apps' startup tasks are shown, but their switch opens Windows Settings → Startup
     apps instead. Windows offers no documented way to change them from outside the app.
  6. It runs on other PCs, not only this one:
     - Windows 10 and 11 x64, any display language, a standard account without admin rights;
     - missing keys, entries without a flag, orphaned flags and broken shortcuts are
       handled, never crashing or guessing.
     Verified by tests over synthetic registry and folder fixtures, and by the Owner
     running the exe on a second PC.
- **Constraints:**
  - The app stays unelevated. Admin rights are asked per change, only for criterion 4
    (VALUES.md → Restraint: least privilege, reversible actions).
  - Tests never touch the real autostart entries; they use a scratch registry root and
    temp folders (VALUES.md → prefer reversible actions).
  - Windows only, like G-001.
  - Nothing is published or signed without Owner approval. An unsigned exe shows a
    SmartScreen warning on other PCs; code signing costs money.

**Milestones:**
- [x] M1 — Startup data engine. Core model and rules: command-line to executable, flag
  bytes to state, matching entries to running processes. A Windows reader for the Run
  keys, the Startup folders (shortcut targets) and Store startup tasks. Tests over fixtures,
  including the odd cases in criterion 6. A cross-check against Task Manager on this PC.
- [x] M2 — Startup tab. The list with memory now, running state and *Show on map*. A switch
  for user entries, with exactly the bytes Task Manager writes. *Open file location*.
  Headless UI tests; a real round trip on a test entry the app creates and removes.
- [x] M3 — Machine-wide entries and Store apps. A per-change administrator prompt through a
  one-shot elevated `MemoryMap.exe` helper; a declined prompt changes nothing. Store tasks
  link to Windows Settings.
- [x] M4 — Finish. Publish and install to C:\bin, README and handover, an end-to-end pass,
  and a short checklist for the Owner's second-PC run.

**Progress log** (newest first):
- 2026-10-12 — **M4 done; G-002 built, awaiting the Owner's checks and sign-off.** Installed
  C:\bin\MemoryMap.exe from 44933d1 (hash matches publish). Verified: `check.ps1` 237/237;
  helper check 4/4 on the installed exe, 8/8 on Release builds; UI smoke of the installed exe
  41/41 switches enabled, Store switch opened Settings; round trip 0 failures; crosscheck
  41/41 names, 1 known state difference (D019). Record:
  `docs/reviews/2026-10-12-g002-m4-end-to-end.md`. Not run (needs the Owner): the real
  administrator prompt and a second PC (criterion 6), in `docs/second-pc-checklist.md`.
- 2026-10-12 — **M3 done** (every startup switch works: machine-wide entries through a one-shot
  elevated `--startup-helper` run of the same exe, declined prompt changes nothing; Store apps'
  switch opens Settings > Apps > Startup; D024). Verified: `check.ps1` 237/237 (Core 104, Windows
  83, App 50), helper code tested in-process against the scratch key; `startup-helper-check.ps1` 8/8
  (real exes, unelevated, nothing written); real app via UI Automation 41/41 switches enabled,
  Calendar opened Settings' Startup page and stayed Off, 82 MB. /simplify (flag writer extracted,
  launcher stub removed, Settings opening shares Explorer's shell start) and /code-review (`.`/`..`
  folder ids now rejected; six risks declined, see milestone note) applied. Real UAC prompt not
  run (secure desktop): M4 hand checklist. Next: M4.
- 2026-10-12 — **M2 done** (Startup tab: list, memory now, *Show on map*, *Open file location*,
  switches for this user's entries; CLI `--startup-set`). Switch writes Settings' own flag bytes
  (D021); list read on tab open, matched off the UI thread while shown (D022). Owner asked for a
  repo: private `yunniko/memory-map`, CI runs `check.ps1` on windows-2025 (D023; first run green).
  Verified: `check.ps1` 220/220 (Core 104, Windows 66, App 50); `startup-roundtrip.ps1` 8/8 with
  throwaway entries removed; `startup-crosscheck.ps1` 41/41, Killer still differs (D019); real app
  via UI Automation 41 switches, 81 MB. /simplify and /code-review applied (match moved off the UI
  thread, busy-row reload fix, singular wording, Show on map needs a tile, crosscheck no longer
  closes Settings). Next: M3.
- 2026-10-12 — **M1 done** (startup data engine, CLI `memmap-dump --startup`). Reads HKCU/HKLM
  Run, HKLM Run 32-bit, both Startup folders and Store startup tasks; matches running processes
  (D018–D020). Verified: `check.ps1` 192/192 (Core 100, Windows 53, App 39), tests use a scratch
  key and temp folders only (cleanup checked); `startup-crosscheck.ps1` vs Settings 41/41 names,
  1 state differs (Killer Control Center, open, D019); read 336 ms, match 26 ms. /simplify and
  /code-review applied (shared path helper, host programs not counted, resource packages
  skipped, duplicate ids, transient path failures retried). Next: M2, the Startup tab.
- 2026-10-12 — Owner accepted the plan ("accept the G-002 plan, go ahead"); no milestone
  marked review-before-continuing. Starting M1.
- 2026-10-12 — Goal created on Owner request ("manage other apps' autostart"; "it should be
  able to run on other pcs too"). Plan presented; awaiting acceptance.
