# Goals — memory-map

Completed goals: `docs/goals-archive.md`.

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
- [ ] M3 — After acceptance: SignPath project and GitHub secrets set, a signed release (v0.4.1)
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
