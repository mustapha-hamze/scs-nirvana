using System;
using System.Collections.Generic;

namespace Application.Contracts.CMSApi
{
    // Culture-aware public content read: the current master graph (layout, images, files,
    // galleries, ElementTitle, relations) with its translatable text resolved for one culture.
    // Deliberately carries no FarsiContent, provider, model, prompt or error data.
    public class LocalizedContentApiDto
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public int TypeId { get; set; }
        public string Title { get; set; }
        public string HeadLine { get; set; }
        public string Abstract { get; set; }
        public string Description { get; set; }
        public string Categories { get; set; }
        public string Tags { get; set; }
        public string Cultures { get; set; }
        public DateTime PublishDt { get; set; }
        public ContentMetadataApiDto Metadata { get; set; }
        public List<ContentSectionApiDto> Sections { get; set; }
        public List<ContentImageApiDto> Images { get; set; }

        // The requested culture's stored key (canonical casing), or null when the application has
        // no active culture with that key.
        public string Culture { get; set; }

        // One of LocalizedContentResolution: where the text came from.
        public string Resolution { get; set; }
    }

    public static class LocalizedContentResolution
    {
        // A Ready ContentTranslation matching the current source fingerprint.
        public const string Translation = "translation";

        // The legacy FarsiContent snapshot, rebased onto the current master IDs.
        public const string LegacyFarsi = "legacy-farsi";

        // The English master text.
        public const string Source = "source";
    }
}
