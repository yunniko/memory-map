# D007 · Scene arrangement, hit-testing and tooltip text live in Core, not the UI
Date: 2026-10-12 · Goal: G-001 M2 · Status: active (superseded by: —)
Context: The map window needs nested layout (group frames with headers, tiles inside), stable order across refreshes, hit-testing and tooltip/table wording.
Decision: `SceneArranger` produces a `Scene` (frames + tiles + `HitTest`) and `Describe` produces the words; `TreemapControl` only draws a scene and forwards the pointer.
Force: judgment — STANDARDS.md asks for business logic in pure, unit-testable modules; the arrangement then works for any node, which M3's drill-down needs.
Rejected: layout inside the Avalonia control (testable only through a headless window); separate tooltip and table wording (they would drift apart).
Consequence: A new view (drill-down, history) arranges through `SceneArranger` and words through `Describe`; the control stays a renderer.
Evidence: src/MemoryMap.Core/Scene.cs; src/MemoryMap.Core/Describe.cs; tests/MemoryMap.Core.Tests/SceneTests.cs
