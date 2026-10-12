# D004 · dotnet format, xUnit v3 on Microsoft Testing Platform, local check script
Date: 2026-10-12 · Goal: G-001 M1 · Status: active (superseded by: —)
Context: The portfolio had no C# project, so no formatter config or test stack to follow.
Decision: Format with `dotnet format` against the canonical `COMPANY/configs/csharp.editorconfig` (created here); unit and integration tests with xUnit v3 on Microsoft Testing Platform; `scripts/check.ps1` (format, warnings-as-errors build, tests) is the merge gate until CI exists.
Force: judgment — `dotnet format` ships with the SDK and follows the .NET runtime team's conventions (STANDARDS.md prefers the community standard); the .NET 10 SDK no longer runs VSTest for xUnit v3, so Microsoft Testing Platform is opted into in `global.json`.
Rejected: CSharpier (extra tool; not the SDK default); NUnit/MSTest (no reason over xUnit, which Avalonia's headless test package supports).
Consequence: Later C# projects copy the same editorconfig; improvements go to `COMPANY/configs/`.
Evidence: scripts/check.ps1; global.json
