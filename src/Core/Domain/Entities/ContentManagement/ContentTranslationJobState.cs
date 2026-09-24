namespace Domains.Entities.ContentManagement
{
    // Lifecycle of one ContentTranslationJob. Stored as tinyint. Succeeded, Failed and Superseded
    // are terminal until an explicit translation request re-queues the job.
    public enum ContentTranslationJobState : byte
    {
        Queued = 0,
        Processing = 1,
        Succeeded = 2,
        Failed = 3,

        // The master source changed before the result could be stored; nothing was persisted.
        Superseded = 4
    }
}
