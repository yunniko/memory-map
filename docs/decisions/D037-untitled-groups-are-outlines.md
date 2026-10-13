# D037 · Without titles a group is an outline, and the whole frame answers the pointer
Date: 2026-10-13 · Goal: G-004 · Status: active (superseded by: —)
Context: The Owner asked for group titles on the map to be optional, with only a border when they are off; a group then has no header strip to hover or click.
Decision: With titles off the arranger leaves no header and sets tiles 3 px inside the frame, the map draws a 1 px border-coloured outline, and hit-testing returns the frame for any point inside it that no tile covers, with titles on or off.
Force: judgment — the outline is the Owner's request; the inset and the hit area are chosen so the outline shows and a group stays clickable.
Rejected: hiding only the label and keeping the empty 20 px strip (wastes the space the Owner wants back); keeping header-only hit-testing (an untitled group could not be opened or hovered).
Consequence: Clicking a gap between a group's tiles now opens that group in both modes.
Evidence: tests/MemoryMap.Core.Tests/SceneTests.cs; tests/MemoryMap.App.Tests/MainWindowTests.cs
