# memory-map

A Windows desktop app that shows the PC's RAM as a live treemap — like SequoiaView does
for disk space — so you can see at a glance what is using memory, including memory no
app owns (kernel pools, shared pages).

Built in C# with Avalonia. **State:** planning — goal G-001 in `GOALS.md` is awaiting the
Owner's acceptance; no code yet.

Predecessors (one-off snapshots, PowerShell): `C:\bin\memory-report.ps1` (text report)
and `C:\bin\memory-map.ps1` (HTML treemap).

See `HANDOVER.md` for the current state and `docs/decisions/` for why things are the way
they are.
