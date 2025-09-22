using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_SectionElements")]
    public class SectionElement : BaseEntity
    {
        // property
        public int SectionId { get; set; }
        public int ElementType { get; set; }

        [StringLength(256)]
        public string TinyText { get; set; }

        public string EditorText { get; set; }

        [StringLength(256)]
        public string FileNameText { get; set; }

        [StringLength(4092)]
        public string GalleryImages { get; set; }

        public int Size { get; set; }

        [StringLength(256)]
        public string ElementTitle { get; set; }

        // foregin key
        [ForeignKey("SectionId")]
        public virtual ContentSection Section { get; set; }
    }
}
