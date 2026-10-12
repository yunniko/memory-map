# memory-map

A Windows desktop app that shows the PC's RAM as a live treemap — like SequoiaView does
for disk space — so you can see at a glance what is using memory, including memory no
app owns (kernel pools, shared pages).

Built in C# (.NET 10) with Avalonia. **State:** goal G-001 in progress — the data engine
and the live map window (map, hover tooltips, table view, light/dark theme) work;
drill-down, process actions, tray icon and history come next (see `GOALS.md`).

## Use

- `dotnet run --project src/MemoryMap.App -c Release` — the live map window (refreshes every second).
- `dotnet run --project src/MemoryMap.Cli -- --json` — one snapshot as text or JSON.
- `./scripts/check.ps1` — format check, warnings-as-errors build, all tests.
  Set `MEMORYMAP_SCREENSHOTS=<folder>` to have the window tests save PNGs there.

Predecessors (one-off snapshots, PowerShell): `C:\bin\memory-report.ps1` (text report)
and `C:\bin\memory-map.ps1` (HTML treemap).

See `HANDOVER.md` for the current state and `docs/decisions/` for why things are the way
they are.
