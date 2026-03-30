using System.Collections.Generic;
using Newtonsoft.Json;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_ContentSections")]
    public class ContentSection : BaseEntity
    {

        // property
        public int ContentId { get; set; }

        // [StringLength(128)]
        // public string Title { get; set; }

        public int Priority { get; set; }

        // relation
        public virtual ICollection<SectionElement> Elements { get; set; }

        // foreign key
        [ForeignKey("ContentId")]
        [JsonIgnore]
        public virtual Content Content { get; set; }
    }
}
