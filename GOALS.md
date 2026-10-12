# Goals — memory-map

### G-001 · Realtime memory map desktop app (C# / Avalonia) — ACTIVE
- **What:** A desktop app that shows this PC's RAM as a live treemap (SequoiaView-style,
  for memory instead of disk), refreshing every 1–2 s, with drill-down from app to
  processes, right-click process actions, a history graph, and a tray icon.
- **Why:** The Owner wants to see at a glance, continuously, what is eating memory —
  including memory no app owns (kernel pools, shared pages) that Task Manager hides.
  It grows out of `C:\bin\memory-report.ps1` and `C:\bin\memory-map.ps1` (2026-10-12),
  which do this as one-off snapshots.
- **Acceptance criteria:**
  1. The treemap covers total RAM in the same six groups as `memory-map.ps1` (Apps,
     Windows & services, WSL/Docker VMs, Kernel & drivers, Shared & other, Available),
     with tile area = physical RAM; per-process size = private working set.
  2. It refreshes live (default 1 s, configurable) without tiles jumping around
     between frames when sizes barely change.
  3. Totals agree with Windows' own counters (Task Manager / `memory-report.ps1`)
     within 2% of total RAM at the same moment.
  4. Hover shows name, size, % of RAM, process count, PIDs; a table view lists the same data.
  5. Clicking an app zooms to its individual processes; a breadcrumb or Back returns.
  6. Right-click a process: *End process* (always asks for confirmation; reports
     access-denied honestly instead of failing silently) and *Open file location*.
  7. A history graph shows used memory, kernel pools and the top apps over a
     selectable recent window (at least the last 30 minutes).
  8. A tray icon shows % in use in its tooltip, alerts when available memory falls
     below a configurable threshold, and the window can close to tray.
  9. The app's own footprint stays small: under 150 MB private working set and under
     2% average CPU at a 1 s refresh (measured on this PC).
  10. Light and dark themes follow the system setting.
- **Constraints:**
  - C# (Owner, 2026-10-12) and Avalonia (Owner's choice, 2026-10-12; D001).
  - Memory data comes from Windows APIs; v1 runs on Windows only. The data source sits
    behind an interface so another OS could be added later, but none is in scope.
  - Nothing is published: no GitHub repo, store listing or upload without Owner approval
    (VALUES.md → Restraint).

**Milestones:**
- [x] M1 — Data engine. Solution with a pure `MemoryMap.Core` library (snapshot model,
  grouping/folding, squarified layout) and a Windows sampler (native APIs, cheap enough
  for a 1 s refresh). xUnit tests for the pure logic; a `--dump` console mode whose
  numbers are cross-checked against `memory-report.ps1`. Formatter (`dotnet format` +
  shared `.editorconfig` in `COMPANY/configs/`) set up from the first commit.
- [x] M2 — Live treemap window. Avalonia app with a custom-drawn treemap that refreshes
  live with stable tiles, hover tooltip, legend, table view, light/dark. Avalonia
  headless UI tests for the window.
- [x] M3 — Drill-down and process actions. Click to zoom app → processes, breadcrumb/Back;
  context menu with confirmed *End process* and *Open file location*; clear handling of
  protected/elevated processes.
- [x] M4 — History graph and tray. In-memory ring buffer of samples, a timeline view
  (used, kernel pools, top apps), tray icon with live % tooltip, low-memory alert,
  close-to-tray, settings (refresh interval, alert threshold).
- [x] M5 — Finish and hand over. Footprint measured against criterion 9, self-contained
  single-file build installed to `C:\bin\MemoryMap.exe` (local only), README/HANDOVER
  final, full end-to-end pass.

**Progress log** (newest first):
- 2026-10-12 — M5 done (27ddf24); goal complete, awaiting the Owner's sign-off. An hour on the
  History tab on the fixed build: 1.42% of one core, 64-67 MB private, so criterion 9 is met
  on either CPU reading. Optional slower refresh while hidden, off by default (D016,
  Owner): minimised CPU 0.98% off, 0.37% at 5 s. Skipping unchanged map frames was measured
  and dropped (D017). Review: 2 simplify depth fixes, 2 code-review fixes, 8 findings
  rejected with reasons. Gate 147/147; installed exe checked on the Map and Settings tabs.
  PENDING APPROVAL: Owner sign-off on G-001.
- 2026-10-12 — M5 in progress (7720d4e). Single-file exe built and installed to C:\bin
  (D014); WMI cross-check 0.15% (limit 2%). Hour run found History-tab memory growing
  under GPU rendering (135 to 231 MB in 10 min); software rendering holds 48-55 MB at equal
  CPU (D015, docs/reviews/2026-10-12-m5-footprint.md). Gate 137/137. Next: hour run on the
  fixed build, then the M5 check-in and end-of-goal report.
- 2026-10-12 — M4 done (a89c100). History tab (per-second ring, 60 min; used, kernel pools,
  top 5 apps on one axis, gap breaks, hover), tray gauge icon with % tooltip, low-memory
  alert window, close-to-tray, Settings tab saved to %APPDATA% (D010–D013). Review
  (/simplify + code-review) fixes in a89c100. Verified: check.ps1 137/137 (Core 79,
  Windows 20, App 38), 0 warnings; live: History tab shows real data, close hides to tray
  with the process alive; 60 s on History after 20 s warm-up: 2.71% of one core, 129 MB
  private. Next: M5.
- 2026-10-12 — M3 done (6a53bec). Drill-down with breadcrumb, Back and back gestures;
  context menu with confirmed End (one process, start-time guard) and Open file location;
  denied/exited/failed outcomes in a notice bar. Verified with `scripts/check.ps1`: format
  clean, 0 warnings, 105/105 tests. Live: 122 MB private. The critical-process refusal was
  not run (needs elevation). /simplify and /code-review fixes applied in 6a53bec.
  Decisions D008 and D009. Next: M4, history graph and tray.
- 2026-10-12 — M2 done (af0ae53). Live Avalonia map window: group frames with app tiles,
  hover tooltip, legend, Table tab, light/dark theme. Verified with `scripts/check.ps1`:
  format clean, 0 warnings, 73/73 tests (including 10 headless window tests). Run live on
  this PC: 0.45% CPU over 20 s, 134 MB private. /simplify gave 18 cleanups; /code-review
  gave 8 fixes, 1 rejected, 1 deferred to M5. Decisions D006 and D007. Next: M3,
  drill-down and process actions.
- 2026-10-12 — M1 done (dbbeb13). Core breakdown tree, squarified layout and Windows sampler,
  plus the `memmap-dump` CLI. Verified with `scripts/check.ps1`: format clean, 0 warnings,
  48/48 tests. The cross-check against WMI passes with a worst difference of 0.08% of RAM
  (limit 2%); one run straight after a build failed at 2.47% from process churn (see
  `docs/reviews/`). A refresh costs ~7.9 ms. /simplify led to the restructure in D005;
  /code-review led to 5 fixes. Next: M2, the live treemap window.
- 2026-10-12 — Owner accepted the plan ("accept the plan, go ahead"); no milestone marked
  review-before-continuing. Starting M1.
- 2026-10-12 — Goal created on Owner request ("realtime memory map written on c#").
  Owner chose Avalonia and all four extras (drill-down, process actions, tray, history).
  Plan presented; awaiting acceptance.

### G-002 · Startup view: see and switch other programs' autostart — DRAFT
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
- [ ] M1 — Startup data engine. Core model and rules: command-line to executable, flag
  bytes to state, matching entries to running processes. A Windows reader for the Run
  keys, the Startup folders (shortcut targets) and Store startup tasks. Tests over fixtures,
  including the odd cases in criterion 6. A cross-check against Task Manager on this PC.
- [ ] M2 — Startup tab. The list with memory now, running state and *Show on map*. A switch
  for user entries, with exactly the bytes Task Manager writes. *Open file location*.
  Headless UI tests; a real round trip on a test entry the app creates and removes.
- [ ] M3 — Machine-wide entries and Store apps. A per-change administrator prompt through a
  one-shot elevated `MemoryMap.exe` helper; a declined prompt changes nothing. Store tasks
  link to Windows Settings.
- [ ] M4 — Finish. Publish and install to C:\bin, README and handover, an end-to-end pass,
  and a short checklist for the Owner's second-PC run.

**Progress log** (newest first):
- 2026-10-12 — Goal created on Owner request ("manage other apps' autostart"; "it should be
  able to run on other pcs too"). Plan presented; awaiting acceptance.
