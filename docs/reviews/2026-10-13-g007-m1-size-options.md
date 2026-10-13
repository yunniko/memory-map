# G-007 M1 — size options for the published app

Measured 2026-10-13 on the development PC (Windows 11, 16 GB) and its WSL Ubuntu 24.04 (WSLg), from the
tree that carries the generated COM interop and settings JSON (D042). All builds are self-contained
`win-x64` / `linux-x64` publishes with `DebugType=none`; sizes count only the files that ship (the
native `.pdb` files of SkiaSharp and HarfBuzz are left out). The scripts were one-off measurement scripts
in the session scratchpad; the method is described here so it can be repeated.

## Size

| Option | Windows | Linux x64 | Files | Trim/AOT warnings |
|---|---|---|---|---|
| 0.4.1 (baseline: single file, untrimmed) | 97.3 MB | 92.7 MB | 1 | — |
| Compressed single file | 46.5 MB | 46.1 MB | 1 | none |
| Trimmed single file | 39.0 MB | 34.6 MB | 1 | none |
| Trimmed and compressed | 21.0 MB | 20.4 MB | 1 | none |
| Trimmed with ReadyToRun | 63.0 MB | 62.5 MB | 1 | none |
| Trimmed with ReadyToRun, compressed | 31.4 MB | 31.8 MB | 1 | none |
| Native AOT | 40.1 MB | 35.8 MB | 4 / 3 | none |
| Native AOT without ANGLE (`av_libglesv2.dll`) | 34.9 MB (zip 14.5 MB) | — | 3 | none |

ANGLE is Avalonia's OpenGL-on-Direct3D layer; the app renders in software on both systems
(`src/MemoryMap.App/Program.cs`), so the library is never loaded. Linux AOT does not ship it.

## Startup (Windows)

Time from `Start-Process` to the first "In use…" summary read through UI Automation; one warm-up launch,
then the median of 5. Two runs, because the PC's load differed between them: compare within a run.

| Option | Run 1 | Run 2 |
|---|---|---|
| 0.4.1 baseline | 2.65 s | 1.93 s |
| Compressed | 2.67 s | — |
| Trimmed | 3.25 s | — |
| Trimmed and compressed | 3.46 s | — |
| Trimmed with ReadyToRun | — | 0.96 s |
| Trimmed with ReadyToRun, compressed | — | 1.01 s |
| Native AOT | 0.72 s | — |

Trimming rewrites the framework assemblies and drops their precompiled (ReadyToRun) code, so more is
compiled at start; ReadyToRun puts it back. Linux startup was not timed (no UI automation in WSL);
every Linux option starts the same runtime the same way as its Windows twin.

## Memory and CPU after 10 minutes

All options of a run side by side, refreshing every second, window shown. Private bytes (Windows) and
RssAnon (Linux); CPU is total processor time since start.

| Option | Windows private | Windows CPU | Linux private | Linux CPU |
|---|---|---|---|---|
| 0.4.1 baseline (run 1 / run 2) | 49.5 / 50.7 MB | 19.8 / 17.4 s | 55.2 / 53.7 MB | 18.4 / 18.9 s |
| Compressed | 69.3 MB | 18.8 s | — | — |
| Trimmed | 47.1 MB | 18.9 s | — | — |
| Trimmed and compressed | 46.3 MB | 20.6 s | — | — |
| Trimmed with ReadyToRun | 47.2 MB | 17.5 s | — | — |
| Trimmed with ReadyToRun, compressed | 69.2 MB | 16.4 s | 66.9 MB | 15.6 s |
| Native AOT | 41.4 MB | 12.5 s | 28.9 MB | 11.1 s |

Compression keeps 12–20 MB more private memory for the app's whole life wherever it holds
ReadyToRun or untrimmed code, the cost D014 expected. Steady-state CPU (5 to 10 minutes) is about the same
for every option (5.6–6.1 s per 5 minutes on Windows); the differences are start-up compilation.

## Does it still work (criterion 2)

- Windows, UI Automation on the published exe of AOT (with and without ANGLE), trimmed with ReadyToRun,
  trimmed and compressed, and the baseline: every tab opens and shows its content, the settings file
  is saved on a change and back, and a value written to the file is shown at the next start. 0 failures
  each. Screenshots of each tab of AOT were looked at: map, startup list with memory and switches,
  history chart.
- Windows close-to-tray: closing the AOT window hides it and the process keeps running, as in the
  baseline.
- `scripts/startup-helper-check.ps1 -Exe` against trimmed and compressed, trimmed with ReadyToRun
  compressed, and AOT: 4/4 each.
- Linux AOT under WSLg: map, table, history, startup list and settings shown; settings read from the
  file at start and saved on a change and back.
- Gates on the tree: Windows `scripts/check.ps1` 334 total, 308 passed, 26 Linux-only skipped; WSL
  251/251, format and build clean, crosscheck 0.16% of RAM.

## Against the criteria

| Option | 1: half the size | 3: startup and memory no worse | 4: no warnings |
|---|---|---|---|
| Compressed | yes | no: +20 MB private | yes |
| Trimmed | yes | no: start +0.6 s | yes |
| Trimmed and compressed | yes | no: start +0.8 s | yes |
| Trimmed with ReadyToRun | no: 65% | yes | yes |
| Trimmed with ReadyToRun, compressed | yes | no: +18 MB private | yes |
| Native AOT without ANGLE | yes: 36% / 39% | yes: start 3.7× faster, 16–46% less private | yes |

Only Native AOT passes criteria 1, 3 and 4. It is not a single file: the exe needs `libSkiaSharp` and
`libHarfBuzzSharp` beside it, because native libraries cannot be linked into an AOT exe from their
NuGet packages, so the Windows download becomes a zip (14.5 MB) instead of one exe. Whether that is
acceptable is the Owner's choice (G-007 progress log).
