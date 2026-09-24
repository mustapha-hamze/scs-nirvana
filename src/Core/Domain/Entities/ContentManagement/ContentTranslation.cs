using System;

namespace Domains.Entities.ContentManagement
{
    public class ContentTranslation : BaseEntity
    {
        public int ContentId { get; set; }
        public int CultureId { get; set; }
        public TranslationStatus TranslationStatus { get; set; }

        // Fingerprint of the source content this translation was produced from; a mismatch
        // with the current source marks the translation Stale.
        public string SourceFingerprint { get; set; }

        // JSON payload; validity is enforced by the write use cases, not the database.
        public string LocalizedTextJson { get; set; }

        public string Provider { get; set; }
        public string Model { get; set; }

        // UTC.
        public DateTime? TranslatedAt { get; set; }

        public string Error { get; set; }
    }
}
