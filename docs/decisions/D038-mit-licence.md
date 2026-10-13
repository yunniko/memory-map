# D038 · The app's own code is MIT-licensed, credited to yunniko and Claude
Date: 2026-10-13 · Goal: G-004 · Status: active (superseded by: —)
Context: The code was all rights reserved; the Owner asked for an open-source licence that only requires naming them and Claude as authors.
Decision: `LICENSE` is the MIT licence, "Copyright (c) 2026 yunniko and Claude (Anthropic)"; the .deb's copyright file declares the same (Expat).
Force: requirement — Owner instruction, 2026-10-13: "open source licensing which would just need mentioning me and you as author".
Rejected: BSD-2-Clause (same terms, less widely recognised); Apache-2.0 (adds patent and change-notice conditions beyond attribution).
Consequence: Bundled components keep their own licences; `THIRD-PARTY-NOTICES.md` lists them, and each release ships their notice texts beside the binaries.
Evidence: LICENSE; THIRD-PARTY-NOTICES.md; assets/linux/copyright
