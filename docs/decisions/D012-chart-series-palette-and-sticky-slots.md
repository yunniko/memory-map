# D012 · Chart lines use the validated categorical palette, and an app keeps its slot
Date: 2026-10-12 · Goal: G-001 M4 · Status: active (superseded by: —)
Context: The chart shows In use, kernel pools and the window's five biggest apps on one byte axis; the ranking changes as the window or the apps change.
Decision: Lines wear the seven categorical slots in their validated order (separate light and dark steps, `Series.0-6` in App.axaml); In use and kernel pools hold slots 0 and 1, and `SeriesSlots` keeps an app's slot while it stays in the chart, reusing freed slots for newcomers.
Force: requirement — the dataviz palette validator passes these steps in both modes, and colour must follow the entity, not its rank.
Rejected: colouring by rank (a line repaints when the ranking shifts); a second y-axis for apps (misleading scale); generated hues (unvalidated).
Consequence: Light mode has contrast warnings for aqua, yellow and magenta, so the legend names every line with its value and the tooltip lists all values.
Evidence: src/MemoryMap.Core/HistoryChart.cs; src/MemoryMap.App/App.axaml; tests/MemoryMap.Core.Tests/HistoryTests.cs
