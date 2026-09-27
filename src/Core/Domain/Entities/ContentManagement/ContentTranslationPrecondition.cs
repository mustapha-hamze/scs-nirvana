namespace Domains.Entities.ContentManagement
{
    // What a job may assume about the canonical (ContentId, CultureId) translation row when it
    // stores its result. Fixed when the job is (re-)queued.
    public enum ContentTranslationPrecondition : byte
    {
        // Normal request (and every job queued before this column existed): the worker may create
        // the row or update an existing non-deleted one (the stale/failed retry workflow).
        None = 0,

        // Legacy-Farsi bulk queue: no row, not even a soft-deleted one, existed when the job was
        // queued. The worker only ever inserts, so any row written since - in any state - makes
        // the job fail with translation_conflict instead of being overwritten.
        NoTranslation = 1
    }
}
