# D003 · A process tile's size is its private working set
Date: 2026-10-12 · Goal: G-001 M1 · Status: active (superseded by: —)
Context: Per-process working sets count shared pages once per process, so they sum to more than RAM in use; tile areas must add up to total RAM.
Decision: Size each process by its private working set; memory in use that no process or kernel figure accounts for becomes "Shared & other", with the modified page list named separately.
Force: requirement — G-001 acceptance criterion 1, accepted by the Owner on 2026-10-12.
Rejected: full working set (double-counts shared pages); private commit (includes paged-out memory, which is not RAM).
Consequence: "Shared & other" is large on browser-heavy machines (about 4 GB of 15.4 GB measured); it is real shared memory and cannot be split per process from these counters.
Evidence: tests/MemoryMap.Core.Tests/BreakdownTests.cs; docs/reviews/2026-10-12-m1-sampler-crosscheck.md
