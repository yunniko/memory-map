# D036 · The .deb declares measured dependencies and keeps Skia's embedded libraries
Date: 2026-10-12 · Goal: G-003 M4 · Status: active (superseded by: —)
Context: dpkg-shlibdeps sees only linked libraries; Avalonia and .NET open X11, ICE/SM, D-Bus and ICU at run time, which it cannot see.
Decision: Depends is dpkg-shlibdeps' output plus the list measured with LD_DEBUG under WSLg; the two native libraries are stripped; lintian's embedded-library tags (freetype, libjpeg, libpng in libSkiaSharp.so, zlib in the program) are overridden with a reason.
Force: requirement — criterion 6 (declared dependencies, lintian-clean); the libraries come built into upstream SkiaSharp and .NET.
Rejected: building Skia against system libraries (an unsupported SkiaSharp build); leaving the run-time libraries undeclared (the app fails on a minimal install).
Consequence: Those embedded copies get security fixes only through SkiaSharp and .NET updates. `scripts/deb-check.sh` re-measures the opened libraries in CI and fails when one's package is missing from Depends.
Evidence: assets/linux/lintian-overrides; scripts/deb-check.sh; docs/reviews/2026-10-12-g003-m4-deb.md
