# D020 · An entry that starts a host program (cmd, rundll32, a script host) is credited with no processes
Date: 2026-10-12 · Goal: G-002 M1 · Status: active (superseded by: —)
Context: Matching runs by executable file. Entries such as `cmd.exe /c …` or `rundll32 x.dll,Entry` share their file with every other use of that host, including windows the user opened.
Decision: The reader marks entries whose program is a known Windows host (`CountsProcesses = false`); matching skips them and the UI says "not counted".
Force: judgment — crediting the first such entry with every cmd or rundll32 process gives a wrong memory figure, and telling them apart needs each process's command line, which an unelevated read cannot get for every process.
Rejected: reading command lines from each process (fails for protected and elevated ones, and costs a remote read per process per refresh); first-entry-wins (code review: misattributes).
Consequence: The host list lives in `WindowsStartupReader`; a host missing from it misattributes again.
Evidence: tests/MemoryMap.Core.Tests/StartupMatchingTests.cs; tests/MemoryMap.Windows.Tests/StartupReaderTests.cs
