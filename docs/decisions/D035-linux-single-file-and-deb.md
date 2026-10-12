# D035 · Linux ships as a self-extracting single file and a .deb with its libraries beside the program
Date: 2026-10-12 · Goal: G-003 M4 · Status: active (superseded by: —)
Context: Criterion 6 asks for a `.deb`; the plan also keeps a single file that runs anywhere, like the Windows exe (D014).
Decision: `scripts/publish-linux.sh` builds both from one source: the single file carries Skia and HarfBuzz inside and unpacks them on first run; the package installs the same program to /usr/lib/memory-map with the two libraries beside it, and a /usr/bin/memory-map link.
Force: judgment — a package should not unpack code into users' homes, where `apt remove` cannot reach it.
Rejected: the self-extracting file inside the package (leaves ~/.net copies after removal); framework-dependent build on Ubuntu's dotnet packages (not on every Debian-family release).
Consequence: The version lives in Directory.Build.props (a tie-break: it also stamps the Windows exe) and must match the head of `assets/linux/changelog`; the script refuses to build otherwise.
Evidence: scripts/publish-linux.sh; scripts/deb-check.sh; docs/reviews/2026-10-12-g003-m4-deb.md
