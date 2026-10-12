# D015 · The app renders in software, not on the GPU
Date: 2026-10-12 · Goal: G-001 M5 · Status: active (superseded by: —)
Context: With the History tab open, private memory grew about 10 MB a minute under Avalonia's default GPU rendering, past criterion 9's 150 MB limit.
Decision: `Program.BuildAvaloniaApp` sets `Win32RenderingMode.Software`.
Force: requirement — criterion 9 (under 150 MB private); measured: software stays at 48–55 MB over 10 minutes, GPU grew from 135 to 231 MB.
Rejected: a smaller Skia GPU cache (stops the growth but stays near 140 MB); drawing the chart less often (the growth is per path drawn, and the line must move every second).
Consequence: Drawing costs CPU, not GPU: measured equal at idle and while hovering. A future animated or full-screen view should re-measure before relying on this.
Evidence: docs/reviews/2026-10-12-m5-footprint.md; src/MemoryMap.App/Program.cs
