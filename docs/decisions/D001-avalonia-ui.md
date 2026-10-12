# D001 · Avalonia for the desktop UI
Date: 2026-10-12 · Goal: G-001 planning · Status: active (superseded by: —)
Context: The app needs a desktop window with fast custom drawing (a live treemap), a tray icon and context menus, in C#.
Decision: Use Avalonia on .NET 10 (the SDK installed on this PC, 10.0.303).
Force: requirement — the Owner chose Avalonia on 2026-10-12 when offered WPF, WinUI 3, WinForms and Avalonia.
Rejected: WPF (recommended as the Windows-only fit; Owner preferred Avalonia); WinUI 3 (packaging overhead); WinForms (dated look, weaker high-DPI).
Consequence: The UI could run on Linux/macOS, but the memory data source is Windows-only in v1; keep it behind an interface so another OS stays possible.
Evidence: GOALS.md G-001 progress log, 2026-10-12.
