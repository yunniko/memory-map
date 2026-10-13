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
| Material Symbols Light "memory-sharp" and "bento-sharp", adapted as the app icon | 2026 | Apache-2.0 | Google LLC |

The app icon (`assets/memory-map.svg`, and `assets/memory-map.ico` made from it) combines those two
symbols: the chip and its pins from "memory", the three-tile layout from "bento", without the bento's
dot, the tiles recoloured and set on a dark tile. The Apache License 2.0 text is
`assets/licenses/material-symbols-LICENSE.txt`, from
https://github.com/google/material-design-icons (retrieved 2026-10-13; the project has no NOTICE file).

The full notice texts come with each package. A release attaches them as
`MemoryMap-<version>-notices.zip`; the `.deb` installs the Linux ones under
/usr/share/doc/memory-map.
