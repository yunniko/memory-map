# D006 · xUnit v3 pinned to 3.2.2 for Avalonia's headless test runner
Date: 2026-10-12 · Goal: G-001 M2 · Status: active (superseded by: —)
Context: The window tests use `Avalonia.Headless.XUnit` 12.1.4, which is built against `xunit.v3.extensibility.core` 3.2.2. With xunit.v3 4.0.2, test discovery threw `MissingMethodException`.
Decision: Every test project uses xunit.v3 3.2.2 (one version in `Directory.Packages.props`), which runs on Microsoft Testing Platform v1.
Force: requirement — an external limit: the Avalonia headless package's xUnit dependency. Recheck when Avalonia ships a headless package built for xUnit 4.
Rejected: xunit.v3 4.x for Core/Windows tests only (two xUnit versions in one solution for no gain); Avalonia.Headless without its xUnit integration (UI-thread dispatch hand-written in every test).
Consequence: Upgrading xunit.v3 means first checking which version `Avalonia.Headless.XUnit` depends on.
Evidence: Directory.Packages.props; tests/MemoryMap.App.Tests/Helpers/TestApp.cs
