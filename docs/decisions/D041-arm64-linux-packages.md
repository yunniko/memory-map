# D041 · Linux packages are built for amd64 and arm64, each on its own architecture's runner
Date: 2026-10-13 · Goal: G-006 M1 · Status: active (superseded by: —)
Context: The Owner's ARM Chromebook (arm64) could not install the amd64 `.deb`.
Decision: `publish-linux.sh` builds for the machine's own architecture (amd64 or arm64, others refused); CI and the release workflow build and deb-check arm64 on GitHub's `ubuntu-24.04-arm` runner, and the merge gate runs there too.
Force: requirement — the Owner's arm64 machine; dpkg-shlibdeps reads the libraries of the architecture it runs on, so a cross-built package would get the wrong Depends.
Rejected: cross-publishing arm64 on an amd64 runner (Depends could not be computed or the app run there); emulating arm64 with QEMU in CI (slow, and not the real CPU).
Consequence: A release carries both architectures; `release-files.sh` fails if either is missing. A new architecture needs a native runner and an entry in both scripts.
Evidence: scripts/publish-linux.sh; scripts/release-files.sh; .github/workflows/check.yml; .github/workflows/release.yml
