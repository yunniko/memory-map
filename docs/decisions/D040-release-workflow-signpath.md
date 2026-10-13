# D040 · Releases are built by a tag-triggered workflow; SignPath signs the Windows exe
Date: 2026-10-13 · Goal: G-005 M1 · Status: active (superseded by: —)
Context: v0.4.0 was built on the Owner's PC; SignPath Foundation signs only binaries built automatically from the repository, and the Owner chose SignPath.
Decision: `release.yml` builds the exe, the Linux file and the .deb from a `v*` tag (failing if the tag differs from the version), runs deb-check, sends the exe to SignPath once `SIGNPATH_ORGANIZATION_ID` is set, and creates a draft release published by hand.
Force: requirement — SignPath Foundation's terms (automated builds, hand-approved signing, fixed product name and version in file metadata); Owner's choice of SignPath, 2026-10-13.
Rejected: committing binaries to git (100 MB file limit, history bloat); publishing the release automatically (a draft lets a person look before anything is public).
Consequence: A release's notes come from the newest `assets/linux/changelog` entry, so a version bump adds one there. The exe's product version is the bare version, with no commit hash.
Evidence: .github/workflows/release.yml; scripts/release-files.sh; scripts/collect-notices.ps1; .signpath/artifact-configurations/default.xml
