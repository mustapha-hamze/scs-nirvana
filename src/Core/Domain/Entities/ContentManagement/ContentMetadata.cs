using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_ContentMetadata")]
    public class ContentMetadata : BaseEntity
    {
        public int ContentId { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(128)]
        public string Author { get; set; }

        [StringLength(1024)]
        public string Keywords { get; set; }

        [StringLength(2048)]
        public string Description { get; set; }

        // relations
        [ForeignKey("ContentId")]
        public virtual Content Content { get; set; }
    }
}