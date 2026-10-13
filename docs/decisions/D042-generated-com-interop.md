# D042 · Windows shell COM calls use generated interop, and settings JSON a generated context
Date: 2026-10-13 · Goal: G-007 M1 · Status: active (superseded by: —)
Context: Trimmed and AOT builds failed on IL2050 (COM p/invoke) and IL2026 (reflection JSON). The shortcut reader's `new ShellLink()` COM activation would also break in a trimmed build without a build warning.
Decision: `Shortcut` and `ShellProperties` use `[GeneratedComInterface]` interfaces created through one `StrategyBasedComWrappers` (`src/MemoryMap.Windows/Startup/Com.cs`); `AppSettings` reads and writes through the source-generated `SettingsJson` context.
Force: requirement — trimming and AOT do not support built-in COM interop or reflection-based JSON (IL2050, IL2026 in the publish logs).
Rejected: keeping built-in COM and turning trimming off for those assemblies (keeps most of the size, hides the warning); suppressing the warnings (the code would fail at run time).
Consequence: A new COM interface is declared with `[GeneratedComInterface]`, in vtable order, cast from a `ComObject` via `object`, and released with `FinalRelease`. New JSON types go into a generated context.
Evidence: src/MemoryMap.Windows/Startup/Shortcut.cs; src/MemoryMap.Windows/Startup/ShellProperties.cs; src/MemoryMap.Core/AppSettings.cs; docs/reviews/2026-10-13-g007-m1-size-options.md
