# D010 · History keeps one point a second for an hour, with every app's size
Date: 2026-10-12 · Goal: G-001 M4 · Status: active (superseded by: —)
Context: The chart needs past values for any window up to an hour, and its top apps change with the window, so the history cannot store only the current top five.
Decision: `MemoryHistory` keeps one point per wall-clock second (the newest sample in a second wins) for 60 minutes, each holding In use, kernel pools and every app's bytes as sorted id and KiB arrays.
Force: judgment — an hour at 1 s is the longest window offered, and a few hundred apps at 4 bytes each keep the ring to a few MB.
Rejected: storing only the top five apps (a window's ranking could not be rebuilt); storing whole breakdowns (tens of MB an hour); downsampling older points (complexity for no visible gain at chart width).
Consequence: A faster refresh does not grow the history; a longer window needs a coarser tier, not a longer ring.
Evidence: src/MemoryMap.Core/History.cs; tests/MemoryMap.Core.Tests/HistoryTests.cs
