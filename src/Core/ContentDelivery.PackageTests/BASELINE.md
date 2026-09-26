# Content Delivery SDK - local performance baseline

Non-gating. Regenerate with `src/Core/ContentDelivery.PackageTests/run.sh baseline` (packs the SDK,
restores it into the fixture from the local feed, runs `DeliveryBaseline`). These are local
SQLite in-memory timings of the packaged .NET 9 adapter: they compare runs of this benchmark
with each other and are **not** SQL Server or production capacity figures (no network, no
server, different query plans). Query counts and allocations transfer better than times.

- Runtime: .NET 9.0.9, osx-arm64, 14 logical CPUs
- Provider: EF Core 9.0.20.0 with SQLite in-memory, substituted for SQL Server in the adapter's options (packages Cms.ContentDelivery[.SqlServer] 1.0.0-preview.1)
- Dataset: 2,000 tenant contents (+500 of another tenant), each with metadata, 4 sections x 5 elements (1 inactive per section) and 3 images; all in the listed category; 50% with a Ready fa-IR translation, 10% of those stale
- Reads: document set of 50 scattered ids (the maximum); category + type listing, page 3 of size 100 (the maximum)
- Method: one DI scope per read, as per web request; 5 warmup reads, then 30 measured reads, sequential. Allocation is process-wide bytes / read; queries are database commands / read. Cache disabled = every read hits the database; cache enabled = in-process cache after warmup, so every measured read is a hit

| Read | Culture | Cache | Median ms | p95 ms | Allocated KB/read | Queries/read |
|---|---|---|---:|---:|---:|---:|
| Document set, 50 ids | source | disabled | 7.94 | 9.32 | 1459 | 7 |
| Listing, page 3 x 100 | source | disabled | 35.72 | 37.52 | 447 | 3 |
| Document set, 50 ids | fa-IR | disabled | 10.61 | 11.46 | 2467 | 9 |
| Listing, page 3 x 100 | fa-IR | disabled | 44.64 | 47.46 | 3001 | 8 |
| Document set, 50 ids | source | enabled (steady-state hits) | 0.03 | 0.04 | 46 | 0 |
| Listing, page 3 x 100 | source | enabled (steady-state hits) | 0.03 | 0.03 | 46 | 0 |
| Document set, 50 ids | fa-IR | enabled (steady-state hits) | 0.03 | 0.04 | 46 | 0 |
| Listing, page 3 x 100 | fa-IR | enabled (steady-state hits) | 0.03 | 0.04 | 46 | 0 |
