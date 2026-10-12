# D005 · One node tree for the breakdown; the sampler tags each process's kind
Date: 2026-10-12 · Goal: G-001 M1 · Status: active (superseded by: —)
Context: The M1 breakdown had three shapes (group, item, folded item), keys built from display labels, and Windows rules (vmmem, session 0) inside the OS-neutral core. The /simplify altitude review flagged all three before M2/M3 build layout and drill-down on them.
Decision: The breakdown is one `MapNode` tree (root → groups → apps, folded "Other" nodes and figures → processes), keyed by stable IDs. The sampler sets `ProcessKind`. Process keys include the start time, so a reused pid is a new node.
Force: judgment — drill-down (criterion 5) and safe End process (criterion 6) need it; a narrower shape would also have worked.
Rejected: keeping the three shapes and adding drill-down later (each level would need its own layout and hit-test code); classifying in Core (puts Windows facts in the portable layer).
Consequence: Layout, colours and stable order key on `MapNode.Key`, never on `Name`. When processes and kernel overshoot used memory, the overshoot is reported as `Skew`, not clamped away.
Evidence: tests/MemoryMap.Core.Tests/BreakdownTests.cs; src/MemoryMap.Core/Breakdown.cs
