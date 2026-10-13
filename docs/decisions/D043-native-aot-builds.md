# D043 · The published app is a Native AOT build, with its two native libraries beside it
Date: 2026-10-13 · Goal: G-007 M1 · Status: active (supersedes D014)
Context: G-007 asks for downloads at most half 0.4.1's size, with startup and memory no worse.
Decision: Windows and Linux publish with Native AOT; the exe ships beside `libSkiaSharp` and `libHarfBuzzSharp`, without the unused ANGLE library (35 MB, against 97 MB).
Force: requirement — the Owner's choice (2026-10-13, "Native AOT, zip") of the only measured option passing criteria 1, 3 and 4; every single-file option missed half the size, started slower or held more memory.
Rejected: trimmed and compressed (21 MB, but 0.8 s slower to start); trimmed with ReadyToRun (63 MB); compressed options (12–20 MB more private memory); untrimmed single file (D014, 97 MB).
Consequence: The Windows download is a zip of three files, not one exe; the Linux single file becomes a folder. Code must stay trim- and AOT-safe (D042); the build fails on IL/AOT warnings.
Evidence: docs/reviews/2026-10-13-g007-m1-size-options.md
