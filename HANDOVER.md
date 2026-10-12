# memory-map — Handover
Last verified: 2026-10-12 at (initial commit)

A Windows desktop app (C#, Avalonia) showing RAM as a live treemap. Goal: `GOALS.md` G-001
(DRAFT, awaiting Owner acceptance). Charter: `E:\CLAUDE\COMPANY\`.

## Current state

- Planning only: `GOALS.md`, `README.md`, this file and D001 exist. No code.
- The behaviour to reproduce is in the PowerShell predecessors outside this repo:
  `C:\bin\memory-report.ps1` (text report) and `C:\bin\memory-map.ps1` (HTML treemap).
  Their grouping and their "private working set per process" sizing are the reference.

## How things fit together

- Planned layout (G-001 M1): a pure `MemoryMap.Core` library (model, grouping, layout),
  a Windows sampler behind an interface, and the Avalonia app on top.

## Rules in force

- v1 is Windows-only for data; keep the sampler behind an interface (D001).
- Nothing leaves this PC without Owner approval: no GitHub repo or upload (VALUES.md).

## Next steps and open questions

- PENDING APPROVAL: the G-001 milestone plan in `GOALS.md`.
- Open: a GitHub remote (needed for CI) has not been requested; ask before creating one.

## Deploy log

| Date | Commit | What changed | How verified |
|---|---|---|---|

## Decisions

See `docs/decisions/README.md`.
