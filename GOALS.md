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
- [ ] M3 — Drill-down and process actions. Click to zoom app → processes, breadcrumb/Back;
  context menu with confirmed *End process* and *Open file location*; clear handling of
  protected/elevated processes.
- [ ] M4 — History graph and tray. In-memory ring buffer of samples, a timeline view
  (used, kernel pools, top apps), tray icon with live % tooltip, low-memory alert,
  close-to-tray, settings (refresh interval, alert threshold).
- [ ] M5 — Finish and hand over. Footprint measured against criterion 9, self-contained
  single-file build installed to `C:\bin\MemoryMap.exe` (local only), README/HANDOVER
  final, full end-to-end pass.

**Progress log** (newest first):
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
