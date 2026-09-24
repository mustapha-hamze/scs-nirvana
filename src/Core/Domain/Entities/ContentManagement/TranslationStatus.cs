namespace Domains.Entities.ContentManagement
{
    // Lifecycle of one ContentTranslation row. Deliberately separate from BaseEntity.Status,
    // which is a legacy column with no business meaning. Stored as tinyint.
    public enum TranslationStatus : byte
    {
        Draft = 0,
        Queued = 1,
        Translating = 2,
        Ready = 3,
        Stale = 4,
        Failed = 5,
        NeedsReview = 6
    }
}
