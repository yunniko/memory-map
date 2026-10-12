# D017 · The map redraws on every refresh; skipping unchanged frames was measured and dropped
Date: 2026-10-12 · Goal: G-001 M5 · Status: active (superseded by: —)
Context: Redrawing while shown costs about 1.2% of one core, so skipping redraws when nothing visible changed was proposed.
Decision: Keep redrawing the whole map each refresh; add no change detection.
Force: judgment — measured: no 1 s tick in 90 left the scene identical, and 34 of 36 tiles moved or relabelled each tick.
Rejected: a scene signature to skip identical frames (never fires); per-tile visuals with dirty rectangles (88% of the area changes anyway).
Consequence: The way to cut shown cost is a slower refresh setting, not render caching. Revisit if the layout stops moving every tile on small changes.
Evidence: docs/reviews/2026-10-12-m5-footprint.md
