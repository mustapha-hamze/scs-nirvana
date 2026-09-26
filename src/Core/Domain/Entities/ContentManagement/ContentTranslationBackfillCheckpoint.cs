namespace Domains.Entities.ContentManagement
{
    // Resume point and cumulative metrics of one operator-invoked legacy FarsiContent backfill
    // run. One row per RunKey; every batch advances it in the same transaction as its imports.
    public class ContentTranslationBackfillCheckpoint : BaseEntity
    {
        public string RunKey { get; set; }
        public int CultureId { get; set; }

        // Highest Content.Id already processed (keyset cursor); 0 before the first batch.
        public int LastContentId { get; set; }

        public int ScannedCount { get; set; }
        public int ImportedStaleCount { get; set; }
        public int NeedsReviewCount { get; set; }
        public int SkippedExistingCount { get; set; }

        // Optimistic concurrency token, incremented by every batch: two runners sharing a RunKey
        // can't both commit a batch from the same checkpoint.
        public int Version { get; set; }
    }
}
