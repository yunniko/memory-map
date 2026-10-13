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
- [ ] M1 — Release workflow: tag-triggered build of the exe, the `.deb` and the Linux file;
  deb-check; draft release with checksums; signing step present but skipped until SignPath is set up.
- [ ] M2 — Repo ready for the application: code signing policy section, attribution, roles,
  privacy statement, exe metadata, SignPath artifact configuration; application answers drafted
  for the Owner.
- [ ] M3 — After acceptance: SignPath project and GitHub secrets set, a signed release (v0.4.1)
  verified with `Get-AuthenticodeSignature`. Needs the Owner first.

**Progress log** (newest first):
- 2026-10-13 — Goal created from the Owner's choice of SignPath (path 1); the choice is the
  go-ahead. Facts: the app makes no network calls (no HTTP or socket code in `src`), so SignPath's
  short no-transfer privacy statement applies; GitHub could not report the Owner's 2FA state.
