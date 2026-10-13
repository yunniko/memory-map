# D046 · The app icon combines two Material Symbols, one SVG source for every platform
Date: 2026-10-13 · Goal: G-006 (Owner request during M2) · Status: active (superseded by: —)
Context: The exe and windows had no icon and the Linux package carried an older treemap drawing; the Owner asked for an icon combining Material Symbols Light "memory-sharp" and "bento-sharp".
Decision: `assets/memory-map.svg` (chip from "memory", tiles from "bento" in the map's dark-theme group colours, on a dark tile) is the one source; `scripts/make-icon.ps1` renders `assets/memory-map.ico` from it with headless Edge, and the exe, the windows and the Linux package use those two files.
Force: requirement for the two symbols and their Apache-2.0 attribution (Owner's choice; the licence's terms); judgment for the colours, the dark tile and Edge as renderer.
Rejected: a hand-drawn ICO per size (two sources to keep in step); System.Drawing scaling one PNG (blurs small sizes); Inkscape or ImageMagick (not installed; Edge is).
Consequence: change the SVG, then rerun `scripts/make-icon.ps1` and commit both. The licence text ships in the notices zip, and `assets/linux/copyright` keeps the icon's Apache-2.0 stanza.
Evidence: `tests/MemoryMap.App.Tests/MainWindowTests.cs` (Windows_carry_the_app_icon_and_it_decodes); `THIRD-PARTY-NOTICES.md`
