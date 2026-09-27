using System;

namespace Domains.Entities.ContentManagement
{
    // One durable background translation of a Content into a Culture, pinned to the source
    // fingerprint it was requested for. Unique on (ContentId, CultureId, SourceFingerprint), so a
    // repeated request for unchanged source reuses the same job instead of paying for another call.
    public class ContentTranslationJob : BaseEntity
    {
        public int ContentId { get; set; }
        public int CultureId { get; set; }
        public string SourceFingerprint { get; set; }
        public ContentTranslationJobState State { get; set; }

        // Provider calls started for this job since it was last (re-)queued; bounded by
        // ContentTranslationOptions.MaxAttempts.
        public int AttemptCount { get; set; }

        // UTC. A Queued job is claimable once this has passed (retry backoff).
        public DateTime NextAttemptAt { get; set; }

        // Claim of a Processing job: which worker holds it and until when (UTC).
        public string LeaseOwner { get; set; }
        public DateTime? LeaseExpiresAt { get; set; }

        // Safe, fixed error code (ContentTranslationErrorCodes) - never provider text or payloads.
        public string ErrorCode { get; set; }

        // UTC; set when the job reaches a terminal state.
        public DateTime? CompletedAt { get; set; }

        // Optimistic concurrency token, incremented by every state change: two workers can't both
        // claim or complete the same job.
        public int Version { get; set; }
    }
}
