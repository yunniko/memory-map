# D045 · Linux packages are built in an Ubuntu 22.04 container, with glibc 2.35 as the floor
Date: 2026-10-13 · Goal: G-006 M2 · Status: active (superseded by: —)
Context: The 0.5.0 arm64 `.deb` would not install on the Owner's Chromebook (Debian 12, glibc 2.36): built on Ubuntu 24.04, the AOT program linked `fmod@GLIBC_2.38`, so Depends asked for libc6 >= 2.38.
Decision: `scripts/publish-linux-container.sh` runs `publish-linux.sh` in `ubuntu:22.04`; `publish-linux.sh` refuses output needing a glibc above 2.35; CI's deb-check installs on ubuntu:24.04, ubuntu:22.04 and debian:12.
Force: requirement — the Owner's Chromebook runs Debian 12 (G-006 criterion 4); 22.04 rather than Debian 12 is judgment, the older glibc covering both.
Rejected: pinning `fmod` to an old symbol version (per architecture, and misses the next symbol glibc versions again); ubuntu-22.04 runners (ties the floor to a runner image GitHub retires); debian:12 as the build image (excludes Ubuntu 22.04 for no gain).
Consequence: raising the floor means a newer build image and dropping a supported system, and the debian:12 install check stays.
Evidence: `scripts/publish-linux-container.sh`; `scripts/deb-check.sh`; the Owner's apt error, 2026-10-13.
