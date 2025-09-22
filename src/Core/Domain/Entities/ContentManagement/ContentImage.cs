using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_ContentImages")]
    public class ContentImage : BaseEntity
    {
        public int ContentId { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }

        [ForeignKey("ContentId")]
        public virtual Content Content { get; set; }

        public int Size { get; set; }
    }
}