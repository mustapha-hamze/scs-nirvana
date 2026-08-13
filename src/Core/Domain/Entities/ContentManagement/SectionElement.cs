using System.Text.Json.Serialization;

namespace Domains.Entities.ContentManagement
{
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

        // foreign key
        [ForeignKey("SectionId")]
        [JsonIgnore]
        public virtual ContentSection Section { get; set; }
    }
}
