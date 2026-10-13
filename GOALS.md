# Goals — memory-map

Completed goals: `docs/goals-archive.md`.

### G-006 · Linux packages for 64-bit ARM — ACTIVE
- **What:** An arm64 `.deb` and an arm64 single file beside the amd64 ones, built and checked in CI and
  carried by every release.
- **Why:** The Owner's Chromebook Linux is arm64 (`dpkg --print-architecture`, 2026-10-13); the amd64
  `.deb` cannot install there ("libc6:amd64 … not installable").
- **Acceptance criteria:**
  1. `scripts/publish-linux.sh` builds for the machine's own architecture (amd64 or arm64), naming the
     files by it; any other architecture is refused.
  2. CI builds the arm64 package on an ARM runner and `deb-check` passes there (lintian, install, the
     window shown, run-time libraries in Depends, clean remove); the merge gate also runs on ARM.
  3. The release workflow attaches both architectures' `.deb` and single file, with checksums and
     notes naming which is which.
  4. The arm64 `.deb` installs and runs on the Owner's Chromebook (the Owner checks, from a draft).
- **Constraints:** Each package is built on its own architecture, so dpkg-shlibdeps reads that
  architecture's libraries (D036). Publishing a release waits for the Owner (VALUES.md → Restraint).

**Milestones:**
- [x] M1 — Scripts and CI: architecture-aware `publish-linux.sh`, ARM jobs in `check.yml`, both
  architectures in `release.yml` and `release-files.sh`, version 0.4.1; CI green on ARM.
- [ ] M2 — A draft release with both architectures for the Owner to try on the Chromebook; published
  after the Owner's check and OK.

**Progress log** (newest first):
- 2026-10-13 — M1 done (659d259, D041). CI check run 38437233862 green on windows-2025 (334: 308 passed,
  26 skipped), ubuntu-24.04 and ubuntu-24.04-arm (251/251; crosscheck PASS 0.10%); deb-check passed on
  both runners, arm64 included (window shown, 9 run-time libraries in Depends, clean remove). Local:
  amd64 build and deb-check in WSL exit 0; `release-files.sh` with fake artifacts lists both
  architectures and fails when one is missing. Release run 38437251400 made draft v0.4.1 (7 files,
  no tag yet). **BLOCKED:** M2 waits for the Owner to install `memory-map_0.4.1_arm64.deb` from the
  draft on the Chromebook and say whether to publish.
- 2026-10-13 — Goal created from the Owner's failed install on an arm64 Chromebook; the Owner's reply
  ("arm64") to the proposed fix is the go-ahead. The Linux code's only raw syscalls (pidfd 424, 434)
  share their numbers on every architecture.

### G-007 · Smaller downloads — ACTIVE
- **What:** A smaller Windows exe, Linux single file and `.deb`, with every feature still working and
  no worse startup or memory.
- **Why:** The Owner asked whether the app must weigh 90 MB (2026-10-13). Most of it is the whole .NET
  runtime and libraries packed in untrimmed: the exe is 97 MB, the `.deb` 30 MB.
- **Acceptance criteria:**
  1. The exe and the Linux single file are at most half their 0.4.1 size (proposed target; the
     measured options in M1 may show more or less is possible, and the Owner may set it).
  2. Everything still works in the shipped build, not just the test build: the gate, deb-check on
     amd64 and arm64, every tab opened in the published app on Windows and Linux, settings saved and
     read back, the startup helper check against the published exe.
  3. Time to the first map and private memory after 10 minutes are no worse than 0.4.1's, measured
     the same way, and the 150 MB and 2% CPU limit (G-001 criterion 9) still holds.
  4. The build shows no trimming or AOT warnings (warnings already fail the build).
- **Constraints:** CI runs the shipped (trimmed or compiled) build, not only the test build
  (STANDARDS.md → "Verified means the path production runs"). Publishing waits for the Owner.

**Milestones:**
- [x] M1 — Measure: 0.4.1's size, startup and memory; then build and measure the options: compressed
  single file; trimmed; trimmed and compressed; a Native AOT trial. Results in `docs/reviews/`, the
  choice as a decision: the smallest option that passes criteria 2–4.
- [x] M2 — Make it the real build: source-generated JSON for the settings file, the chosen settings in
  `publish.ps1`, `publish-linux.sh` and CI, and a CI step that starts the published Windows exe and
  checks its window and a settings round trip (deb-check already runs the published Linux app).
- [ ] M3 — Draft release 0.5.0 with both Linux architectures; published after the Owner's OK.

**Progress log** (newest first):
- 2026-10-13 — M2 done (992dbc9, 38aae6d, 0e08d6c; D044). AOT in `publish.ps1`, `publish-linux.sh`, CI and
  the release workflow; the Windows zip and Linux tarballs replace the single files. Verified: Windows
  gate 334 (308 passed, 26 skipped); WSL 251/251; CI run 38452888393 all green with `smoke-published.ps1`
  14/14 on the published exe and deb-check on amd64 and arm64; release build-only run 38449811250 green;
  release files 14.7 MB zip, 10.8 / 9.4 MB debs (were 30 / 27), same checksums on a rerun. The smoke
  test's final form ran on CI only: the Owner's C:\bin copy was running here. Next: M3, 0.5.0 draft.
- 2026-10-13 — Owner chose Native AOT ("Native AOT, zip"): D043, superseding D014. M1 done (5e77b69 and
  the check-in commit). Next: M2, the AOT build in `publish.ps1`, `publish-linux.sh`, CI and releases.
- 2026-10-13 — (resolved above) **BLOCKED:** M2 waits for the Owner's choice of build. Only Native AOT passes criteria
  1, 3 and 4 (35 MB against 97 MB, start 0.7 s against 2.7 s, 16–46% less private memory), but it is
  three files, so the Windows download becomes a 14.5 MB zip instead of one exe. One-file options:
  trimmed and compressed (21 MB, 0.8 s slower start) or trimmed with ReadyToRun (63 MB, faster start).
  Recommended: AOT. A draft D043 (AOT, superseding D014) is ready for that answer.
- 2026-10-13 — M1 measured (`docs/reviews/2026-10-13-g007-m1-size-options.md`): seven options on
  Windows, five on Linux; sizes, startup, 10-minute memory and CPU. Trim/AOT-safe interop and settings
  JSON (D042) build with no warnings. Every tab, settings save and load, close-to-tray and the startup
  helper check pass in the published AOT build (Windows; Linux under WSLg). Gates: Windows 334 total
  (308 passed, 26 Linux-only skipped), WSL 251/251.
- 2026-10-13 — Owner accepted the plan ("accept"): the go-ahead for M1–M3. D014 (untrimmed,
  uncompressed) was a judgment to revisit if size mattered; M1 measures its memory concern.
- 2026-10-13 — Goal planned at the Owner's request ("yes, plan it please"). Found: compiled bindings
  already on (`AvaloniaUseCompiledBindingsByDefault`); the settings file uses reflection-based JSON
  (`src/MemoryMap.Core/AppSettings.cs`), which trimming needs replaced.

### G-005 · Signed Windows releases through SignPath Foundation — ACTIVE
- **What:** Releases built by GitHub Actions from a version tag: the Windows exe signed with a
  SignPath Foundation certificate, the `.deb` and the Linux single file beside it, all attached to
  a GitHub release.
- **Why:** An unsigned exe has no publisher, and SmartScreen warns on it. The Owner chose SignPath's
  free open-source signing (2026-10-13: "let's go path 1").
- **Acceptance criteria:**
  1. Pushing a `v*` tag builds all three packages in CI, checks them (the `.deb` with deb-check), and
     creates a draft release with checksums; a tag that differs from the project version fails.
  2. The repo meets SignPath Foundation's terms: a "Code signing policy" section with the
     attribution, team roles and privacy statement on the home page and the release pages; exe
     metadata (product name, a fixed product version) set.
  3. Once SignPath accepts the project, the exe in a release is signed: its Authenticode
     signature verifies, and the publisher is SignPath Foundation.
- **Constraints:** SignPath Foundation's terms (signpath.org/terms, read 2026-10-13). Applying,
  accepting their terms and turning on two-factor sign-in are the Owner's (accounts, identity).
  Every signing request is approved by hand in SignPath (their terms).

**Milestones:**
- [x] M1 — Release workflow: tag-triggered build of the exe, the `.deb` and the Linux file;
  deb-check; draft release with checksums; signing step present but skipped until SignPath is set up.
- [x] M2 — Repo ready for the application: code signing policy section, attribution, roles,
  privacy statement, exe metadata, SignPath artifact configuration; application answers drafted
  for the Owner.
- [ ] M3 — After acceptance: SignPath project and GitHub secrets set, a signed release (the next version)
  verified with `Get-AuthenticodeSignature`. Needs the Owner first.

**Progress log** (newest first):
- 2026-10-13 — **BLOCKED:** M3 needs the Owner: turn on GitHub two-factor sign-in, apply at
  signpath.org/apply (answers drafted in the session note), and after acceptance set the repo
  variable `SIGNPATH_ORGANIZATION_ID` and secret `SIGNPATH_API_TOKEN` (project slug `memory-map`,
  policy `release-signing`). Nothing else in the goal is left to do before then.
- 2026-10-13 — M1 and M2 done (133d57e, D040). `release.yml` test run 38361375257 (manual, with
  draft-tag): windows, linux and release jobs green, sign skipped; the draft had the five files and
  notes; its exe reads "Memory Map" 0.4.0, matches SHA256SUMS, NotSigned. Draft deleted, no tag
  left. Gate: Windows 334 tests (308 passed, 26 Linux-only skipped), WSL 251/251; CI green.
- 2026-10-13 — Goal created from the Owner's choice of SignPath (path 1); the choice is the
  go-ahead. Facts: the app makes no network calls (no HTTP or socket code in `src`), so SignPath's
  short no-transfer privacy statement applies; GitHub could not report the Owner's 2FA state.
