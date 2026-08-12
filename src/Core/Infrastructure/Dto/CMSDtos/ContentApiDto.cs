using System;
using System.Collections.Generic;

namespace Infrastructure.Dto.CMSDtos
{
    // API-facing shape for public content endpoints (src/Web/Areas/Api/ContentController.cs).
    // Property names intentionally mirror the Domains.Entities.ContentManagement.Content entity
    // so existing JSON consumers see the same keys/casing as before. Unlike the entity, this type
    // carries no EF navigation back-references, so it can never produce a serialization cycle.
    // The entity's `Application` navigation is deliberately not mirrored here: across every public
    // read path it is always null (never `.Include()`-ed), so dropping it changes no observable value.
    public class ContentApiDto : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int TypeId { get; set; }
        public string Title { get; set; }
        public string HeadLine { get; set; }
        public string Abstract { get; set; }
        public string Description { get; set; }
        public string FarsiContent { get; set; }
        public string Categories { get; set; }
        public string Tags { get; set; }
        public string Cultures { get; set; }
        public DateTime PublishDt { get; set; }
        public List<ContentSectionApiDto> Sections { get; set; }
        public List<ContentImageApiDto> Images { get; set; }
        public ContentMetadataApiDto Metadata { get; set; }
    }
}
