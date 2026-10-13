# Third-party notices

Memory Map's own code is under the MIT licence in `LICENSE`. The published programs
(the Windows zip, the Linux archives and the `.deb`) are self-contained: compiled ahead of time,
they carry the components below, each under its own licence.

| Component | Version | Licence | Copyright |
|---|---|---|---|
| .NET runtime (Native AOT) | 10.0 | MIT (with the runtime's own third-party notices) | .NET Foundation and Contributors |
| Avalonia (core, Desktop, Win32, X11, FreeDesktop, Skia, HarfBuzz, Themes.Fluent) | 12.1.4 | MIT | The AvaloniaUI Project |
| SkiaSharp, with native Skia | 3.119.4 | MIT; Skia: BSD-3-Clause | Microsoft Corporation; Google Inc. |
| HarfBuzzSharp, with native HarfBuzz | 8.3.1.3 | MIT; HarfBuzz: Old MIT | Microsoft Corporation; HarfBuzz contributors |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Tom Deseyn |
| MicroCom.Runtime | 0.11.6 | MIT | Nikita Tsukanov |

The full notice texts come with each package. A release attaches them as
`MemoryMap-<version>-notices.zip`; the `.deb` installs the Linux ones under
/usr/share/doc/memory-map.
