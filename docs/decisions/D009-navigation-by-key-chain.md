# D009 · Drill-down keeps a chain of node keys and falls back to the nearest ancestor
Date: 2026-10-12 · Goal: G-001 M3 · Status: active (superseded by: —)
Context: Every refresh builds a new tree, so the zoomed node must be found again each second; it can also move into or out of a group's "Other" fold, or disappear.
Decision: `Navigation` stores the keys from the root to the zoomed node and resolves the deepest one still in the tree; when the zoomed node is gone, the view stays on its nearest surviving ancestor.
Force: judgment — keys are already stable across refreshes (D005), so following them needs no extra state.
Rejected: holding the `MapNode` itself (stale after one refresh); returning to the root when the node goes (loses the user's place); jumping back in when an exited app restarts (the view would move under the user).
Consequence: Anything that zooms goes through `Navigation`; a new node kind needs a stable key to be zoomable.
Evidence: src/MemoryMap.Core/Navigation.cs; tests/MemoryMap.Core.Tests/NavigationTests.cs
